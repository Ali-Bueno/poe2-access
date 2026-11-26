namespace Poe2Accessibility.Services
{
    /// <summary>What a skill's id says by itself (shared with the extractor, which links this file). Pure.</summary>
    public static class SkillIds
    {
        /// <summary>
        ///     A skill id past its kind's capitals ("EAS", "GA", "GS", "MPA"…): the name from the last capital before the first
        ///     lower-case letter; null when the id starts with a word (no kind prefix).
        /// </summary>
        public static string? Stem(string skillId)
        {
            var lower = 0;
            while (lower < skillId.Length && !char.IsLower(skillId[lower]))
            {
                lower++;
            }

            return lower >= 2 && lower < skillId.Length ? skillId[(lower - 1)..] : null;
        }
    }
}
