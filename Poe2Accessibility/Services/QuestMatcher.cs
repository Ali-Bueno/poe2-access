namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The scanner's Quest category: entries the quest tracker's objective names. Pure.</summary>
    public static class QuestMatcher
    {
        /// <summary>
        ///     What the quest tracker names: an exit, NPC, object or unique monster whose name the objective text
        ///     contains ("Find Clearfell" → the exit to Clearfell). Copies in <see cref="ScanCategory.Quest"/>, same
        ///     ids, so locking one guides to the real thing. A label equal to the name is the thing's own nameplate.
        /// </summary>
        /// <param name="currentArea">
        ///     The area the player is in: its name in the objective ("Registra Sierraclara para encontrar la entrada al
        ///     Lodazal") is where to search, not where to go — it made the exit back to Campamento de Sierraclara the target.
        /// </param>
        public static List<ScanEntry> Targets(IReadOnlyList<ScanEntry> entries, IReadOnlyList<string> questTexts, string currentArea = "")
        {
            var texts = questTexts.Select(Fold).Where(text => text.Length > 0).ToList();
            var targets = new List<ScanEntry>();
            if (texts.Count == 0)
            {
                return targets;
            }

            // Proper nouns inside the objective ("Search for the Miller and find refuge in Clearfell"): words written
            // with a capital letter after the first word. A name sharing one ("The Bloated Miller", "Clearfell
            // Encampment") is what the quest means even when the sentence names it differently.
            var properNouns = new HashSet<string>(questTexts.SelectMany(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1))
                .Where(word => word.Length >= 4 && char.IsUpper(word[0]))
                .Select(word => Fold(word.Trim(',', '.', ':', ';', '!', '?', '\'', '"'))));
            properNouns.ExceptWith(Fold(currentArea).Split(' ', StringSplitOptions.RemoveEmptyEntries));

            foreach (var entry in entries)
            {
                if (entry.Category is ScanCategory.Players or ScanCategory.Items or ScanCategory.Unexplored ||
                    (entry.Category == ScanCategory.Enemies && !entry.Name.Contains(", ", StringComparison.Ordinal)))
                {
                    continue; // ordinary monsters share names with flavour text; bosses carry their rarity
                }

                var name = Fold(entry.Category == ScanCategory.Enemies ? entry.Name[..entry.Name.IndexOf(", ", StringComparison.Ordinal)] : entry.Name);
                var named = texts.Any(text => text != name && ContainsWord(text, name)) ||
                            name.Split(' ').Any(word => word.Length >= 4 && properNouns.Contains(word));
                if (name.Length >= 4 && named && targets.All(target => target.Id != entry.Id))
                {
                    targets.Add(entry with { Category = ScanCategory.Quest });
                }
            }

            return targets;
        }

        private static bool ContainsWord(string text, string name)
        {
            for (var at = text.IndexOf(name, StringComparison.Ordinal); at >= 0; at = text.IndexOf(name, at + 1, StringComparison.Ordinal))
            {
                var end = at + name.Length;
                if ((at == 0 || !char.IsLetter(text[at - 1])) && (end == text.Length || !char.IsLetter(text[end])))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Lower case without accents, so "Sierraclara" in a sentence matches the exit's label.</summary>
        private static string Fold(string text)
        {
            var decomposed = text.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            return new string(decomposed.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        }
    }
}
