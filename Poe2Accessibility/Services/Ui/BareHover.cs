namespace Poe2Accessibility.Services
{
    using Poe2Accessibility.Models;

    /// <summary>Pure: whether a control the free cursor rests on that never gets a tooltip is said (docs/research/robustness-audit-2026-11-10.md, N1).</summary>
    public static class BareHover
    {
        /// <summary>How long the cursor stays on one control before it is at rest: twice the tracker's own re-check of a hovered control.</summary>
        public const int RestMs = 500;

        /// <summary>The longest caption a control has; longer is a page's text gathered under a background.</summary>
        public const int MaxCaption = 80;

        /// <summary>
        ///     In game, a control a player acts on, named by a caption of its own, once the cursor has rested on it. Out of the
        ///     game the pointer is the mouse's: the login's Exit under a mouse left there is not where the pad is.
        /// </summary>
        public static bool IsSaid(bool inGame, UiRole role, string text, bool namedByControlId, long restedMs) =>
            inGame && restedMs >= RestMs && text.Length > 0 && text.Length <= MaxCaption && !namedByControlId &&
            role is UiRole.Button or UiRole.CheckBox or UiRole.Tab or UiRole.ComboBox or UiRole.Slider or UiRole.TextField;
    }
}
