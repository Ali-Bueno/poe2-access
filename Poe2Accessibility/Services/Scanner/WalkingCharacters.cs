namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     Characters the game holds out of play who walk all the same: an escort such as Asala at Halani's gates (owner,
    ///     2026-11-16). A story variant set aside never moves, so one seen walking stays in play for the rest of the area (pure).
    /// </summary>
    public sealed class WalkingCharacters
    {
        /// <summary>Half a spoken step (100 world units): more than a standing character's sway.</summary>
        public const float MovedWorld = 50f;

        private readonly object gate = new();
        private readonly Dictionary<uint, Vector2> firstSeen = new();
        private readonly HashSet<uint> walked = new();

        /// <summary>Notes where the character stands; true once it has walked away from where it was first seen.</summary>
        public bool Walks(uint id, Vector2 at)
        {
            lock (this.gate)
            {
                if (this.walked.Contains(id))
                {
                    return true;
                }

                if (!this.firstSeen.TryGetValue(id, out var first))
                {
                    this.firstSeen[id] = at;
                    return false;
                }

                if (Vector2.Distance(first, at) <= MovedWorld)
                {
                    return false;
                }

                this.walked.Add(id);
                this.firstSeen.Remove(id);
                return true;
            }
        }

        /// <summary>Whether the character has been seen walking, without noting anything.</summary>
        public bool Walked(uint id)
        {
            lock (this.gate)
            {
                return this.walked.Contains(id);
            }
        }

        /// <summary>A new area: its ids are other entities.</summary>
        public void Clear()
        {
            lock (this.gate)
            {
                this.firstSeen.Clear();
                this.walked.Clear();
            }
        }
    }
}
