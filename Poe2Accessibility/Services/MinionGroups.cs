namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>"Minions: 3 Skeletal Archer, 80 percent; Spectral Wolf, 45 percent": by kind, the most first. Pure.</summary>
    public static class MinionGroups
    {
        public static string Describe(IEnumerable<(string Name, int LifePercent)> minions)
        {
            var groups = minions.GroupBy(minion => minion.Name)
                .Select(group => (Name: group.Key, Count: group.Count(), Life: (int)Math.Round(group.Average(minion => minion.LifePercent))))
                .OrderByDescending(group => group.Count).ThenBy(group => group.Name, StringComparer.CurrentCulture)
                .Select(group => Announcements.MinionGroup(group.Name, group.Count, group.Life))
                .ToList();
            return groups.Count == 0 ? string.Empty : Announcements.Minions(groups);
        }
    }
}
