namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Which bit of a flags word marks a kind of element, judged on words of elements of that kind and of others. Pure.</summary>
    public static class BitSearch
    {
        /// <summary>Elements of each kind needed to judge (fewer carriers may be allowed when a screen has few of them).</summary>
        public const int MinOthers = 3;

        /// <summary>
        ///     The bit, nearest to where it was measured, that at least <paramref name="carrierShare"/> of the
        ///     <paramref name="carriers"/> have and at most <paramref name="otherShare"/> of the <paramref name="others"/>;
        ///     null when none does or there is too little to judge.
        /// </summary>
        public static int? FindBit(IReadOnlyList<uint> carriers, IReadOnlyList<uint> others, int measured, double carrierShare, double otherShare, int minCarriers)
        {
            if (carriers.Count < minCarriers || others.Count < MinOthers)
            {
                return null;
            }

            return OffsetSearch.Outward(measured, 31, 1).Where(bit => bit < 32)
                .Cast<int?>()
                .FirstOrDefault(bit => Share(carriers, bit!.Value) >= carrierShare && Share(others, bit.Value) <= otherShare);
        }

        /// <summary>The share of flag words with the bit set.</summary>
        public static double Share(IReadOnlyList<uint> flags, int bit) => flags.Count == 0 ? 0 : flags.Count(value => (value & (1u << bit)) != 0) / (double)flags.Count;

        /// <summary>
        ///     Judges a bit on one witness's flags (a screen, a round of focused elements): the measured bit is kept while
        ///     <paramref name="keepShare"/> of the carriers have it and few others do; else the nearest that fits, another than
        ///     the measured one only once a second witness names it (with <paramref name="moved"/>); none fitting is no verdict
        ///     until a second witness finds none either (<paramref name="misses"/>).
        /// </summary>
        public static BitVerdict Judge(
            IReadOnlyList<uint> carriers, IReadOnlyList<uint> others, int measured, double keepShare, double carrierShare, double otherShare, int minCarriers,
            LossWitnesses misses, long witness, MovedBit? moved = null)
        {
            if (Share(carriers, measured) >= keepShare && Share(others, measured) <= otherShare)
            {
                moved?.Clear();
                return new BitVerdict(BitJudgement.Found, measured, false);
            }

            if (FindBit(carriers, others, measured, carrierShare, otherShare, minCarriers) is not { } bit)
            {
                var say = misses.Against(witness);
                return new BitVerdict(misses.Lost ? BitJudgement.Lost : BitJudgement.Pending, measured, say);
            }

            if (bit != measured && moved != null && !moved.Confirms(bit, witness))
            {
                return new BitVerdict(BitJudgement.Pending, bit, false);
            }

            return new BitVerdict(bit == measured ? BitJudgement.Found : BitJudgement.Moved, bit, false);
        }
    }

    /// <summary>What one witness makes of a flag bit.</summary>
    public enum BitJudgement
    {
        /// <summary>Where measured: settled.</summary>
        Found,

        /// <summary>Elsewhere, at the verdict's bit: settled.</summary>
        Moved,

        /// <summary>No verdict yet: another witness must agree.</summary>
        Pending,

        /// <summary>No bit fits, on enough witnesses: settled, and said once.</summary>
        Lost,
    }

    /// <param name="SayLost">This witness is the one that makes the bit lost: the loss is said now.</param>
    public readonly record struct BitVerdict(BitJudgement Judgement, int Bit, bool SayLost);
}
