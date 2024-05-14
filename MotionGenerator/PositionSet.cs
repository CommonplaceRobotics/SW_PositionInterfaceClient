using System.Numerics;

namespace PositionInterfaceClient.MotionGenerator
{
    public class PositionSet
    {
        // 6 robot joints and 3 external joints
        public double[] Joints = new double[9];
        // cartesian XYZ position
        public Vector3 CartesianPosition = new();
        // cartesian ABC orientation
        public Vector3 CartesianOrientation = new();
        // platform XY position
        public Vector2 PlatformPosition = new();
        // platform heading
        public double PlatformHeading = 0;

        // Set true for cartesian position instead of joints
        public bool isCartesian = false;

        /// <summary>
        /// Constructor
        /// </summary>
        public PositionSet() { }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other"></param>
        public PositionSet(PositionSet other)
        {
            Array.Copy(other.Joints, Joints, Joints.Length);
            CartesianPosition = other.CartesianPosition;
            CartesianOrientation = other.CartesianOrientation;
            PlatformPosition = other.PlatformPosition;
            PlatformHeading = other.PlatformHeading;
            isCartesian = other.isCartesian;
        }

    }
}
