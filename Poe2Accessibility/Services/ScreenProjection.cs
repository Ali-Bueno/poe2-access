namespace Poe2Accessibility.Services
{
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
    using GameOffsets.Natives;

    /// <summary>How the camera draws a world offset at a place: the screen vector (x right, y down) for panning cues and directions.</summary>
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

        /// <summary>
        ///     Screen vectors of a probe step along world +X and +Y at a place, from the game's camera; the measured camera
        ///     (<see cref="CameraView"/>) when its matrix gives nothing usable, as before the first frame or with no window.
        /// </summary>
        public static (Vector2 AlongX, Vector2 AlongY) Axes(StdTuple3D<float> origin)
        {
            var world = Core.States.InGameStateObject.CurrentWorldInstance;
            var here = world.WorldToScreen(origin);
            var alongX = world.WorldToScreen(new StdTuple3D<float> { X = origin.X + ProbeWorld, Y = origin.Y, Z = origin.Z }) - here;
            var alongY = world.WorldToScreen(new StdTuple3D<float> { X = origin.X, Y = origin.Y + ProbeWorld, Z = origin.Z }) - here;
            return CameraView.IsUsable(alongX, alongY) ? (alongX, alongY) : (CameraView.Offset(ProbeWorld, 0), CameraView.Offset(0, ProbeWorld));
        }

        /// <summary>A 16:9 screen around the followed player shows this world point: a sighted player sees it now.</summary>
        public static bool InView(StdTuple3D<float> world) =>
            Seats.Followed.TryGetComponent<Render>(out var player) &&
            CameraView.InView(world.X - player.WorldPosition.X, world.Y - player.WorldPosition.Y);
    }
}
