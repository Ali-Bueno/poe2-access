namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     What names a dropdown for <see cref="AgreedChoice{T}"/> across a rebuild: the game recreates a dropdown at a new
    ///     address, and counting addresses made one dropdown look like two. The element's own name and its ancestors'
    ///     names, plus its entry count, are the same after the rebuild and differ between two dropdowns. Pure.
    /// </summary>
    public static class ComboIdentity
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>A stable key from the names (empty ones included, they still separate depth) and the entry count.</summary>
        public static long Of(string name, IEnumerable<string> ancestorNames, int entryCount)
        {
            var hash = FnvOffset;
            hash = Mix(hash, name);
            foreach (var ancestor in ancestorNames)
            {
                hash = Mix(hash, ancestor);
            }

            hash = (hash ^ (uint)entryCount) * FnvPrime;
            return (long)hash;
        }

        private static ulong Mix(ulong hash, string text)
        {
            foreach (var character in text)
            {
                hash = (hash ^ character) * FnvPrime;
            }

            return (hash ^ 0xFF) * FnvPrime; // a separator, so "ab","c" differs from "a","bc"
        }
    }
}
