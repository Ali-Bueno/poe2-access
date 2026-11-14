namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>A class offered on the Altar of Ascendancy, as its card is drawn.</summary>
    /// <param name="Name">The class, empty when the game's tables do not name it.</param>
    /// <param name="About">The card's own line about the class.</param>
    /// <param name="InBuild">The Build Planner marks the card with the build's name.</param>
    /// <param name="Centre">The card's middle on screen, left to right.</param>
    /// <param name="Width">How wide the card is drawn.</param>
    /// <param name="Tree">The element of the card's tree.</param>
    public readonly record struct AscendancyCard(string Name, string About, bool InBuild, float Centre, float Width, System.IntPtr Tree = default);

    /// <summary>
    ///     The Altar of Ascendancy's choice: the stick moves between cards without moving the focus, and the card it is on
    ///     is drawn larger than the rest (docs/research/trials-and-campaign.md §3). Pure.
    /// </summary>
    public static class AscendancyChoice
    {
        /// <summary>A card's line about its class: a label of this name inside the card's tree (dump 2026-11-14).</summary>
        public const string AboutName = "flavour_text_label";

        /// <summary>The Build Planner's fold button and the part it unfolds, siblings under the planner (dump 2026-11-14).</summary>
        public const string PlannerButtonName = "collapse_button";
        public const string PlannerOpenName = "expanded_frame";

        /// <summary>
        ///     How much wider than the narrowest card the chosen one must be: it was drawn 1.4 times the others (819 against
        ///     585 wide, dump 2026-11-14), so half of that growth tells it from cards of one size.
        /// </summary>
        public const float ChosenGrowth = 1.2f;

        /// <summary>The cards from left to right, as the stick walks them.</summary>
        public static List<AscendancyCard> InOrder(IEnumerable<AscendancyCard> cards) => cards.OrderBy(card => card.Centre).ToList();

        /// <summary>The place in <paramref name="ordered"/> of the card the stick is on; -1 while none stands out.</summary>
        public static int Chosen(IReadOnlyList<AscendancyCard> ordered)
        {
            if (ordered.Count < 2)
            {
                return -1;
            }

            var narrowest = ordered.Min(card => card.Width);
            var widest = ordered.Max(card => card.Width);
            if (narrowest <= 0 || widest < narrowest * ChosenGrowth)
            {
                return -1;
            }

            for (var index = 0; index < ordered.Count; index++)
            {
                if (ordered[index].Width == widest)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        ///     How much wider than the narrowest card the one A opened is: it fills the screen at twice the others (1170
        ///     against 585 wide, dump 2026-11-14), past the chosen card's 1.4.
        /// </summary>
        public const float OpenedGrowth = 1.7f;

        /// <summary>Whether A has opened the chosen card: it shows its tree alone, and B goes back to the three.</summary>
        public static bool Opened(IReadOnlyList<AscendancyCard> ordered, int chosen) =>
            chosen >= 0 && chosen < ordered.Count && ordered[chosen].Width >= ordered.Min(card => card.Width) * OpenedGrowth;

        /// <summary>The chosen card's line: "Herrero de Kitava, 3 de 3, en la build. Ansías el poder…"; opened, "Herrero de Kitava, vista previa del árbol".</summary>
        public static string Line(IReadOnlyList<AscendancyCard> ordered, int chosen) =>
            chosen < 0 || chosen >= ordered.Count ? string.Empty
                : Opened(ordered, chosen) ? Announcements.AscendancyTree(ordered[chosen].Name)
                : Announcements.AscendancyCard(ordered[chosen].Name, chosen + 1, ordered.Count, ordered[chosen].InBuild, ordered[chosen].About);
    }
}
