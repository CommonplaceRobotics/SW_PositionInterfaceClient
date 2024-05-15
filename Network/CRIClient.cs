using System.Net.Sockets;
using System.Text;

namespace PositionInterfaceClient.Network
{
    public class CRIClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        // Target IP address
        private string m_address = "192.168.3.11";
        public static readonly int CRIDefaultPort = 3920;
        // Target port number
        private int m_port = CRIDefaultPort;

        // Read task
        private Task? m_readTask;
        // Protects access to the read task
        private readonly Mutex m_readTaskMutex = new();
        // Network stream to read from and write to
        NetworkStream? stream = null;
        // Timer for sending the keepalive
        private readonly System.Timers.Timer m_sendKeepaliveTimer = new();
        // Timer for sending state requests
        private readonly System.Timers.Timer m_sendStateRequestTimer = new();

        // If set true the connection is closed
        private bool m_stopRunning = true;

        // Maximum size of the message buffer
        private static readonly int MessageBufferMaxLength = 1024;

        public delegate void ConnectionChange(bool connected);
        // This event is called when the connection changed
        public event ConnectionChange? ConnectionChanged;

        /// <summary>
        /// Number of the sent message
        /// </summary>
        private int m_messageNumber = 1;
        /// <summary>
        /// Protects access to m_messageNumber
        /// </summary>
        private readonly ReaderWriterLockSlim m_writeLock = new();

        /// <summary>
        /// Is the connection active or passive?
        /// </summary>
        public bool IsConnectionActive { get; private set; } = false;
        /// <summary>
        /// Position interface is started on the robot control
        /// </summary>
        public bool IsPositionInterfaceRunning { get; private set; } = false;
        /// <summary>
        /// Position interface is in use as position source on the robot control
        /// </summary>
        public bool IsPositionInterfaceActive { get; private set; } = false;
        /// <summary>
        /// Hardware error code
        /// </summary>
        public string ErrorCode { get; private set; } = "Not Connected";

