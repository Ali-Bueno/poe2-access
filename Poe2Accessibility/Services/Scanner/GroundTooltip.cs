namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Pure: which item on the ground a tooltip on the HUD is of, when several lie there under one title.</summary>
    public static class GroundTooltip
    {
        /// <summary>An item on the ground: the title its tooltip shows, its base name (a rare's tooltip names it on a line of its own) and its spoken name, empty for gold.</summary>
        public readonly record struct Item(string Title, string BaseName, string Spoken);

        /// <summary>An item's name without its stack or rarity ("Orbe de aumento, 3" gives "Orbe de aumento").</summary>
        public static string TitleOf(string spoken) => spoken.Split(',')[0].Trim();

        /// <summary>
        ///     The spoken name of the item whose tooltip holds <paramref name="shown"/>: the one the game targets (what A
        ///     picks up, what the tooltip is drawn for), else the one lying in view under that title; two that are said
        ///     differently (stacks of 3 and 7) give the title alone, never one stack's count for the other. An item no
        ///     longer there is answered from what was <paramref name="remembered"/>. Null when the texts are no item's.
        /// </summary>
        public static string? Name(IReadOnlyCollection<string> shown, Item? target, IReadOnlyList<Item> inView, Func<string, string?> remembered)
        {
            bool Shows(Item item) => (item.Title.Length > 0 && shown.Contains(item.Title)) || (item.BaseName.Length > 0 && shown.Contains(item.BaseName));
            if (target is { } aimed && Shows(aimed))
            {
                return aimed.Spoken;
            }

            var named = inView.Where(Shows).ToList();
            if (named.Count > 0)
            {
                return named.Select(item => item.Spoken).Distinct().Count() == 1 ? named[0].Spoken : named[0].Title;
            }

            return shown.Select(remembered).FirstOrDefault(spoken => spoken != null);
        }

        /// <summary>
        ///     Keeps an item's spoken name under its title for a tooltip that comes after the item is gone; a second item of
        ///     the title said differently leaves the title alone, since which of them a late tooltip is of is not known.
        /// </summary>
        public static void Remember(IDictionary<string, string> titles, string title, string spoken)
        {
            if (title.Length > 0)
            {
                titles[title] = titles.TryGetValue(title, out var known) && known != spoken && known.Length > 0 && spoken.Length > 0 ? title : spoken;
            }
        }
    }
}
