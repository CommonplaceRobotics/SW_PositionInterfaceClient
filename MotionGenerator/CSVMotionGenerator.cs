namespace PositionInterfaceClient.MotionGenerator
{
    internal class CSVMotionGenerator : IPositionSource
    {
        public PositionSet GetPosition(PositionSet currentPosition, double timeDiff) { return currentPosition; }
    }
}
