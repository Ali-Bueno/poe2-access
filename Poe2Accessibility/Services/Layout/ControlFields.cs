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

        /// <summary>GameLayout.ClassSelector: the highlighted class's index and id agree with the selector's first row.</summary>
        ClassSelector,

        /// <summary>GameLayout.Element placeholder: a text field's hint reads as words.</summary>
        Placeholder,

        /// <summary>GameLayout.Element panel title: an ancestor of the focus names its panel (weak: confirmed by a second control).</summary>
        PanelTitle,

        /// <summary>GameLayout.Element check box state: found by watching it change as the box is ticked (<see cref="ChangingField"/>).</summary>
        CheckBox,

        /// <summary>GameLayout.SelectableList index: found by watching it change as the selection moves (<see cref="ChangingField"/>).</summary>
        SelectableList,

        /// <summary>GameLayout.OptionsSection: an active section's row index names the focused row (weak: confirmed by a second section).</summary>
        OptionsSection,

        /// <summary>GameLayout.WorldMap depth byte: found by watching it change as the toggle is pressed (<see cref="ChangingField"/>).</summary>
        MapDepth,

        /// <summary>
        ///     GameLayout.WorldMap hovered pin: of the act canvas's keys into MapPins, the one that changes as the cursor moves over
        ///     pins (the current area's key beside it stays put; <see cref="ChangingField"/>).
        /// </summary>
        HoveredPin,
    }

    /// <summary>
    ///     Fields of the control subclasses after a game update. They sit after the base control, so when it grows they
    ///     all move as far as the label text did (<see cref="LayoutProbe.TextShift"/>): that is where each is tried
    ///     first. The groups here are judged on a live control and looked for nearby when that fails; subclasses with
    ///     nothing to check follow the text (<see cref="LayoutProbe"/>).
    /// </summary>
    public sealed class ControlFields
    {
        /// <summary>How far from where they were measured a group's fields are looked for, and in what steps.</summary>
        public const int Radius = 0x100;

        public const int Step = 8;

        /// <summary>
        ///     A failed search on a control is not repeated sooner than this (a control can be briefly unreadable); kept per
        ///     control, so one that can never prove the layout (an empty slot) does not hold up the one that can.
        /// </summary>
        private const int RetryMs = 5000;

        /// <summary>Controls remembered per group for <see cref="RetryMs"/>; more are forgotten all at once.</summary>
        private const int MaxTriedControls = 64;

        private const int MissesToReport = 3;

        /// <summary>Different controls that must miss before a group is said lost: one empty or half-built dropdown is no layout.</summary>
        private const int ControlsToReport = 2;

        /// <summary>
        ///     Once a group missed this often nearby, the whole control is searched (<see cref="OffsetSearch.Only"/>), no more
        ///     often than <see cref="WideRetryMs"/>: a search costs a judgment per place of the control.
        /// </summary>
        private const int MissesBeforeWide = MissesToReport;

        private const int WideRetryMs = 30000;

        private readonly CompatibilityReport report;
        private readonly Func<int> textShift;
        private readonly Func<long> clock;
        private readonly Dictionary<ControlGroup, GroupState> states = new();

        /// <param name="report">Where each group's outcome goes.</param>
        /// <param name="textShift">How far the label text moved: where each group is tried first.</param>
        /// <param name="clock">Milliseconds, for the retry timers; the system's when null (a test's own otherwise).</param>
        public ControlFields(CompatibilityReport report, Func<int> textShift, Func<long>? clock = null)
        {
            this.report = report;
            this.textShift = textShift;
            this.clock = clock ?? (() => Environment.TickCount64);
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
                    return candidate;
                }

                return IsWeak(group) || state.CandidateIsFar ? expected : candidate;
            }

            var now = this.clock();
            if (state.Tried.TryGetValue(control, out var triedAt) && now - triedAt < RetryMs)
            {
                return expected;
            }

            if (state.Tried.Count >= MaxTriedControls)
            {
                state.Tried.Clear();
            }

            state.Tried[control] = now;
            if (OffsetSearch.First(expected, Radius, Step, fits, expected - Radius) is { } found)
            {
                (state.Candidate, state.CandidateControl, state.CandidateIsFar) = (found, control, false);
                return IsWeak(group) ? expected : found;
            }

            // Nothing nearby, again: the fields may have moved farther than a class grows. The one place of the whole control
            // that fits is kept as a candidate and read only once a second control fits there too.
            if (++state.Misses >= MissesBeforeWide && now >= state.NextWideAt)
            {
                state.NextWideAt = now + WideRetryMs;
                var first = -Measured(group);
                if (OffsetSearch.Only(expected, first, first + GameLayout.Element.ProbeBytes, Step, fits) is { } far)
                {
                    (state.Candidate, state.CandidateControl, state.CandidateIsFar) = (far, control, true);
                    return expected;
                }
            }

            if (state.Missed.Count < MaxTriedControls)
            {
                state.Missed.Add(control);
            }

            if (state.Misses >= MissesToReport && state.Missed.Count >= ControlsToReport && !state.Reported && group == ControlGroup.ComboBox)
            {
                state.Reported = true;
                // Only a combo box always proves its layout: a grid is recognised by reading it (a miss may be another
                // control) and a slider at its minimum fits nearly anywhere.
                this.report.Missing(Part(group));
            }

            return expected;
        }

        /// <summary>
        ///     The shift of a group with nothing static to check it by, found by watching it change on one control as the
        ///     player moves the value (<see cref="ChangingField"/>); the expected one until then. <paramref name="read"/>
        ///     reads bytes of the control (offset, length); a value is <paramref name="size"/> bytes (1 or 4).
        /// </summary>
        public int Changing(ControlGroup group, IntPtr control, int measured, int size, Func<int, int, byte[]> read, Func<long, bool> valid)
        {
            var state = this.State(group);
            if (state.Known is { } known)
            {
                return known;
            }

            var expected = this.textShift();
            var first = expected - Radius;
            var block = read(measured + first, (2 * Radius) + size);
            var values = new Dictionary<int, long>();
            for (var shift = first; shift <= expected + Radius; shift += Step)
            {
                var at = shift - first;
                if (at + size <= block.Length && measured + shift >= 0)
                {
                    values[shift] = size == 1 ? block[at] : BitConverter.ToInt32(block, at);
                }
            }

            state.Watch ??= new ChangingField();
            if (state.Watch.Observe(control, expected, values, valid) && state.Watch.Proved is { } proved)
            {
                this.Adopt(group, state, proved);
            }

            return state.Known ?? expected;
        }

        /// <summary>
        ///     As <see cref="Changing"/>, for a field the caller reads itself at each shift within reach (a dat key read through
        ///     its file): <paramref name="valueAt"/> gives the value there, 0 when none fits (a key into another table).
        /// </summary>
        public int ChangingAt(ControlGroup group, IntPtr control, Func<int, long> valueAt)
        {
            var state = this.State(group);
            if (state.Known is { } known)
            {
                return known;
            }

            var expected = this.textShift();
            var values = new Dictionary<int, long>();
            foreach (var shift in OffsetSearch.Outward(expected, Radius, Step, expected - Radius))
            {
                values[shift] = valueAt(shift);
            }

            state.Watch ??= new ChangingField();
            if (state.Watch.Observe(control, expected, values, value => value != 0) && state.Watch.Proved is { } proved)
            {
                this.Adopt(group, state, proved);
            }

            return state.Known ?? expected;
        }

        /// <summary>Whether a live control has proved the group's shift yet.</summary>
        public bool IsKnown(ControlGroup group) => this.State(group).Known != null;

        /// <summary>The group's shift as known so far, for a field read without a control to judge it on.</summary>
        public int Current(ControlGroup group) =>
            this.State(group).Known ?? (IsWeak(group) || this.State(group).CandidateIsFar ? null : this.State(group).Candidate) ?? this.textShift();

        /// <summary>A new game process (perhaps a patched one): every group is judged again.</summary>
        public void Reset() => this.states.Clear();

        /// <summary>How far the label text moved: the shift of a subclass field with nothing of its own to check.</summary>
        public int TextShift => this.textShift();

        /// <summary>
        ///     Research (dev command "layout"): on the last control each group was judged on, every other shift within reach
        ///     that would also fit; any of them could be taken for the true one after an update moves the fields.
        /// </summary>
        public string SelfTest()
        {
            var lines = new List<string>();
            foreach (var (group, state) in this.states)
            {
                if (state.Known != null && state.Watch != null)
                {
                    lines.Add($"{Part(group)} ok (seen changing)");
                }
                else if (state.Known is { } known && state.Fits is { } fits)
                {
                    var others = OffsetSearch.Outward(known, Radius, Step, known - Radius).Where(at => at != known && fits(at)).Order().ToList();
                    lines.Add(others.Count == 0
                        ? $"{Part(group)} ok"
                        : $"{Part(group)} FAIL (also fits at {string.Join(", ", others.Select(at => $"{(at < known ? '-' : '+')}0x{Math.Abs(at - known):X}"))})");
                }
            }

            return lines.Count == 0 ? "no controls judged yet" : string.Join("; ", lines);
        }

        /// <summary>
        ///     Groups whose check another field could pass by chance (a hover byte on a tab bar): a shift found by search is
        ///     used only once a second control agrees; the others are used at once, as their check names the field.
        /// </summary>
        private static bool IsWeak(ControlGroup group) => group is ControlGroup.Tabs or ControlGroup.PanelTitle or ControlGroup.OptionsSection;

        private static string Part(ControlGroup group) => group switch
        {
            ControlGroup.Slider => "sliders",
            ControlGroup.ComboBox => "combo boxes",
            ControlGroup.Tabs => "tabs",
            ControlGroup.SkillBinding => "skill slot bindings",
            ControlGroup.SkillDetails => "skill slot skills",
            ControlGroup.PassiveCanvas => "passive tree state",
            ControlGroup.PassiveNode => "passive nodes",
            ControlGroup.ClassSelector => "class selector",
            ControlGroup.Placeholder => "text field hints",
            ControlGroup.PanelTitle => "panel titles",
            ControlGroup.CheckBox => "check boxes",
            ControlGroup.SelectableList => "selectable lists",
            ControlGroup.OptionsSection => "options sections",
            ControlGroup.MapDepth => "quest map depth",
            ControlGroup.HoveredPin => "world map hovered pin",
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
            ControlGroup.ClassSelector => GameLayout.ClassSelector.IndexOffset,
            ControlGroup.Placeholder => GameLayout.Element.PlaceholderOffset,
            ControlGroup.PanelTitle => GameLayout.Element.PanelTitleOffset,
            ControlGroup.CheckBox => GameLayout.Element.CheckBoxStateOffset,
            ControlGroup.SelectableList => GameLayout.SelectableList.SelectedIndexOffset,
            ControlGroup.OptionsSection => GameLayout.OptionsSection.RowIndexOffset,
            ControlGroup.MapDepth => GameLayout.WorldMap.SurfaceShownOffset,
            ControlGroup.HoveredPin => GameLayout.WorldMap.HoveredPinOffset,
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
                state = new GroupState();
                this.states[group] = state;
            }

            return state;
        }

        private sealed class GroupState
        {
            public int? Known { get; set; }

            public int? Candidate { get; set; }

            public IntPtr CandidateControl { get; set; }

            /// <summary>The candidate came from a search of the whole control: used only once a second control agrees.</summary>
            public bool CandidateIsFar { get; set; }

            public long NextWideAt { get; set; }

            public Dictionary<IntPtr, long> Tried { get; } = new();

            public int Misses { get; set; }

            public bool Reported { get; set; }

            public HashSet<IntPtr> Missed { get; } = new();

            public Func<int, bool>? Fits { get; set; }

            public ChangingField? Watch { get; set; }
        }
    }
}
