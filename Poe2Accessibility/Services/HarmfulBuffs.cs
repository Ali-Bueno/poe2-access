namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>
    ///     The harmful buffs (debuffs and curses) of the game's BuffDefinitions with their names per language
    ///     (catalog/buff-names.json, `Poe2UiExtract buff-names`), and which of them have begun on the player. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class HarmfulBuffs
    {
        /// <summary>A buff gone this long and back is a new one, as <see cref="StatusAilments"/> holds its ends.</summary>
        public const long HoldMs = 500;

        private const string Fallback = "en";

        private readonly Dictionary<string, Dictionary<string, string>> names;
        private readonly Dictionary<string, long> lastSeen = new(StringComparer.Ordinal);
        private bool baseline = true;

        public HarmfulBuffs(Dictionary<string, Dictionary<string, string>> names) => this.names = names;

        public int Count => this.names.TryGetValue(Fallback, out var english) ? english.Count : 0;

        public static HarmfulBuffs Load(string path)
        {
            try
            {
                return new HarmfulBuffs(File.Exists(path)
                    ? JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path)) ?? new()
                    : new());
            }
            catch (JsonException)
            {
                return new HarmfulBuffs(new());
            }
        }

        /// <summary>A new area or character: what is there now is not news.</summary>
        public void Reset()
        {
            this.lastSeen.Clear();
            this.baseline = true;
        }

        /// <summary>The game's name for a harmful buff in the language (English when it has none there), empty for any other buff.</summary>
        public string NameOf(string buffId, string language)
        {
            var id = BuffIds.Base(buffId);
            if (this.names.TryGetValue(language, out var own) && own.TryGetValue(id, out var name))
            {
                return name;
            }

            return this.names.TryGetValue(Fallback, out var english) && english.TryGetValue(id, out var fallback) ? fallback : string.Empty;
        }

        /// <summary>
        ///     The names of harmful buffs among <paramref name="ids"/> that were not there a moment ago, each once;
        ///     <paramref name="isCovered"/> marks those something else already tells (an ailment, a hazard's alarm).
        /// </summary>
        public IReadOnlyList<string> Begun(IEnumerable<string> ids, long now, string language, Func<string, bool> isCovered)
        {
            var begun = new List<string>();
            foreach (var id in ids)
            {
                var name = this.NameOf(id, language);
                if (name.Length == 0 || isCovered(id))
                {
                    continue;
                }

                var key = BuffIds.Base(id);
                if (!this.baseline && !this.lastSeen.ContainsKey(key) && !begun.Contains(name))
                {
                    begun.Add(name);
                }

                this.lastSeen[key] = now;
            }

            foreach (var key in this.lastSeen.Where(pair => now - pair.Value >= HoldMs).Select(pair => pair.Key).ToList())
            {
                this.lastSeen.Remove(key);
            }

            this.baseline = false;
            return begun;
        }
    }
}
