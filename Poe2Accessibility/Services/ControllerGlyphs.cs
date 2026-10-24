namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

        /// <summary>The button that interacts (A, Cross), named for the pad the game shows.</summary>
        public static string Interact => Announcements.ButtonName(InteractGlyph, PlayStationSeen);

        private const string InteractGlyph = "Button4";

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
        private static readonly Regex SpaceBeforeStop = new(@"\s+([.,;:!?])", RegexOptions.Compiled);

        /// <summary>A button name's last word this long is a word the sentence may already say ("derecho", "stick"); "A" is not.</summary>
        private const int MinNamedWordChars = 4;

        /// <summary>The run of buttons a legend starts with ("&lt;&lt;rt&gt;&gt; + &lt;&lt;lstick&gt;&gt; "), joined by "+" or spaces.</summary>
        private static readonly Regex LeadingTokens = new(@"^\s*(?:<<\w+(?:;[^>]*)?>>\s*\+?\s*)+", RegexOptions.Compiled);

        /// <summary>A wide gap in one text separates two prompts ("&lt;&lt;lstick&gt;&gt; to move     &lt;&lt;rstick&gt;&gt; to aim").</summary>
        private static readonly Regex WideGap = new(@"[ \t]{3,}", RegexOptions.Compiled);

        /// <summary>
        ///     Inline item art ("Identificar &lt;&lt;itemvisual:CurrencyIdentification&gt;&gt; x 1"); "Use an &lt;&lt;itemvisual:…&gt;&gt;
        ///     &lt;currencyitem&gt;{Orb of Transmutation}" names the item itself right after.
        /// </summary>
        private static readonly Regex ItemArt = new(@"<<itemvisual:(\w+)>>(\s*<\w+>\{)?", RegexOptions.Compiled);

        /// <summary>Any other inline art token, dropped as the game's stripped copy drops it.</summary>
        private static readonly Regex ArtToken = new(@"<<\w+:[^>]*>>", RegexOptions.Compiled);

        private static readonly Regex HorizontalSpaces = new(@"[ \t]{2,}", RegexOptions.Compiled);

        /// <summary>
        ///     True when every button of a tagged text comes first (a legend: "&lt;&lt;rt&gt;&gt; + &lt;&lt;lstick&gt;&gt; Move"); false when
        ///     a button also sits later in the text, which is then a sentence read in place ("stick izquierdo para
        ///     moverte, stick derecho para apuntar").
        /// </summary>
        public static bool OnlyLeadingButtons(string tagged) => !MarkupToken.IsMatch(LeadingTokens.Replace(tagged, string.Empty, 1));

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
            var source = WideGap.Replace(NameItemArt(tagged), ", ");
            var spoken = MarkupToken.Replace(source, match =>
            {
                if (!MarkupFiles.TryGetValue(match.Groups[1].Value, out var file))
                {
                    return " ";
                }

                // "with the right stick <<rstick>> to strafe": the sentence names the button, the icon only shows it.
                var name = Announcements.ButtonName(file, PlayStationSeen);
                return NamedBefore(source[..match.Index], name) ? " " : $" {name} ";
            });
            spoken = Tag.Replace(spoken, string.Empty).Replace("{", string.Empty).Replace("}", string.Empty);
            return SpaceBeforeStop.Replace(Spaces.Replace(spoken, " ").Trim(), "$1"); // "press A ." from "<<a>>."
        }

        /// <summary>True when the text shows an item only by its art, one <see cref="ItemNames"/> can name.</summary>
        public static bool HasUnnamedItemArt(string tagged) =>
            ItemArt.Matches(tagged).Any(match => !match.Groups[2].Success && ItemNames.Of(match.Groups[1].Value) != null);

        /// <summary>
        ///     The plain text as the game strips it, the item art named in place ("Identificar Pergamino de sabiduría x 1"):
        ///     buttons and other art dropped, tags and braces gone, line breaks kept.
        /// </summary>
        public static string Plain(string tagged)
        {
            var plain = Tag.Replace(MarkupToken.Replace(NameItemArt(tagged), " "), string.Empty).Replace("{", string.Empty).Replace("}", string.Empty);
            return SpaceBeforeStop.Replace(HorizontalSpaces.Replace(plain, " "), "$1").Trim();
        }

        /// <summary>Item art the text does not name itself is named; all other art tokens go.</summary>
        private static string NameItemArt(string tagged) =>
            ArtToken.Replace(
                ItemArt.Replace(tagged, match => match.Groups[2].Success || ItemNames.Of(match.Groups[1].Value) is not { } name ? match.Groups[2].Value : $" {name} "),
                string.Empty);

        /// <summary>The text before a glyph already ends with the button's name ("joystick derecho" before "stick derecho").</summary>
        private static bool NamedBefore(string before, string name)
        {
            var last = name.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } words ? words[^1] : string.Empty;
            return last.Length >= MinNamedWordChars && Tag.Replace(before, string.Empty).TrimEnd().EndsWith(last, StringComparison.OrdinalIgnoreCase);
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
