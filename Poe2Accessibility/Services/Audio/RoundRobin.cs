namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Which take of a repeated sound plays next: any but the one played last. Pure.</summary>
    public static class RoundRobin
    {
        /// <summary>A take of <paramref name="count"/>, never <paramref name="last"/> (-1 at first) when there is another.</summary>
        public static int Next(int count, int last, Random random)
        {
            if (count <= 1)
            {
                return 0;
            }

            var pick = random.Next(last >= 0 && last < count ? count - 1 : count);
            return last >= 0 && last < count && pick >= last ? pick + 1 : pick;
        }
    }
}
