namespace PositionInterfaceClient.MotionGenerator
{
    public interface IPositionSource
    {
        /// <summary>
        /// Returs the new target position
        /// </summary>
        /// <param name="currentPosition">Current position</param>
        /// <param name="timeDiffMS">Time difference since previous call in ms</param>
        /// <returns>New position set</returns>
        PositionSet GetPosition(PositionSet currentPosition, double timeDiffMS);
    }
}
