namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Control subclasses whose fields have a shape of their own to check on a live control.</summary>
    public enum ControlGroup
    {
        /// <summary>GameLayout.Slider: the value lies within its range.</summary>
        Slider,

        /// <summary>GameLayout.ComboBox: the selected index points into the entries vector.</summary>
        ComboBox,

        /// <summary>GameLayout.InventoryGrid and GameLayout.InventoryItem: the cursor cell holds an item element whose entity is an item.</summary>
        Inventory,

        /// <summary>GameLayout.TabButton: exactly one tab of a bar is selected.</summary>
        Tabs,

        /// <summary>GameLayout.SkillSlot binding index: the skill bar's slots hold distinct indices of both sets.</summary>
        SkillBinding,

        /// <summary>GameLayout.SkillSlot details: a slot icon's skill leads to a GrantedEffectsPerLevel row.</summary>
        SkillDetails,

        /// <summary>GameLayout.PassiveTree canvas state: the tree state holds the passive skill graph file.</summary>
        PassiveCanvas,

        /// <summary>GameLayout.PassiveTree node record: a node element's record leads to a PassiveSkills row.</summary>
        PassiveNode,
    }

    /// <summary>
    ///     Fields of the control subclasses after a game update. They sit after the base control, so when it grows they
    ///     all move as far as the label text did (<see cref="LayoutProbe.TextShift"/>): that is where each is tried
    ///     first. The groups here are judged on a live control and looked for nearby when that fails; subclasses with
    ///     nothing to check follow the text (<see cref="LayoutProbe"/>).
    /// </summary>
    public sealed class ControlFields
    {
        private const int Radius = 0x100;
        private const int Step = 8;

        /// <summary>A failed search is not repeated sooner than this (a control can be briefly unreadable).</summary>
        private const int RetryMs = 5000;

        private const int MissesToReport = 3;

        private readonly CompatibilityReport report;
        private readonly Func<int> textShift;
        private readonly Dictionary<ControlGroup, GroupState> states = new();

        public ControlFields(CompatibilityReport report, Func<int> textShift)
        {
            this.report = report;
            this.textShift = textShift;
        }

        /// <summary>
        ///     The shift of the group's fields from where they were measured, judged on <paramref name="control"/>, a
        ///     control known to be of that kind: <paramref name="fits"/> says whether it reads sensibly with the fields
        ///     moved by a shift. A shift other than the expected one is trusted once two controls agree on it.
        /// </summary>
        public int Shift(ControlGroup group, IntPtr control, Func<int, bool> fits)
        {
            var state = this.State(group);
            state.Fits = fits;
            if (state.Known is { } known)
            {
                return known;
            }

            var expected = this.textShift();
            if (fits(expected))
            {
                this.Adopt(group, state, expected);
                return expected;
            }

            if (state.Candidate is { } candidate && fits(candidate))
            {
                if (control != state.CandidateControl)
                {
                    this.Adopt(group, state, candidate);
                }

                return candidate;
            }

            var now = Environment.TickCount64;
            if (now - state.TriedAt < RetryMs)
            {
                return expected;
            }

            state.TriedAt = now;
            if (OffsetSearch.First(expected, Radius, Step, fits, expected - Radius) is { } found)
            {
                (state.Candidate, state.CandidateControl) = (found, control);
                return found;
            }

            if (++state.Misses == MissesToReport && group == ControlGroup.ComboBox)
            {
                // Only a combo box always proves its layout: a grid is recognised by reading it (a miss may be another
                // control) and a slider at its minimum fits nearly anywhere.
                this.report.Missing(Part(group));
            }

            return expected;
        }

        /// <summary>Whether a live control has proved the group's shift yet.</summary>
        public bool IsKnown(ControlGroup group) => this.State(group).Known != null;

        /// <summary>The group's shift as known so far, for a field read without a control to judge it on.</summary>
        public int Current(ControlGroup group) => this.State(group).Known ?? this.State(group).Candidate ?? this.textShift();

        /// <summary>A new game process (perhaps a patched one): every group is judged again.</summary>
        public void Reset() => this.states.Clear();

        /// <summary>
        ///     Research (dev command "layout"): on the last control each group was judged on, every other shift within reach
        ///     that would also fit; any of them could be taken for the true one after an update moves the fields.
        /// </summary>
        public string SelfTest()
        {
            var lines = new List<string>();
            foreach (var (group, state) in this.states)
            {
                if (state.Known is { } known && state.Fits is { } fits)
                {
                    var others = OffsetSearch.Outward(known, Radius, Step, known - Radius).Where(at => at != known && fits(at)).Order().ToList();
                    lines.Add(others.Count == 0
                        ? $"{Part(group)} ok"
                        : $"{Part(group)} FAIL (also fits at {string.Join(", ", others.Select(at => $"{(at < known ? '-' : '+')}0x{Math.Abs(at - known):X}"))})");
                }
            }

            return lines.Count == 0 ? "no controls judged yet" : string.Join("; ", lines);
        }

        private static string Part(ControlGroup group) => group switch
        {
            ControlGroup.Slider => "sliders",
            ControlGroup.ComboBox => "combo boxes",
            ControlGroup.Tabs => "tabs",
            ControlGroup.SkillBinding => "skill slot bindings",
            ControlGroup.SkillDetails => "skill slot skills",
            ControlGroup.PassiveCanvas => "passive tree state",
            ControlGroup.PassiveNode => "passive nodes",
            _ => "inventory grid",
        };

        /// <summary>The group's first field where it was measured, for the log.</summary>
        private static int Measured(ControlGroup group) => group switch
        {
            ControlGroup.Slider => GameLayout.Slider.ValueOffset,
            ControlGroup.ComboBox => GameLayout.ComboBox.SelectedIndexOffset,
            ControlGroup.Tabs => GameLayout.TabButton.SelectedOffset,
            ControlGroup.SkillBinding => GameLayout.SkillSlot.BindingIndexOffset,
            ControlGroup.SkillDetails => GameLayout.SkillSlot.SkillDetailsOffset,
            ControlGroup.PassiveCanvas => GameLayout.PassiveTree.CanvasStateOffset,
            ControlGroup.PassiveNode => GameLayout.PassiveTree.NodeRecordOffset,
            _ => GameLayout.InventoryGrid.CellSizeOffset,
        };

        private void Adopt(ControlGroup group, GroupState state, int shift)
        {
            state.Known = shift;
            if (shift == 0)
            {
                this.report.Found(Part(group));
            }
            else
            {
                this.report.Moved(Part(group), Measured(group), Measured(group) + shift);
            }
        }

        private GroupState State(ControlGroup group)
        {
            if (!this.states.TryGetValue(group, out var state))
            {
                state = new GroupState { TriedAt = long.MinValue / 2 };
                this.states[group] = state;
            }

            return state;
        }

        private sealed class GroupState
        {
            public int? Known { get; set; }

            public int? Candidate { get; set; }

            public IntPtr CandidateControl { get; set; }

            public long TriedAt { get; set; }

            public int Misses { get; set; }

            public Func<int, bool>? Fits { get; set; }
        }
    }
}
