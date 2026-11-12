namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;

    /// <summary>What a trial icon stands for, by its key's prefix.</summary>
    public enum TrialEntryKind
    {
        Affliction,
        Boon,
        Modifier,
        Room,
        Ascendancy,
        Other,
    }

    /// <summary>
    ///     The Ascendancy trials' icons and HUD formats from the game's tables (catalog/trials.json, `Poe2UiExtract trial-data`):
    ///     an icon's art names a Sekhemas affliction or boon, a Trial of Chaos modifier, a room type or a class, in the game's
    ///     language; the HUD lines "Round {0}/{1}" and "{0} Death Crystals remaining" become patterns. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class TrialCatalog
    {
        public const string IconsEntry = "icons";
        public const string AboutSuffix = ":about";
        public const string RoundLabel = "label:round";
        public const string CrystalsLabel = "label:crystals";

        private const string EffectPrefix = "effect:";
        private const string AfflictionPrefix = EffectPrefix + "Affliction";
        private const string BoonPrefix = EffectPrefix + "Boon";
        private const string ModifierPrefix = "mod:";
        private const string RoomPrefix = "room:";
        private const string AscendancyPrefix = "ascendancy:";

        /// <summary>A markup tag, "&lt;white&gt;" or "&lt;ultimatumnumber&gt;", shown as nothing.</summary>
        private static readonly Regex Tag = new(@"<\w+>", RegexOptions.Compiled);

        /// <summary>A number's place in the format, "{0}".</summary>
        private static readonly Regex Hole = new(@"\{\d+\}", RegexOptions.Compiled);

        private readonly Dictionary<string, string> icons;
        private readonly Dictionary<string, Dictionary<string, string>> texts;
        private readonly Dictionary<string, Regex?> patterns = new(StringComparer.Ordinal);

        public TrialCatalog(Dictionary<string, string> icons, Dictionary<string, Dictionary<string, string>> texts)
        {
            this.icons = icons;
            this.texts = texts;
        }

        public static TrialCatalog Empty { get; } = new(new Dictionary<string, string>(), new Dictionary<string, Dictionary<string, string>>());

        /// <summary>Icons known.</summary>
        public int Count => this.icons.Count;

        public static TrialCatalog Load(string path, PluginLog? log)
        {
            if (!File.Exists(path))
            {
                log?.Warn($"No trial data at {path}: trial icons and counters stay unnamed");
                return Empty;
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path)) ?? new();
                var icons = parsed.TryGetValue(IconsEntry, out var byIcon) ? new Dictionary<string, string>(byIcon, StringComparer.Ordinal) : new();
                var texts = parsed.Where(pair => pair.Key != IconsEntry).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                log?.Info($"Trial data: {icons.Count} icons, {texts.Count} languages");
                return new TrialCatalog(icons, texts);
            }
            catch (Exception ex)
            {
                log?.Error("Trial data not read", ex);
                return Empty;
            }
        }

        /// <summary>The pattern a HUD line follows, from the game's format: "Round &lt;ultimatumnumber&gt;{{{0}/{1}}}" matches "Round 3/10"; null for no numbers.</summary>
        public static Regex? PatternOf(string format)
        {
            var plain = Tag.Replace(format, string.Empty);
            var parts = Hole.Split(plain);
            if (parts.Length < 2)
            {
                return null;
            }

            var pattern = string.Join(@"\s*(\d+)\s*", parts.Select(part => Regex.Escape(part.Replace("{", string.Empty).Replace("}", string.Empty).Trim())));
            return new Regex($"^{pattern}$", RegexOptions.Compiled);
        }

        /// <summary>The key an icon's art stands for ("Art/2DArt/UIImages/InGame/Sanctum/BoonCurseIcons/CurseHonedClawsIcon.dds"); null for any other image.</summary>
        public string? KeyOfIcon(string textureKey)
        {
            var end = textureKey.LastIndexOf('.');
            var art = (end > textureKey.LastIndexOf('/') ? textureKey[..end] : textureKey).ToLowerInvariant();
            return this.icons.TryGetValue(art, out var key) ? key : null;
        }

        /// <summary>The key of an affliction or boon by its SanctumPersistentEffects Id; null for an Id the tables do not hold.</summary>
        public string? KeyOfEffect(string id) =>
            id.Length > 0 && EffectPrefix + id is var key && KindOf(key) is TrialEntryKind.Affliction or TrialEntryKind.Boon && this.Text(key).Length > 0 ? key : null;

        public static TrialEntryKind KindOf(string key) => key switch
        {
            _ when key.StartsWith(AfflictionPrefix, StringComparison.Ordinal) => TrialEntryKind.Affliction,
            _ when key.StartsWith(BoonPrefix, StringComparison.Ordinal) => TrialEntryKind.Boon,
            _ when key.StartsWith(ModifierPrefix, StringComparison.Ordinal) => TrialEntryKind.Modifier,
            _ when key.StartsWith(RoomPrefix, StringComparison.Ordinal) => TrialEntryKind.Room,
            _ when key.StartsWith(AscendancyPrefix, StringComparison.Ordinal) => TrialEntryKind.Ascendancy,
            _ => TrialEntryKind.Other,
        };

        /// <summary>The text of a key in the game's language, else in English, else empty.</summary>
        public string Text(string key) => Localization.Pick(this.texts, Localization.Language, key) ?? string.Empty;

        /// <summary>A key's description.</summary>
        public string About(string key) => this.Text(key + AboutSuffix);

        /// <summary>The pattern of a HUD label, null when the catalog has none.</summary>
        public Regex? Pattern(string label)
        {
            if (!this.patterns.TryGetValue(label, out var pattern))
            {
                pattern = this.Text(label) is { Length: > 0 } format ? PatternOf(format) : null;
                this.patterns[label] = pattern;
            }

            return pattern;
        }
    }
}
