namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The world map's pin under the cursor, told from the game's data when its popup was not heard: in a 5:4 window the
    ///     game builds part of some popups a moment late (the Trial's level and words, 2026-11-08) and the screen reading
    ///     had only its title. So once the cursor rests on a pin, its words not having been said, name, monster level, words
    ///     and the area's other pins come from its MapPins row (<see cref="UiSemantics.DescribePin"/>). A popup that was
    ///     heard is left alone: the screen reading also gives its quests.
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
        private IntPtr pin;
        private DateTime since;
        private bool settled;
        private bool opening = true;

        public MapPinNarrator(UiSemantics semantics, IAccessibilityOutputService output, PluginLog log)
        {
            this.semantics = semantics;
            this.output = output;
            this.log = log;
        }

        /// <param name="worldScreen">The shown world map, zero while none is (<see cref="UiSemantics.WorldScreen"/>).</param>
        public void Tick(IntPtr worldScreen)
        {
            if (worldScreen == IntPtr.Zero)
            {
                (this.pin, this.settled, this.opening) = (IntPtr.Zero, false, true);
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
            if (this.semantics.PinMarker(hovered) is not { Length: > 0 } marker)
            {
                return;
            }

            if (this.output.SaidSince(marker, this.since))
            {
                // The popup was heard, maybe without its level: the screen reading said Keth's words but not "Nivel de
                // monstruos: 21" at 16:9 (2026-11-08), the level arriving after them. The missing level alone, after it.
                var kind = this.semantics.PinParts(worldScreen, hovered).Kind;
                if (kind.Length > 0 && !this.output.SaidSince(kind, this.since))
                {
                    this.log.Info($"Map pin heard without its kind, told from its data: {kind}");
                    this.output.Speak(kind);
                }

                return;
            }

            if (this.semantics.DescribePin(worldScreen, hovered) is not { Length: > 0 } line)
            {
                return;
            }

            // The first pin is the current area's, after the panel's title: queued. A pin the cursor moved to: at once.
            this.log.Info($"Map pin not heard, told from its data: {line}");
            this.output.Speak(line, !first);
        }
    }
}
