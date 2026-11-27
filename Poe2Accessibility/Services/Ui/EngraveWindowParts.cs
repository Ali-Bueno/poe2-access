namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>The parts of the support engraving window by their control ids (UI dump 2026-11-26 23:26, 0.5.5f). Pure.</summary>
    public static class EngraveWindowParts
    {
        /// <summary>The window, which holds the tab's label, the choices and the focused choice's description.</summary>
        public const string Window = "contents_frame";

        /// <summary>The tab's label: a skill's name, or "Strength Gems" and the like.</summary>
        public const string Category = "category_name_label";

        /// <summary>The focused choice's description, and in it the layout whose last part is the game's "Supportable Skills" list.</summary>
        public const string Description = "item_popup";
        public const string Bottom = "bottom_layout";

        /// <summary>The description's gem kind line ("[SupportGem|Support]" with its markup, whatever the language) and the mark of a support.</summary>
        public const string KindLine = "skill_type";
        public const string SupportMark = "[SupportGem|";

        /// <summary>
        ///     What a text is, by its ancestors (control id and child count, from the parent up to the window). The "Supportable
        ///     Skills" list is the unnamed part of the bottom layout that holds the heading and the grid; it is a list only on a
        ///     skill's tab, where the tab already names the skill. A skill gem's description (the uncut skill gem window) is
        ///     no support's and is left as it was read.
        /// </summary>
        public static EngravePart Classify(IReadOnlyList<(string Name, int Children)> chain, bool describesSupport, bool skillTab)
        {
            var popup = chain.ToList().FindIndex(ancestor => ancestor.Name == Description);
            if (popup < 0 || popup + 1 >= chain.Count || chain[popup + 1].Name != Window || !describesSupport)
            {
                return EngravePart.Other;
            }

            var bottom = chain.Take(popup).ToList().FindIndex(ancestor => ancestor.Name == Bottom);
            var list = bottom > 0 && chain[bottom - 1].Name.Length == 0 && chain[bottom - 1].Children >= 2;
            return list && skillTab ? EngravePart.SkillList : EngravePart.Description;
        }
    }
}
