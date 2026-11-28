namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The world map's pin under the cursor, told from the game's data when its popup was not heard: in a 5:4 window the
    ///     game builds part of some popups a moment late (the Trial's level and words, 2026-11-08) and the screen reading
    ///     had only its title. So once the cursor rests on a pin, its words not having been said, name, monster level, words
    ///     and the area's other pins come from its MapPins row (<see cref="UiSemantics.DescribePin"/>). A popup that was
    ///     heard is left alone: the screen reading also gives its quests. The quests' marks are told too: a place the map marks
    ///     with its exclamation mark says "Quest location" under the cursor, and each time the map opens or shows another act
    ///     the places it marks there are said once with their nearest active waypoint, as a look at the map shows them.
    /// </summary>
    public sealed class MapPinNarrator
    {
        /// <summary>The popup is read a few frames after the cursor lands: it is waited for this long.</summary>
        private const int SettleMs = 700;

        /// <summary>The current area's popup is read with the panel's title, a moment before its pin is seen.</summary>
        private const int OpeningLeadMs = 2000;

        private readonly UiSemantics semantics;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly Func<bool> questMarks;
        private IntPtr pin;
        private IntPtr glancedCanvas;
        private DateTime since;
        private bool settled;
        private bool opening = true;

        /// <param name="questMarks">Whether the quests' marks are told (setting AnnounceQuestMarks).</param>
        public MapPinNarrator(UiSemantics semantics, IAccessibilityOutputService output, PluginLog log, Func<bool>? questMarks = null)
        {
            this.semantics = semantics;
            this.output = output;
            this.log = log;
            this.questMarks = questMarks ?? (() => true);
        }

        /// <param name="worldScreen">The shown world map, zero while none is (<see cref="UiSemantics.WorldScreen"/>).</param>
        public void Tick(IntPtr worldScreen)
        {
            if (worldScreen == IntPtr.Zero)
            {
                (this.pin, this.settled, this.opening, this.glancedCanvas) = (IntPtr.Zero, false, true, IntPtr.Zero);
                return;
            }

            var hovered = this.semantics.HoveredPin(worldScreen);
            var now = DateTime.UtcNow;
            if (hovered != this.pin)
            {
                (this.pin, this.since, this.settled) = (hovered, this.opening ? now.AddMilliseconds(-OpeningLeadMs) : now, false);
                return;
            }

            if (this.settled || hovered == IntPtr.Zero || (now - this.since).TotalMilliseconds < SettleMs + (this.opening ? OpeningLeadMs : 0))
            {
                return;
            }

            this.settled = true;
            var first = this.opening;
            this.opening = false;

            // The look at the marks comes once a map's first pin has been told: on opening, after the current area's.
            var canvas = this.questMarks() ? this.semantics.MapCanvas(worldScreen) : IntPtr.Zero;
            var glance = canvas != IntPtr.Zero && canvas != this.glancedCanvas;
            this.TellPin(worldScreen, hovered, first, quest: canvas != IntPtr.Zero && !glance);
            if (glance)
            {
                this.glancedCanvas = canvas;
                this.Glance(worldScreen);
            }
        }

        /// <summary>The settled pin: its popup's missing parts or the whole from data, then what its area's pictures say.</summary>
        private void TellPin(IntPtr worldScreen, IntPtr hovered, bool first, bool quest)
        {
            if (this.semantics.PinMarker(hovered) is not { Length: > 0 } marker)
            {
                return;
            }

            if (this.output.SaidSince(marker, this.since))
            {
                // The popup was heard, maybe without its level: the screen reading said Keth's words but not "Nivel de
                // monstruos: 21" at 16:9 (2026-11-08), the level arriving after them. The missing level alone, after it.
                var kind = this.semantics.PinParts(worldScreen, hovered).Kind;
                var level = this.semantics.PinLevelStart;
                if (kind.Length > 0 && !this.output.SaidSince(kind, this.since) &&
                    !(level.Length > 0 && kind.StartsWith(level, StringComparison.Ordinal) && this.output.SaidSince(level, this.since)))
                {
                    this.log.Info($"Map pin heard without its kind, told from its data: {kind}");
                    this.output.Speak(kind);
                }

                this.TellArea(worldScreen, hovered, heard: true, quest);
                return;
            }

            if (this.semantics.DescribePin(worldScreen, hovered) is not { Length: > 0 } line)
            {
                return;
            }

            // The first pin is the current area's, after the panel's title: queued. A pin the cursor moved to: at once.
            this.log.Info($"Map pin not heard, told from its data: {line}");
            this.output.Speak(line, !first);
            this.TellArea(worldScreen, hovered, heard: false, quest);
        }

        /// <summary>The places the shown act's map marks for the quests, each with its nearest active waypoint; nothing when it marks none.</summary>
        private void Glance(IntPtr worldScreen)
        {
            var marks = this.semantics.QuestMarks(worldScreen).Marks;
            if (marks.Count == 0)
            {
                return;
            }

            var lines = marks.Select(Announcements.QuestMarkGlance).ToList();
            this.log.Info($"Quest marks on the map: {string.Join(" | ", lines)}");
            this.output.Speak(Announcements.Sentences(lines));
        }

        /// <summary>
        ///     What the map's pictures say and no text does: the quests marking the area, whether it was entered, and on a travel
        ///     map what is within it.
        /// </summary>
        /// <param name="quest">Whether to say the quests that mark the area: not when the look at the marks follows at once.</param>
        private void TellArea(IntPtr worldScreen, IntPtr hovered, bool heard, bool quest)
        {
            var pictures = this.semantics.PinArea(worldScreen, hovered);
            var lines = new List<string>();
            if (quest && this.semantics.QuestMarkOf(worldScreen, hovered) is { } mark)
            {
                lines.Add(Announcements.QuestLocation(mark.Quests));
            }

            if (pictures.RealName.Length > 0)
            {
                lines.Add(Announcements.MapMisnamed(pictures.RealName));
            }

            if (Announcements.MapArea(pictures.State) is { Length: > 0 } entered)
            {
                lines.Add(entered);
            }

            if (heard)
            {
                lines.AddRange(pictures.Pending.Where(thing => !this.output.SaidSince(thing.Name, this.since)).Select(thing => thing.Line));
                var done = pictures.Done.Where(thing => !this.output.SaidSince(thing, this.since)).ToList();
                if (done.Count > 0)
                {
                    lines.Add(Announcements.MapDone(done));
                }
            }

            if (lines.Count > 0)
            {
                this.output.Speak(Announcements.Sentences(lines));
            }
        }
    }
}
