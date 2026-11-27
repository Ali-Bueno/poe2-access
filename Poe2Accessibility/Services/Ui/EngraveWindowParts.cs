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

        /// <summary>The focused choice's description, down to the game's "Supportable Skills" list that ends it.</summary>
        public const string Description = "item_popup";

        /// <summary>The description's gem kind line ("[SupportGem|Support]" with its markup, whatever the language) and the mark of a support.</summary>
        public const string KindLine = "skill_type";
        public const string SupportMark = "[SupportGem|";

        /// <summary>
        ///     What a text is, by its ancestors' control ids from the parent up to the window: a line of a support's description,
        ///     or anything else. A skill gem's description (the uncut skill gem window) is no support's and is read as it was.
        /// </summary>
        public static EngravePart Classify(IReadOnlyList<string> chain, bool describesSupport)
        {
            var popup = chain.ToList().IndexOf(Description);
            return popup >= 0 && popup + 1 < chain.Count && chain[popup + 1] == Window && describesSupport ? EngravePart.Description : EngravePart.Other;
        }
    }
}