        /// <summary>
        /// Resets received state values
        /// </summary>
        private void ResetValues()
        {
            IsConnectionActive = false;
            IsPositionInterfaceRunning = false;
            IsPositionInterfaceActive = false;
            ErrorCode = "Not Connected";
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public CRIClient()
        {
            m_sendKeepaliveTimer.AutoReset = true;
            m_sendStateRequestTimer.AutoReset = true;
            m_sendKeepaliveTimer.Elapsed += SendKeepalive;
            m_sendStateRequestTimer.Elapsed += SendStateRequest;
            ResetValues();
        }

        /// <summary>
        /// Returns true
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return !m_stopRunning && stream != null;
            }
        }

        /// <summary>
        /// Starts the CRI interface
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        public void Start(string address, int port)
        {
            Stop();
            m_readTaskMutex.WaitOne();
            try
            {
                log.InfoFormat("CRI Client: Connecting to server at {0}:{1}", address, port);
                m_address = address;
                m_port = port;
                m_stopRunning = false;
                m_sendKeepaliveTimer.Interval = 500;
                m_sendStateRequestTimer.Interval = 500;
                m_readTask = Task.Factory.StartNew(ReadTask, TaskCreationOptions.LongRunning);
            }
            finally
            {
                ResetValues();
                m_readTaskMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Stops the CRI interface
        /// </summary>
        public void Stop()
        {
            m_readTaskMutex.WaitOne();
            try
            {
                Send("QUIT");
                m_stopRunning = true;
                m_sendKeepaliveTimer.Stop();
                m_sendStateRequestTimer.Stop();
                m_readTask?.Wait();
            }
            finally
            {
                ResetValues();
                m_readTaskMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Task method for reading incoming messages
        /// </summary>
        private void ReadTask()
        {
            try
            {
                using TcpClient client = new(m_address, m_port);
                stream = client.GetStream();
                m_sendKeepaliveTimer.Start();
                m_sendStateRequestTimer.Start();

                log.Info("Client connected");
                ConnectionChanged?.Invoke(true);

                RequestGetActive();
                RequestGetPositionInterface();

                byte[] buffer = new byte[client.ReceiveBufferSize];
                string strBuffer = "";

                while (!m_stopRunning)
                {
                    if (client.Available > 0) // TODO: könnte falsch sein
                    {
                        int cnt = stream.Read(buffer, 0, buffer.Length);
                        strBuffer += Encoding.UTF8.GetString(buffer, 0, cnt);
                        Consume(ref strBuffer);
                    }
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat("CRI Client: Reader failed: {0}", ex.Message);
            }
            finally
            {
                m_stopRunning = true;
                m_sendKeepaliveTimer.Stop();
                m_sendStateRequestTimer.Stop();
                stream = null;

                ConnectionChanged?.Invoke(false);
                log.Info("CRI Client: Stopped");
            }
        }

        /// <summary>
        /// Adds a partial message to the buffer and tries to parse it
        /// </summary>
        /// <param name="messageBuffer">message buffer</param>
        private void Consume(ref string messageBuffer)
        {
            // find MSGSTART and MSGEND, then parse the chars inbetween
            int msgStartIdx = messageBuffer.IndexOf("CRISTART");
            int msgEndIdx = messageBuffer.IndexOf("CRIEND", msgStartIdx + 9);
            int lastEnd = msgEndIdx;
            while (msgStartIdx != -1 && msgEndIdx != -1)
            {
                string msgPayload = messageBuffer.Substring(msgStartIdx + 9, msgEndIdx - msgStartIdx - 10);
                Parse(msgPayload);
                lastEnd = msgEndIdx;
                msgStartIdx = messageBuffer.IndexOf("CRISTART", msgEndIdx + 6);
                msgEndIdx = messageBuffer.IndexOf("CRIEND", msgStartIdx + 9);
            }

            // snip consumed bytes or by buffer limit
            int charsToRemove = 0;
            if (lastEnd > 0) charsToRemove = lastEnd + 6;
            if (messageBuffer.Length > MessageBufferMaxLength) charsToRemove = Math.Max(charsToRemove, messageBuffer.Length - MessageBufferMaxLength);
            messageBuffer = messageBuffer.Substring(charsToRemove);
        }

        /// <summary>
        /// Parses an incoming message
        /// </summary>
        /// <param name="message"></param>
        private void Parse(string message)
        {
            string[] msgSplit = message.Split();
            if (msgSplit.Length <= 0) return;

            switch(msgSplit[1])
            {
                case "STATUS":
                    // Get error code
                    int errorIdx = -1;
                    for(int i = 0; i < msgSplit.Length;i++)
                    {
                        if (msgSplit[i] == "ERROR")
                        {
                            errorIdx = i;
                            break;
                        }
                    }
                    if (errorIdx < 0) ErrorCode = msgSplit[errorIdx + 1];
                    break;
                case "CMD":
                    if(msgSplit.Length >= 4 && msgSplit[1] == "Active") // active / passive state
                    {
                        if (bool.TryParse(msgSplit[3], out bool active)) IsConnectionActive = active;
                    }
                    else if (msgSplit.Length >= 5 && msgSplit[2] == "PositionInterface") // is the position interface used as position source?
                    {
                        if (bool.TryParse(msgSplit[3], out bool running)) IsPositionInterfaceRunning = running;
                        if (bool.TryParse(msgSplit[4], out bool inUse)) IsPositionInterfaceActive = inUse;
                    }
                    break;
                case "CONFIG":
                    if (msgSplit.Length >= 4 && msgSplit[2] == "PositionInterface") // is the position interface enabled?
                    {
                        if(bool.TryParse(msgSplit[3], out bool running)) IsPositionInterfaceRunning = running;
                    }
                    break;
                default:
                    // ignore
                    break;
            }
        }

        /// <summary>
        /// Sends the current position to the robot control
        /// </summary>
        private void SendKeepalive(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Send("ALIVEJOG 0 0 0 0 0 0 0 0 0");
        }

        /// <summary>
        /// Requests state info
        /// </summary>
        private void SendStateRequest(object? sender, System.Timers.ElapsedEventArgs e)
        {
            RequestGetActive();
            RequestGetPositionInterface();
        }

        /// <summary>
        /// Sends a message to the server
        /// </summary>
        /// <param name="message"></param>
        private void Send(string message)
        {
            if (m_stopRunning || stream == null) return;

            m_writeLock.EnterWriteLock();
            try
            {
                if (++m_messageNumber >= 10000) m_messageNumber = 1;
                string messageComplete = string.Format("CRISTART {0} {1} CRIEND", m_messageNumber, message);

                byte[] buffer = Encoding.ASCII.GetBytes(messageComplete);
                stream?.Write(buffer, 0, buffer.Length);
            }
            catch (ObjectDisposedException) { } // ignore, may occur due to race condition on connection loss
            catch (Exception ex)
            {
                log.ErrorFormat("CRI Client: Could not send message: {0}", ex.Message);
                m_stopRunning = true;
            }
            finally
            {
                m_writeLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Requests the active/passive state
        /// </summary>
        public void RequestGetActive()
        {
            Send("CMD GetActive");
        }

        /// <summary>
        /// Requests activating the connection
        /// </summary>
        public void SendSetActive()
        {
            log.Info("CRI Client: Requesting active connection");
            Send("CMD SetActive true");
        }

        /// <summary>
        /// Requests an error reset
        /// </summary>
        public void SendResetErrors()
        {
            if (!IsConnectionActive) SendSetActive();
            log.Info("CRI Client: Requesting error reset");
            Send("CMD Reset");
        }

        /// <summary>
        /// Requests enabling the motors
        /// </summary>
        public void SendEnableMotors()
        {
            if (!IsConnectionActive) SendSetActive();
            log.Info("CRI Client: Requesting enable motors");
            Send("CMD Enable");
        }

        /// <summary>
        /// Requests disabling the motors
        /// </summary>
        public void SendDisableMotors()
        {
            if (!IsConnectionActive) SendSetActive();
            log.Info("CRI Client: Requesting disable motors");
            Send("CMD Disable");
        }

        /// <summary>
        /// Requests the position interface configuration
        /// </summary>
        public void RequestGetPositionInterface()
        {
            Send("CMD GetPositionInterface");
        }

        /// <summary>
        /// Requests starting / stopping the position interface
        /// </summary>
        /// <param name="enabled"></param>
        public void SendConfigurePositionInterface(bool enabled)
        {
            if (!IsConnectionActive) SendSetActive();
            log.Info("CRI Client: Requesting starting position interface");
            Send("CONFIG SetPositionInterface " + enabled.ToString());
        }

        /// <summary>
        /// Requests activating / deactivating the position interface to be used as position source
        /// </summary>
        /// <param name="use"></param>
        public void SendUsePositionInterface(bool use)
        {
            if (!IsConnectionActive) SendSetActive();
            log.Info("CRI Client: Requesting using position interface");
            Send("CMD UsePositionInterface " + use.ToString());
        }

    }
}
