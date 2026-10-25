namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     The bits of the controls' flags word a game update could renumber (docs/compatibility.md), checked on live
    ///     controls: interactive (controls with a click sound carry it, plain labels do not) and text input (combo boxes
    ///     and text fields carry it, buttons do not) on the pre-game screens; navigable (what the in-game controller focus
    ///     lands on carries it, its containers mostly not) on the in-game focus. Enabled has nothing to tell it apart by (a
    ///     disabled control is rare), so it is only noticed lost: then every control counts as enabled.
    /// </summary>
    public sealed class FlagBits
    {
        private const int MaxWalkNodes = 3000;
        private const int SamplesPerKind = 30;
        private const int MinCarriers = 3;
        /// <summary>Focus targets gathered before the navigable bit is judged (a panel's own frame can take the focus too).</summary>
        private const int MinFocusSamples = 12;

        /// <summary>The bit where measured is kept while at least half its carriers have it (and few others): a search needs a clear loss.</summary>
        private const double KeepShare = 0.5;

        /// <summary>Most carriers have the bit and few others do (a stray control or label is tolerated).</summary>
        private const double CarrierShare = 0.9;
        private const double OtherShare = 0.1;

        /// <summary>A container of a navigable control is sometimes navigable itself (an equipment slot's frame).</summary>
        private const double ContainerShare = 0.3;

        /// <summary>Below this share of enabled controls on a screen the enabled bit is taken for lost.</summary>
        private const double EnabledLostShare = 0.2;

        /// <summary>The login screen has one text field and two combo boxes: one input is enough to judge the text input bit.</summary>
        private const int MinInputs = 1;

        private const string TextFieldName = "textField";

        private readonly UiElementReader elements;
        private readonly CompatibilityReport report;
        private readonly int measuredInteractive = GameLayout.Element.InteractiveFlagBit;
        private readonly int measuredTextInput = GameLayout.Element.TextInputFlagBit;
        private readonly int measuredNavigable = GameLayout.Element.NavigableFlagBit;
        private readonly int measuredEnabled = GameLayout.Element.EnabledFlagBit;
        private readonly List<uint> focusFlags = new();
        private readonly List<uint> containerFlags = new();
        private readonly HashSet<IntPtr> focusSeen = new();
        private (List<uint> Controls, List<uint> Labels, List<uint> Inputs, List<uint> Buttons) screen = (new(), new(), new(), new());
        private bool screenJudged;
        private bool navigableJudged;

        public FlagBits(UiElementReader elements, CompatibilityReport report)
        {
            this.elements = elements;
            this.report = report;
        }

        /// <summary>The enabled bit is believed until a screen shows it lost.</summary>
        public static bool EnabledTrusted { get; private set; } = true;

        /// <summary>Pre-game: judges the interactive, text-input and enabled bits on a screen's controls, once per game process.</summary>
        public void ProbeScreen(IntPtr root)
        {
            if (this.screenJudged || root == IntPtr.Zero)
            {
                return;
            }

            this.screen = this.Gather(root);
            var (controls, labels, inputs, buttons) = this.screen;
            if (controls.Count < MinCarriers || labels.Count < MinCarriers)
            {
                return; // a loading screen: judged on the next one
            }

            this.screenJudged = true;
            this.Judge("interactive flag", controls, labels, this.measuredInteractive, OtherShare, MinCarriers, bit => GameLayout.Element.InteractiveFlagBit = bit);
            if (inputs.Count >= MinInputs)
            {
                this.Judge("text input flag", inputs, buttons, this.measuredTextInput, OtherShare, MinInputs, bit => GameLayout.Element.TextInputFlagBit = bit);
            }

            if (BitSearch.Share(controls, GameLayout.Element.EnabledFlagBit) < EnabledLostShare)
            {
                EnabledTrusted = false;
                this.report.Missing("enabled flag");
            }
            else
            {
                this.report.Found("enabled flag");
            }
        }

        /// <summary>
        ///     In game: an element the controller focus landed on (and its container), for the navigable bit. An inventory
        ///     grid is the focus while the cursor walks its cells, which the bit is on instead: grids are left out.
        /// </summary>
        public void SampleFocus(IntPtr focus, UiRole role)
        {
            if (this.navigableJudged || focus == IntPtr.Zero || role == UiRole.GridCell || !this.focusSeen.Add(focus) || !this.elements.TryReadElement(focus, out var info))
            {
                return;
            }

            this.focusFlags.Add(info.Raw.Flags);
            if (this.elements.TryReadElement(info.Raw.ParentPtr, out var parent))
            {
                this.containerFlags.Add(parent.Raw.Flags);
            }

            if (this.focusFlags.Count >= MinFocusSamples)
            {
                this.navigableJudged = true;
                this.Judge("navigable flag", this.focusFlags, this.containerFlags, this.measuredNavigable, ContainerShare, MinCarriers, bit => GameLayout.Element.NavigableFlagBit = bit);
            }
        }

        /// <summary>A new game process: every bit is judged again from where it was measured.</summary>
        public void Reset()
        {
            GameLayout.Element.InteractiveFlagBit = this.measuredInteractive;
            GameLayout.Element.TextInputFlagBit = this.measuredTextInput;
            GameLayout.Element.NavigableFlagBit = this.measuredNavigable;
            GameLayout.Element.EnabledFlagBit = this.measuredEnabled;
            EnabledTrusted = true;
            (this.screenJudged, this.navigableJudged) = (false, false);
            this.focusFlags.Clear();
            this.containerFlags.Clear();
            this.focusSeen.Clear();
        }

        /// <summary>Research (dev command "layout"): each bit judged so far, and any other bit that would fit as well.</summary>
        public string SelfTest()
        {
            var (controls, labels, inputs, buttons) = this.screen;
            return string.Join("; ", Line("interactive flag", controls, labels, GameLayout.Element.InteractiveFlagBit, OtherShare, MinCarriers),
                Line("text input flag", inputs, buttons, GameLayout.Element.TextInputFlagBit, OtherShare, MinInputs),
                Line("navigable flag", this.focusFlags, this.containerFlags, GameLayout.Element.NavigableFlagBit, ContainerShare, MinCarriers),
                $"enabled flag {(EnabledTrusted ? "ok" : "lost")} ({BitSearch.Share(controls, GameLayout.Element.EnabledFlagBit):P0} of {controls.Count} controls)");
        }

        private static string Line(string part, IReadOnlyList<uint> carriers, IReadOnlyList<uint> others, int now, double otherShare, int minCarriers)
        {
            if (carriers.Count < minCarriers || others.Count < BitSearch.MinOthers)
            {
                return $"{part}: not judged yet";
            }

            var fits = Enumerable.Range(0, 32).Where(bit => BitSearch.Share(carriers, bit) >= CarrierShare && BitSearch.Share(others, bit) <= otherShare).ToList();
            var kept = BitSearch.Share(carriers, now) >= KeepShare && BitSearch.Share(others, now) <= otherShare;
            return kept && fits.All(bit => bit == now) ? $"{part} ok"
                : $"{part} FAIL (fits {string.Join(", ", fits.Select(bit => $"0x{bit:X}"))}, is 0x{now:X}: on {BitSearch.Share(carriers, now):P0} of {carriers.Count}, {BitSearch.Share(others, now):P0} of {others.Count} others)";
        }

        private void Judge(string part, IReadOnlyList<uint> carriers, IReadOnlyList<uint> others, int measured, double otherShare, int minCarriers, Action<int> use)
        {
            if (BitSearch.Share(carriers, measured) >= KeepShare && BitSearch.Share(others, measured) <= otherShare)
            {
                use(measured);
                this.report.Found(part);
                return;
            }

            if (BitSearch.FindBit(carriers, others, measured, CarrierShare, otherShare, minCarriers) is not { } bit)
            {
                this.report.Missing(part);
                return;
            }

            use(bit);
            if (bit == measured)
            {
                this.report.Found(part);
            }
            else
            {
                this.report.Moved(part, measured, bit);
            }
        }

        /// <summary>Visible controls with a click sound, plain labels, text inputs and buttons under the root, breadth first.</summary>
        private (List<uint> Controls, List<uint> Labels, List<uint> Inputs, List<uint> Buttons) Gather(IntPtr root)
        {
            var found = (Controls: new List<uint>(), Labels: new List<uint>(), Inputs: new List<uint>(), Buttons: new List<uint>());
            var queue = new Queue<IntPtr>();
            queue.Enqueue(root);
            for (var walked = 0; queue.Count > 0 && walked < MaxWalkNodes; walked++)
            {
                if (!this.elements.TryReadElement(queue.Dequeue(), out var info) || !info.IsVisibleFlag)
                {
                    continue;
                }

                var children = this.elements.ReadChildren(info);
                if (children.Length == 0)
                {
                    if (found.Labels.Count < SamplesPerKind && this.elements.TryReadText(info.Address, out var text) && text.Length > 0)
                    {
                        found.Labels.Add(info.Raw.Flags);
                    }
                }
                else if (found.Controls.Count < SamplesPerKind)
                {
                    var role = this.elements.DetectRoleBySound(info.Address);
                    if (role is UiRole.Button or UiRole.CheckBox or UiRole.Slider or UiRole.ComboBox or UiRole.Tab)
                    {
                        found.Controls.Add(info.Raw.Flags);
                        (role == UiRole.ComboBox ? found.Inputs : role == UiRole.Button ? found.Buttons : null)?.Add(info.Raw.Flags);
                    }
                }

                if (info.Name == TextFieldName)
                {
                    found.Inputs.Add(info.Raw.Flags);
                }

                foreach (var child in children)
                {
                    queue.Enqueue(child);
                }
            }

            return found;
        }
    }
}
