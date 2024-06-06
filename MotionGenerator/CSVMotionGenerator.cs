using System.Globalization;
using System.IO;

namespace PositionInterfaceClient.MotionGenerator
{
    /// <summary>
    /// Generates a motion path from a CSV file
    /// Line format:
    /// J 1 2 3 4 5 6 7 8 9 1 2 3 4 5 6 (6 robot joints, 3 external joints, XYZ, ABC - cartesian is ignored)
    /// C 1 2 3 4 5 6 7 8 9 1 2 3 4 5 6 (6 robot joints, 3 external joints, XYZ, ABC - robot joints are ignored)
    /// Lines are read with the output cycle time
    /// </summary>
    internal class CSVMotionGenerator : IPositionSource
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        /// <summary>
        /// CSV file
        /// </summary>
        private string m_filename = "";
        /// <summary>
        /// CSV file
        /// </summary>
        public string Filename
        {
            get { return m_filename; }
            set
            {
                Stop();
                ClearBuffer();
                m_filename = value;
            }
        }

        /// <summary>
        /// Repeat the motion
        /// </summary>
        public bool Repeat { get; set; } = false;

        /// <summary>
        /// List of CSV lines
        /// </summary>
        private readonly List<PositionSet> m_lines = new();
        /// <summary>
        /// Protects access to m_lines
        /// </summary>
        private readonly ReaderWriterLockSlim m_linesLock = new();

        /// <summary>
        /// Is the CSV program running?
        /// </summary>
        private bool m_isRunning = false;
        /// <summary>
        /// Current line
        /// </summary>
        private int m_index = 0;

        /// <summary>
        /// Starts sending positions from CSV
        /// </summary>
        public void Start()
        {
            if (!m_isRunning)
            {
                m_linesLock.EnterUpgradeableReadLock();
                try
                {
                    if (m_lines.Count == 0) ReadFile(m_filename);
                }
                finally
                {
                    m_linesLock.ExitUpgradeableReadLock();
                }
                log.Info("CSV Motion Generator: Starting");
                m_isRunning = true;
                m_index = 0;
            }
        }

        /// <summary>
        /// Stops sending positions from CSV
        /// </summary>
        public void Stop()
        {
            if (m_isRunning)
            {
                log.Info("CSV Motion Generator: Stopping");
                m_isRunning = false;
                m_index = 0;
            }
        }

        /// <summary>
        /// Gets the current position from CSV
        /// </summary>
        /// <param name="currentPosition"></param>
        /// <param name="timeDiff"></param>
        /// <returns></returns>
        public PositionSet GetPositionSet(PositionSet currentPosition, double timeDiff) {
            if(m_isRunning)
            {
                // repeat if end is reached
                if (Repeat && m_index >= m_lines.Count) m_index = 0;

                if (m_index < m_lines.Count)
                {
                    m_linesLock.EnterReadLock();
                    try
                    {
                        PositionSet targetPosition = m_lines[m_index];
                        if (targetPosition.IsCartesian)
                        {
                            currentPosition.CartesianPosition = targetPosition.CartesianPosition;
                            currentPosition.CartesianOrientation = targetPosition.CartesianOrientation;
                            currentPosition.Joints[6] = targetPosition.Joints[6];
                            currentPosition.Joints[7] = targetPosition.Joints[7];
                            currentPosition.Joints[8] = targetPosition.Joints[8];
                        }
                        else
                        {
                            currentPosition.Joints = targetPosition.Joints;
                        }
                        m_index++;
                        return targetPosition;
                    }
                    finally
                    {
                        m_linesLock.ExitReadLock();
                    }
                }
                else
                {
                    Stop();
                }
            }
            return currentPosition;
        }

        /// <summary>
        /// Clears the lines buffer
        /// </summary>
        private void ClearBuffer()
        {
            m_linesLock.EnterWriteLock();
            try
            {
                m_lines.Clear();
            }
            finally
            {
                m_linesLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Reads a CSV file into the buffer
        /// </summary>
        /// <param name="file"></param>
        private void ReadFile(string file)
        {
            m_linesLock.EnterWriteLock();
            try
            {
                log.InfoFormat("CSV Motion Generator: Reading file '{0}'", file);

                // CSV separator
                char[] separator = [';'];

                m_lines.Clear();
                foreach (var line in File.ReadLines(file))
                {
                    string[] lineSplit = line.Split(separator);
                    bool isCartesian = lineSplit.Length >= 1 && lineSplit[0].ToLower() == "c";
                    double[] values = new double[15];
                    for(int i = 0; i < values.Length && i < lineSplit.Length - 1; i++)
                    {
                        values[i] = double.Parse(lineSplit[i+1].Replace(',', '.'), CultureInfo.InvariantCulture);
                    }

                    PositionSet position = new();
                    position.IsCartesian = isCartesian;
                    for (int i = 0; i < 9; i++) position.Joints[i] = values[i];
                    position.CartesianPosition[0] = (float)values[9];
                    position.CartesianPosition[1] = (float)values[10];
                    position.CartesianPosition[2] = (float)values[11];
                    position.CartesianOrientation[0] = (float)values[12];
                    position.CartesianOrientation[1] = (float)values[13];
                    position.CartesianOrientation[2] = (float)values[14];
                    m_lines.Add(position);
                }

                log.InfoFormat("CSV Motion Generator: File '{0}' read, {1} positions", file, m_lines.Count);
            }
            catch(Exception ex)
            {
                m_lines.Clear();
                log.InfoFormat("CSV Motion Generator: Could not read file '{0}': {1}", file, ex.Message);
            }
            finally
            {
                m_linesLock.ExitWriteLock();
            }
        }
    }
}
