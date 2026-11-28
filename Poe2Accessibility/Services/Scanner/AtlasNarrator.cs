namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    /// <summary>
    ///     The Atlas node the cursor rests on, told with its state first, and where it sits (atlas-navigator
    ///     Phases 1–3): on the Atlas map neither the focus nor the hovered slot ever points at a node, so the
    ///     endgame reader never fired and only the popup's flavour was heard (live 2026-11-25,
    ///     docs/research/atlas-navigator.md §6). map_control keeps the selected node; once it settles, its name,
    ///     state, contents and links are said (<see cref="UiSemantics.AtlasNodeLine"/>), then where the player
    ///     stands: on opening, where the last session left off and the accessible frontier; per hop, where they
    ///     came from and the nearest landmark (nearest revealed neighbours for a row-less node, whose edges no
    ///     catalog holds). The destination finder chooses from the same opening scan and replaces the anchor
    ///     with the next push and how many links remain. The scan is read once per opening
    ///     (<see cref="UiSemantics.AtlasScan"/>).
    /// </summary>
    public sealed class AtlasNarrator
    {
        /// <summary>Names each list of the opening's changes says before counting the rest, as the frontier's.</summary>
        private const int NamedChanges = 3;

        /// <summary>The popup is built a few hundred ms after a hop lands: the line is composed no sooner than this.</summary>
        private const int SettleMs = 700;

        /// <summary>A popup still unbuilt this long after landing is composed without (catalogue name and words).</summary>
        private const int GiveUpMs = 1600;

        private readonly UiSemantics semantics;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly AtlasDestinationFinder destinations = new();
        private PassiveResume resume = new(null);
        private AtlasProgress progress = new(null);
        private IntPtr node;
        private DateTime since;
        private bool settled;
        private DateTime nextLineAttempt;
        private bool opening = true;

        /// <summary>The opening found no character name: the changes since last time are worked out at the first landing that has one.</summary>
        private bool progressPending;
        private IReadOnlyList<AtlasScanEntry>? scan;
        private AtlasScanEntry? opened;
        private AtlasScanEntry? previous;
        private string destinationName = string.Empty;

        public AtlasNarrator(UiSemantics semantics, IAccessibilityOutputService output, PluginLog log)
        {
            this.semantics = semantics;
            this.output = output;
            this.log = log;
        }

        /// <summary>Say the landed node and its orientation; off leaves a chosen destination's guide.</summary>
        public bool AnnounceNodes { get; set; } = true;

        /// <summary>The Atlas destination finder answers on the shoulder buttons and guides each landing.</summary>
        public bool FinderOn { get; set; } = true;

        /// <summary>The character whose Atlas is open, for the resume file; set by the plugin.</summary>
        public Func<string>? CharacterName { get; set; }

        /// <summary>Where each character's last Atlas node is kept (config/atlas-resume.txt); null keeps it for this session.</summary>
        public string? ResumeFile
        {
            set => this.resume = new PassiveResume(value);
        }

        /// <summary>Where each character's node states at the last opening are kept (config/atlas-progress.txt); null keeps them for this session.</summary>
        public string? ProgressFile
        {
            set => this.progress = new AtlasProgress(value);
        }

        /// <summary>
        ///     One finder move on the Atlas map: a list or entry step, answered with the destination and its way;
        ///     Position says where the cursor stands and lets any destination go. Null when no Atlas node is selected.
        /// </summary>
        public string? DestinationMove(IntPtr worldScreen, int listStep, int entryStep)
        {
            if (!this.FinderOn || !this.Current(worldScreen, out var scan, out var here))
            {
                return null;
            }

            IReadOnlyList<string> Members(AtlasDestinationCategory category) => this.semantics.AtlasDestinationEntries(scan, here, category);
            var hadDestination = this.destinations.Target.Length > 0;
            if (listStep != 0)
            {
                this.destinations.ChangeCategory(listStep, Members);
            }
            else
            {
                this.destinations.ChangeEntry(entryStep, Members(this.destinations.Category));
            }

            var target = scan.FirstOrDefault(entry => entry.Key == this.destinations.Target);
            if (this.destinations.Category == AtlasDestinationCategory.Position)
            {
                this.destinationName = string.Empty;
                var position = this.semantics.AtlasDestinationPosition(scan, here, this.opened);
                return hadDestination && this.destinations.Target.Length == 0 ? $"{position}. {Announcements.BeaconOff}" : position;
            }

            if (target == null)
            {
                // The destination left the scan (an emptied list keeps the old target): say the guide is off
                // now, as Guidance does on a landing, not on some unrelated hop later.
                this.destinations.Drop();
                this.destinationName = string.Empty;
                var position = this.semantics.AtlasDestinationPosition(scan, here, this.opened);
                return hadDestination ? $"{position}. {Announcements.BeaconOff}" : position;
            }

            this.destinationName = target.Name;
            var found = this.semantics.AtlasDestinationFound(scan, here, target);
            this.log.Info($"Atlas destination: {found}");
            return listStep == 0
                ? found
                : Announcements.InContext(Announcements.AtlasDestinationCategoryName(this.destinations.Category), found);
        }

        /// <summary>Drops a chosen destination, saying so only when there was one.</summary>
        public string? DropDestination()
        {
            if (!this.FinderOn || this.destinations.Target.Length == 0)
            {
                return null;
            }

            this.destinations.Drop();
            this.destinationName = string.Empty;
            return Announcements.BeaconOff;
        }

        /// <summary>A scan just read, with its nodes' fields judged: a game update that moved them is reported, not believed in silence.</summary>
        private IReadOnlyList<AtlasScanEntry> Judged(IReadOnlyList<AtlasScanEntry> scan)
        {
            var catalogued = scan.Where(entry => entry.Id != null).Select(entry => (entry.GridX, entry.GridY, entry.Status)).ToList();
            var census = $"{scan.Count} nodes, {AtlasState.Census(catalogued)}";
            if (census != this.lastCensus)
            {
                this.lastCensus = census;
                this.log.Info($"Atlas scan: {census}");
            }

            if (AtlasState.Plausible(catalogued) is { } plausible)
            {
                if (plausible)
                {
                    CompatibilityReport.Current?.Found(AtlasState.NodesPart);
                }
                else
                {
                    CompatibilityReport.Current?.Missing(AtlasState.NodesPart);
                }
            }

            return scan;
        }

        private string lastCensus = string.Empty;

        /// <summary>
        ///     The scan's entry for the selected node. A held scan that misses it is read again once — the game
        ///     may have rebuilt the canvas under the same world screen (a tab switch keeps the screen, drops the
        ///     selection) — and a scan that still misses is let go, never kept to name a reused address.
        /// </summary>
        private AtlasScanEntry? ScanEntry(IntPtr selected)
        {
            var justScanned = this.scan == null;
            this.scan ??= this.Judged(this.semantics.AtlasScan(selected));
            var found = this.semantics.AtlasEntryOf(this.scan, selected);
            if (found == null && !justScanned)
            {
                this.scan = this.Judged(this.semantics.AtlasScan(selected));
                found = this.semantics.AtlasEntryOf(this.scan, selected);
            }

            if (found == null)
            {
                this.scan = null;
            }

            return found;
        }

        /// <summary>The selected node and a scan holding it, read now when a finder press precedes the opening landing.</summary>
        private bool Current(IntPtr worldScreen, out IReadOnlyList<AtlasScanEntry> scan, out AtlasScanEntry here)
        {
            scan = Array.Empty<AtlasScanEntry>();
            here = null!;
            if (worldScreen == IntPtr.Zero || this.semantics.SelectedAtlasNode(worldScreen) is not { } selected)
            {
                return false;
            }

            if (this.ScanEntry(selected) is not { } found)
            {
                return false;
            }

            this.semantics.AtlasRefresh(this.scan!, found);
            scan = this.scan!;
            here = found;
            return true;
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
                if (selected == IntPtr.Zero)
                {
                    // The selection went while the screen stands (an act-map tab): the scan's element addresses
                    // belong to a canvas the game may rebuild, so the opening starts over with the next node.
                    (this.scan, this.opened, this.previous, this.opening) = (null, null, null, true);
                }

                (this.node, this.since, this.settled, this.nextLineAttempt) = (selected, now, false, DateTime.MinValue);
                return;
            }

            if (this.settled || selected == IntPtr.Zero)
            {
                return;
            }

            // The popup walk behind a line is retried a few times a second, not every frame.
            var elapsed = (now - this.since).TotalMilliseconds;
            if (elapsed < SettleMs || now < this.nextLineAttempt)
            {
                return;
            }

            this.nextLineAttempt = now.AddMilliseconds(150);
            if (this.semantics.AtlasNodeLine(selected, allowPending: elapsed >= GiveUpMs) is not { } line)
            {
                return;
            }

            this.settled = true;
            var guiding = this.FinderOn && this.destinations.Target.Length > 0;
            if (line.Length == 0 && !guiding)
            {
                return;
            }
            // Queued behind the map's own words: the popup's flavour, objective and blocked text are the
            // screen reading's and finish first, as a tree node's details come before its links (the owner,
            // 2026-11-25: the state-and-links line cut the map's speech after its name). A further hop
            // interrupts as before and drops this still-waiting line with the node it belonged to.
            var first = this.opening;
            this.opening = false;
            if (this.AnnounceNodes && line.Length > 0)
            {
                this.log.Info($"Atlas node landed: {line}");
                this.output.Speak(line);
            }

            this.Orient(selected, first);
        }

        private AtlasProgressChange? UpdateProgress(string character, IReadOnlyList<AtlasScanEntry> scan) =>
            this.progress.Update(character, scan.Select(entry => (entry.GridX, entry.GridY, entry.State, entry.Name)), NamedChanges);

        /// <summary>Where this landing sits: on opening the last session and the frontier; per hop the way back and an anchor or the guide.</summary>
        private void Orient(IntPtr selected, bool first)
        {
            if (this.ScanEntry(selected) is not { } here)
            {
                // Nothing to orient by this landing; an opening missed this way is not spent.
                this.opening = first;
                return;
            }

            var scan = this.scan!;
            this.semantics.AtlasRefresh(scan, here);

            var extras = new List<string>();
            if (first)
            {
                this.opened = here;
                var character = this.CharacterName?.Invoke() ?? string.Empty;
                var change = character.Length > 0 ? this.UpdateProgress(character, scan) : null;
                this.progressPending = character.Length == 0;
                if (this.AnnounceNodes)
                {
                    if (change is { IsEmpty: false })
                    {
                        extras.Add(Announcements.AtlasSinceLastTime(change));
                    }

                    extras.Add(this.semantics.AtlasResumeLine(scan, here, character.Length > 0 ? this.resume.Get(character) : null));
                    extras.Add(this.semantics.AtlasFrontierLine(scan, here));
                }
            }
            else if (this.AnnounceNodes && this.previous is { Name.Length: > 0 } came && came.Element != here.Element)
            {
                extras.Add(Announcements.AtlasCameFrom(came.Name));
            }

            if (!first && this.progressPending && this.CharacterName?.Invoke() is { Length: > 0 } known)
            {
                this.progressPending = false;
                if (this.UpdateProgress(known, scan) is { IsEmpty: false } late && this.AnnounceNodes)
                {
                    extras.Add(Announcements.AtlasSinceLastTime(late));
                }
            }

            var guide = this.Guidance(scan, here);
            if (guide.Length > 0)
            {
                extras.Add(guide);
            }
            else if (this.AnnounceNodes && !first)
            {
                extras.Add(this.semantics.AtlasAnchorLine(scan, here, this.opened));
                if (here.Id == null)
                {
                    extras.Add(this.semantics.AtlasNearestLine(scan, here));
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

        /// <summary>The next push toward a chosen destination, its arrival, or the guide going off when it left the scan.</summary>
        private string Guidance(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here)
        {
            if (!this.FinderOn || this.destinations.Target.Length == 0)
            {
                return string.Empty;
            }

            if (this.destinations.Arrive(here.Key))
            {
                var arrived = Announcements.BeaconArrived(this.destinationName.Length > 0 ? this.destinationName : here.Name);
                this.destinationName = string.Empty;
                return arrived;
            }

            var target = scan.FirstOrDefault(entry => entry.Key == this.destinations.Target);
            if (target == null)
            {
                this.destinations.Drop();
                this.destinationName = string.Empty;
                return Announcements.BeaconOff;
            }

            this.destinationName = target.Name;
            return this.semantics.AtlasDestinationGuide(scan, here, target);
        }
    }
}
