namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>
    ///     How the game's camera draws the world, in screen heights from the screen's centre (x right, y down): the same at
    ///     every window size and aspect, so what a sighted player would see is judged on a 16:9 screen whatever the window,
    ///     the monitor or its scaling. First from the camera's own matrix (<see cref="Draw"/>), whose width-to-height ratio is
    ///     divided out; the camera measured 2026-11-07 at six window sizes only when no matrix can be read
    ///     (docs/research/camera-view.md). Pure.
    /// </summary>
    public static class CameraView
    {
        /// <summary>Screen heights across per world unit: world +X draws right, world +Y left.</summary>
        private const float AcrossPerWorld = 0.059217f / 100f;

        /// <summary>Screen heights down per world unit: world +X and +Y both draw up.</summary>
        private const float DownPerWorld = -0.043852f / 100f;

        /// <summary>The camera keeps the player this far above the screen's centre, in heights.</summary>
        private const float PlayerAboveCentre = 0.0566f;

        private const float HalfHeight = 0.5f;

        /// <summary>Half a 16:9 screen's width, in heights: the most common aspect, and the narrowest that hides nothing.</summary>
        private const float HalfWidth = 0.5f * 16f / 9f;

        /// <summary>A point whose clip depth is below this is at or behind the camera: nothing is drawn there.</summary>
        private const float MinDepth = 1e-3f;

        /// <summary>Where the measured camera draws the player, from the screen's centre.</summary>
        private static readonly Vector2 PlayerDrawn = new(0f, -PlayerAboveCentre);

        /// <summary>A world offset from the player as the measured camera draws it, in screen heights.</summary>
        public static Vector2 Offset(float dx, float dy) => new((dx - dy) * AcrossPerWorld, (dx + dy) * DownPerWorld);

        /// <summary>
        ///     Where a camera matrix (world to clip space, row vectors, as the game keeps it) draws a world point, in screen
        ///     heights from the screen's centre. Clip x spans the width and clip y the height whatever the window, so x is
        ///     scaled by the matrix's own width-to-height ratio: its across and up columns without their part along the view.
        ///     Null for a point at or behind the camera, or a matrix that draws nothing.
        /// </summary>
        public static Vector2? Draw(in Matrix4x4 camera, Vector3 world)
        {
            var clip = Vector4.Transform(new Vector4(world, 1f), camera);
            var view = new Vector3(camera.M14, camera.M24, camera.M34);
            if (!(clip.W > MinDepth) || !(view.LengthSquared() > 0f) || !float.IsFinite(view.LengthSquared()))
            {
                return null;
            }

            view = Vector3.Normalize(view);
            var wide = Flat(new Vector3(camera.M11, camera.M21, camera.M31), view);
            var tall = Flat(new Vector3(camera.M12, camera.M22, camera.M32), view);
            if (!(wide > 0f) || !(tall > 0f))
            {
                return null;
            }

            var drawn = new Vector2(clip.X / clip.W * (tall / wide), -clip.Y / clip.W) * HalfHeight;
            return float.IsFinite(drawn.X) && float.IsFinite(drawn.Y) ? drawn : null;
        }

        /// <summary>Whether a 16:9 screen shows a place drawn this far from its centre.</summary>
        public static bool InView(Vector2 drawn) => MathF.Abs(drawn.X) <= HalfWidth && MathF.Abs(drawn.Y) <= HalfHeight;

        /// <summary>Whether a 16:9 screen around the player shows the place at this world offset, by the measured camera.</summary>
        public static bool InView(float dx, float dy) => InView(Offset(dx, dy) + PlayerDrawn);

        /// <summary>A drawn offset in half screens (1 = a 16:9 screen's edge), for panning by side and nearness.</summary>
        public static Vector2 HalfScreens(Vector2 drawn) => new(drawn.X / HalfWidth, drawn.Y / HalfHeight);

        /// <summary>A world offset from the player in half screens, by the measured camera.</summary>
        public static Vector2 HalfScreens(float dx, float dy) => HalfScreens(Offset(dx, dy));

        /// <summary>Whether measured screen axes can be trusted: finite and not folded flat (a window gone or a camera not drawn yet).</summary>
        public static bool IsUsable(Vector2 alongX, Vector2 alongY) =>
            float.IsFinite(alongX.X) && float.IsFinite(alongX.Y) && float.IsFinite(alongY.X) && float.IsFinite(alongY.Y) &&
            MathF.Abs((alongX.X * alongY.Y) - (alongX.Y * alongY.X)) > float.Epsilon;

        /// <summary>A matrix column's length across the view: the clip units per world unit across (or up) the screen.</summary>
        private static float Flat(Vector3 column, Vector3 view) => (column - (Vector3.Dot(column, view) * view)).Length();
    }
}
