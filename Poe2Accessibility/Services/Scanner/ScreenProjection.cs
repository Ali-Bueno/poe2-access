namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq.Expressions;
    using System.Numerics;
    using System.Reflection;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Natives;

    /// <summary>
    ///     How the game's camera draws the world, straight from the camera's matrix in the game's memory (world to clip
    ///     space), never through the window: GameHelper's WorldToScreen maps it onto the window's pixels, which a 5:4 window,
    ///     an ultrawide, a minimised game or a monitor with no window rectangle would change. Screen heights, x right, y down.
    /// </summary>
    public static class ScreenProjection
    {
        /// <summary>World offset used to measure how the camera draws the world axes.</summary>
        private const float ProbeWorld = 100f;

        /// <summary>
        ///     GameHelper's copy of the camera matrix, which it reads from the game every frame but keeps private (field
        ///     "matrixSnap", a record holding "M"); null if this GameHelper keeps it some other way, and then the measured
        ///     camera (<see cref="CameraView"/>) stands in.
        /// </summary>
        private static Func<WorldData, Matrix4x4>? readMatrix = MatrixReader();

        /// <summary>Whether this GameHelper's camera matrix can be read at all (dev command "camera").</summary>
        public static bool MatrixReadable => readMatrix != null;

        public static Vector2 Direction(StdTuple3D<float> origin, Vector2 worldOffset)
        {
            var (alongX, alongY) = Axes(origin);
            return ((worldOffset.X / ProbeWorld) * alongX) + ((worldOffset.Y / ProbeWorld) * alongY);
        }

        /// <summary>
        ///     Pan and pitch of a sound at a world offset (<see cref="BeaconMath.Voice"/>), drawn in to the centre as it comes
        ///     within <paramref name="centreWorld"/> of the listener.
        /// </summary>
        public static (float Pan, float Pitch) Voice(StdTuple3D<float> origin, Vector2 worldOffset, float distance, float centreWorld)
        {
            var (side, height) = BeaconMath.Voice(Direction(origin, worldOffset));
            var spread = Math.Min(1f, distance / centreWorld);
            return (side * spread, 1f + ((height - 1f) * spread));
        }

        /// <summary>The stick direction (<see cref="StickDirection"/> octant) of a world offset from a place.</summary>
        public static int Octant(StdTuple3D<float> origin, Vector2 worldOffset)
        {
            var (alongX, alongY) = Axes(origin);
            return StickDirection.Octant(alongX, alongY, worldOffset.X, worldOffset.Y);
        }

        /// <summary>
        ///     How the camera draws a probe step along world +X and +Y at a place, in screen heights; the measured camera when
        ///     the matrix gives nothing usable, as before the first frame.
        /// </summary>
        public static (Vector2 AlongX, Vector2 AlongY) Axes(StdTuple3D<float> origin)
        {
            var at = At(origin);
            if (Camera() is { } camera && CameraView.Draw(camera, at) is { } here &&
                CameraView.Draw(camera, at + new Vector3(ProbeWorld, 0f, 0f)) is { } x &&
                CameraView.Draw(camera, at + new Vector3(0f, ProbeWorld, 0f)) is { } y &&
                CameraView.IsUsable(x - here, y - here))
            {
                return (x - here, y - here);
            }

            return (CameraView.Offset(ProbeWorld, 0), CameraView.Offset(0, ProbeWorld));
        }

        /// <summary>A 16:9 screen around the followed player shows this world point: a sighted player sees it now.</summary>
        public static bool InView(StdTuple3D<float> world)
        {
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return false;
            }

            return Camera() is { } camera
                ? CameraView.Draw(camera, At(world)) is { } drawn && CameraView.InView(drawn)
                : CameraView.InView(world.X - player.WorldPosition.X, world.Y - player.WorldPosition.Y);
        }

        /// <summary>A world point's place from the player's on a 16:9 screen, in half screens (1 = its edge): pan and nearness.</summary>
        public static Vector2 HalfScreens(StdTuple3D<float> player, StdTuple3D<float> world) =>
            Camera() is { } camera && CameraView.Draw(camera, At(world)) is { } there && CameraView.Draw(camera, At(player)) is { } here
                ? CameraView.HalfScreens(there - here)
                : CameraView.HalfScreens(world.X - player.X, world.Y - player.Y);

        /// <summary>Where the camera draws a world point, in screen heights from the centre; null without a usable camera.</summary>
        public static Vector2? Draw(StdTuple3D<float> world) => Camera() is { } camera ? CameraView.Draw(camera, At(world)) : null;

        /// <summary>
        ///     The game's camera when it draws the followed player within a 16:9 view, as a camera following the player does:
        ///     not GameHelper's identity before the first frame, nor a camera left from the area before. A matrix and its
        ///     negative draw the same, so it is turned to give points in front a positive depth.
        /// </summary>
        public static Matrix4x4? Camera()
        {
            if (readMatrix is not { } read || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            Matrix4x4 camera;
            try
            {
                camera = read(Core.States.InGameStateObject.CurrentWorldInstance);
            }
            catch (NullReferenceException)
            {
                return null;
            }
            catch (MemberAccessException)
            {
                readMatrix = null; // the runtime refused the private field: the measured camera from now on
                return null;
            }

            var at = At(player.WorldPosition);
            if (Vector4.Transform(new Vector4(at, 1f), camera).W < 0f)
            {
                camera = -camera;
            }

            return CameraView.Draw(camera, at) is { } drawn && CameraView.InView(drawn) ? camera : null;
        }

        private static Vector3 At(StdTuple3D<float> world) => new(world.X, world.Y, world.Z);

        private static Func<WorldData, Matrix4x4>? MatrixReader()
        {
            var snap = typeof(WorldData).GetField("matrixSnap", BindingFlags.Instance | BindingFlags.NonPublic);
            var matrix = snap?.FieldType.GetProperty("M", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (snap == null || matrix == null || matrix.PropertyType != typeof(Matrix4x4))
            {
                return null;
            }

            try
            {
                var world = Expression.Parameter(typeof(WorldData), "world");
                return Expression.Lambda<Func<WorldData, Matrix4x4>>(Expression.Property(Expression.Field(world, snap), matrix), world).Compile();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
