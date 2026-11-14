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

        private readonly BuildPlanFolder buildPlans = new(BuildPlanFolder.Default, BuildPlanFolder.DefaultConfig);
        private (IntPtr Canvas, long At, HashSet<IntPtr> Records)? buildMarks;
        private bool buildMarksUnreadable;
        private (IntPtr Canvas, bool Preview) classPreview;

        /// <summary>
        ///     A class's tree on the Altar of Ascendancy, shown before the class is taken (its canvas holds the card's
        ///     line about the class): the game marks no build on it, so the build's file says which nodes are its own.
        /// </summary>
        private bool IsClassPreview(PassiveGraph graph)
        {
            if (this.classPreview.Canvas != graph.Canvas)
            {
                this.classPreview = (graph.Canvas, this.elements.FindNamed(graph.Canvas, AscendancyChoice.AboutName, 1) != null);
            }

            return this.classPreview.Preview;
        }

        /// <summary>The build file behind the marks; on a class's preview the character's active one, which its unmarked tree must not undo.</summary>
        private BuildPlan? PlanOf(PassiveGraph graph, HashSet<IntPtr> marked) =>
            this.IsClassPreview(graph) ? this.buildPlans.Active(AskingCharacter())
            : this.buildMarksUnreadable ? null
            : this.buildPlans.Matching(AskingCharacter(), marked.Select(this.PassiveIdOf).ToHashSet());

        /// <summary>Gets or sets the log of which build file a character follows and why (set by the plugin).</summary>
        public Action<string>? BuildPlanLog
        {
            get => this.buildPlans.Log;
            set => this.buildPlans.Log = value;
        }

        /// <summary>The build marks' name in the layout report.</summary>
        private const string BuildMarksPart = "passive build marks";

        /// <summary>The planner's list names the active build itself, in the tree and in the gem engraving window.</summary>
        private void NoteActiveBuild(in UiElementReader.ElementInfo list)
        {
            if (list.Name is "BuildPlannerComboBox" or "build_planner_combobox" && this.comboBoxes.Read(list.Address).Selected is { Length: > 0 } name)
            {
                this.buildPlans.Shown(AskingCharacter(), name);
            }
        }

        /// <summary>The records of the active build's nodes (element byte, <see cref="GameLayout.PassiveTree.NodeInBuildOffset"/>), read again every second.</summary>
        private HashSet<IntPtr> BuildMarks(PassiveGraph graph)
        {
            var now = Environment.TickCount64;
            if (this.buildMarks is { } cached && cached.Canvas == graph.Canvas && now - cached.At < BuildMarksMs)
            {
                return cached.Records;
            }

            var records = new HashSet<IntPtr>();
            if (this.IsClassPreview(graph))
            {
                var ids = this.buildPlans.Active(AskingCharacter())?.Passives.Select(passive => passive.Id).ToHashSet() ?? new HashSet<string>();
                records.UnionWith(graph.Elements.Keys.Where(record => ids.Contains(this.PassiveIdOf(record))));
                this.buildMarks = (graph.Canvas, now, records);
                return records;
            }

            var shift = this.elements.Controls?.Current(ControlGroup.PassiveNode) ?? 0;
            foreach (var (record, element) in graph.Elements)
            {
                if (this.reader.TryRead<byte>(element + GameLayout.PassiveTree.NodeInBuildOffset + shift, out var mark) && mark == 1)
                {
                    records.Add(record);
                }
            }

            this.buildMarksUnreadable = !BuildPlan.MarksPlausible(records.Count, graph.Elements.Count);
            if (this.buildMarksUnreadable)
            {
                CompatibilityReport.Current?.Missing(BuildMarksPart);
                records.Clear();
            }

            this.buildMarks = (graph.Canvas, now, records);
            return records;
        }

        /// <summary>The plan's steps over the marked nodes: the step numbers and the Build list's count are of the same nodes.</summary>
        private BuildSteps BuildStepsOf(BuildPlan plan, HashSet<IntPtr> marked) => plan.StepsOver(marked.Select(this.PassiveIdOf).ToHashSet());

        /// <summary>The build's step, weapon set and note for a marked node, from the build file.</summary>
        private IEnumerable<string> BuildLines(BuildPlan plan, IntPtr record, HashSet<IntPtr> marked, IReadOnlyDictionary<int, int> choices)
        {
            var id = this.PassiveIdOf(record);
            var steps = this.BuildStepsOf(plan, marked);
            if (plan.Passives.FirstOrDefault(passive => passive.Id == id) is not { } passive || !steps.Steps.TryGetValue(id, out var step))
            {
                yield break;
            }

            yield return Announcements.BuildStep(step + 1, steps.Count);
            if (plan.WeaponSetOf(id) is var set and > 0)
            {
                yield return Announcements.BuildSetPointsHow(set);
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

            // An attribute node taken with another attribute than the note recommends: what a refund would set right.
            if (this.AttributesOf(plan, record, choices) is (BuildNotes.Attribute wanted, BuildNotes.Attribute chosen) && wanted != chosen)
            {
                yield return Announcements.BuildAttributeDiffers(Announcements.AttributeName(wanted), Announcements.AttributeName(chosen));
            }
        }

        /// <summary>The attribute the build's note recommends for an attribute node, and the one the node was given once it is allocated; null where there is none.</summary>
        private (BuildNotes.Attribute? Wanted, BuildNotes.Attribute? Chosen) AttributesOf(BuildPlan? plan, IntPtr record, IReadOnlyDictionary<int, int> choices)
        {
            var id = this.PassiveIdOf(record);
            var chosen = this.PassiveRow(record) is { } row && ChoiceOf(this.ReadPassiveRow(record, row), choices) is { } variant ? PassiveTreeCatalog.Shared.AttributeOf(variant) : null;
            var wanted = plan != null && BuildNotes.IsAttributeNode(id)
                ? plan.Passives.Where(passive => passive.Id == id && passive.Note.Length > 0).Select(passive => BuildNotes.Recommended(passive.Note, AttributeWords)).FirstOrDefault(attribute => attribute != null)
                : null;
            return (wanted, chosen);
        }

        /// <summary>An attribute as a note may name it: in English and in the mod's language.</summary>
        private static IEnumerable<string> AttributeWords(BuildNotes.Attribute attribute) => new[] { attribute.ToString(), Announcements.AttributeName(attribute) };

        /// <summary>The count of points that pays for a node of the build: the ascendancy's, a weapon set's where the file names one, else the tree's.</summary>
        private int PoolOf(BuildPlan? plan, IntPtr record)
        {
            var id = this.PassiveIdOf(record);
            return TreePoints.PoolOf(BuildPlan.IsAscendancyNode(id), plan?.WeaponSetOf(id) ?? 0);
        }

        /// <summary>
        ///     The build's next node to take: the file's first passive not yet allocated, or without a file the marked node
        ///     nearest the hovered one, those reachable now first; null when every marked node is allocated.
        /// </summary>
        private IntPtr? NextBuildNode(PassiveGraph graph, HashSet<IntPtr> marked, BuildPlan? plan, HashSet<int> allocated, IntPtr hovered)
        {
            var open = marked.Where(record => !graph.IsOwnStart(record) && (this.PassiveRow(record) is not { } row || !this.RowTaken(row, allocated))).ToList();
            if (open.Count == 0)
            {
                return null;
            }

            // The class's tree, the ascendancy's and each weapon set are paid from their own count of points: the nodes there are points for come first.
            var points = this.PointsLeft(graph);
            int Waits(IntPtr record) => BuildPlan.PointsOrder(this.PoolOf(plan, record), points);
            if (plan != null)
            {
                var order = plan.Passives.GroupBy(passive => passive.Id).ToDictionary(group => group.Key, group => group.First().Order);
                return open.OrderBy(Waits).ThenBy(record => order.TryGetValue(this.PassiveIdOf(record), out var step) ? step : int.MaxValue).First();
            }

            var places = this.PlacesOf(graph);
            float DistanceFromHovered(IntPtr record) =>
                places.Index.TryGetValue(record, out var at) && places.Index.TryGetValue(hovered, out var here) ? Vector2.Distance(places.Positions[at], places.Positions[here]) : float.MaxValue;
            return open.OrderBy(Waits)
                .ThenBy(record => this.PassiveRow(record) is { } row && this.StateOf(graph, this.ReadPassiveRow(record, row), allocated) == PassiveState.Reachable ? 0 : 1)
                .ThenBy(DistanceFromHovered).First();
        }

        /// <summary>
        ///     A line of the note the game shows beside the node that the build file's note already holds: the game splits it in
        ///     lines and adds full stops ("Allocation Step #15." / "- Next node: [Jewel] Socket.", 2026-11-10).
        /// </summary>
        internal static bool SaysAlready(string line, string note) => Letters(note) is { Length: > 0 } words && Letters(line).Contains(words, StringComparison.Ordinal);

        private static string Letters(string text) => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

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

            var pushes = this.PushesBetween(graph, places, from, here, there, MaxBuildPushes, out var far);
            var side = Announcements.StickDirections[PassiveStick.Nearest(places.Positions[there] - places.Positions[here])];
            return pushes != null
                ? Announcements.BuildNext(named, pushes.Select(octant => Announcements.StickDirections[octant]))
                : Announcements.BuildNext(named, new[] { far ? Announcements.Far(side) : side });
        }
    }
}
