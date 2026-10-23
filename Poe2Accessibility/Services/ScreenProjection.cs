namespace Poe2Accessibility.Services
{
    using System.Numerics;
    using GameHelper;
    using GameOffsets.Natives;

    /// <summary>How the camera draws a world offset at a place: the screen vector (x right, y down) for panning cues.</summary>
    public static class ScreenProjection
    {
        /// <summary>World offset used to measure how the camera draws the world axes.</summary>
        private const float ProbeWorld = 100f;

        public static Vector2 Direction(StdTuple3D<float> origin, Vector2 worldOffset)
        {
            var world = Core.States.InGameStateObject.CurrentWorldInstance;
            var here = world.WorldToScreen(origin);
            var alongX = world.WorldToScreen(new StdTuple3D<float> { X = origin.X + ProbeWorld, Y = origin.Y, Z = origin.Z }) - here;
            var alongY = world.WorldToScreen(new StdTuple3D<float> { X = origin.X, Y = origin.Y + ProbeWorld, Z = origin.Z }) - here;
            return ((worldOffset.X / ProbeWorld) * alongX) + ((worldOffset.Y / ProbeWorld) * alongY);
        }
    }
}
