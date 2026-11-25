namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The Atlas map finder's lists; Position holds no destination and lets the chosen one go. Objective
    ///     places are those the game data itself asks for, as opposed to whatever content a map happens to roll.
    /// </summary>
    public enum AtlasDestinationCategory
    {
        Position,
        Accessible,
        Landmarks,
        Objectives,
    }

    /// <summary>
    ///     The Atlas destination choice: changing lists or entries chooses the destination at once, with nothing
    ///     to confirm, and returning to Position lets it go. Targets are scan keys (a catalog id, or a grid key
    ///     for a row-less node), so a choice survives the Atlas closing and a fresh scan on reopening. Pure.
    /// </summary>
    public sealed class AtlasDestinationFinder
    {
        private static readonly AtlasDestinationCategory[] Categories = Enum.GetValues<AtlasDestinationCategory>();

        public AtlasDestinationCategory Category { get; private set; } = AtlasDestinationCategory.Position;

        /// <summary>The destination's scan key; empty when none is chosen.</summary>
        public string Target { get; private set; } = string.Empty;

        /// <param name="step">+1 next list, -1 previous.</param>
        /// <param name="members">A list's destination keys, nearest first; an empty list is passed over.</param>
        public void ChangeCategory(int step, Func<AtlasDestinationCategory, IReadOnlyList<string>> members)
        {
            for (var tried = 0; tried < Categories.Length; tried++)
            {
                this.Category = Categories[SettingsCursor.Wrap((int)this.Category + step, Categories.Length)];
                var list = this.Category == AtlasDestinationCategory.Position ? Array.Empty<string>() : members(this.Category);
                if (this.Category == AtlasDestinationCategory.Position || list.Count > 0)
                {
                    this.Target = list.Count > 0 ? list[0] : string.Empty;
                    return;
                }
            }
        }

        /// <param name="step">+1 next entry, -1 previous.</param>
        /// <param name="members">This list's destination keys now, nearest first; resorting never loses the choice.</param>
        public void ChangeEntry(int step, IReadOnlyList<string> members)
        {
            if (members.Count > 0)
            {
                var at = members.ToList().IndexOf(this.Target);
                this.Target = members[at < 0 ? 0 : SettingsCursor.Wrap(at + step, members.Count)];
            }
        }

        /// <returns>True when this landing is the destination, the first time; the guide then ends.</returns>
        public bool Arrive(string key)
        {
            if (key.Length == 0 || key != this.Target)
            {
                return false;
            }

            this.Drop();
            return true;
        }

        /// <summary>Lets the destination go without changing the list the player is browsing.</summary>
        public void Drop() => this.Target = string.Empty;
    }
}
