namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The passive tree finder's research report for <see cref="UiSemantics"/>, kept apart from the finder itself.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Research (dev command "passivemap"): from each class's start, how many points the nearest keystones and notables cost.</summary>
        public IEnumerable<string> PassiveMapReport(IntPtr hovered)
        {
            if (!this.elements.TryReadElement(hovered, out var info) || !this.elements.TryReadElement(info.Raw.ParentPtr, out var group) ||
                this.PassiveGraphOf(this.TreeCanvasOf(group.Raw.ParentPtr), info) is not { } graph)
            {
                yield break;
            }

            var atlas = this.AtlasOf(graph);
            yield return $"{atlas.Nodes.Length} drawn nodes, rings by node: {string.Join(" ", atlas.Nodes.Select((_, index) => PassiveMap.Zone(atlas.Places.Positions, index).Ring).GroupBy(ring => ring).OrderBy(ring => ring.Key).Select(ring => $"{ring.Key}={ring.Count()}"))}";
            foreach (var start in graph.Links.Keys)
            {
                if (this.PassiveRow(start) is not { } row || !this.ReadPassiveRow(start, row).IsStart)
                {
                    continue;
                }

                var steps = PassiveMap.Steps(atlas.LinksWithStart(graph, start), new[] { atlas.Nodes.Length });
                string Nearest(PassiveClass kind) => string.Join(", ", atlas.Nodes.Select((node, index) => (node, Steps: steps[index]))
                    .Where(entry => entry.node.Class == kind && entry.Steps > 0).OrderBy(entry => entry.Steps).Take(3).Select(entry => $"{entry.node.Name} {entry.Steps}"));
                yield return $"{this.StartClasses(row)}: keystones {Nearest(PassiveClass.Keystone)}; notables {Nearest(PassiveClass.Notable)}; " +
                             $"keystones within 20/30/40 points: {string.Join("/", new[] { 20, 30, 40 }.Select(limit => atlas.Nodes.Where((node, index) => node.Class == PassiveClass.Keystone && steps[index] > 0 && steps[index] <= limit).Count()))}";
            }
        }
    }
}
