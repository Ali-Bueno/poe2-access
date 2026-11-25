namespace Poe2Accessibility.Services
{
    using System;
    /// <summary>
    ///     The Atlas node the cursor rests on, told with its state first (atlas-navigator Phase 1): on the Atlas map
    ///     neither the focus nor the hovered slot ever points at a node, so the endgame reader never fired and only
    ///     the popup's flavour was heard (live 2026-11-25, docs/research/atlas-navigator.md §6). map_control keeps
    ///     the selected node; once it settles, its name, state, contents and links are said
    ///     (<see cref="UiSemantics.AtlasNodeLine"/>), after the popup's own words.
    /// </summary>
    public sealed class AtlasNarrator
    {
        /// <summary>The popup is built a few hundred ms after a hop lands: the line is composed no sooner than this.</summary>
        private const int SettleMs = 700;

        /// <summary>A popup still unbuilt this long after landing is composed without (catalog name and words).</summary>
        private const int GiveUpMs = 2500;

        private readonly UiSemantics semantics;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private IntPtr node;
        private DateTime since;
        private bool settled;
        private bool opening = true;

        public AtlasNarrator(UiSemantics semantics, IAccessibilityOutputService output, PluginLog log)
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
                (this.node, this.settled, this.opening) = (IntPtr.Zero, false, true);
                return;
            }

            var selected = this.semantics.SelectedAtlasNode(worldScreen) ?? IntPtr.Zero;
            var now = DateTime.UtcNow;
            if (selected != this.node)
            {
                (this.node, this.since, this.settled) = (selected, now, false);
                return;
            }

            if (this.settled || selected == IntPtr.Zero)
            {
                return;
            }

            var elapsed = (now - this.since).TotalMilliseconds;
            if (elapsed < SettleMs || this.semantics.AtlasNodeLine(selected, allowPending: elapsed >= GiveUpMs) is not { } line)
            {
                return;
            }

            this.settled = true;
            if (line.Length == 0)
            {
                return;
            }

            // The first node follows the panel's opening words: queued. One hopped to: over its flavour text,
            // which has had the settle time to say its name.
            var first = this.opening;
            this.opening = false;
            this.log.Info($"Atlas node landed: {line}");
            this.output.Speak(line, !first);
        }
    }
}
