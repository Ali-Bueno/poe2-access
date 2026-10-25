namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Where the Targetable component keeps its in-play flag, judged on component bytes (Clearfell Encampment,
    ///     2026-10-25: +0x69 is 1 on players, the waypoint and the well, and on one story variant of an NPC, Renly; 0 on
    ///     the other, RenlyAfterIronCount). Pure.
    /// </summary>
    public static class InPlayFlag
    {
        /// <summary>The flag is judged on at least this many things surely in play.</summary>
        public const int MinInPlay = 3;

        /// <summary>Most of what is surely in play reads 1 (a monster rising from the ground is briefly out of play).</summary>
        private const double InPlayShare = 0.9;

        /// <summary>The share of things surely in play that read 1 at <paramref name="offset"/>.</summary>
        public static double Share(IReadOnlyList<byte[]> inPlay, int offset) =>
            inPlay.Count == 0 ? 0 : inPlay.Count(block => At(block, offset) == 1) / (double)inPlay.Count;

        /// <summary>
        ///     The offset holds the flag: nearly all of what is surely in play reads 1 and nothing reads other than 0 or 1;
        ///     each group of story variants of one NPC has at most one in play; and something reads 0, or a constant 1
        ///     would fit too.
        /// </summary>
        public static bool Fits(IReadOnlyList<byte[]> inPlay, IReadOnlyList<IReadOnlyList<byte[]>> variants, int offset)
        {
            if (inPlay.Count < MinInPlay || inPlay.Any(block => At(block, offset) > 1) || Share(inPlay, offset) < InPlayShare)
            {
                return false;
            }

            var zeros = 0;
            foreach (var group in variants)
            {
                var values = group.Select(block => At(block, offset)).ToList();
                if (values.Any(value => value > 1) || values.Count(value => value == 1) > 1)
                {
                    return false;
                }

                zeros += values.Count(value => value == 0);
            }

            return zeros > 0;
        }

        /// <summary>The byte at the offset; 0xFF past the block's end (never fits).</summary>
        private static int At(byte[] block, int offset) => offset >= 0 && offset < block.Length ? block[offset] : 0xFF;
    }
}
