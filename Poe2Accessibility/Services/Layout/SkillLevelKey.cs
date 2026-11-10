namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The GrantedEffectsPerLevel key of a skill the player lists (<see cref="GameLayout.SkillSlot.LevelKeyOffset"/>), which the
    ///     game's code places at attach and nothing judged live: read where it stands; while no listed skill has read there,
    ///     a key into that table nearest it is looked for and taken once two skills agree (<see cref="DatKeySite"/>), and every
    ///     reader of the offset follows.
    /// </summary>
    public static class SkillLevelKey
    {
        private const string Part = "skill level key";
        private const int Measured = GameLayout.SkillSlot.MeasuredLevelKeyOffset;
        private static readonly DatKeySite Site = new(Part, Measured, GameLayout.SkillSlot.LevelTable);
        private static bool movedHere;

        /// <summary>The level row of the skill details at <paramref name="details"/>, an entry of the player's skill list; null when none reads.</summary>
        public static IntPtr? Row(HostMemoryReader reader, UiElementReader elements, IntPtr details)
        {
            // The code put the field elsewhere (layout:SkillLevelKey): that reading stands, the live search is for where it could not.
            if (GameLayout.SkillSlot.LevelKeyOffset != Site.Offset)
            {
                return DatKey.Row(reader, elements, details + GameLayout.SkillSlot.LevelKeyOffset, GameLayout.SkillSlot.LevelTable);
            }

            var row = Site.Row(reader, elements, details);
            if (Site.Offset != GameLayout.SkillSlot.LevelKeyOffset)
            {
                (GameLayout.SkillSlot.LevelKeyOffset, movedHere) = (Site.Offset, true);
            }

            return row;
        }

        /// <summary>A new game process: a place this search found in the last one says nothing of the next.</summary>
        public static void Reset()
        {
            if (movedHere)
            {
                (GameLayout.SkillSlot.LevelKeyOffset, movedHere) = (Measured, false);
            }
        }
    }
}
