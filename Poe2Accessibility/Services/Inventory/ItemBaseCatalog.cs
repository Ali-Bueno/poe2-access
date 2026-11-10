namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     The bases a character wears or wields as the game's files describe them (catalog/item-bases.json,
    ///     `Poe2UiExtract item-bases`; docs/research/file-catalogs.md, "Item bases"): each by its BaseItemTypes Id with its
    ///     item class, level and base defences, and its name in every language. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class ItemBaseCatalog : IItemBases
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        /// <summary>A keyword link of the game's texts, "[Keyword|shown]" or "[Keyword]".</summary>
        private static readonly Regex KeywordLink = new(@"\[([^\]|]+)(?:\|([^\]]+))?\]", RegexOptions.Compiled);

        private readonly Dictionary<string, ItemBase> byId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ItemBase?> byLastPart = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ItemBase?> byName = new(StringComparer.Ordinal);

        private ItemBaseCatalog(FileData data)
        {
            var names = data.Names ?? new();
            foreach (var (id, entry) in (data.Bases ?? new()).OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var own = names.Where(language => language.Value.ContainsKey(id)).ToDictionary(language => language.Key, language => language.Value[id], StringComparer.Ordinal);
                var item = new ItemBase(id, entry.Class ?? string.Empty, own, entry.Armour, entry.Evasion, entry.Es, entry.Level);
                this.byId[id] = item;
                var last = id[(id.LastIndexOf('/') + 1)..];
                this.byLastPart[last] = this.byLastPart.ContainsKey(last) ? null : item;
                foreach (var key in own.Values.Select(NameKey).Where(key => key.Length > 0).Distinct(StringComparer.Ordinal))
                {
                    // The first in Id order stands for bases the files tell nothing apart of; a name of two different ones is none's.
                    this.byName[key] = !this.byName.TryGetValue(key, out var known) ? item : known != null && SameBase(known, item) ? known : null;
                }
            }

            this.Languages = names.Count;
        }

        /// <summary>The catalog the plugin loaded; empty until then, so every lookup answers null.</summary>
        public static ItemBaseCatalog Shared { get; private set; } = new(new FileData());

        /// <summary>Gets how many bases the catalog holds.</summary>
        public int Count => this.byId.Count;

        /// <summary>Gets how many languages name them.</summary>
        public int Languages { get; }

        /// <summary>Reads the catalog; a missing or unreadable one is logged once and leaves every base unknown.</summary>
        public static void Load(PluginLog log, string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    log.Warn($"Item base catalog: {path} not found; no item base is known");
                    return;
                }

                Shared = Parse(File.ReadAllText(path));
                log.Info($"Item base catalog: {Shared.Count} bases, {Shared.Languages} languages from the game files");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                log.Warn($"Item base catalog unreadable ({error.Message}); no item base is known");
            }
        }

        /// <summary>The catalog in <paramref name="json"/>; a JsonException when it is not one.</summary>
        public static ItemBaseCatalog Parse(string json) => new(JsonSerializer.Deserialize<FileData>(json, Json) ?? new FileData());

        /// <inheritdoc/>
        /// <remarks>Null too when same-class bases share the name and the files give them another level or other defences.</remarks>
        public ItemBase? ByName(string name) => this.byName.TryGetValue(NameKey(name), out var item) ? item : null;

        /// <inheritdoc/>
        public ItemBase? ById(string id) =>
            this.byId.TryGetValue(id.Trim(), out var item) ? item : this.byLastPart.TryGetValue(id.Trim(), out var last) ? last : null;

        /// <summary>A name as it is compared: markup out, no spaces at all, one case.</summary>
        internal static string NameKey(string name)
        {
            var plain = BuildNotes.Plain(KeywordLink.Replace(name, link => link.Groups[2].Success ? link.Groups[2].Value : link.Groups[1].Value));
            return string.Concat(plain.Where(letter => !char.IsWhiteSpace(letter))).ToLowerInvariant();
        }

        private static bool SameBase(ItemBase a, ItemBase b) =>
            a.ItemClass == b.ItemClass && a.RequiredLevel == b.RequiredLevel && a.Armour == b.Armour && a.Evasion == b.Evasion && a.EnergyShield == b.EnergyShield;

        /// <summary>The file as `item-bases` writes it.</summary>
        private sealed class FileData
        {
            public Dictionary<string, BaseData>? Bases { get; set; }

            public Dictionary<string, Dictionary<string, string>>? Names { get; set; }
        }

        private sealed class BaseData
        {
            public string? Class { get; set; }

            public int Level { get; set; }

            public int Armour { get; set; }

            public int Evasion { get; set; }

            public int Es { get; set; }
        }
    }
}
