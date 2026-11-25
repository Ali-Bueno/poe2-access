namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     The spoken Atlas destination lists and the way to the chosen one (atlas-navigator Phase 3). The
    ///     destinations are the revealed nodes of the narrator's scan; a catalogued way keeps to revealed links,
    ///     so the game can actually be hopped along it, and a row-less end (whose links no catalog holds) falls
    ///     back to its side and grid steps. Bookmarks are the game's own jumps and need no route.
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>A destination list's keys, nearest first: by a revealed link way where one joins them, else by grid steps.</summary>
        public IReadOnlyList<string> AtlasDestinationEntries(
            IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here, AtlasDestinationCategory category)
        {
            bool Holds(AtlasScanEntry entry) => category switch
            {
                AtlasDestinationCategory.Accessible => entry.State == AtlasNodeState.Accessible,
                AtlasDestinationCategory.Landmarks => this.AtlasIsLandmark(entry),
                AtlasDestinationCategory.Objectives => entry.Id is { } id && EndgameCatalog.Shared.Node(id)?.Objective is { Length: > 0 },
                _ => false,
            };
            var allowed = scan.Where(entry => entry.Revealed && entry.Id != null).Select(entry => entry.Id!).ToHashSet(StringComparer.Ordinal);
            var distances = here.Id is { } from
                ? AtlasGraph.Distances(from, id => EndgameCatalog.Shared.Node(id)?.Links, id => id == from || allowed.Contains(id))
                : null;
            return scan
                .Where(entry => entry.Revealed && entry.Element != here.Element && entry.Name.Length > 0 && Holds(entry))
                .Select(entry => (Entry: entry, Linked: distances != null && entry.Id != null && distances.ContainsKey(entry.Id),
                    Away: distances != null && entry.Id != null && distances.TryGetValue(entry.Id, out var links)
                        ? links
                        : AtlasGraph.GridSteps(here.GridX, here.GridY, entry.GridX, entry.GridY)))
                .Where(part => part.Away > 0)
                .OrderBy(part => !part.Linked)
                .ThenBy(part => part.Away)
                .ThenBy(part => this.AtlasDistance(here, part.Entry))
                .ThenBy(part => part.Entry.Name, StringComparer.CurrentCulture)
                .ThenBy(part => part.Entry.Key, StringComparer.Ordinal)
                .Select(part => part.Entry.Key)
                .ToList();
        }

        /// <summary>Where the cursor stands for the finder's home list: the landed node and its nearest anchor.</summary>
        public string AtlasDestinationPosition(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here, AtlasScanEntry? opened) =>
            Announcements.Sentences(new[]
            {
                Announcements.PassivePosition($"{here.Name}, {Announcements.AtlasState(here.State)}"),
                this.AtlasAnchorLine(scan, here, opened),
            });

        /// <summary>"Caldera, Accessible, Ritual, 4 links away. From here: down right, 4 pushes": a chosen destination.</summary>
        public string AtlasDestinationFound(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here, AtlasScanEntry target)
        {
            var catalog = EndgameCatalog.Shared;
            var node = target.Id != null ? catalog.Node(target.Id) : null;
            var parts = new List<string> { Announcements.AtlasState(target.State) };
            if (catalog.ObjectiveWord(node?.Objective, Localization.Language) is { Length: > 0 } objective)
            {
                parts.Add(objective);
            }

            if (target.Id != null && catalog.TowerReach(target.Id) is > 0 and var reach)
            {
                parts.Add(Announcements.AtlasTowerReaches(reach));
            }

            var way = this.AtlasDestinationWay(scan, here, target);
            parts.Add(way.Linked ? Announcements.AtlasLinksAway(way.Away) : Announcements.AtlasStepsAway(way.Away));
            return Announcements.PassiveFound(target.Name, string.Join(", ", parts), way.Words.Words(false));
        }

        /// <summary>
        ///     The way on at a landing while a destination leads ("Toward Caldera: down right, 3", the goal only
        ///     when it changes). A way with no link route says its side alone.
        /// </summary>
        public string AtlasDestinationGuide(IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here, AtlasScanEntry target)
        {
            var way = this.AtlasDestinationWay(scan, here, target);
            var spoken = way.Linked ? way.Words : new Way(way.Words.Octant, null, false);
            return this.GoalAndWay($"atlas {target.Key}", Announcements.PassiveToward(target.Name), spoken);
        }

        /// <summary>The next push toward a destination and how far it lies: links on a catalogued way, else grid steps.</summary>
        private AtlasWayInfo AtlasDestinationWay(
            IReadOnlyList<AtlasScanEntry> scan, AtlasScanEntry here, AtlasScanEntry target)
        {
            if (here.Id is { } from && target.Id is { } to)
            {
                var byId = new Dictionary<string, AtlasScanEntry>(StringComparer.Ordinal);
                foreach (var entry in scan.Where(entry => entry.Revealed && entry.Id != null))
                {
                    byId.TryAdd(entry.Id!, entry);
                }

                var route = AtlasGraph.Route(from, to, id => EndgameCatalog.Shared.Node(id)?.Links, id => id == from || byId.ContainsKey(id));
                if (route.Count > 1 && byId.TryGetValue(route[1], out var next))
                {
                    var (herePlace, nextPlace) = this.AtlasPlaces(here, next);
                    return new AtlasWayInfo(
                        new Way(PassiveStick.Nearest(nextPlace - herePlace), route.Count - 1, false),
                        route.Count - 1, true);
                }
            }

            var (fromPlace, toPlace) = this.AtlasPlaces(here, target);
            return new AtlasWayInfo(
                new Way(PassiveStick.Nearest(toPlace - fromPlace), null, false),
                AtlasGraph.GridSteps(here.GridX, here.GridY, target.GridX, target.GridY), false);
        }

        /// <summary>
        ///     Two scanned nodes' places in one set of units: drawn centres when both draw, else catalogue
        ///     places when both are known, else grid places. Mixing sets would invent a side.
        /// </summary>
        private (Vector2 From, Vector2 To) AtlasPlaces(AtlasScanEntry here, AtlasScanEntry there)
        {
            if (here.Centre is { } from && there.Centre is { } to)
            {
                return (from, to);
            }

            var catalog = EndgameCatalog.Shared;
            if (here.Id != null && there.Id != null && catalog.Node(here.Id) is { } first && catalog.Node(there.Id) is { } second)
            {
                return (new Vector2(first.X, first.Y), new Vector2(second.X, second.Y));
            }

            return (AtlasGraph.ScreenPlace(here.GridX, here.GridY), AtlasGraph.ScreenPlace(there.GridX, there.GridY));
        }

        private float AtlasDistance(AtlasScanEntry here, AtlasScanEntry there)
        {
            var (from, to) = this.AtlasPlaces(here, there);
            return Vector2.Distance(from, to);
        }

        private readonly record struct AtlasWayInfo(Way Words, int Away, bool Linked);
    }
}
