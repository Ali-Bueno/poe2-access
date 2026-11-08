namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     The pictures a text shows inline ("&lt;&lt;village_gold&gt;&gt; 500" in the Currency Exchange, "{0} &lt;&lt;spirit_resource_icon&gt;&gt;"
    ///     on the spirit tab, "&lt;&lt;TickIcon&gt;&gt; Name" for a finished entry): the game's stripped copy drops them, so the amount
    ///     was said without its unit and a tick without its meaning. Tokens come from the game's own table
    ///     (`catalog/inline-icons.txt`, `Poe2UiExtract inline-icons` from uisettings.xml MarkupImages); a token is named by
    ///     its art, so one a patch adds over known art is read too. Pad glyph tokens ("Controller/Button4") stay
    ///     <see cref="ControllerGlyphs"/>' business; this class only gives their file.
    /// </summary>
    public static class InlineIcons
    {
        /// <summary>How an icon's word joins the text it sits in.</summary>
        public enum Kind
        {
            /// <summary>A unit of the number beside it ("500 gold"): said after the number.</summary>
            Unit,

            /// <summary>A unit things are paid in (gold, tribute): also a price unit beside a tooltip's number image.</summary>
            Price,

            /// <summary>A state of the line (ticked, checked, deferred): said after the line's text.</summary>
            State,

            /// <summary>Said where it stands (a weapon set's numeral).</summary>
            InPlace,
        }

        private const string GlyphFolder = "Controller/";
        private const char Separator = '\t';
        private const char KeyDetailsMark = '|';

        /// <summary>A placeholder standing for a named icon while the markup is stripped (private-use characters).</summary>
        private const char MarkStart = '';
        private const char MarkEnd = '';

        private static readonly Regex Token = new(@"<<(\w+)(?:;[^>]*)?>>", RegexOptions.Compiled);

        private static readonly Regex Mark = new("(\\w+)", RegexOptions.Compiled);

        /// <summary>A number right after a unit icon: "500", "1.250", "3/10".</summary>
        private static readonly Regex LeadingNumber = new(@"^\s*(\d[\d.,]*(?:\s*/\s*\d[\d.,]*)?)", RegexOptions.Compiled);

        private static readonly Regex Spaces = new(@"[ \t]{2,}", RegexOptions.Compiled);
        private static readonly Regex SpaceBeforeStop = new(@"[ \t]+([.,;:!?])", RegexOptions.Compiled);

        /// <summary>A weapon set's own art ("InventoryPanelWeaponSwap2Hover"): its numeral is the set.</summary>
        private static readonly Regex WeaponSetArt = new(@"^inventorypanelweaponswap(\d)", RegexOptions.Compiled);

        /// <summary>Art file stems (lower case) whose picture means a word; every other picture stays unsaid, as the game strips it.</summary>
        private static readonly Dictionary<string, Kind> Meanings = new(StringComparer.Ordinal)
        {
            ["marketicongold"] = Kind.Price,
            ["killpointicon"] = Kind.Price,
            ["spiriticon"] = Kind.Unit,
            ["partyportalicon"] = Kind.Unit,
            ["partyrevivesicon"] = Kind.Unit,
            ["stashtickicon"] = Kind.State,
            ["checkboxchecked"] = Kind.State,
            ["checkboxunchecked"] = Kind.State,
            ["savedshopitemicon"] = Kind.State,
        };

        private static Dictionary<string, string> arts = new(StringComparer.Ordinal);

        /// <summary>Reads the token table ("token TAB art" lines); without it no icon is named and texts read as before.</summary>
        public static void Load(PluginLog log, string file)
        {
            try
            {
                Set(File.Exists(file) ? Parse(File.ReadAllLines(file)) : new Dictionary<string, string>());
                log.Info($"Inline icons: {arts.Count} markup images, {arts.Values.Count(art => Meaning(art) != null)} named, from {file}");
            }
            catch (IOException ex)
            {
                log.Warn($"Inline icons: cannot read {file}: {ex.Message}");
            }
        }

        /// <summary>For tests and <see cref="Load"/>: token → art as the game's table holds it.</summary>
        public static void Set(IReadOnlyDictionary<string, string> table) => arts = new Dictionary<string, string>(table, StringComparer.Ordinal);

        public static Dictionary<string, string> Parse(IEnumerable<string> lines)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in lines)
            {
                var tab = line.IndexOf(Separator);
                if (tab > 0 && tab < line.Length - 1)
                {
                    table.TryAdd(line[..tab].Trim(), line[(tab + 1)..].Trim());
                }
            }

            return table;
        }

        /// <summary>The glyph file a pad token shows ("Controller/Button4" → "Button4"); null for other tokens.</summary>
        public static string? GlyphFile(string token) =>
            arts.TryGetValue(token, out var art) && art.StartsWith(GlyphFolder, StringComparison.Ordinal) ? art[GlyphFolder.Length..] : null;

        /// <summary>The word and kind an icon token means; null when the picture means nothing to say.</summary>
        public static (string Word, Kind Kind)? MeaningOfToken(string token) => arts.TryGetValue(token, out var art) ? Meaning(art) : null;

        /// <summary>
        ///     The word and kind an art path or texture key means ("Art/2DArt/UIImages/InGame/Ritual/killpointicon|…"),
        ///     by its file stem; null for any other picture.
        /// </summary>
        public static (string Word, Kind Kind)? Meaning(string art)
        {
            var stem = Stem(art);
            if (WeaponSetArt.Match(stem) is { Success: true } set)
            {
                return (Announcements.WeaponSet(set.Groups[1].Value[0] - '0'), Kind.InPlace);
            }

            return Meanings.TryGetValue(stem, out var kind) && Announcements.InlineIconWord(stem) is { } word ? (word, kind) : null;
        }

        /// <summary>The unit a price picture beside a number means (a tooltip's cost shown with the tribute icon); null otherwise.</summary>
        public static string? PriceUnit(string textureKey) => Meaning(textureKey) is { Kind: Kind.Price } price ? price.Word : null;

        /// <summary>
        ///     A bare number drawn with a unit picture of its own beside it, a child element and not inline markup (the spirit tab's
        ///     assigned spirit: "30" holding the spirit icon, 2026-11-08), said with its unit: "30 spirit"; any other text as it is.
        /// </summary>
        public static string WithPicture(string text, string art) =>
            LeadingNumber.Match(text) is { Success: true } number && number.Length == text.Length &&
            Meaning(art) is { Kind: Kind.Unit or Kind.Price } unit
                ? $"{text.Trim()} {unit.Word}"
                : text;

        /// <summary>True when the tagged text shows a picture that means a word.</summary>
        public static bool HasNamed(string tagged) =>
            tagged.Contains("<<", StringComparison.Ordinal) && Token.Matches(tagged).Any(match => MeaningOfToken(match.Groups[1].Value) != null);

        /// <summary>
        ///     Replaces each named picture with a placeholder that survives the markup being stripped; <see cref="Place"/>
        ///     turns the placeholders into words once the text is plain.
        /// </summary>
        public static string MarkNamed(string tagged) =>
            !tagged.Contains("<<", StringComparison.Ordinal)
                ? tagged
                : Token.Replace(tagged, match => MeaningOfToken(match.Groups[1].Value) != null ? $"{MarkStart}{match.Groups[1].Value}{MarkEnd}" : match.Value);

        /// <summary>
        ///     The plain text with each placeholder said as a word: a unit after the number it belongs to ("500 gold"), a state
        ///     after the line ("Name, completed"), any other where it stands; a word the line already says is not repeated
        ///     ("30 Spirit &lt;&lt;spirit_resource_icon&gt;&gt;" stays "30 Spirit").
        /// </summary>
        public static string Place(string plain)
        {
            if (plain.IndexOf(MarkStart) < 0)
            {
                return plain;
            }

            return string.Join('\n', plain.Split('\n').Select(PlaceLine));
        }

        private static string PlaceLine(string line)
        {
            var bare = Mark.Replace(line, " ");
            var states = new List<string>();
            var result = line;
            Match match;
            while ((match = Mark.Match(result)).Success)
            {
                var meaning = MeaningOfToken(match.Groups[1].Value);
                var before = result[..match.Index];
                var after = result[(match.Index + match.Length)..];
                if (meaning is not { } named || SaysWord(bare, named.Word) || states.Contains(named.Word))
                {
                    result = before + after;
                    continue;
                }

                if (named.Kind == Kind.State)
                {
                    states.Add(named.Word);
                    result = before + after;
                }
                else if (named.Kind is Kind.Unit or Kind.Price && LeadingNumber.Match(after) is { Success: true } number)
                {
                    result = $"{before} {number.Groups[1].Value} {named.Word}{after[(number.Index + number.Length)..]}";
                }
                else
                {
                    result = $"{before} {named.Word} {after}";
                }
            }

            result = SpaceBeforeStop.Replace(Spaces.Replace(result, " "), "$1").Trim();
            return states.Count == 0 ? result : result.Length == 0 ? string.Join(", ", states) : $"{result}, {string.Join(", ", states)}";
        }

        /// <summary>The line already holds the icon's word as a word of its own (any case).</summary>
        private static bool SaysWord(string line, string word) =>
            Regex.IsMatch(line, $@"(?<![\p{{L}}]){Regex.Escape(word)}(?![\p{{L}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>"Art/2DArt/UIImages/InGame/Ritual/killpointicon|0,0|0" or "…/DodgeRoll.dds" → "killpointicon" / "dodgeroll".</summary>
        private static string Stem(string art)
        {
            var end = art.IndexOf(KeyDetailsMark);
            var path = end < 0 ? art : art[..end];
            var file = path[(path.LastIndexOf('/') + 1)..];
            var dot = file.IndexOf('.');
            return (dot < 0 ? file : file[..dot]).ToLowerInvariant();
        }
    }
}
