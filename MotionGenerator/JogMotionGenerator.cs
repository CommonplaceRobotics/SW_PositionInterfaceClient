using System.Numerics;

namespace PositionInterfaceClient.MotionGenerator
{
    /// <summary>
    /// A simple jog motion generator
    /// </summary>
    internal class JogMotionGenerator : IPositionSource
    {
        // Is this cartesian or joint jog?
        private bool m_isCartesian = false;

        // The current target position
        private readonly double[] m_currentJoints = new double[9];
        // The current jog values
        private readonly double[] m_currentJog = new double[9];
        // The current cartesian position
        private Vector3 m_currentCartPos = new();
        // The current cartesian position jog values
        private Vector3 m_currentCartPosJog = new();
        // The current cartesian orientation
        private Vector3 m_currentCartOri = new();
        // The current cartesian orientation jog values
        private Vector3 m_currentCartOriJog = new();

        // Joint velocity in degrees/s or mm/s
        public double JointVelocity { get; set; } = 10;
        // Cartesian translation velocity in mm/s
        public double CartPosVelocity { get; set; } = 10;
        // Cartesian orientaiton velocity in degrees/s
        public double CartOriVelocity { get; set; } = 10;

        /// <summary>
        /// Re-Sets the current position
        /// </summary>
        /// <param name="joints">6 robot joints, 3 external joints</param>
        public void SetJoints(double[] joints, Vector3 cartPos, Vector3 cartOri)
        {
            for (int i = 0; i < joints.Length && i < m_currentJoints.Length; i++)
            {
                m_currentJoints[i] = joints[i];
            }
            m_currentCartPos = cartPos;
            m_currentCartOri = cartOri;
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
            m_isCartesian = false;
        }

        /// <summary>
        /// Sets the jog values
        /// </summary>
        /// <param name="jogValues">6 robot joints, 3 external joints, range -1.0 .. 1.0</param>
        public void SetJog(Vector3 cartPosValues, Vector3 cartOriValues)
        {
            m_currentCartPosJog = cartPosValues;
            m_currentCartOriJog = cartOriValues;
            m_isCartesian = true;
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
                m_currentJoints[i] += m_currentJog[i] * JointVelocity * 0.001 * timeDiff;
            }
            m_currentCartPos += m_currentCartPosJog * (float)(CartPosVelocity * 0.001 * timeDiff);
            m_currentCartOri += m_currentCartOriJog * (float)(CartOriVelocity * 0.001 * timeDiff);
            return m_currentJoints;
        }

        /// <summary>
        /// Returs the new target position
        /// </summary>
        /// <param name="currentPosition">Current position</param>
        /// <param name="timeDiff">Time difference since previous call</param>
        /// <returns>New position set</returns>
        public PositionSet GetPositionSet(PositionSet currentPosition, double timeDiff)
        {
            PositionSet result = new(currentPosition)
            {
                IsCartesian = m_isCartesian,
                Joints = GetJoints(timeDiff),
                CartesianPosition = m_currentCartPos,
                CartesianOrientation = m_currentCartOri
            };
            return result;
        }
    }
}
