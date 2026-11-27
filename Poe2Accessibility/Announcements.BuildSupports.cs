namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Spoken strings of a build skill's supports, heard group by group (same translatable layer).</summary>
    public static partial class Announcements
    {
        /// <summary>A gem the build takes later: "Combat Frenzy, from level 26".</summary>
        public static string GemFromLevel(string gem, int level) => T($"{gem}, from level {level}", $"{gem}, desde el nivel {level}");

        /// <summary>
        ///     A skill's supports in the build, the count first and then each group that holds something: "Build supports: 1 of
        ///     4 socketed. Socketed: Rage II. Missing: Zenith II; Bludgeon, from level 26". All socketed, the count alone.
        /// </summary>
        public static string BuildSupportGroups(
            int wanted, IReadOnlyList<string> socketed, IEnumerable<string> inBag, IEnumerable<string> otherTier, IEnumerable<string> inOtherSkill,
            IEnumerable<string> missing, IEnumerable<string> outside)
        {
            var parts = new List<string>();
            void Group(string label, IEnumerable<string> gems)
            {
                if (string.Join("; ", gems) is { Length: > 0 } list)
                {
                    parts.Add(label + list);
                }
            }

            if (wanted > 0)
            {
                parts.Add(T($"Build supports: {socketed.Count} of {wanted} socketed", $"Asistencias de la build: {socketed.Count} de {wanted} engarzadas"));
            }

            if (socketed.Count < wanted)
            {
                Group(T("Socketed: ", "Engarzadas: "), socketed);
                Group(T("In your inventory: ", "En tu inventario: "), inBag);
                Group(T("Another tier: ", "De otro nivel: "), otherTier);
                Group(T("In another skill: ", "En otra habilidad: "), inOtherSkill);
                Group(T("Missing: ", "Faltan: "), missing);
            }

            Group(T("Outside the build: ", "Fuera de la build: "), outside);
            return string.Join(". ", parts);
        }

        /// <summary>A build support held at another tier: "you have Rage I, the build asks for Rage II".</summary>
        public static string BuildTierHeld(string held, string wanted) => T($"you have {held}, the build asks for {wanted}", $"tienes {held}, la build pide {wanted}");

        /// <summary>A build skill among the engraving window's choices: whether the character has it, asked before cutting it again.</summary>
        public static string EngraveSkillHeld(bool owned, bool inBag) => owned ? T("you already have it", "ya la tienes") : inBag ? HeldInBag : NotHeld;

        /// <summary>
        ///     A build support among the engraving window's choices: "already socketed in Twister; missing in Whirling Slash",
        ///     "you have it in your inventory", "you do not have it".
        /// </summary>
        public static string EngraveSupportHeld(
            IReadOnlyList<string> socketedIn, bool inBag, IEnumerable<string> inOtherSkills, IEnumerable<string> otherTiers, IReadOnlyList<string> missingIn)
        {
            var parts = new List<string>();
            if (socketedIn.Count > 0)
            {
                parts.Add(T("already socketed in ", "ya engarzada en ") + string.Join(", ", socketedIn));
            }

            if (inBag)
            {
                parts.Add(HeldInBag);
            }

            if (string.Join(", ", inOtherSkills) is { Length: > 0 } others)
            {
                parts.Add(T("you have it in ", "la tienes en ") + others);
            }

            parts.AddRange(otherTiers);
            if (missingIn.Count > 0)
            {
                parts.Add(parts.Count == 0 ? NotHeld : T("missing in ", "falta en ") + string.Join(", ", missingIn));
            }

            return string.Join("; ", parts);
        }

        /// <summary>A skill's tab of the support engraving window, whose skill has the support offered socketed.</summary>
        public static string SkillHasSupport(string skill) => T($"{skill} already has this support", $"{skill} ya tiene esta asistencia");

        /// <summary>The same, holding another tier of it: "Lightning Rod already has Elemental Armament II".</summary>
        public static string SkillHasGem(string skill, string gem) => T($"{skill} already has {gem}", $"{skill} ya tiene {gem}");

        /// <summary>The build puts the support offered in the tab's skill, which does not have it.</summary>
        public static string SkillNeedsSupport(string skill) => T($"{skill} needs this support", $"{skill} necesita esta asistencia");

        /// <summary>After <see cref="SkillNeedsSupport"/>: a cut copy lies in the inventory.</summary>
        public static string SpareInBag => T("You have one in your inventory", "Tienes una en tu inventario");

        /// <summary>The build does not put the support offered in the tab's skill.</summary>
        public static string NotInBuildFor(string skill) => T($"Not in the build for {skill}", $"No está en la build para {skill}");

        /// <summary>A tab that is no skill's (Strength Gems): the build skills that use the support offered.</summary>
        public static string InBuildFor(IReadOnlyList<string> skills) => T($"In the build for {JoinAnd(skills)}", $"En la build para {JoinAnd(skills)}");

        /// <summary>After <see cref="InBuildFor"/>: one of those skills has it socketed.</summary>
        public static string SkillHasIt(string skill) => T($"{skill} already has it", $"{skill} ya la tiene");

        /// <summary>A tab that is no skill's: no build skill uses the support offered.</summary>
        public static string NotInBuild => T("Not in the build", "No está en la build");

        /// <summary>"A", "A and B", "A, B and C".</summary>
        private static string JoinAnd(IReadOnlyList<string> items) =>
            items.Count <= 1 ? string.Concat(items) : string.Join(", ", items.Take(items.Count - 1)) + T(" and ", " y ") + items[^1];

        private static string HeldInBag => T("you have it in your inventory", "la tienes en tu inventario");

        private static string NotHeld => T("you do not have it", "no la tienes");

        /// <summary>A build support socketed in another skill: "Rage II, in Twister".</summary>
        public static string GemInSkill(string gem, string skill) => T($"{gem}, in {skill}", $"{gem}, en {skill}");
    }
}
