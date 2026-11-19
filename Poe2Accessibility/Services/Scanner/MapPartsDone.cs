namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The parts of the map (<see cref="MapPart"/>) the player has just finished exploring: news once, when a part with ground
    ///     left becomes explored. How they stood on arrival is not news. Pure; docs/radar.md "The parts of the map".
    /// </summary>
    public sealed class MapPartsDone
    {
        private readonly Dictionary<(int Across, int Down), bool> explored = new();

        /// <summary>A new area: its first reading is how it stands, not news.</summary>
        public void Reset() => this.explored.Clear();

        /// <summary>The parts explored since the last update, and every part still with ground left; null when none was finished.</summary>
        public (IReadOnlyList<MapPart> Done, IReadOnlyList<MapPart> Left)? Update(IReadOnlyList<MapPart> parts)
        {
            if (parts.Count < 2)
            {
                return null; // a map too small for parts, or no frontier yet
            }

            var first = this.explored.Count == 0;
            var done = new List<MapPart>();
            foreach (var part in parts)
            {
                var key = (part.Across, part.Down);
                if (!first && part.Explored && this.explored.TryGetValue(key, out var was) && !was)
                {
                    done.Add(part);
                }

                this.explored[key] = part.Explored;
            }

            return done.Count == 0 ? null : (done, parts.Where(part => !part.Explored).ToList());
        }
    }
}
