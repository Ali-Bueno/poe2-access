namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    /// <summary>
    ///     The skill and support gems as the game's files describe them (catalog/gems.json, `Poe2UiExtract gems`;
    ///     docs/research/file-catalogs.md): each gem by its base item Id with its kind, GemEffects and GrantedEffects Ids, tags
    ///     and attribute shares, and per language the gems' names, the tags' names, what a support does and every skill's
    ///     displayed name by GrantedEffects Id. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class GemCatalog
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        private readonly FileData data;

        private GemCatalog(FileData data)
        {
            this.data = data;
            this.Gems = (data.Gems ?? new()).Select(pair => new Gem(
                    pair.Key, pair.Value.Kind ?? string.Empty, pair.Value.Effects ?? new(), pair.Value.Skills ?? new(), pair.Value.Tags ?? new(),
                    pair.Value.Str, pair.Value.Dex, pair.Value.Int))
                .ToList();
        }

        /// <summary>The catalog the plugin loaded; empty until then, so every lookup finds nothing and memory is read.</summary>
        public static GemCatalog Shared { get; private set; } = new(new FileData());

        /// <summary>Gets every gem of the files, in the file's order (by Id).</summary>
        public IReadOnlyList<Gem> Gems { get; }

        /// <summary>Reads the catalog; a missing or unreadable one is logged once and leaves skill names read from memory.</summary>
        public static void Load(PluginLog log, string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    log.Warn($"Gem catalog: {path} not found; skill names are read from memory, gems have no data");
                    return;
                }

                Shared = Parse(File.ReadAllText(path));
                log.Info($"Gem catalog: {Shared.Gems.Count} gems, {Shared.SkillCount("en")} skill names, {Shared.data.Names?.Count ?? 0} languages from the game files");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                log.Warn($"Gem catalog unreadable ({error.Message}); skill names are read from memory, gems have no data");
            }
        }

        /// <summary>The catalog in <paramref name="json"/>; a JsonException when it is not one.</summary>
        public static GemCatalog Parse(string json) => new(JsonSerializer.Deserialize<FileData>(json, Json) ?? new FileData());

        /// <summary>A gem's name by its base item Id, in the language, else in English; null when the files name none.</summary>
        public string? GemName(string baseItemId, string language) => Pick(this.data.Names, language, baseItemId);

        /// <summary>A tag's name ("Hielo" for cold) in the language, else in English; null for a tag the game shows no name for.</summary>
        public string? TagName(string tagId, string language) => Pick(this.data.Tags, language, tagId);

        /// <summary>What a support gem does, by its base item Id, markup out; null when the files hold no such text.</summary>
        public string? SupportText(string baseItemId, string language) => Pick(this.data.Support, language, baseItemId);

        /// <summary>A skill's displayed name ("Nova de hielo") by its GrantedEffects Id ("IceNovaPlayer"); null when the files name none.</summary>
        public string? SkillName(string grantedEffectId, string language) => Pick(this.data.Skills, language, grantedEffectId);

        /// <summary>
        ///     Whether the files give the skill this name in any language (outer spaces ignored): the game shows names in its
        ///     own language, which a language setting of the mod's need not be.
        /// </summary>
        public bool IsSkillName(string grantedEffectId, string name) =>
            this.data.Skills != null && this.data.Skills.Values.Any(skills => skills.TryGetValue(grantedEffectId, out var known) && string.Equals(known.Trim(), name.Trim(), StringComparison.Ordinal));

        /// <summary>How many skills have a name in the language.</summary>
        public int SkillCount(string language) => this.data.Skills != null && this.data.Skills.TryGetValue(language, out var skills) ? skills.Count : 0;

        /// <summary>Every gem's name in the language alone (no English stand-in), for an index by name.</summary>
        public IEnumerable<KeyValuePair<string, string>> NamesIn(string language) =>
            this.data.Names != null && this.data.Names.TryGetValue(language, out var names) ? names : Enumerable.Empty<KeyValuePair<string, string>>();

        private static string? Pick(Dictionary<string, Dictionary<string, string>>? table, string language, string key) =>
            table == null ? null : Localization.Pick(table, language, key);

        /// <summary>
        ///     One gem: its BaseItemTypes Id, the last part of its base item's parent path (ActiveSkillGem, SupportSkillGem,
        ///     MetaSkillGem), its GemEffects and GrantedEffects Ids, its GemTags Ids, and the shares (0 to 100) of its attribute
        ///     requirement.
        /// </summary>
        public sealed record Gem(string Id, string Kind, IReadOnlyList<string> Effects, IReadOnlyList<string> Skills, IReadOnlyList<string> Tags, int Strength, int Dexterity, int Intelligence);

        /// <summary>The file as `gems` writes it.</summary>
        private sealed class FileData
        {
            public Dictionary<string, GemData>? Gems { get; set; }

            public Dictionary<string, Dictionary<string, string>>? Names { get; set; }

            public Dictionary<string, Dictionary<string, string>>? Tags { get; set; }

            public Dictionary<string, Dictionary<string, string>>? Support { get; set; }

            public Dictionary<string, Dictionary<string, string>>? Skills { get; set; }
        }

        private sealed class GemData
        {
            public string? Kind { get; set; }

            public List<string>? Effects { get; set; }

            public List<string>? Skills { get; set; }

            public List<string>? Tags { get; set; }

            public int Str { get; set; }

            public int Dex { get; set; }

            public int Int { get; set; }
        }
    }
}
