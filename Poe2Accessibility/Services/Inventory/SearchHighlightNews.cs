namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>How a panel's search draws an item (GameLayout.InventoryItem.HighlightOffset).</summary>
    public enum ItemHighlight : byte
    {
        None = 0,
        Dimmed = 1,
        Match = 2,
    }

    /// <summary>What to say when the items a search lights change; a set must be seen twice running before it is news. Pure.</summary>
    public sealed class SearchHighlightNews
    {
        /// <summary>Matches named one by one; the rest are counted.</summary>
        public const int MaxNamed = 6;

        private string? candidate;
        private string? said;

        /// <summary>Nothing is known any more (the panel closed): the next search is news again.</summary>
        public void Forget() => (this.candidate, this.said) = (null, null);

        /// <summary>The line for this reading, or null: <paramref name="matches"/> is null while no search dims anything.</summary>
        public string? Line(IReadOnlyList<(string Name, int Column, int Row)>? matches)
        {
            var signature = matches == null ? string.Empty : "|" + string.Join("|", matches.Select(match => $"{match.Name}@{match.Column},{match.Row}"));
            if (signature != this.candidate)
            {
                this.candidate = signature;
                return null;
            }

            if (signature == (this.said ?? string.Empty))
            {
                return null;
            }

            var before = this.said;
            this.said = signature;
            if (matches == null)
            {
                return before == null ? null : Announcements.SearchCleared;
            }

            return Announcements.SearchMatches(
                matches.Count,
                matches.Take(MaxNamed).Select(match => $"{match.Name}, {Announcements.GridPosition(match.Column, match.Row)}"));
        }
    }
}
