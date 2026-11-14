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
        /// <summary>A buff gone this long and back is a new one, as <see cref="StatusAilments"/> holds the ends of what each hit renews.</summary>
        public const long HoldMs = 2500;

        private const string Fallback = "en";

        private readonly Dictionary<string, Dictionary<string, string>> names;
        private readonly Dictionary<string, long> lastSeen = new(StringComparer.Ordinal);
        private bool baseline = true;

        public HarmfulBuffs(Dictionary<string, Dictionary<string, string>> names)
        {
            this.names = names;
            this.isNamed = names.TryGetValue(Fallback, out var english) ? english.ContainsKey : _ => false;
        }

        /// <summary>Kept: asked for every buff of the player at each poll.</summary>
        private readonly Func<string, bool> isNamed;

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
        public string NameOf(string buffId, string language) => Localization.Pick(this.names, language, this.Key(buffId)) ?? string.Empty;

        /// <summary>The table's id for a buff key of the host (<see cref="BuffIds.In"/>).</summary>
        private string Key(string buffId) => BuffIds.In(buffId, this.isNamed);

        /// <summary>
        ///     The names of harmful buffs among <paramref name="ids"/> that were not there a moment ago, each once;
        ///     <paramref name="isCovered"/> marks those something else already tells (an ailment, a hazard's alarm).
        /// </summary>
        public IReadOnlyList<string> Begun(IEnumerable<string> ids, long now, string language, Func<string, bool> isCovered)
        {
            // Before the buffs are looked at: one last seen before a menu or a loading screen and there again is a new one.
            foreach (var key in this.lastSeen.Where(pair => now - pair.Value >= HoldMs).Select(pair => pair.Key).ToList())
            {
                this.lastSeen.Remove(key);
            }

            var begun = new List<string>();
            foreach (var id in ids)
            {
                var name = this.NameOf(id, language);
                if (name.Length == 0 || isCovered(id))
                {
                    continue;
                }

                var key = this.Key(id);
                if (!this.baseline && !this.lastSeen.ContainsKey(key) && !begun.Contains(name))
                {
                    begun.Add(name);
                }

                this.lastSeen[key] = now;
            }

            this.baseline = false;
            return begun;
        }
    }
}
