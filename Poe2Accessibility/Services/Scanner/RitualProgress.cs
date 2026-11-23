namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>The rituals of an area: which altars are finished, and which the swarm flies to next. Pure.</summary>
    public sealed class RitualProgress
    {
        /// <summary>The altar's state once its fight is won.</summary>
        private const long FinishedState = 3;

        /// <summary>Two altars nearer than this are the same one, told by two of its objects.</summary>
        private const float SameAltarCells = 30f;

        /// <summary>The boss's altar stands in the middle of its arena.</summary>
        private const float ArenaCells = 60f;

        private readonly Dictionary<uint, long> states = new();
        private readonly List<Vector2> finished = new();

        /// <summary>Notes an altar's state; true when its ritual has just been finished.</summary>
        public bool Finished(uint altar, long state, Vector2 cell)
        {
            var known = this.states.TryGetValue(altar, out var before);
            this.states[altar] = state;
            if (state < FinishedState)
            {
                return false;
            }

            if (!this.finished.Any(done => Vector2.Distance(done, cell) < SameAltarCells))
            {
                this.finished.Add(cell);
            }

            return known && before < FinishedState;
        }

        /// <summary>The nearest altar not finished, the boss's left out; null when all are done or none is known.</summary>
        public Vector2? Next(IEnumerable<Vector2> altars, Vector2 here, Vector2? boss)
        {
            var left = altars.Where(altar => !this.finished.Any(done => Vector2.Distance(done, altar) < SameAltarCells) &&
                                             (boss is not { } arena || Vector2.Distance(arena, altar) > ArenaCells)).ToList();
            return left.Count > 0 ? left.MinBy(altar => Vector2.Distance(altar, here)) : null;
        }
    }
}
