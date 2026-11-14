namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>One row of the game's Shrines table.</summary>
    /// <param name="Id">The row's Id ("Acceleration", "AccelerationLesser", "FireShrine").</param>
    /// <param name="Lesser">The row's IsLesserShrine flag.</param>
    /// <param name="Buff">The BuffDefinitions Id of the buff the shrine gives the player; empty when the row has none.</param>
    /// <param name="Seconds">How long that buff lasts.</param>
    /// <param name="Names">Language code to the shrine's name.</param>
    /// <param name="Descriptions">Language code to the shrine's flavour text.</param>
    public sealed record ShrineEntry(string Id, bool Lesser, string Buff, int Seconds, IReadOnlyDictionary<string, string> Names, IReadOnlyDictionary<string, string> Descriptions);

    /// <summary>
    ///     The shrines of the game's Shrines table (catalog/shrines.json, `Poe2UiExtract shrines`), looked up by the row's Id
    ///     (before the shrine is used) or by the buff its use gives the player (after). Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class ShrineCatalog
    {
        private const string FallbackLanguage = "en";

        private readonly Dictionary<string, ShrineEntry> byId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ShrineEntry> byBuff = new(StringComparer.Ordinal);

        public ShrineCatalog(IEnumerable<ShrineEntry> entries)
        {
            foreach (var entry in entries)
            {
                this.byId.TryAdd(entry.Id, entry);
                // Several rows give the same buff (the old PoE1-style row and the lesser shrine give shrine_acceleration; a royale
                // row repeats an ordinary one): the first row in table order names it, except that a lesser shrine takes the buff
                // over, because the old rows' buffs are the ones the game no longer places (LeaguePieces.ShrineBuffs).
                if (entry.Buff.Length > 0 && (!this.byBuff.TryGetValue(entry.Buff, out var named) || (entry.Lesser && !named.Lesser)))
                {
                    this.byBuff[entry.Buff] = entry;
                }
            }

            this.Count = this.byId.Count;
        }

        public static ShrineCatalog Empty { get; } = new(Array.Empty<ShrineEntry>());

        /// <summary>The catalog <see cref="Load"/> read last; empty until then.</summary>
        public static ShrineCatalog Shared { get; private set; } = Empty;

        /// <summary>How many rows the catalog holds.</summary>
        public int Count { get; }

        /// <summary>A missing or unreadable file gives an empty catalog and one log line; the result is also <see cref="Shared"/>.</summary>
        public static ShrineCatalog Load(PluginLog log, string file)
        {
            var catalog = Empty;
            if (!File.Exists(file))
            {
                log.Warn($"No shrines at {file}: shrines are named from their buffs' words");
            }
            else
            {
                try
                {
                    catalog = Parse(File.ReadAllText(file));
                    log.Info($"Shrines: {catalog.Count} rows, {catalog.Languages().Count} languages");
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    log.Warn($"Shrines not read from {file}: {ex.Message}");
                }
            }

            Shared = catalog;
            return catalog;
        }

        /// <summary>The catalog of a shrines.json text; throws <see cref="JsonException"/> when it is not one.</summary>
        public static ShrineCatalog Parse(string json)
        {
            var parsed = JsonConvert.DeserializeObject<ShrineFile>(json) ?? new ShrineFile();
            return new ShrineCatalog(parsed.Shrines.Where(row => !string.IsNullOrEmpty(row.Id)).Select(row =>
                new ShrineEntry(row.Id, row.Lesser, row.Buff ?? string.Empty, row.Seconds, row.Names ?? new(), row.Descriptions ?? new())));
        }

        /// <summary>The shrine of a Shrines row Id, or null.</summary>
        public ShrineEntry? ById(string id) => !string.IsNullOrEmpty(id) && this.byId.TryGetValue(id, out var entry) ? entry : null;

        /// <summary>The shrine whose use gives this buff (a BuffDefinitions Id), or null. Rows that share a buff (see the constructor) give the lesser shrine's, else the first.</summary>
        public ShrineEntry? ByBuff(string buffId) => !string.IsNullOrEmpty(buffId) && this.byBuff.TryGetValue(buffId, out var entry) ? entry : null;

        /// <summary>The shrine's name in the language, else in English, else empty.</summary>
        public string NameOf(ShrineEntry entry, string language) =>
            entry.Names.TryGetValue(language, out var name) && name.Length > 0 ? name :
            entry.Names.TryGetValue(FallbackLanguage, out var english) ? english : string.Empty;

        private List<string> Languages() => this.byId.Values.SelectMany(entry => entry.Names.Keys).Distinct(StringComparer.Ordinal).ToList();

        private sealed class ShrineFile
        {
            [JsonProperty("shrines")]
            public List<ShrineRow> Shrines { get; set; } = new();
        }

        private sealed class ShrineRow
        {
            [JsonProperty("id")]
            public string Id { get; set; } = string.Empty;

            [JsonProperty("lesser")]
            public bool Lesser { get; set; }

            [JsonProperty("buff")]
            public string? Buff { get; set; }

            [JsonProperty("seconds")]
            public int Seconds { get; set; }

            [JsonProperty("names")]
            public Dictionary<string, string>? Names { get; set; }

            [JsonProperty("descriptions")]
            public Dictionary<string, string>? Descriptions { get; set; }
        }
    }
}
