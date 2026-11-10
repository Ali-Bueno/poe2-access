namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     When a part that could not be found may be said lost: one witness (a screen, an area, a control, a batch of
    ///     entities) can lack the evidence on a healthy game, so different witnesses must say so before the player hears a
    ///     compatibility warning. Pure.
    /// </summary>
    public sealed class LossWitnesses
    {
        /// <summary>Different witnesses a loss needs unless a part asks for more.</summary>
        public const int Needed = 2;

        private readonly int needed;
        private readonly HashSet<long> against = new();

        public LossWitnesses(int needed = Needed) => this.needed = needed;

        /// <summary>Gets how many different witnesses have spoken against the part.</summary>
        public int Count => this.against.Count;

        /// <summary>Gets a value indicating whether enough witnesses spoke against the part.</summary>
        public bool Lost => this.against.Count >= this.needed;

        /// <summary><paramref name="witness"/> did not find the part; true on the witness that makes it lost (once).</summary>
        public bool Against(long witness) => this.against.Add(witness) && this.against.Count == this.needed;

        /// <summary>The part was found, or a new game process began: what spoke against it no longer counts.</summary>
        public void Clear() => this.against.Clear();

        /// <summary>
        ///     One witness per loaded area: its instance's address with its hash. The address alone is not an area: the game can
        ///     load the next one into the instance it already had, and a rule that waits for "a second area" would then never
        ///     see one and never say its loss.
        /// </summary>
        public static long AreaKey(long address, string? hash) => address ^ ((long)System.StringComparer.Ordinal.GetHashCode(hash ?? string.Empty) << 20);

        /// <summary>One witness for a set of things (a batch of entities, a screen's controls), whatever their order.</summary>
        public static long KeyOf(IEnumerable<long> members)
        {
            long key = 0;
            var count = 0;
            foreach (var member in members)
            {
                key ^= member;
                count++;
            }

            return key ^ ((long)count << 48);
        }
    }
}
