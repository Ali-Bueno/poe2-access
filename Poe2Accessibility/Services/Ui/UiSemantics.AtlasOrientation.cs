namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.Plugin;

    /// <summary>One node of the Atlas canvas as a scan reads it: identity when catalogued, always its grid, state and rolled area.</summary>
    public sealed class AtlasScanEntry
    {
        public required IntPtr Element { get; init; }

        public required string? Id { get; init; }

        public required int GridX { get; init; }

        public required int GridY { get; init; }

        public required AtlasNodeState State { get; init; }

        /// <summary>The status byte as read, for <see cref="AtlasState.Plausible"/>.</summary>
        public byte Status { get; init; }

        public required string Name { get; init; }

        /// <summary>Drawn when scanned (fog has no rect); refreshed per landing as fog lifts.</summary>
        public required bool Revealed { get; set; }

        public Vector2? Centre { get; set; }

        /// <summary>The key the resume file keeps: the catalog id, or the grid place for a row-less node.</summary>
        public string Key => this.Id ?? $"grid:{this.GridX},{this.GridY}";
    }

    /// <summary>
    ///     Orientation on the Atlas map for <see cref="AtlasNarrator"/> (atlas-navigator Phase 2): a scan of every
    ///     node the canvas holds — catalogued and row-less alike — and the lines built on it: the accessible
    ///     frontier in links, the nearest landmark, where the last session left off, and nearest neighbours for a
    ///     row-less landing, whose edges no catalog holds.
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Candidate elements read in one canvas scan (the canvas holds about a thousand nodes).</summary>
        private const int MaxAtlasScan = 2500;

        private static readonly string[] LandmarkPins = { "Kingsmarch", "InitialTower", "PrecursorGate", "WaypointPin" };


        /// <summary>
        ///     Every node element on the selected node's canvas, read now: identity where the catalog knows it,
        ///     and grid, status and rolled area for every node. The caller holds the scan for one opening and
        ///     lets it go when the selection does; nothing caches element addresses here across canvases.
        /// </summary>
        public IReadOnlyList<AtlasScanEntry> AtlasScan(IntPtr selected)
        {
            if (!this.elements.TryReadElement(selected, out var info) || !this.reader.TryRead<IntPtr>(selected, out var nodeClass))
            {
                return Array.Empty<AtlasScanEntry>();
            }

            var ancestors = this.elements.ReadAncestors(info, 1);
            if (ancestors.Count == 0)
            {
                return Array.Empty<AtlasScanEntry>();
            }

            var canvas = ancestors[0];

            var language = Localization.Language;
            if (!this.endgameRecordByClass.ContainsKey(nodeClass))
            {
                // The selected node may be row-less, with no record to learn from: learn the class's offset from
                // any catalogued sibling instead (a scan off a row-less landing otherwise ids nothing at all).
                var tried = 0;
                foreach (var child in this.elements.ReadChildren(canvas))
                {
                    if (this.endgameRecordByClass.ContainsKey(nodeClass))
                    {
                        break;
                    }

                    // Only same-class children can teach the offset; the budget counts those attempts alone.
                    if (this.reader.TryRead<IntPtr>(child, out var itsClass) && itsClass == nodeClass && tried++ < 48)
                    {
                        _ = this.EndgamePassiveId(child);
                    }
                }
            }

            var offset = this.endgameRecordByClass.TryGetValue(nodeClass, out var learnt) ? learnt : -1;

            // The keys first, on a few nodes: the grid and the status are read as far from where measured as the keys are.
            foreach (var child in this.elements.ReadChildren(canvas).Where(child => this.reader.TryRead<IntPtr>(child, out var itsClass) && itsClass == nodeClass).Take(AtlasNodeFields.NodesTaught))
            {
                AtlasNodeFields.Learn(this.reader, this.elements, child);
            }

            var shift = AtlasNodeFields.Shift;
            var entries = new List<AtlasScanEntry>();
            foreach (var child in this.elements.ReadChildren(canvas))
            {
                if (entries.Count >= MaxAtlasScan || !this.elements.TryReadElement(child, out var childInfo) ||
                    !this.reader.TryRead<IntPtr>(child, out var itsClass) || itsClass != nodeClass)
                {
                    continue;
                }

                if (AtlasNodeFields.Read(this.reader, child, shift) is not { } fields)
                {
                    continue;
                }

                var id = offset >= 0 ? this.EndgameIdAt(child, offset) : null;
                var revealed = PluginUiElementReflection.TryGetAbsoluteRect(child, out var position, out var size);
                var status = fields.Status;
                entries.Add(new AtlasScanEntry
                {
                    Element = child,
                    Id = id,
                    GridX = fields.X,
                    GridY = fields.Y,
                    State = AtlasState.Of(status),
                    Status = status,
                    Name = id != null ? this.EndgameNodeLabel(id, child, language) : this.AtlasAreaName(child, language),
                    Revealed = revealed,
                    Centre = revealed ? position + (size / 2) : null,
                });
            }

            return entries;
        }

        /// <summary>The scan entry of an element, when a scan holds it.</summary>
        public AtlasScanEntry? AtlasEntryOf(IReadOnlyList<AtlasScanEntry> scan, IntPtr element) =>
            scan.FirstOrDefault(entry => entry.Element == element);

        /// <summary>
        ///     Re-reads whether the landed node and its linked neighbours are drawn now: fog lifts as the owner walks,
        ///     and a scan taken at opening would call just-revealed neighbours unrevealed for the rest of the session
        ///     (live 2026-11-25). A handful of rect reads per landing keeps the scan honest where it is spoken from.
        /// </summary>
        public void AtlasRefresh(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here)
        {
            var linked = here.Id != null ? EndgameCatalog.Shared.Node(here.Id)?.Links : null;
            foreach (var entry in scan.Where(entry => entry.Element == here.Element || (entry.Id != null && linked != null && linked.Contains(entry.Id))))
            {
                // Centres of the landed node and its links are re-read even when already revealed: pan and
                // zoom between the scan and this landing would otherwise mix two drawn snapshots in one side.
                if (PluginUiElementReflection.TryGetAbsoluteRect(entry.Element, out var position, out var size))
                {
                    entry.Revealed = true;
                    entry.Centre = position + (size / 2);
                }
            }
        }

        /// <summary>
        ///     Link distances from here through revealed nodes only, as the destination finder's ways count
        ///     them: a spoken "N links" must be a way the cursor can actually be hopped, never one routed
        ///     through fog. Null when here is row-less.
        /// </summary>
        private Dictionary<string, int>? AtlasRevealedDistances(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here)
        {
            if (here.Id is not { } from)
            {
                return null;
            }

            var revealed = scan.Where(entry => entry.Revealed && entry.Id != null).Select(entry => entry.Id!).ToHashSet(StringComparer.Ordinal);
            return AtlasGraph.Distances(from, id => EndgameCatalog.Shared.Node(id)?.Links, id => id == from || revealed.Contains(id));
        }

        /// <summary>
        ///     The frontier from here: the nearest accessible, uncompleted nodes in revealed links (grid steps
        ///     for an entry no revealed way reaches), and how many revealed ones there are in all. Links are never
        ///     ordered against grid steps as one unit. Empty when none is in reach of speech.
        /// </summary>
        public string AtlasFrontierLine(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here)
        {
            var open = scan.Where(entry => entry.Revealed && entry.State == AtlasNodeState.Accessible && entry.Element != here.Element).ToList();
            if (open.Count == 0)
            {
                return string.Empty;
            }

            var named = this.AtlasNearestFirst(scan, here, open.Where(entry => entry.Name.Length > 0))
                .Take(3)
                .Select(part => Announcements.AtlasFrontierEntry(part.Entry.Name, part.Away, this.AtlasSide(here, part.Entry), part.Linked));
            return Announcements.AtlasFrontier(string.Join("; ", named), open.Count);
        }

        /// <summary>
        ///     Entries away from here, nearest first: those a revealed link way joins (in links) before the rest (in grid
        ///     steps), ties by the distance drawn, then the name, then the key.
        /// </summary>
        private IEnumerable<(AtlasScanEntry Entry, int Away, bool Linked)> AtlasNearestFirst(
            IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here, IEnumerable<AtlasScanEntry> among)
        {
            var distances = this.AtlasRevealedDistances(scan, here);
            return among
                .Select(entry => (Entry: entry,
                    Away: distances != null && entry.Id != null && distances.TryGetValue(entry.Id, out var links)
                        ? links
                        : AtlasGraph.GridSteps(here.GridX, here.GridY, entry.GridX, entry.GridY),
                    Linked: distances != null && entry.Id != null && distances.ContainsKey(entry.Id)))
                .Where(part => part.Away > 0)
                .OrderBy(part => !part.Linked)
                .ThenBy(part => part.Away)
                .ThenBy(part => this.AtlasDistance(here, part.Entry))
                .ThenBy(part => part.Entry.Name, StringComparer.CurrentCulture)
                .ThenBy(part => part.Entry.Key, StringComparer.Ordinal);
        }

        /// <summary>
        ///     A place to steer by: a tower those around it are in reach of, a gate, Kingsmarch, a waypoint pin — and any
        ///     pin few nodes show, so a pin a patch adds or renames still counts.
        /// </summary>
        public bool AtlasIsLandmark(AtlasScanEntry entry)
        {
            var catalog = EndgameCatalog.Shared;
            return entry.Id is { } id && (catalog.TowerReach(id) > 0 || LandmarkPins.Contains(catalog.Node(id)?.Pin ?? string.Empty) || catalog.HasRarePin(id));
        }

        /// <summary>
        ///     The nearest landmark to steer by: where the Atlas was opened, a tower, a gate, Kingsmarch — in links
        ///     when both ends are catalogued, else in grid steps. Empty when none is revealed or here is one itself.
        /// </summary>
        public string AtlasAnchorLine(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here, AtlasScanEntry? opened)
        {
            bool IsLandmark(AtlasScanEntry entry) => entry.Element == opened?.Element || this.AtlasIsLandmark(entry);
            if (IsLandmark(here))
            {
                return string.Empty;
            }

            var landmarks = scan.Where(entry => entry.Revealed && entry.Element != here.Element && entry.Name.Length > 0 && IsLandmark(entry));
            var best = this.AtlasNearestFirst(scan, here, landmarks).FirstOrDefault();
            return best.Entry == null ? string.Empty : Announcements.AtlasAway(best.Away, best.Linked, best.Entry.Name);
        }

        /// <summary>Where the last session left off, when that node is on this scan and is not here.</summary>
        public string AtlasResumeLine(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here, string? resumeKey)
        {
            var last = resumeKey == null ? null : scan.FirstOrDefault(entry => entry.Key == resumeKey);
            if (last == null || last.Element == here.Element || last.Name.Length == 0)
            {
                return string.Empty;
            }

            var distances = last.Id != null ? this.AtlasRevealedDistances(scan, here) : null;
            var linked = distances != null && last.Id != null && distances.TryGetValue(last.Id, out var links) ? links : -1;
            var steps = AtlasGraph.GridSteps(here.GridX, here.GridY, last.GridX, last.GridY);
            if ((linked >= 0 ? linked : steps) == 0)
            {
                return string.Empty;
            }

            var away = linked >= 0 ? Announcements.AtlasLinksAway(linked) : Announcements.AtlasStepsAway(steps);
            return Announcements.AtlasLastTime(last.Name, away);
        }

        /// <summary>
        ///     The nearest revealed nodes by direction around a row-less landing, whose edges no catalog holds:
        ///     spoken as nearest, never as links.
        /// </summary>
        public string AtlasNearestLine(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here)
        {
            // One frame for origin and candidates alike (AtlasPlaces' rule: mixing sets would invent a side):
            // drawn centres when the landing itself draws, grid places for every entry when it does not.
            var drawn = here.Centre != null;
            Vector2 Place(AtlasScanEntry entry) => drawn ? entry.Centre!.Value : AtlasGraph.ScreenPlace(entry.GridX, entry.GridY);
            var origin = Place(here);
            var nearest = AtlasGraph.NearestPerDirection(
                origin,
                scan.Where(entry => entry.Revealed && entry.Element != here.Element && entry.Name.Length > 0 && (!drawn || entry.Centre != null))
                    .Select(entry => (Place(entry), entry)));
            if (nearest.Count == 0)
            {
                return string.Empty;
            }

            var parts = AtlasLinks.Describe(
                origin,
                nearest.Take(3).Select(entry => ((Vector2?)Place(entry), entry.Name)));
            return Announcements.AtlasNearest(string.Join("; ", parts));
        }

        /// <summary>The side one scanned node lies on from another, in AtlasPlaces' single set of units.</summary>
        private string AtlasSide(AtlasScanEntry here, AtlasScanEntry there)
        {
            var (from, to) = this.AtlasPlaces(here, there);
            return AtlasLinks.Side(from, to);
        }
    }
}
