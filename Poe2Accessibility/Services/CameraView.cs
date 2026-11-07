namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>
    ///     How the game's camera draws the ground around the player, in screen heights (x right, y down): the same at every
    ///     window size and aspect, so what a sighted player would see is judged on a 16:9 screen whatever the window, the
    ///     monitor or its scaling. Measured 2026-11-07 at six window sizes (docs/research/camera-view.md). Pure.
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

        /// <summary>A world offset from the player as the camera draws it, in screen heights.</summary>
        public static Vector2 Offset(float dx, float dy) => new((dx - dy) * AcrossPerWorld, (dx + dy) * DownPerWorld);

        /// <summary>Whether a 16:9 screen shows the place at this world offset from the player.</summary>
        public static bool InView(float dx, float dy)
        {
            var at = Offset(dx, dy);
            var fromCentre = at.Y - PlayerAboveCentre;
            return MathF.Abs(at.X) <= HalfWidth && fromCentre >= -HalfHeight && fromCentre <= HalfHeight;
        }

        /// <summary>A world offset from the player in half screens (1 = a 16:9 screen's edge), for panning by side and nearness.</summary>
        public static Vector2 HalfScreens(float dx, float dy)
        {
            var at = Offset(dx, dy);
            return new Vector2(at.X / HalfWidth, at.Y / HalfHeight);
        }

        /// <summary>Whether measured screen axes can be trusted: finite and not folded flat (a window gone or a camera not drawn yet).</summary>
        public static bool IsUsable(Vector2 alongX, Vector2 alongY) =>
            float.IsFinite(alongX.X) && float.IsFinite(alongX.Y) && float.IsFinite(alongY.X) && float.IsFinite(alongY.Y) &&
            MathF.Abs((alongX.X * alongY.Y) - (alongX.Y * alongY.X)) > float.Epsilon;
    }
}
