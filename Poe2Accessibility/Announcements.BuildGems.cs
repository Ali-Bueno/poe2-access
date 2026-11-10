namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using Poe2Accessibility.Services;

    /// <summary>Spoken strings of the Build Planner's gems: what the build asks for set against what the character has (same translatable layer).</summary>
    public static partial class Announcements
    {
        /// <summary>A gem the active build does not use.</summary>
        public static string OutsideBuild => T("outside the build", "fuera de la build");

        /// <summary>"Build: 3 of 9 skills, 0 of 21 supports", on entering the Skills panel.</summary>
        public static string BuildGemCount(int skills, int allSkills, int supports, int allSupports) =>
            T($"Build: {skills} of {allSkills} skills, {supports} of {allSupports} supports", $"Build: {skills} de {allSkills} habilidades, {supports} de {allSupports} asistencias");

        public static string BuildNextSkill(string skill) => T($"Next skill: {skill}", $"Siguiente habilidad: {skill}");

        public static string BuildNextSupport(string support, string skill) =>
            T($"Next support: {support} for {skill}", $"Siguiente asistencia: {support} para {skill}");

        /// <summary>One support of the build: socketed, lying in the inventory, held at another tier, sitting in another skill, or missing.</summary>
        public static string BuildSupport(string name, GemState state, string held) => state switch
        {
            GemState.Socketed => T($"{name}, socketed", $"{name}, engarzada"),
            GemState.InBag => T($"{name}, in your inventory", $"{name}, en tu inventario"),
            GemState.OtherTier => T($"{name}, you have {held}", $"{name}, tienes {held}"),
            GemState.InOtherSkill => SupportEngraved(name, held),
            _ => T($"{name}, missing", $"{name}, falta"),
        };

        /// <summary>On an empty skill slot: the build's skills the character does not have yet.</summary>
        public static string BuildSkillsToGet(IEnumerable<string> skills) => T("Build skills to get: ", "Habilidades de la build por conseguir: ") + string.Join("; ", skills);

        /// <summary>On an empty support socket: the build's supports for that skill not socketed yet.</summary>
        public static string BuildSocketWants(IEnumerable<string> supports) => T("The build puts here: ", "La build pone aquí: ") + string.Join("; ", supports);

        /// <summary>A support gem the build uses, with the skills it goes in: "in the build, for Twister, Ice-Tipped Arrows".</summary>
        public static string BuildGemFor(IEnumerable<string> skills) => T("in the build, for ", "en la build, para ") + string.Join(", ", skills);

        /// <summary>A socketed support of another tier than the build's: "the build asks for Rage II".</summary>
        public static string BuildAsksFor(string gem) => T($"the build asks for {gem}", $"la build pide {gem}");
    }
}
