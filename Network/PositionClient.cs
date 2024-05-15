using PositionInterfaceClient.MotionGenerator;
using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace PositionInterfaceClient.Network
{
    internal class PositionClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        // Target IP address
        private string m_address = "192.168.3.11";
        // Target port number
        private int m_port = -1;
        // Read task
        private Task? m_readTask;
        // Protects access to the read task
        private readonly Mutex m_readTaskMutex = new();
        // Network stream to read from and write to
        NetworkStream? stream = null;
        // Timer for sending target position messages
        private readonly System.Timers.Timer m_sendTimer = new();

        // If set true the connection is closed
        private bool m_stopRunning = true;

        // Maximum size of the message buffer
        private static readonly int MessageBufferMaxLength = 1024;

        public delegate void ConnectionChange(bool connected);
        // This event is called when the connection changed
        public event ConnectionChange? ConnectionChanged;

        // Current joint position
        public PositionSet CurrentPosition { get; private set; } = new();
        public PositionSet LastTargetPosition { get; private set; } = new();

        public IPositionSource? PositionSource { get; set; } = null;

        private DateTime m_lastTargetPositionUpdate = DateTime.Now;
        private double[] m_targetPositionUpdateFrequencyMSBuffer = new double[20];
        public double TargetPositionUpdateFrequencyMS { get { return m_targetPositionUpdateFrequencyMSBuffer.Average(); } }

        private DateTime m_lastCurrentPositionUpdate = DateTime.Now;
        private double[] m_currentPositionUpdateFrequencyMSBuffer = new double[20];
        public double CurrentPositionUpdateFrequencyMS { get { return m_currentPositionUpdateFrequencyMSBuffer.Average(); } }

        /// <summary>
        /// Constructor
        /// </summary>
        public PositionClient()
        {
            m_sendTimer.AutoReset = true;
            m_sendTimer.Elapsed += SendPosition;
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
        /// Starts and connects the client
        /// </summary>
        /// <param name="sendIntervalMS">Position send interval in ms</param>
        public void Start(double sendIntervalMS, string address, int port)
        {
            Stop();
            m_readTaskMutex.WaitOne();
            try
            {
                if(port < 0 || port > 65535)
                {
                    log.ErrorFormat("Position Client: Can not connect to invalid port number {0}", port);
                    return;
                }

                log.InfoFormat("Position Client: Connecting to server at {0}:{1}", address, port);
                m_address = address;
                m_port = port;
                m_stopRunning = false;
                m_sendTimer.Interval = sendIntervalMS;
                m_readTask = Task.Factory.StartNew(ReadTask, TaskCreationOptions.LongRunning);
            }
            finally
            {
                m_readTaskMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Stops the client and disconnects
        /// </summary>
        public void Stop()
        {
            m_readTaskMutex.WaitOne();
            try
            {
                m_stopRunning = true;
                m_sendTimer.Stop();
                m_readTask?.Wait();
            }
            finally
            {
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
                m_lastTargetPositionUpdate = DateTime.Now;
                m_sendTimer.Start();

                log.Info("Position client connected");
                ConnectionChanged?.Invoke(true);

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
                log.ErrorFormat("Position Client: Reader failed: {0}", ex.Message);
            }
            finally
            {
                m_stopRunning = true;
                m_sendTimer.Stop();
                stream = null;

                ConnectionChanged?.Invoke(false);
                log.Info("Position Client: Stopped");
            }
        }

        /// <summary>
        /// Sends the current position to the robot control, event method for the timer
        /// </summary>
        private void SendPosition(object? sender, System.Timers.ElapsedEventArgs e)
        {
            SendPosition();
        }

        /// <summary>
        /// Sends the current position to the robot control
        /// </summary>
        private void SendPosition()
        {
            if (m_stopRunning || stream == null) return;

            try
            {
                // Get position from position source or current position from robot
                DateTime now = DateTime.Now;
                PositionSet targetPosition = PositionSource?.GetPosition(CurrentPosition, (now - m_lastTargetPositionUpdate).TotalMilliseconds) ?? CurrentPosition;
                LastTargetPosition = targetPosition;
                // calculate frequency
                for (int i = 1; i < m_targetPositionUpdateFrequencyMSBuffer.Length; i++)
                {
                    m_targetPositionUpdateFrequencyMSBuffer[i - 1] = m_targetPositionUpdateFrequencyMSBuffer[i];
                }
                m_targetPositionUpdateFrequencyMSBuffer[m_targetPositionUpdateFrequencyMSBuffer.Length - 1] = (now - m_lastTargetPositionUpdate).TotalMilliseconds;
                m_lastTargetPositionUpdate = now;

                string message;
                var c = CultureInfo.InvariantCulture;
                if (targetPosition.isCartesian)
                {
                    message = string.Format("MSGSTART Pos C {0} {1} {2} {3} {4} {5} E {6} {7} {8} P {9} {10} {11} MSGEND", targetPosition.CartesianPosition.X.ToString(c), targetPosition.CartesianPosition.Y.ToString(c), targetPosition.CartesianPosition.Z.ToString(c), targetPosition.CartesianOrientation.X.ToString(c), targetPosition.CartesianOrientation.Y.ToString(c), targetPosition.CartesianOrientation.Z.ToString(c), targetPosition.Joints[6].ToString(c), targetPosition.Joints[7].ToString(c), targetPosition.Joints[8].ToString(c), targetPosition.PlatformPosition.X.ToString(c), targetPosition.PlatformPosition.Y.ToString(c), targetPosition.PlatformHeading.ToString(c));
                }
                else
                {
                    message = string.Format("MSGSTART Pos J {0} {1} {2} {3} {4} {5} E {6} {7} {8} P {9} {10} {11} MSGEND", targetPosition.Joints[0].ToString(c), targetPosition.Joints[1].ToString(c), targetPosition.Joints[2].ToString(c), targetPosition.Joints[3].ToString(c), targetPosition.Joints[4].ToString(c), targetPosition.Joints[5].ToString(c), targetPosition.Joints[6].ToString(c), targetPosition.Joints[7].ToString(c), targetPosition.Joints[8].ToString(c), targetPosition.PlatformPosition.X.ToString(c), targetPosition.PlatformPosition.Y.ToString(c), targetPosition.PlatformHeading.ToString(c));
                }
                byte[] buffer = Encoding.ASCII.GetBytes(message);
                stream?.Write(buffer, 0, buffer.Length);
            }
            catch (ObjectDisposedException) { } // ignore, may occur due to race condition on connection loss
            catch (Exception ex)
            {
                log.ErrorFormat("Position Client: Could not send position: {0}", ex.Message);
                m_stopRunning = true;
            }
        }

        /// <summary>
        /// Adds a partial message to the buffer and tries to parse it
        /// </summary>
        /// <param name="messageBuffer">message buffer</param>
        private void Consume(ref string messageBuffer)
        {
            // find MSGSTART and MSGEND, then parse the chars inbetween
            int msgStartIdx = messageBuffer.IndexOf("MSGSTART");
            int msgEndIdx = messageBuffer.IndexOf("MSGEND", msgStartIdx + 9);
            int lastEnd = msgEndIdx;
            while (msgStartIdx != -1 && msgEndIdx != -1)
            {
                string msgPayload = messageBuffer.Substring(msgStartIdx + 9, msgEndIdx - msgStartIdx - 10);
                Parse(msgPayload);
                lastEnd = msgEndIdx;
                msgStartIdx = messageBuffer.IndexOf("MSGSTART", msgEndIdx + 6);
                msgEndIdx = messageBuffer.IndexOf("MSGEND", msgStartIdx + 9);
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
        /// <param name="message">Message</param>
        /// <returns>True if a valid message was found</returns>
        private void Parse(string message)
        {
            string[] msgSplit = message.Split();
            if (msgSplit.Length <= 0) return;

            if (msgSplit[0] == "Pos")
            {
                double[] robotJoints = new double[6];
                double[] externalJoints = new double[3];
                double[] cartesian = new double[6];
                double[] platform = new double[3];

                char currentType = '?';
                int idx = 0;

                foreach (string str in msgSplit)
                {
                    switch (str)
                    {
                        case "J": // robot joints
                            currentType = 'J';
                            idx = 0;
                            break;
                        case "E": // external joints
                            currentType = 'E';
                            idx = 0;
                            break;
                        case "C": // cartesian position and orientation
                            currentType = 'C';
                            idx = 0;
                            break;
                        case "P": // platform position and heading
                            currentType = 'P';
                            idx = 0;
                            break;
                        default:
                            if (!char.IsNumber(str[0]) && str[0] != '-') // unknown token
                            {
                                currentType = '?';
                                idx = 0;
                            }
                            else // number
                            {
                                switch (currentType)
                                {
                                    case 'J':
                                        if (idx < robotJoints.Length && double.TryParse(str, CultureInfo.InvariantCulture, out double rval))
                                        {
                                            robotJoints[idx] = rval;
                                        }
                                        idx++;
                                        break;
                                    case 'E':
                                        if (idx < externalJoints.Length && double.TryParse(str, CultureInfo.InvariantCulture, out double eval))
                                        {
                                            externalJoints[idx] = eval;
                                        }
                                        idx++;
                                        break;
                                    case 'C':
                                        if (idx < cartesian.Length && double.TryParse(str, CultureInfo.InvariantCulture, out double cval))
                                        {
                                            cartesian[idx] = cval;
                                        }
                                        idx++;
                                        break;
                                    case 'P':
                                        if (idx < platform.Length && double.TryParse(str, CultureInfo.InvariantCulture, out double pval))
                                        {
                                            platform[idx] = pval;
                                        }
                                        idx++;
                                        break;
                                    default:
                                        break;
                                }
                            }
                            break;
                    }
                }

                // copy new values
                PositionSet position = new();
                Array.Copy(robotJoints, position.Joints, robotJoints.Length);
                Array.Copy(externalJoints, 0, position.Joints, 6, externalJoints.Length);
                position.CartesianPosition.X = (float)cartesian[0];
                position.CartesianPosition.Y = (float)cartesian[1];
                position.CartesianPosition.Z = (float)cartesian[2];
                position.CartesianOrientation.X = (float)cartesian[3];
                position.CartesianOrientation.Y = (float)cartesian[4];
                position.CartesianOrientation.Z = (float)cartesian[5];
                position.PlatformPosition.X = (float)platform[0];
                position.PlatformPosition.Y = (float)platform[1];
                position.PlatformHeading = platform[2];
                CurrentPosition = position;

                // calculate frequency
                DateTime now = DateTime.Now;
                for (int i = 1; i < m_currentPositionUpdateFrequencyMSBuffer.Length; i++) {
                    m_currentPositionUpdateFrequencyMSBuffer[i - 1] = m_currentPositionUpdateFrequencyMSBuffer[i];
                }
                m_currentPositionUpdateFrequencyMSBuffer[m_currentPositionUpdateFrequencyMSBuffer.Length - 1] = (now - m_lastCurrentPositionUpdate).TotalMilliseconds;
                m_lastCurrentPositionUpdate = now;
            }
            else if(msgSplit[0] == "OK")
            {
                return;
            }
            else if (msgSplit[0] == "ERROR")
            {
                return;
            }
            else
            {
                log.WarnFormat("Position Client: Received unknown message: '{0}'", message);
            }
        }

    }
}
