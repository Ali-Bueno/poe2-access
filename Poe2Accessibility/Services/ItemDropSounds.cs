namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    ///     The drop sound the game plays for each item base (a scroll rustles, an orb clinks), from
    ///     <c>catalog/item-sounds.json</c> (base id → sample, generated from the game's BaseItemTypes, ItemVisualIdentity
    ///     and SoundEffects tables by <c>tools/Poe2UiExtract item-sounds</c>) and the samples in <c>sounds/items/</c>.
    /// </summary>
    public sealed class ItemDropSounds
    {
        private readonly Dictionary<string, string> samples;
        private readonly GameSounds sounds;

        private ItemDropSounds(Dictionary<string, string> samples, GameSounds sounds)
        {
            this.samples = samples;
            this.sounds = sounds;
        }

        /// <summary>Loads the table and the samples; either missing gives an empty set (the generic cues play).</summary>
        public static ItemDropSounds Load(string catalogFile, string soundDirectory, float maxSeconds, PluginLog log)
        {
            var samples = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(catalogFile))
                {
                    samples = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(catalogFile)) ?? samples;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                log.Error($"Item sound table {catalogFile} unreadable", ex);
            }

            var sounds = GameSounds.Load(soundDirectory, maxSeconds, log);
            log.Info($"Item drop sounds: {samples.Count} bases, {sounds.Count} samples");
            return new ItemDropSounds(samples, sounds);
        }

        /// <summary>A drop sample by name ("Flask_Drop_01"), null when not extracted.</summary>
        public float[]? Sample(string name) => this.sounds.Get(name);

        /// <summary>The game's drop sound for an item base id, or null when it has none of its own.</summary>
        public float[]? For(string baseId) =>
            baseId.Length > 0 && this.samples.TryGetValue(baseId, out var sample) ? this.sounds.Get(sample) : null;
    }
}
