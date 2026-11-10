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

        /// <summary>The login screen has one text field and two combo boxes: one input is enough to judge the text input bit.</summary>
        private const int MinInputs = 1;

        private const string TextFieldName = "textField";
        private const long ScreenKeyPrime = 1099511628211;

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
        private readonly EnabledBitCheck enabled = new();
        private readonly LossWitnesses interactiveMisses = new();
        private readonly LossWitnesses textInputMisses = new();
        private readonly LossWitnesses navigableMisses = new();
        private long focusRounds;
        private long screenKey;
        private bool screenJudged;
        private bool textInputJudged;
        private bool navigableJudged;

        public FlagBits(UiElementReader elements, CompatibilityReport report)
        {
            this.elements = elements;
            this.report = report;
        }

        /// <summary>The enabled bit is believed until two screens show it lost, and again once one shows it working (<see cref="EnabledBitCheck"/>).</summary>
        public static bool EnabledTrusted { get; private set; } = true;

        /// <summary>
        ///     Pre-game: judges the interactive and text-input bits on a screen's controls, once per game process, and the enabled
        ///     bit on every screen until one shows it working.
        /// </summary>
        public void ProbeScreen(IntPtr root)
        {
            if ((this.screenJudged && this.textInputJudged && this.enabled.Proved) || root == IntPtr.Zero)
            {
                return;
            }

            var gathered = this.Gather(root);
            var (controls, labels, inputs, buttons) = gathered;
            if (controls.Count < MinCarriers || labels.Count < MinCarriers)
            {
                return; // a loading screen: judged on the next one
            }

            this.screen = gathered;
            if (!this.screenJudged)
            {
                this.screenJudged = this.Judge("interactive flag", controls, labels, this.measuredInteractive, OtherShare, MinCarriers, bit => GameLayout.Element.InteractiveFlagBit = bit,
                    this.interactiveMisses, this.screenKey);
            }

            if (this.enabled.Observe(this.screenKey, BitSearch.Share(controls, GameLayout.Element.EnabledFlagBit)))
            {
                EnabledTrusted = this.enabled.Trusted;
                if (EnabledTrusted)
                {
                    this.report.Found("enabled flag");
                }
                else
                {
                    this.report.Missing("enabled flag");
                }
            }

            // A screen with text inputs (the login, character creation) judges their bit against plain labels: some buttons
            // carry it too (3 of 14 at character creation, 2026-10-25).
            if (!this.textInputJudged && inputs.Count >= MinInputs)
            {
                this.textInputJudged = this.Judge("text input flag", inputs, labels, this.measuredTextInput, OtherShare, MinInputs, bit => GameLayout.Element.TextInputFlagBit = bit,
                    this.textInputMisses, this.screenKey);
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
                this.navigableJudged = this.Judge("navigable flag", this.focusFlags, this.containerFlags, this.measuredNavigable, ContainerShare, MinCarriers, bit => GameLayout.Element.NavigableFlagBit = bit,
                    this.navigableMisses, ++this.focusRounds);
                if (!this.navigableJudged)
                {
                    // No bit fits these: another round of other elements must agree (those seen stay seen, so none counts twice).
                    this.focusFlags.Clear();
                    this.containerFlags.Clear();
                }
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
            this.enabled.Reset();
            (this.screenJudged, this.textInputJudged, this.navigableJudged, this.focusRounds) = (false, false, false, 0);
            this.interactiveMisses.Clear();
            this.textInputMisses.Clear();
            this.navigableMisses.Clear();
            this.focusFlags.Clear();
            this.containerFlags.Clear();
            this.focusSeen.Clear();
        }

        /// <summary>Research (dev command "layout"): each bit judged so far, and any other bit that would fit as well.</summary>
        public string SelfTest()
        {
            var (controls, labels, inputs, _) = this.screen;
            return string.Join("; ", Line("interactive flag", controls, labels, GameLayout.Element.InteractiveFlagBit, OtherShare, MinCarriers),
                Line("text input flag", inputs, labels, GameLayout.Element.TextInputFlagBit, OtherShare, MinInputs),
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
                : kept ? $"{part} ok (also fits {string.Join(", ", fits.Where(bit => bit != now).Select(bit => $"0x{bit:X}"))}: a search from a moved bit takes the nearest)"
                : $"{part} FAIL (fits {string.Join(", ", fits.Select(bit => $"0x{bit:X}"))}, is 0x{now:X}: on {BitSearch.Share(carriers, now):P0} of {carriers.Count}, {BitSearch.Share(others, now):P0} of {others.Count} others)";
        }

        /// <summary>
        ///     Judges one bit on a screen's or a round's controls; true once settled. No bit fitting on one screen is no verdict
        ///     (its controls may all be greyed or of one odd kind): a second <paramref name="witness"/> must find none either,
        ///     and until then the bit is judged again on the next.
        /// </summary>
        private bool Judge(
            string part, IReadOnlyList<uint> carriers, IReadOnlyList<uint> others, int measured, double otherShare, int minCarriers, Action<int> use, LossWitnesses misses, long witness)
        {
            if (BitSearch.Share(carriers, measured) >= KeepShare && BitSearch.Share(others, measured) <= otherShare)
            {
                use(measured);
                this.report.Found(part);
                return true;
            }

            if (BitSearch.FindBit(carriers, others, measured, CarrierShare, otherShare, minCarriers) is not { } bit)
            {
                if (misses.Against(witness))
                {
                    this.report.Missing(part);
                }

                return misses.Lost;
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

            return true;
        }

        /// <summary>Visible controls with a click sound, plain labels, text inputs and buttons under the root, breadth first.</summary>
        private (List<uint> Controls, List<uint> Labels, List<uint> Inputs, List<uint> Buttons) Gather(IntPtr root)
        {
            var found = (Controls: new List<uint>(), Labels: new List<uint>(), Inputs: new List<uint>(), Buttons: new List<uint>());
            this.screenKey = root.ToInt64();
            var queue = new Queue<IntPtr>();
            queue.Enqueue(root);
            for (var walked = 0; queue.Count > 0 && walked < MaxWalkNodes; walked++)
            {
                if (!this.elements.TryReadElement(queue.Dequeue(), out var info))
                {
                    continue;
                }

                // Hidden layers are walked through (a root can be drawn without the flag); only visible elements count.
                var children = this.elements.ReadChildren(info);
                if (info.IsVisibleFlag && children.Length == 0)
                {
                    if (found.Labels.Count < SamplesPerKind && this.elements.TryReadText(info.Address, out var text) && text.Length > 0)
                    {
                        found.Labels.Add(info.Raw.Flags);
                    }
                }
                else if (info.IsVisibleFlag && found.Controls.Count < SamplesPerKind)
                {
                    var role = this.elements.DetectRoleBySound(info.Address);
                    if (role is UiRole.Button or UiRole.CheckBox or UiRole.Slider or UiRole.ComboBox or UiRole.Tab)
                    {
                        found.Controls.Add(info.Raw.Flags);
                        this.screenKey = unchecked((this.screenKey * ScreenKeyPrime) ^ info.Address.ToInt64()); // other controls, another screen
                        (role == UiRole.ComboBox ? found.Inputs : role == UiRole.Button ? found.Buttons : null)?.Add(info.Raw.Flags);
                    }
                }

                if (info.IsVisibleFlag && info.Name == TextFieldName)
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
