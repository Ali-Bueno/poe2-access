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
    }
}
