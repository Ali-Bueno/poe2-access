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

        private (IntPtr Canvas, IReadOnlyList<AtlasScanEntry> Entries)? atlasScan;

        /// <summary>
        ///     Every node element on the selected node's canvas, read once per open (fresh) and kept while the
        ///     canvas stands: identity where the catalog knows it, and grid, status and rolled area for every node.
        /// </summary>
        public IReadOnlyList<AtlasScanEntry> AtlasScan(IntPtr selected, bool fresh)
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
            if (!fresh && this.atlasScan is { } cached && cached.Canvas == canvas.Address)
            {
                return cached.Entries;
            }

            var language = Localization.Language;
            if (!this.endgameRecordByClass.ContainsKey(nodeClass))
            {
                // The selected node may be row-less, with no record to learn from: learn the class's offset from
                // any catalogued sibling instead (a scan off a row-less landing otherwise ids nothing at all).
                var tried = 0;
                foreach (var child in this.elements.ReadChildren(canvas))
                {
                    if (tried++ >= 48 || this.endgameRecordByClass.ContainsKey(nodeClass))
                    {
                        break;
                    }

                    if (this.reader.TryRead<IntPtr>(child, out var itsClass) && itsClass == nodeClass)
                    {
                        _ = this.EndgamePassiveId(child);
                    }
                }
            }

            var offset = this.endgameRecordByClass.TryGetValue(nodeClass, out var learnt) ? learnt : -1;
            var entries = new List<AtlasScanEntry>();
            foreach (var child in this.elements.ReadChildren(canvas))
            {
                if (entries.Count >= MaxAtlasScan || !this.elements.TryReadElement(child, out var childInfo) ||
                    !this.reader.TryRead<IntPtr>(child, out var itsClass) || itsClass != nodeClass)
                {
                    continue;
                }

                var bytes = this.reader.ReadBytes(child + GameLayout.AtlasNode.GridOffset, 0x20);
                if (bytes.Length != 0x20)
                {
                    continue;
                }

                var id = offset >= 0 ? this.EndgameIdAt(child, offset) : null;
                var revealed = PluginUiElementReflection.TryGetAbsoluteRect(child, out var position, out var size);
                entries.Add(new AtlasScanEntry
                {
                    Element = child,
                    Id = id,
                    GridX = BitConverter.ToInt32(bytes, 0),
                    GridY = BitConverter.ToInt32(bytes, 4),
                    State = AtlasState.Of(bytes[GameLayout.AtlasNode.StatusOffset - GameLayout.AtlasNode.GridOffset]),
                    Name = id != null ? this.EndgameNodeLabel(id, child, language) : this.AtlasAreaName(child, language),
                    Revealed = revealed,
                    Centre = revealed ? position + (size / 2) : null,
                });
            }

            this.atlasScan = (canvas.Address, entries);
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
            foreach (var entry in scan.Where(entry => !entry.Revealed && (entry.Element == here.Element || (entry.Id != null && linked != null && linked.Contains(entry.Id)))))
            {
                if (PluginUiElementReflection.TryGetAbsoluteRect(entry.Element, out var position, out var size))
                {
                    entry.Revealed = true;
                    entry.Centre = position + (size / 2);
                }
            }
        }

        /// <summary>
        ///     The frontier from here: the nearest accessible, uncompleted nodes in links (grid steps when here is
        ///     row-less), and how many revealed ones there are in all. Empty when none is in reach of speech.
        /// </summary>
        public string AtlasFrontierLine(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here)
        {
            var open = scan.Where(entry => entry.Revealed && entry.State == AtlasNodeState.Accessible && entry.Element != here.Element).ToList();
            if (open.Count == 0)
            {
                return string.Empty;
            }

            var distances = here.Id is { } from ? AtlasGraph.Distances(from, id => EndgameCatalog.Shared.Node(id)?.Links) : null;
            var named = open
                .Select(entry => (Entry: entry, Away: distances != null && entry.Id != null && distances.TryGetValue(entry.Id, out var links) ? links : AtlasGraph.GridSteps(here.GridX, here.GridY, entry.GridX, entry.GridY), Linked: distances != null && entry.Id != null && distances.ContainsKey(entry.Id)))
                .Where(part => part.Away > 0 && part.Entry.Name.Length > 0)
                .OrderBy(part => part.Away).Take(3)
                .Select(part => Announcements.AtlasFrontierEntry(part.Entry.Name, part.Away, this.AtlasSide(here, part.Entry), part.Linked));
            return Announcements.AtlasFrontier(string.Join("; ", named), open.Count);
        }

        /// <summary>A place to steer by: a tower those around it are in reach of, a gate, Kingsmarch, a waypoint pin.</summary>
        public bool AtlasIsLandmark(AtlasScanEntry entry)
        {
            var catalog = EndgameCatalog.Shared;
            return entry.Id is { } id && (catalog.TowerReach(id) > 0 || LandmarkPins.Contains(catalog.Node(id)?.Pin ?? string.Empty));
        }

        /// <summary>
        ///     The nearest landmark to steer by: where the Atlas was opened, a tower, a gate, Kingsmarch — in links
        ///     when both ends are catalogued, else in grid steps. Empty when none is revealed or here is one itself.
        /// </summary>
        public string AtlasAnchorLine(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here, AtlasScanEntry? opened)
        {
            bool IsLandmark(AtlasScanEntry entry) => entry.Element == opened?.Element || this.AtlasIsLandmark(entry);
            var distances = here.Id is { } from ? AtlasGraph.Distances(from, id => EndgameCatalog.Shared.Node(id)?.Links) : null;
            var best = scan
                .Where(entry => entry.Revealed && entry.Element != here.Element && entry.Name.Length > 0 && IsLandmark(entry))
                .Select(entry => (Entry: entry, Away: distances != null && entry.Id != null && distances.TryGetValue(entry.Id, out var links) ? links : AtlasGraph.GridSteps(here.GridX, here.GridY, entry.GridX, entry.GridY), Linked: distances != null && entry.Id != null && distances.ContainsKey(entry.Id)))
                .Where(part => part.Away > 0)
                .OrderBy(part => part.Away).FirstOrDefault();
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

            var distances = here.Id != null && last.Id != null ? AtlasGraph.Distances(here.Id, id => EndgameCatalog.Shared.Node(id)?.Links) : null;
            var away = distances != null && last.Id != null && distances.TryGetValue(last.Id, out var links)
                ? Announcements.AtlasAway(links, true, last.Name)
                : Announcements.AtlasAway(AtlasGraph.GridSteps(here.GridX, here.GridY, last.GridX, last.GridY), false, last.Name);
            return Announcements.AtlasLastTime(last.Name, away);
        }

        /// <summary>
        ///     The nearest revealed nodes by direction around a row-less landing, whose edges no catalog holds:
        ///     spoken as nearest, never as links.
        /// </summary>
        public string AtlasNearestLine(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here)
        {
            var nearest = AtlasGraph.NearestPerDirection(
                here.Centre ?? AtlasGraph.ScreenPlace(here.GridX, here.GridY),
                scan.Where(entry => entry.Revealed && entry.Element != here.Element && entry.Name.Length > 0)
                    .Select(entry => (entry.Centre ?? AtlasGraph.ScreenPlace(entry.GridX, entry.GridY), entry)));
            if (nearest.Count == 0)
            {
                return string.Empty;
            }

            var parts = AtlasLinks.Describe(
                here.Centre ?? AtlasGraph.ScreenPlace(here.GridX, here.GridY),
                nearest.Take(3).Select(entry => ((Vector2?)(entry.Centre ?? AtlasGraph.ScreenPlace(entry.GridX, entry.GridY)), entry.Name)));
            return Announcements.AtlasNearest(string.Join("; ", parts));
        }

        /// <summary>The side one scanned node lies on from another: drawn centres when both are drawn, else grid places.</summary>
        private string AtlasSide(AtlasScanEntry here, AtlasScanEntry there) =>
            here.Centre is { } from && there.Centre is { } to
                ? AtlasLinks.Side(from, to)
                : AtlasLinks.Side(AtlasGraph.ScreenPlace(here.GridX, here.GridY), AtlasGraph.ScreenPlace(there.GridX, there.GridY));
    }
}
