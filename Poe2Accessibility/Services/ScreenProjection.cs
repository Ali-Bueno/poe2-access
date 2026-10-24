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
            var (alongX, alongY) = Axes(origin);
            return ((worldOffset.X / ProbeWorld) * alongX) + ((worldOffset.Y / ProbeWorld) * alongY);
        }

        /// <summary>The stick direction (<see cref="StickDirection"/> octant) of a world offset from a place.</summary>
        public static int Octant(StdTuple3D<float> origin, Vector2 worldOffset)
        {
            var (alongX, alongY) = Axes(origin);
            return StickDirection.Octant(alongX, alongY, worldOffset.X, worldOffset.Y);
        }

        /// <summary>Screen vectors of a probe step along world +X and +Y at a place.</summary>
        private static (Vector2 AlongX, Vector2 AlongY) Axes(StdTuple3D<float> origin)
        {
            var world = Core.States.InGameStateObject.CurrentWorldInstance;
            var here = world.WorldToScreen(origin);
            return (world.WorldToScreen(new StdTuple3D<float> { X = origin.X + ProbeWorld, Y = origin.Y, Z = origin.Z }) - here,
                    world.WorldToScreen(new StdTuple3D<float> { X = origin.X, Y = origin.Y + ProbeWorld, Z = origin.Z }) - here);
        }

        /// <summary>The camera draws this world point inside the game window: a sighted player sees it now.</summary>
        public static bool IsOnScreen(StdTuple3D<float> world)
        {
            var window = Core.Process.WindowArea;
            var screen = Core.States.InGameStateObject.CurrentWorldInstance.WorldToScreen(world);
            return window.Width > 0 && window.Height > 0 && screen.X >= 0 && screen.Y >= 0 && screen.X <= window.Width && screen.Y <= window.Height;
        }
    }
}
