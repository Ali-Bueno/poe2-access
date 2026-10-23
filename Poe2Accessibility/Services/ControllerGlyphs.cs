namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     Identifies controller button glyphs: image art keys ("…/Gamepad/DualSense/Button4.dds|…") and text
    ///     markup tokens ("&lt;&lt;xbox_button_a&gt;&gt;"). Glyph files are the same in every controller family;
    ///     only the folder says whether the game shows PlayStation or Xbox buttons.
    /// </summary>
    public static class ControllerGlyphs
    {
        private const string GamepadFolder = "/Gamepad/";

        /// <summary>Glyph token, optionally with a variant suffix: "&lt;&lt;xbox_button_lt;0&gt;&gt;" (panel legends).</summary>
        private static readonly Regex MarkupToken = new(@"<<(\w+)(?:;[^>]*)?>>", RegexOptions.Compiled);

        /// <summary>Markup token → glyph file, from the game's own glyph table (heap, 2026-10-23).</summary>
        private static readonly Dictionary<string, string> MarkupFiles = new(StringComparer.Ordinal)
        {
            ["xbox_button_a"] = "Button4",
            ["xbox_button_b"] = "Button3",
            ["xbox_button_x"] = "Button1",
            ["xbox_button_y"] = "Button2",
            ["xbox_button_lb"] = "ShoulderButtonLeft",
            ["xbox_button_rb"] = "ShoulderButtonRight",
            ["xbox_button_lt"] = "TriggerLeft",
            ["xbox_button_rt"] = "TriggerRight",
            ["xbox_button_lstick"] = "LeftStickButton2",
            ["xbox_button_rstick"] = "RightStickButton2",
            ["xbox_lstick"] = "LeftStick2",
            ["xbox_rstick"] = "RightStick2",
            ["xbox_dpad"] = "DPad",
            ["xbox_button_dpad_up"] = "DPadUp",
            ["xbox_button_dpad_down"] = "DPadDown",
            ["xbox_button_dpad_left"] = "DPadLeft",
            ["xbox_button_dpad_right"] = "DPadRight",
            ["xbox_button_dpad_updown"] = "DPadUpDown",
            ["xbox_button_dpad_leftright"] = "DPadLeftRight",
            ["xbox_button_menu"] = "MenuButton",
            ["xbox_button_view"] = "ViewButton",
        };

        /// <summary>True once a PlayStation glyph folder was seen; markup has no family of its own.</summary>
        public static bool PlayStationSeen { get; private set; }

        /// <summary>Spoken button for an image art key; null when the image is not a controller glyph.</summary>
        public static string? FromTextureKey(string textureKey)
        {
            var folder = textureKey.IndexOf(GamepadFolder, StringComparison.Ordinal);
            if (folder < 0)
            {
                return null;
            }

            var familyStart = folder + GamepadFolder.Length;
            var familyEnd = textureKey.IndexOf('/', familyStart);
            var fileEnd = textureKey.IndexOf(".dds", StringComparison.Ordinal);
            if (familyEnd < 0 || fileEnd < familyEnd)
            {
                return null;
            }

            var family = textureKey[familyStart..familyEnd];
            PlayStationSeen = family is "DualSense" or "DUALSHOCK4";
            return Announcements.ButtonName(textureKey[(familyEnd + 1)..fileEnd], PlayStationSeen);
        }

        private static readonly Regex OtherPlayerToken = new(@"<<\w+;[1-9]>>", RegexOptions.Compiled);
        private static readonly Regex Tag = new(@"<[^<>]*>", RegexOptions.Compiled);
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        public static bool HasGlyphs(string tagged) => MarkupToken.IsMatch(tagged);

        /// <summary>
        ///     The variant suffix is the player: ";0" legends are player one's, ";1" belong to a second controller
        ///     ("Press &lt;&lt;xbox_button_a;1&gt;&gt; to join" for couch co-op), which is never the reader's.
        /// </summary>
        public static bool ForOtherPlayer(string tagged) => OtherPlayerToken.IsMatch(tagged);

        /// <summary>
        ///     Text with each glyph token spoken in place ("&lt;&lt;xbox_lstick&gt;&gt; to Move" → "left stick to Move");
        ///     other markup tags and their braces are dropped, as the game's stripped copy does.
        /// </summary>
        public static string Render(string tagged)
        {
            var spoken = MarkupToken.Replace(tagged, match =>
                MarkupFiles.TryGetValue(match.Groups[1].Value, out var file) ? $" {Announcements.ButtonName(file, PlayStationSeen)} " : " ");
            spoken = Tag.Replace(spoken, string.Empty).Replace("{", string.Empty).Replace("}", string.Empty);
            return Spaces.Replace(spoken, " ").Trim();
        }

        /// <summary>
        ///     Spoken buttons for the glyph tokens of a tagged text ("RT plus L3" for "&lt;&lt;rt&gt;&gt;+&lt;&lt;lstick&gt;&gt;");
        ///     empty when it has none.
        /// </summary>
        public static string FromMarkup(string tagged)
        {
            var phrase = new StringBuilder();
            var previousEnd = -1;
            foreach (Match match in MarkupToken.Matches(tagged))
            {
                if (!MarkupFiles.TryGetValue(match.Groups[1].Value, out var file))
                {
                    continue;
                }

                if (phrase.Length > 0)
                {
                    var between = tagged[previousEnd..match.Index].Trim();
                    phrase.Append(between == "+" ? Announcements.ButtonCombination : Announcements.ButtonSeparator);
                }

                phrase.Append(Announcements.ButtonName(file, PlayStationSeen));
                previousEnd = match.Index + match.Length;
            }

            return phrase.ToString();
        }
    }
}
