namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Two fields that must name the same thing, each read from its own struct: a cooldown entry's skill id and the id in
    ///     the details of one of the actor's skills. Neither has a shape of its own, but when either moves no id of one list
    ///     is in the other any more. One match proves both; never a match, on enough readings in two areas, is a loss of one
    ///     of them (which one cannot be told, so nothing is moved). Pure.
    /// </summary>
    public sealed class IdLinkCheck
    {
        /// <summary>Readings in one area with ids on both sides and none in common before that area speaks against the fields.</summary>
        public const int ReadingsPerArea = 20;

        private readonly LossWitnesses areas = new();
        private long area;
        private int unlinked;

        /// <summary>Gets a value indicating whether a match was seen: the fields are proved, nothing more is judged.</summary>
        public bool Proved { get; private set; }

        /// <summary>Gets a value indicating whether two areas found no id in common.</summary>
        public bool Lost => this.areas.Lost;

        /// <summary>
        ///     One reading in <paramref name="areaNow"/>: the ids of one list and of the other. True when the verdict is reached
        ///     on this reading (<see cref="Proved"/> or <see cref="Lost"/>); a reading with an empty side says nothing.
        /// </summary>
        public bool Observe(long areaNow, IReadOnlyCollection<uint> ids, IReadOnlySet<uint> others)
        {
            if (this.Proved || ids.Count == 0 || others.Count == 0)
            {
                return false;
            }

            if (ids.Any(others.Contains))
            {
                this.Proved = true;
                this.areas.Clear();
                return true;
            }

            if (areaNow != this.area)
            {
                (this.area, this.unlinked) = (areaNow, 0);
            }

            return ++this.unlinked == ReadingsPerArea && this.areas.Against(areaNow);
        }

        public void Reset()
        {
            this.areas.Clear();
            (this.area, this.unlinked, this.Proved) = (0, 0, false);
        }
    }
}
