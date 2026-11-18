namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    ///     What a monster's corpse is and asks in Spirit when bound with Bind Spectre, by the monster's file
    ///     (catalog/spectres.json, `Poe2UiExtract spectres`): from the experience multiplier of its MonsterVarieties
    ///     row on the game's curve (<see cref="SpiritFor"/>) — and, where monsters of one name differ, the weapon
    ///     its main hand holds (an
    ///     ItemClasses Id) and whether it fights ranged or melee, which variety of the name the corpse is
    ///     (docs/research/corpse-scanning.md). The dat files hold no spirit column; a monster the catalog lacks is
    ///     named without a cost rather than with a guessed one. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class SpectreCatalog
    {
        /// <summary>One monster's row: its experience multiplier and, when its variety is told, its weapon and reach.</summary>
        /// <param name="Experience">MonsterVarieties' experience multiplier.</param>
        /// <param name="Weapon">The ItemClasses Id of its main hand ("Bow", "Warstaff"), empty when it holds none.</param>
        /// <param name="Attack">"ranged", "melee", or empty when its tags name neither.</param>
        /// <param name="Told">Monsters of its name differ in cost, weapon or reach: its variety is spoken with its corpse.</param>
        public sealed record Entry(int Experience, string Weapon, string Attack, bool Told);

        private readonly Dictionary<string, Entry> entries;
        private readonly Dictionary<string, Entry>.AlternateLookup<ReadOnlySpan<char>> byId;

        public SpectreCatalog(IReadOnlyDictionary<string, Entry> entries)
        {
            this.entries = new Dictionary<string, Entry>(entries, StringComparer.Ordinal);
            this.byId = this.entries.GetAlternateLookup<ReadOnlySpan<char>>();
        }

        public static SpectreCatalog Empty { get; } = new(new Dictionary<string, Entry>());

        /// <summary>The catalog <see cref="Load"/> read last; empty until then.</summary>
        public static SpectreCatalog Shared { get; private set; } = Empty;

        /// <summary>How many monsters the catalog holds.</summary>
        public int Count => this.entries.Count;

        /// <summary>A missing or unreadable file gives an empty catalog and one log line; the result is also <see cref="Shared"/>.</summary>
        public static SpectreCatalog Load(PluginLog log, string file)
        {
            var catalog = Empty;
            if (!File.Exists(file))
            {
                log.Warn($"No spectres at {file}: corpses are named without their Spirit");
            }
            else
            {
                try
                {
                    catalog = Parse(File.ReadAllText(file));
                    log.Info($"Spectres: {catalog.Count} monsters");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    log.Warn($"Spectres not read from {file}: {ex.Message}");
                }
            }

            Shared = catalog;
            return catalog;
        }

        /// <summary>The catalog of a spectres.json text; throws <see cref="JsonException"/> when it is not one.</summary>
        public static SpectreCatalog Parse(string json)
        {
            var file = JsonConvert.DeserializeObject<SpectreFile>(json) ?? throw new JsonSerializationException("no spectres");
            if (file.Spectres == null)
            {
                throw new JsonSerializationException("no spectres");
            }

            try
            {
                return new SpectreCatalog(file.Spectres.ToDictionary(pair => pair.Key, pair => Read(pair.Value), StringComparer.Ordinal));
            }
            catch (Exception ex) when (ex is not JsonException)
            {
                // A row of the wrong shape ("x": "abc", a row that is an array) fails in Read's conversions with
                // Format/InvalidOperation/Overflow, not a JsonException: it is a bad catalog all the same.
                throw new JsonSerializationException(ex.Message, ex);
            }
        }

        /// <summary>One row as written now (an object), or as the first making of the catalog wrote it (the multiplier alone).</summary>
        private static Entry Read(JToken token) =>
            token is JObject row
                ? new Entry(row.Value<int?>("x") ?? 0, row.Value<string>("w") ?? string.Empty, row.Value<string>("a") ?? string.Empty, row.Value<bool?>("t") ?? false)
                : new Entry(token.Value<int>(), string.Empty, string.Empty, false);

        /// <summary>The Spirit a spectre of the monster at <paramref name="path"/> reserves; null when the catalog lacks it.</summary>
        public int? SpiritOf(string path) => this.RowOf(path) is { } entry ? SpiritFor(entry.Experience) : null;

        /// <summary>
        ///     Both of <see cref="SpiritOf"/> and <see cref="VarietyOf"/> in one lookup, for the listing that names every
        ///     corpse ten times a second.
        /// </summary>
        public (int? Spirit, (string Weapon, string Attack)? Variety) Lookup(string path) =>
            this.RowOf(path) is { } entry
                ? (SpiritFor(entry.Experience), entry.Told ? (entry.Weapon, entry.Attack) : null)
                : (null, null);

        /// <summary>
        ///     What tells the monster at <paramref name="path"/> from the others of its name — its weapon's ItemClasses Id
        ///     and whether it fights ranged or melee — or null when its name needs no telling or the catalog lacks it.
        /// </summary>
        public (string Weapon, string Attack)? VarietyOf(string path) =>
            this.RowOf(path) is { Told: true } entry ? (entry.Weapon, entry.Attack) : null;

        /// <summary>
        ///     The reservation of a monster with this experience multiplier: 50 at a multiplier of 100, scaling with
        ///     its 0.75 power (100 gives 50, 110 gives 53, 70 gives 38, 200 gives 84). The curve is the game's since
        ///     0.3.0 rebalanced spectre costs (Path of Building's minion export has it in this form; the round tens
        ///     published before 0.3.0 no longer hold), and it is the cost the bound gem reads before the player's
        ///     own reservation efficiency. Null for a multiplier worth nothing, which no bindable monster has.
        /// </summary>
        public static int? SpiritFor(int experience)
        {
            if (experience <= 0)
            {
                return null;
            }

            var spirit = (int)Math.Floor(Math.Pow(experience / 100.0, 0.75) * 50);
            return spirit > 0 ? spirit : null;
        }

        /// <summary>
        ///     The row of an entity's path. The path carries its level after an '@' ("…/VaalGuardBowLiving@40"), which
        ///     the table's Id has not: stripped here, as `BossMechanicsCatalog.MonsterId` strips it for its own tables.
        /// </summary>
        private Entry? RowOf(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var at = path.IndexOf('@');
            var id = at > 0 ? path.AsSpan(0, at) : path.AsSpan();
            return id.Length > 0 && this.byId.TryGetValue(id, out var entry) ? entry : null;
        }

        private sealed class SpectreFile
        {
            [JsonProperty("spectres")]
            public Dictionary<string, JToken>? Spectres { get; set; }
        }
    }
}
