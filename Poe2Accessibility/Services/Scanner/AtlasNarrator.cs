namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     The Atlas node the cursor rests on, told with its state first, and where it sits (atlas-navigator
    ///     Phases 1–2): on the Atlas map neither the focus nor the hovered slot ever points at a node, so the
    ///     endgame reader never fired and only the popup's flavour was heard (live 2026-11-25,
    ///     docs/research/atlas-navigator.md §6). map_control keeps the selected node; once it settles, its name,
    ///     state, contents and links are said (<see cref="UiSemantics.AtlasNodeLine"/>), then where the player
    ///     stands: on opening, where the last session left off and the accessible frontier; per hop, where they
    ///     came from and the nearest landmark (nearest revealed neighbours for a row-less node, whose edges no
    ///     catalog holds). The scan behind the orientation lines is read once per opening
    ///     (<see cref="UiSemantics.AtlasScan"/>).
    /// </summary>
    public sealed class AtlasNarrator
    {
        /// <summary>The popup is built a few hundred ms after a hop lands: the line is composed no sooner than this.</summary>
        private const int SettleMs = 700;

        /// <summary>A popup still unbuilt this long after landing is composed without (catalogue name and words).</summary>
        private const int GiveUpMs = 1600;

        private readonly UiSemantics semantics;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private PassiveResume resume = new(null);
        private IntPtr node;
        private DateTime since;
        private bool settled;
        private bool opening = true;
        private IReadOnlyList<AtlasScanEntry>? scan;
        private AtlasScanEntry? opened;
        private AtlasScanEntry? previous;

        public AtlasNarrator(UiSemantics semantics, IAccessibilityOutputService output, PluginLog log)
        {
            this.semantics = semantics;
            this.output = output;
            this.log = log;
        }

        /// <summary>The character whose Atlas is open, for the resume file; set by the plugin.</summary>
        public Func<string>? CharacterName { get; set; }

        /// <summary>Where each character's last Atlas node is kept (config/atlas-resume.txt); null keeps it for this session.</summary>
        public string? ResumeFile
        {
            set => this.resume = new PassiveResume(value);
        }

        /// <param name="worldScreen">The shown world map, zero while none is (<see cref="UiSemantics.WorldScreen"/>).</param>
        public void Tick(IntPtr worldScreen)
        {
            if (worldScreen == IntPtr.Zero)
            {
                (this.node, this.settled, this.opening, this.scan, this.opened, this.previous) = (IntPtr.Zero, false, true, null, null, null);
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
            this.Orient(selected, first);
        }

        /// <summary>Where this landing sits: on opening the last session and the frontier; per hop the way back and an anchor.</summary>
        private void Orient(IntPtr selected, bool first)
        {
            this.scan ??= this.semantics.AtlasScan(selected, fresh: true);
            if (this.semantics.AtlasEntryOf(this.scan, selected) is not { } here)
            {
                return;
            }

            var extras = new List<string>();
            if (first)
            {
                this.opened = here;
                var character = this.CharacterName?.Invoke() ?? string.Empty;
                extras.Add(this.semantics.AtlasResumeLine(this.scan, here, character.Length > 0 ? this.resume.Get(character) : null));
                extras.Add(this.semantics.AtlasFrontierLine(this.scan, here));
            }
            else
            {
                if (this.previous is { Name.Length: > 0 } came && came.Element != here.Element)
                {
                    extras.Add(Announcements.AtlasCameFrom(came.Name));
                }

                extras.Add(this.semantics.AtlasAnchorLine(this.scan, here, this.opened));
                if (here.Id == null)
                {
                    extras.Add(this.semantics.AtlasNearestLine(this.scan, here));
                }
            }

            this.previous = here;
            if (this.CharacterName?.Invoke() is { Length: > 0 } who)
            {
                this.resume.Set(who, here.Key);
            }

            if (Announcements.Sentences(extras) is { Length: > 0 } lines)
            {
                this.log.Info($"Atlas orientation: {lines}");
                this.output.Speak(lines);
            }
        }
    }
}
