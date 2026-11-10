namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The scanner's Quest category: entries the quest tracker's objective names. Pure.</summary>
    public static class QuestMatcher
    {
        /// <summary>An area transition's kind (EntityCatalog.Classify), apart from portals, waypoints and checkpoints.</summary>
        public const string ExitKind = "exit";

        /// <summary>
        ///     The kind of a radar table's note on a tile ("Ogham Farmlands Or Deadend"): the quest means it only when it
        ///     names it whole, not by a word they share — "Ogham" in "The Wild Treasure of Ogham" led to that dead end
        ///     (user 2026-10-28).
        /// </summary>
        public const string TileNote = "tile_note";

        /// <summary>
        ///     What the quest tracker names: an exit, NPC, object or unique monster (never a place) whose name the objective text
        ///     contains ("Find Clearfell" → the exit to Clearfell). Copies in <see cref="ScanCategory.Quest"/>, same
        ///     ids, so locking one guides to the real thing. A label equal to the name is the thing's own nameplate.
        /// </summary>
        /// <param name="currentArea">
        ///     The area the player is in: its name in the objective ("Registra Sierraclara para encontrar la entrada al
        ///     Lodazal") is where to search, not where to go — it made the exit back to Campamento de Sierraclara the target.
        /// </param>
        /// <param name="cameFrom">The area the player entered this one from: the exit there is the way back.</param>
        /// <param name="memo">What an earlier call worked out for the same objective, for a caller that asks many times a second.</param>
        public static List<ScanEntry> Targets(IReadOnlyList<ScanEntry> entries, IReadOnlyList<string> questTexts, string currentArea = "", string cameFrom = "", Memo? memo = null)
        {
            var objective = memo?.For(questTexts, currentArea) ?? new Objective(questTexts, currentArea);
            var texts = objective.Texts;
            // What the minimap marks with the quest's own icon is the objective, whatever the tracker says.
            var targets = entries.Where(entry => entry.QuestMarked).Select(entry => entry with { Category = ScanCategory.Quest }).ToList();
            if (texts.Count == 0)
            {
                return targets;
            }

            foreach (var entry in entries)
            {
                // Ordinary monsters share names with flavour text; bosses carry their rarity. Places are the mod's names
                // for tiles and props, common nouns the game never marks: "Find the entrance to the Red Vale" led to an
                // "entrance" tile and "Search for Asinia in the Consort's Tomb" to a "tomb" prop (user 2026-10-28).
                // A sign names the place it points to ("Pueblo de Ogham" by the road), it is not that place (user 2026-10-28).
                if (entry.Category is ScanCategory.Players or ScanCategory.Items or ScanCategory.Unexplored or ScanCategory.Places ||
                    entry.Kind == MinimapMarks.SignKind ||
                    (entry.Category == ScanCategory.Enemies && !entry.Name.Contains(", ", StringComparison.Ordinal)))
                {
                    continue;
                }

                // Bosses carry their rarity after the name, NPCs their quest marker ("Renly, con misión").
                // A boss room by whom its label names, whole ("Busca a Asinia…"): its "Boss room" words and the radar
                // table's notes ("Arena / Hunting Grounds") share common words with objectives, as tile notes do.
                var byWord = entry.Kind is not (TileNote or MinimapMarks.BossRoomKind);
                var named = entry.Category == ScanCategory.Enemies ? objective.Names(entry.Name, NamePart.BeforeFirstComma, byWord)
                    : entry.Kind == "npc_quest" ? objective.Names(entry.Name, NamePart.BeforeLastComma, byWord)
                    : entry.Kind == MinimapMarks.BossRoomKind ? objective.Names(entry.Subject, NamePart.Whole, byWord)
                    : objective.Names(entry.Name, NamePart.Whole, byWord);
                if (named && targets.All(target => target.Id != entry.Id))
                {
                    targets.Add(entry with { Category = ScanCategory.Quest });
                }
            }

            // "Sigue a través del Enredo sombrío" names only the area the player is in: crossing it, the way on is every
            // exit but the one back, and while none is known, the unexplored ground nearest, where it will be (the marker
            // stayed empty and the exit was lost, user 2026-10-29).
            if (targets.Count == 0 && objective.NamesOnlyItsArea)
            {
                var back = Fold(cameFrom);
                var onward = entries.Where(entry => entry.Category == ScanCategory.Exits && entry.Kind == ExitKind && Fold(entry.Name) != back).ToList();
                targets.AddRange((onward.Count > 0 ? onward : entries.Where(entry => entry.Category == ScanCategory.Unexplored).OrderBy(entry => entry.Kind == ExplorationMap.PocketKind).Take(1))
                    .Select(entry => entry with { Category = ScanCategory.Quest }));
            }

            return targets;
        }

        /// <summary>Which part of an entry's name the objective is asked about.</summary>
        internal enum NamePart
        {
            Whole,

            /// <summary>A boss before its rarity ("Beira, raro").</summary>
            BeforeFirstComma,

            /// <summary>An NPC before its quest marker ("Renly, con misión"); the whole name when it has none.</summary>
            BeforeLastComma,
        }

        /// <summary>
        ///     One objective between calls of <see cref="Targets"/>: the listing asks ten times a second, and folding every
        ///     name against every text each time was most of its cost. Not thread-safe: one caller owns it.
        /// </summary>
        public sealed class Memo
        {
            private Objective? last;

            internal Objective For(IReadOnlyList<string> questTexts, string currentArea) =>
                this.last is { } kept && kept.Is(questTexts, currentArea) ? kept : this.last = new Objective(questTexts, currentArea);
        }

        /// <summary>
        ///     The tracker's texts in one area, folded once, and what they were found to name. Every answer depends on the
        ///     texts, the area and the name asked about alone, so one kept is the one that would be worked out again.
        /// </summary>
        internal sealed class Objective
        {
            /// <summary>Names remembered at most: an area's entries are a few hundred.</summary>
            private const int MaxNames = 4096;

            private readonly string[] questTexts;
            private readonly string currentArea;
            private readonly Dictionary<(string Text, NamePart Part, bool ByWord), bool> names = new();
            private HashSet<string>? properNouns;
            private bool? namesOnlyItsArea;

            public Objective(IReadOnlyList<string> questTexts, string currentArea)
            {
                this.questTexts = questTexts.ToArray();
                this.currentArea = currentArea;
                this.Texts = this.questTexts.Select(Fold).Where(text => text.Length > 0).ToList();
            }

            public List<string> Texts { get; }

            /// <summary>
            ///     The texts name nothing but the area the player is in: no proper noun of another place, and every long word
            ///     of the area's name in one of them.
            /// </summary>
            public bool NamesOnlyItsArea => this.namesOnlyItsArea ??= this.OnlyItsArea();

            // Proper nouns inside the objective ("Search for the Miller and find refuge in Clearfell"): words written
            // with a capital letter after the first word. A name sharing one ("The Bloated Miller", "Clearfell
            // Encampment") is what the quest means even when the sentence names it differently.
            private HashSet<string> ProperNouns
            {
                get
                {
                    if (this.properNouns == null)
                    {
                        var nouns = new HashSet<string>(this.questTexts.SelectMany(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1))
                            .Where(word => word.Length >= 4 && char.IsUpper(word[0]))
                            .Select(word => Fold(word.Trim(',', '.', ':', ';', '!', '?', '\'', '"'))));
                        nouns.ExceptWith(Fold(this.currentArea).Split(' ', StringSplitOptions.RemoveEmptyEntries));
                        this.properNouns = nouns;
                    }

                    return this.properNouns;
                }
            }

            public bool Is(IReadOnlyList<string> texts, string area)
            {
                if (texts.Count != this.questTexts.Length || !string.Equals(area, this.currentArea, StringComparison.Ordinal))
                {
                    return false;
                }

                for (var i = 0; i < this.questTexts.Length; i++)
                {
                    if (!string.Equals(texts[i], this.questTexts[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            ///     Whether the objective names this: a text holds its words, or (<paramref name="byWord"/>) it shares a proper
            ///     noun with one. Never a name under four letters: too short to mean anything, and a boss room that names no one.
            /// </summary>
            public bool Names(string text, NamePart part, bool byWord)
            {
                var key = (text, part, byWord);
                if (this.names.TryGetValue(key, out var known))
                {
                    return known;
                }

                var name = Fold(Part(text, part));
                var named = name.Length >= 4 &&
                            (this.Texts.Any(sentence => sentence != name && ContainsWord(sentence, name)) ||
                             (byWord && name.Split(' ').Any(word => word.Length >= 4 && this.ProperNouns.Contains(word.Trim(',', '.', ':', ';')))));
                if (this.names.Count >= MaxNames)
                {
                    this.names.Clear();
                }

                this.names[key] = named;
                return named;
            }

            private static string Part(string text, NamePart part) => part switch
            {
                NamePart.BeforeFirstComma => text[..text.IndexOf(", ", StringComparison.Ordinal)],
                NamePart.BeforeLastComma when text.LastIndexOf(", ", StringComparison.Ordinal) is > 0 and var marker => text[..marker],
                _ => text,
            };

            private bool OnlyItsArea()
            {
                var here = Fold(this.currentArea).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(word => word.Length >= 4).ToList();
                return this.ProperNouns.Count == 0 && here.Count > 0 && this.Texts.Any(text => here.All(word => Words(text).Contains(word)));
            }
        }

        /// <summary>The name's words in a row in the text, each also in the plural ("Busca los altares de ritual" names an "Altar de ritual").</summary>
        private static bool ContainsWord(string text, string name)
        {
            var words = Words(text);
            var wanted = Words(name);
            for (var at = 0; wanted.Length > 0 && at + wanted.Length <= words.Length; at++)
            {
                if (wanted.Select((word, i) => SameOrPlural(words[at + i], word)).All(same => same))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] Words(string text) =>
            text.Split(text.Where(c => !char.IsLetterOrDigit(c)).Distinct().ToArray(), StringSplitOptions.RemoveEmptyEntries);

        private static bool SameOrPlural(string word, string singular) =>
            word == singular || word == singular + "s" || word == singular + "es";

        /// <summary>Lower case without accents, so "Sierraclara" in a sentence matches the exit's label.</summary>
        private static string Fold(string text)
        {
            var decomposed = text.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            return new string(decomposed.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        }
    }
}
