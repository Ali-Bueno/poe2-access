namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The tagline under an area's name on the arrival banner, found by its shape and said once per area.</summary>
    public sealed class AreaBanner
    {
        private readonly HashSet<string> said = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        ///     The tagline of the banner of <paramref name="areaName"/>: the other text of a group of exactly two texts
        ///     (one parent's text children) one of which is the name; null for any other shape or a banner of another area.
        /// </summary>
        public static string? Match(string areaName, IEnumerable<IReadOnlyList<string>> groups)
        {
            foreach (var group in groups)
            {
                if (group.Count != 2)
                {
                    continue;
                }

                var nameAt = group.ToList().FindIndex(text => string.Equals(text, areaName, StringComparison.CurrentCultureIgnoreCase));
                var tagline = nameAt < 0 ? string.Empty : group[1 - nameAt].Trim();
                if (tagline.Length > 0 && !string.Equals(tagline, areaName, StringComparison.CurrentCultureIgnoreCase))
                {
                    return tagline;
                }
            }

            return null;
        }

        /// <summary>The tagline when this area has not had it said yet in the session, remembering it as said.</summary>
        public string? TakeOnce(string areaId, string areaName, IEnumerable<IReadOnlyList<string>> groups)
        {
            if (areaId.Length == 0 || this.said.Contains(areaId) || Match(areaName, groups) is not { } tagline)
            {
                return null;
            }

            this.said.Add(areaId);
            return tagline;
        }
    }
}
