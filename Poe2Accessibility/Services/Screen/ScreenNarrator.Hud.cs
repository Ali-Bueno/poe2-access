namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>The in-game HUD of <see cref="ScreenNarrator"/>: its quiet period, ground item tooltips, tutorial hints and the news it carries.</summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>A tutorial hint shown this soon after the HUD is back on top is the box reappearing, not news.</summary>
        private const int HudBackMs = 3000;

        /// <summary>The HUD read in this long after the host started inside an area was already there, not news.</summary>
        private const int ResumedHudMs = 2000;

        private readonly HashSet<string> hudSpoken = new();

        /// <summary>Tutorial hints said in this area and when.</summary>
        private readonly Dictionary<string, DateTime> tutorialSpoken = new();

        /// <summary>When the HUD last became the top layer again (a panel, the radial menu or a respawn screen closed).</summary>
        private DateTime hudBackAt = DateTime.MinValue;

        /// <summary>Until then the HUD's texts are only noted (the host restarted with the game in an area).</summary>
        private DateTime hudQuietUntil = DateTime.MinValue;

        /// <summary>When a ground item's name was said: the rest of its tooltip follows in a second step.</summary>
        private DateTime groundItemAt = DateTime.MinValue;

        /// <summary>What was last said of a ground item's tooltip: its lines settling a moment apart are one tooltip, said once.</summary>
        private string groundItemSaid = string.Empty;

        /// <summary>
        ///     A new area (its name changed): the HUD's and the tutorial box's texts are news again. Not on every game
        ///     state change — the game menu closing and a respawn in the same area would repeat the quest tracker,
        ///     "Nuevo punto de control" and the last tutorial hint.
        /// </summary>
        /// <summary>A hint never said in this area; or said long ago and shown again while playing (the HUD on top, no panel just closed).</summary>
        /// <summary>
        ///     The inventory's gold amount and its header (the unit word alone, "Oro"): a reward or vendor panel opens
        ///     beside the inventory and read "20 oro. Oro." first.
        /// </summary>
        private HashSet<IntPtr> Chrome(IReadOnlyList<ScreenText> texts)
        {
            var chrome = new HashSet<IntPtr>();
            var counter = this.GoldCounter?.Invoke() ?? IntPtr.Zero;
            if (counter == IntPtr.Zero || texts.FirstOrDefault(text => text.Address == counter) is not { } amount)
            {
                return chrome;
            }

            chrome.Add(counter);
            var unit = amount.Text.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ',', ' ');
            foreach (var text in texts.Where(text => unit.Length > 0 && string.Equals(text.Text, unit, StringComparison.CurrentCultureIgnoreCase)))
            {
                chrome.Add(text.Address);
            }

            return chrome;
        }

        /// <summary>Notes the HUD coming back on top, once per snapshot.</summary>
        private void NoteTop(ScreenSnapshot snapshot)
        {
            if (ReferenceEquals(snapshot, this.seenSnapshot))
            {
                return;
            }

            var top = snapshot.TopInteractive?.Address ?? IntPtr.Zero;
            if (this.seenSnapshot != null && top != this.seenTop && top == this.HudLayer && top != IntPtr.Zero)
            {
                this.hudBackAt = DateTime.UtcNow;
            }

            this.seenSnapshot = snapshot;
            this.seenTop = top;
        }

        /// <summary>A new area; resumed when the host started inside it, so what the HUD shows now was already said.</summary>
        public void NewArea(bool resumed)
        {
            this.hudSpoken.Clear();
            this.trialCounters.Reset();
            this.honour.Reset();
            this.tutorialSpoken.Clear();
            this.hudQuietUntil = resumed ? DateTime.UtcNow.AddMilliseconds(ResumedHudMs) : DateTime.MinValue;
            this.hudBaseline = resumed;
        }

        /// <summary>
        ///     A host restart: the HUD's first texts were already said, even when a panel hid it past the quiet time
        ///     (the whole quest tracker was read on closing the inventory, 2026-10-24).
        /// </summary>
        private bool hudBaseline;

        /// <summary>The in-game objective / tutorial box: whatever it shows next is read, whoever has the focus.</summary>
        public IntPtr TutorialLayer { get; set; }

        /// <summary>A HUD text read elsewhere (the target display's name, TargetNarrator), left out of the HUD news.</summary>
        public Func<IntPtr>? IgnoredHudText { get; set; }

        /// <summary>The inventory's gold amount text (GoldWatch): with its unit word as a header, chrome of any panel beside the inventory.</summary>
        public Func<IntPtr>? GoldCounter { get; set; }

        /// <summary>
        ///     The spoken name of the ground item whose tooltip the HUD shows, from its texts (empty for gold); null when the
        ///     texts are not an item's. Such a tooltip says only the name and the quality: the whole text is read in the
        ///     inventory (user, 2026-10-24); a shrine's or a portal's is read whole.
        /// </summary>
        public Func<IEnumerable<string>, string?>? GroundItemName { get; set; }

        /// <summary>True while a ground item's tooltip is not said (a fight, <see cref="FightWatch"/>).</summary>
        public Func<bool>? QuietGroundItems { get; set; }

        /// <summary>
        ///     True while A uses an item on the ground and it is named at once (ProximityNarrator): the tooltip the game shows
        ///     for it is that item's, magic and rare names included ("Bastón largo intenso de impacto" for "Bastón largo"),
        ///     and is not said again.
        /// </summary>
        public Func<bool>? ItemTargetNamed { get; set; }

        /// <summary>The quality line of an item tooltip ("Calidad: +10%"): the only property with a signed percentage.</summary>
        private static readonly System.Text.RegularExpressions.Regex QualityLine = new(@":\s*\+\d+\s*%\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        ///     A tooltip property line ("Probabilidad de impacto crítico: 5.00%") that arrived ahead of its item's name, the
        ///     tooltip's texts settling at different times (2026-10-26): never HUD news.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex PropertyLine = new(@"^[^:]{2,60}:\s*[-+]?\d", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>The in-game HUD: new texts (quest tracker, ground item) are read after the rest, never as a page.</summary>
        public IntPtr HudLayer { get; set; }

        /// <summary>
        ///     A HUD text in the skill bar: its alert line ("Sin maná", "Inventario lleno", "En tiempo de recarga"), said
        ///     each time it shows rather than once per area (a second "Inventario lleno" was silent, 2026-10-24).
        /// </summary>
        public Func<IntPtr, bool>? IsSkillBarText { get; set; }

        /// <summary>A quest's title or objective line in the HUD's quest tracker.</summary>
        public Func<IntPtr, bool>? IsQuestTrackerText { get; set; }

        /// <summary>
        ///     The area just entered is still to be named: its quest page waits for that line, which says where the player
        ///     stands and came after the page or not at all (user 2026-11-11).
        /// </summary>
        public Func<bool>? AreaLinePending { get; set; }
    }
}
