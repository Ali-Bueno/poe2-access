namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     The game's Build Planner on the passive tree for <see cref="UiSemantics"/>: the active build's nodes (marked on
    ///     their elements), the next one to take and the pushes there, and the build file's steps and notes
    ///     (docs/research/passive-tree.md).
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The build's marks are read again this often (a build chosen or cleared while the tree is open).</summary>
        private const int BuildMarksMs = 1000;

        /// <summary>The pushes counted toward the build's next node; beyond, only its direction is said.</summary>
        private const int MaxBuildPushes = 6;

        private readonly BuildPlanFolder buildPlans = new(BuildPlanFolder.Default);
        private (IntPtr State, long At, HashSet<IntPtr> Records)? buildMarks;

        /// <summary>The records of the active build's nodes (element byte, <see cref="GameLayout.PassiveTree.NodeInBuildOffset"/>), read again every second.</summary>
        private HashSet<IntPtr> BuildMarks(PassiveGraph graph)
        {
            var now = Environment.TickCount64;
            if (this.buildMarks is { } cached && cached.State == graph.State && now - cached.At < BuildMarksMs)
            {
                return cached.Records;
            }

            var shift = this.elements.Controls?.Current(ControlGroup.PassiveNode) ?? 0;
            var records = new HashSet<IntPtr>();
            foreach (var (record, element) in graph.Elements)
            {
                if (this.reader.TryRead<byte>(element + GameLayout.PassiveTree.NodeInBuildOffset + shift, out var mark) && mark == 1)
                {
                    records.Add(record);
                }
            }

            this.buildMarks = (graph.State, now, records);
            return records;
        }

        /// <summary>The build's step, weapon set and note for a marked node, from the build file.</summary>
        private IEnumerable<string> BuildLines(BuildPlan plan, IntPtr record, PassiveNodeRow node)
        {
            var id = this.PassiveIdOf(record);
            if (plan.Passives.FirstOrDefault(passive => passive.Id == id) is not { } passive)
            {
                yield break;
            }

            yield return Announcements.BuildStep(passive.Order + 1, plan.Passives.Count);
            if (passive.WeaponSet > 0)
            {
                yield return Announcements.BuildWeaponSet(passive.WeaponSet);
            }

            if (BuildNotes.Plain(passive.Note) is { Length: > 0 } note)
            {
                yield return note;
            }

            // A note that shows the attribute by colour only ("Attribute is recommended" in red), the game's convention.
            var words = Enum.GetValues<BuildNotes.Attribute>().SelectMany(attribute => new[] { attribute.ToString(), Announcements.AttributeName(attribute) });
            if (BuildNotes.IsAttributeNode(id) && BuildNotes.ColourOnly(passive.Note, words) is { } colour)
            {
                yield return Announcements.RecommendedAttribute(colour);
            }
        }

        /// <summary>
        ///     The build's next node to take: the file's first passive not yet allocated, or without a file the marked node
        ///     nearest the hovered one, those reachable now first; null when every marked node is allocated.
        /// </summary>
        private IntPtr? NextBuildNode(PassiveGraph graph, HashSet<IntPtr> marked, BuildPlan? plan, HashSet<int> allocated, IntPtr hovered)
        {
            var open = marked.Where(record => this.GraphIdOf(record) is not { } id || !allocated.Contains(id)).ToList();
            if (open.Count == 0)
            {
                return null;
            }

            if (plan != null)
            {
                var order = plan.Passives.GroupBy(passive => passive.Id).ToDictionary(group => group.Key, group => group.First().Order);
                return open.OrderBy(record => order.TryGetValue(this.PassiveIdOf(record), out var step) ? step : int.MaxValue).First();
            }

            var places = this.PlacesOf(graph);
            float DistanceFromHovered(IntPtr record) =>
                places.Index.TryGetValue(record, out var at) && places.Index.TryGetValue(hovered, out var here) ? Vector2.Distance(places.Positions[at], places.Positions[here]) : float.MaxValue;
            return open.OrderBy(record => this.PassiveRow(record) is { } row && this.StateOf(graph, this.ReadPassiveRow(record, row), allocated) == PassiveState.Reachable ? 0 : 1)
                .ThenBy(DistanceFromHovered).First();
        }

        /// <summary>"Next in the build: Attribute, reachable: up right, up" — the pushes the stick needs, or its direction when far.</summary>
        private string DescribeBuildNext(PassiveGraph graph, IntPtr from, IntPtr next, HashSet<int> allocated)
        {
            var node = this.PassiveRow(next) is { } row ? this.ReadPassiveRow(next, row) : null;
            if (node == null)
            {
                return string.Empty;
            }

            var named = Announcements.PassiveLink(node.Name, StateWord(this.StateOf(graph, node, allocated)));
            var places = this.PlacesOf(graph);
            if (!places.Index.TryGetValue(from, out var here) || !places.Index.TryGetValue(next, out var there))
            {
                return Announcements.BuildNext(named, Array.Empty<string>());
            }

            var pushes = PassiveStick.Route(places.Positions, here, there, MaxBuildPushes);
            return pushes != null
                ? Announcements.BuildNext(named, pushes.Select(octant => Announcements.StickDirections[octant]))
                : Announcements.BuildNext(named, new[] { Announcements.Far(Announcements.StickDirections[PassiveStick.Nearest(places.Positions[there] - places.Positions[here])]) });
        }
    }
}
