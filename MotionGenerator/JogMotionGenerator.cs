namespace PositionInterfaceClient.MotionGenerator
{
    /// <summary>
    /// A simple jog motion generator
    /// </summary>
    internal class JogMotionGenerator : IPositionSource
    {
        // The current target position
        private readonly double[] m_currentJoints = new double[9];
        // The current jog values
        private readonly double[] m_currentJog = new double[9];

        /// <summary>
        /// Velocity for all axes
        /// </summary>
        public double Velocity { get; set; } = 10;

        /// <summary>
        /// Re-Sets the current position
        /// </summary>
        /// <param name="joints">6 robot joints, 3 external joints</param>
        public void SetJoints(double[] joints)
        {
            for (int i = 0; i < joints.Length && i < m_currentJoints.Length; i++)
            {
                m_currentJoints[i] = joints[i];
            }
        }

        /// <summary>
        /// Sets the jog values
        /// </summary>
        /// <param name="jogValues">6 robot joints, 3 external joints, range -1.0 .. 1.0</param>
        public void SetJog(double[] jogValues)
        {
            for (int i = 0; i < jogValues.Length && i < m_currentJog.Length; i++)
            {
                m_currentJog[i] = jogValues[i];
            }
        }

        /// <summary>
        /// Calculates and returns the next position
        /// </summary>
        /// <param name="timeDiff">Time difference since the previous call in seconds</param>
        /// <returns></returns>
        public double[] GetJoints(double timeDiff)
        {
            for (int i = 0; i < m_currentJoints.Length; i++)
            {
                m_currentJoints[i] += m_currentJog[i] * Velocity * 0.001 * timeDiff;
            }
            return m_currentJoints;
        }

        /// <summary>
        /// Returs the new target position
        /// </summary>
        /// <param name="currentPosition">Current position</param>
        /// <param name="timeDiff">Time difference since previous call</param>
        /// <returns>New position set</returns>
        public PositionSet GetPosition(PositionSet currentPosition, double timeDiff)
        {
            PositionSet result = new(currentPosition)
            {
                Joints = GetJoints(timeDiff)
            };
            return result;
        }
    }
}
