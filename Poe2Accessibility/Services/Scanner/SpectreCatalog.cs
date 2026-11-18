namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    ///     What a monster's corpse asks in Spirit when bound with Bind Spectre, by the monster's file
    ///     (catalog/spectres.json, `Poe2UiExtract spectres`): the experience multiplier of its MonsterVarieties row,
    ///     halved and rounded to the nearest ten — the cost the game's tooltip over a corpse shows, on every monster
    ///     whose cost is published (docs/research/corpse-scanning.md). The dat files hold no spirit column; a monster
    ///     the catalog lacks is named without a cost rather than with a guessed one. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class SpectreCatalog
    {
        private readonly Dictionary<string, int> experience;

        public SpectreCatalog(IReadOnlyDictionary<string, int> experience) =>
            this.experience = new Dictionary<string, int>(experience, StringComparer.Ordinal);

        public static SpectreCatalog Empty { get; } = new(new Dictionary<string, int>());

        /// <summary>The catalog <see cref="Load"/> read last; empty until then.</summary>
        public static SpectreCatalog Shared { get; private set; } = Empty;

        /// <summary>How many monsters the catalog holds.</summary>
        public int Count => this.experience.Count;

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
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    log.Warn($"Spectres not read from {file}: {ex.Message}");
                }
            }

            Shared = catalog;
            return catalog;
        }

        /// <summary>The catalog of a spectres.json text; throws <see cref="JsonException"/> when it is not one.</summary>
        public static SpectreCatalog Parse(string json) =>
            new(JsonConvert.DeserializeObject<SpectreFile>(json)?.Spectres ?? throw new JsonSerializationException("no spectres"));

        /// <summary>
        ///     The Spirit a spectre of the monster at <paramref name="path"/> reserves; null when the catalog lacks it.
        ///     An entity's path carries its level after an '@' ("…/VaalGuardBowLiving@40"), which the table's Id has not:
        ///     stripped here, as `BossMechanicsCatalog.MonsterId` strips it for its own tables.
        /// </summary>
        public int? SpiritOf(string path)
        {
            var at = path.IndexOf('@');
            var id = at > 0 ? path[..at] : path;
            return id.Length > 0 && this.experience.TryGetValue(id, out var value) ? SpiritFor(value) : null;
        }

        /// <summary>
        ///     The reservation of a monster with this experience multiplier: half of it, to the nearest ten (100 gives 50,
        ///     110 gives 60, 70 gives 40, 200 gives 100). Null for a multiplier worth nothing, which no bindable monster has.
        /// </summary>
        public static int? SpiritFor(int experience) =>
            experience <= 0 ? null : (int)Math.Floor(experience / 20.0 + 0.5) * 10;

        private sealed class SpectreFile
        {
            [JsonProperty("spectres")]
            public Dictionary<string, int>? Spectres { get; set; }
        }
    }
}
