namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using GameOffsets.Objects.States;

    /// <summary>
    ///     Finds the memory layout a game update can move (docs/compatibility.md). Each field is first tried where it was
    ///     measured and only looked for around it when it no longer holds what it must; fields of one structure that
    ///     move together follow their anchor (font, manager and tooltips the control name, label fields the text, manager
    ///     fields the root pointer); the element base itself is the host's judgment (<see cref="ProbeElementBase"/>).
    ///     Outcomes go to the <see cref="CompatibilityReport"/>.
    /// </summary>
    public sealed class LayoutProbe
    {
        /// <summary>Fields are pointer-aligned.</summary>
        private const int Step = 8;

        /// <summary>How far from where it was measured a field is looked for (a class gaining or losing a few fields).</summary>
        private const int Radius = 0x100;

        private const int ManagerScanBytes = 0x1000;
        private const int MaxLength = 256;

        /// <summary>A text layout is trusted once this many labels read through it.</summary>
        private const int MinTextHits = 5;

        private const int MaxWalkNodes = 3000;
        private const string LayerRootsPart = "in-game layer roots";
        private const string FocusPart = "UI focus";
        private const string ManagerFieldPart = "element manager";

        /// <summary>Elements the manager pointer is judged on: the root and the first of its children and theirs.</summary>
        private const int MaxManagerNodes = 16;

        /// <summary>A layer container's children carry at least this many known layer names.</summary>
        private const int MinLayerNames = 2;

        /// <summary>Slots looked at either side of the focus slot (the previous-focus and hovered ones sit within 0x18).</summary>
        private const int FocusWindow = 0x40;

        private const int MaxOwnElements = 512;
        private const int MaxLeaves = 400;
        private const int MaxNamedNodes = 200;

        /// <summary>A texture pointer is trusted once this many images lead through it to an art key.</summary>
        private const int MinTextureHits = 5;

        /// <summary>Entity names are judged on this many monsters, characters and players; half must read.</summary>
        private const int MinRenders = 15;

        /// <summary>An inconclusive check (a screen without texts, a loading root) is tried again after this long.</summary>
        private const int RetryMs = 10000;

        /// <summary>
        ///     Whole-object searches a part gets per game process, once nothing fits nearby: each reads every element looked at
        ///     up to <see cref="GameLayout.Element.ProbeBytes"/> on the frame thread.
        /// </summary>
        private const int MaxWideTries = 3;

        /// <summary>Images looked at in a whole-object search of the texture (each candidate place costs a read per image).</summary>
        private const int MaxWideLeaves = 150;

        /// <summary>Control names the game's roots and in-game layers carry (login and in-game dumps, 2026-09/10).</summary>
        private static readonly string[] KnownLayerNames =
        {
            "root_control", "HUD", "map_overlay", "world_overlay", "world_ui_layer_parent", "message_label", "no_controller_overlay", "death_screen",
        };

        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;
        private readonly CompatibilityReport report;

        // Where each anchor was measured: the shift of an anchor moves its whole group.
        private readonly int measuredName = GameLayout.Element.NameOffset;
        private readonly int measuredFont = GameLayout.Element.FontOffset;
        private readonly int measuredManager = GameLayout.Element.ManagerOffset;
        private readonly int measuredTooltips = GameLayout.Element.TooltipsOffset;
        private readonly int measuredTagged = GameLayout.Element.TextWithTagsOffset;
        private readonly int measuredText = GameLayout.Element.TextOffset;
        private readonly int measuredCopy = GameLayout.Element.TextCopyOffset;
        private readonly int measuredColor = GameLayout.Element.TextColorOffset;
        private readonly int measuredRoot = GameLayout.UiManager.RootOffset;
        private readonly int measuredCurrent = GameLayout.UiManager.CurrentOffset;
        private readonly int measuredLastFocused = GameLayout.UiManager.LastFocusedOffset;
        private readonly int measuredHovered = GameLayout.UiManager.HoveredOffset;
        private readonly int measuredPad = GameLayout.UiManager.PadStateOffset;
        private readonly int measuredRenderName = GameLayout.RenderComponent.NameOffset;
        private readonly int measuredTexture = GameLayout.Image.TextureOffset;
        private readonly int measuredAltTexture = GameLayout.Image.AltTextureOffset;

        private bool namesKnown;
        private bool elementBaseKnown;
        private long? elementBaseLostAt;
        private readonly Dictionary<IntPtr, long> elementBaseTried = new();
        private long nextRootSearchAt;
        private bool layerRootsKnown;
        private long layerRootsTriedAt = long.MinValue / 2;
        private FocusSlotCheck? focusCheck;
        private bool focusKnown;
        private readonly Dictionary<long, bool> ownElements = new();
        private IntPtr ownElementsManager; // the verdicts above are this manager's
        private bool textKnown;
        private bool managerKnown;
        private bool renderKnown;
        private bool texturesKnown;
        private long namesTriedAt = long.MinValue / 2;
        private long textTriedAt = long.MinValue / 2;
        private long texturesTriedAt = long.MinValue / 2;
        private long renderTriedAt = long.MinValue / 2;
        private int wideNameTries;
        private int wideTextTries;
        private int wideTextureTries;
        private readonly LossWitnesses renderMisses = new();

        public LayoutProbe(HostMemoryReader reader, UiElementReader elements, CompatibilityReport report)
        {
            this.reader = reader;
            this.elements = elements;
            this.report = report;
        }

        /// <summary>
        ///     The element base fields every walk of the tree reads (self, children, parent, flags, rects), judged by the host on
        ///     the UI root (<see cref="GameReader.Utils.UiElementLayoutSearch"/>) before anything else reads through them.
        /// </summary>
        public void ProbeElementBase(IntPtr root)
        {
            // An element that is no UI root (or a root the search fails on) is tried again only after a while: the manager is
            // looked for on every frame its cache misses.
            var now = Environment.TickCount64;
            if (this.elementBaseKnown || root == IntPtr.Zero || (this.elementBaseTried.TryGetValue(root, out var tried) && now - tried < RetryMs))
            {
                return;
            }

            if (this.elementBaseTried.Count >= MaxOwnElements)
            {
                this.elementBaseTried.Clear();
            }

            this.elementBaseTried[root] = now;

            var outcomes = GameReader.Utils.OffsetHealer.JudgeUiElementBase(root);
            if (outcomes.Count == 0)
            {
                return;
            }

            // A root judged before its children exist reads the tree lost: judged again on the next roots (LayoutVerdict).
            var lost = outcomes.Any(outcome => outcome.Health == GameReader.Utils.StructHealth.Lost);
            this.elementBaseLostAt ??= lost ? now : null;
            if (!LayoutVerdict.Settled(lost, this.elementBaseLostAt ?? now, now))
            {
                return;
            }

            this.elementBaseKnown = true;
            foreach (var outcome in outcomes)
            {
                this.report.Record(outcome);
            }
        }

        /// <summary>
        ///     When none of a state object's <paramref name="pointers"/> reads as an element: looks for the UI root above them
        ///     by shape alone and judges the element base on it, so base fields a patch moved are found before any screen was
        ///     read (the screens before the game have no other way to a root). Tried again only after a while.
        /// </summary>
        /// <returns>The root found, zero when none; null when it was not looked for this time.</returns>
        public IntPtr? ProbeElementBaseAbove(IReadOnlyList<IntPtr> pointers)
        {
            var now = Environment.TickCount64;
            if (this.elementBaseKnown || pointers.Count == 0 || now < this.nextRootSearchAt)
            {
                return null;
            }

            this.nextRootSearchAt = now + RetryMs;
            var root = GameReader.Utils.OffsetHealer.FindUiRootAbove(pointers);
            this.ProbeElementBase(root);
            return root;
        }

        /// <summary>
        ///     The in-game layer containers (InGameState's mouse and gamepad UI roots): an element whose children carry the
        ///     known layer names. Taken where they read now when either holds one; else the slot of the state nearest them that
        ///     does, and both move by that shift (call once the element names are known).
        /// </summary>
        public void ProbeLayerRoots(IntPtr inGameState)
        {
            var now = Environment.TickCount64;
            if (this.layerRootsKnown || !this.namesKnown || inGameState == IntPtr.Zero || now - this.layerRootsTriedAt < RetryMs)
            {
                return;
            }

            this.layerRootsTriedAt = now;
            var (mouse, pad) = (MeasuredStateField(nameof(InGameStateOffset.UiRootStructPtr)), MeasuredStateField(nameof(InGameStateOffset.GamepadUiRootStructPtr)));
            var (mouseNow, padNow) = (GameReader.Utils.HostStructs.Follow<InGameStateOffset>(mouse), GameReader.Utils.HostStructs.Follow<InGameStateOffset>(pad));
            var slots = this.reader.ReadArray<IntPtr>(inGameState, ManagerScanBytes / IntPtr.Size);
            bool Holds(int offset) => offset >= 0 && offset / IntPtr.Size < slots.Length && this.IsLayerContainer(slots[offset / IntPtr.Size]);
            if (Holds(mouseNow) || Holds(padNow))
            {
                this.layerRootsKnown = true;
                this.Record(LayerRootsPart, mouse, mouseNow);
                return;
            }

            // A slot found elsewhere needs a second fact: its element climbs to the root its UI manager shows.
            bool HoldsLinked(int offset) => Holds(offset) && this.LeadsToManagerRoot(slots[offset / IntPtr.Size]);
            var nearPad = OffsetSearch.First(padNow, ManagerScanBytes, Step, HoldsLinked, Step);
            var nearMouse = OffsetSearch.First(mouseNow, ManagerScanBytes, Step, HoldsLinked, Step);
            if ((nearPad ?? nearMouse) is null)
            {
                this.report.Missing(LayerRootsPart);
                return;
            }

            var shift = nearPad is { } p && (nearMouse is not { } m || Math.Abs(p - padNow) <= Math.Abs(m - mouseNow)) ? p - padNow : nearMouse!.Value - mouseNow;
            GameReader.Utils.OffsetHealer.PlacePointers(typeof(InGameStateOffset), (mouse, mouseNow + shift), (pad, padNow + shift));
            this.layerRootsKnown = true;
            this.report.Moved(LayerRootsPart, mouseNow, mouseNow + shift);
        }

        /// <summary>
        ///     Watches the UI manager's focus slot while the player moves (<see cref="FocusSlotCheck"/>): once judged where it
        ///     reads, nothing changes; judged elsewhere, the last-focused and hovered slots move with it. Call every frame.
        /// </summary>
        public void ObserveFocus(IntPtr manager)
        {
            if (this.focusKnown || manager == IntPtr.Zero)
            {
                return;
            }

            var current = GameLayout.UiManager.CurrentOffset;
            this.focusCheck ??= new FocusSlotCheck(current, GameLayout.UiManager.LastFocusedOffset - current);
            var start = Math.Max(0, current - FocusWindow);
            var slots = this.reader.ReadArray<IntPtr>(manager + start, ((current + FocusWindow - start) / IntPtr.Size) + 1);
            var held = new Dictionary<int, long>(slots.Length);
            for (var i = 0; i < slots.Length; i++)
            {
                held[start + (i * IntPtr.Size)] = this.IsOwnElement(slots[i], manager) ? slots[i].ToInt64() : 0;
            }

            if (this.focusCheck.See(held) is not { } found)
            {
                return;
            }

            this.focusKnown = true;
            var shift = found - current;
            GameLayout.UiManager.CurrentOffset += shift;
            GameLayout.UiManager.LastFocusedOffset += shift;
            GameLayout.UiManager.HoveredOffset += shift;
            this.Record(FocusPart, this.measuredCurrent, found);
        }

        private static int MeasuredStateField(string field) => Marshal.OffsetOf<InGameStateOffset>(field).ToInt32();

        /// <summary>An element of this manager: the focus never holds another's (a stray pointer or a closed state's).</summary>
        private bool IsOwnElement(IntPtr element, IntPtr manager)
        {
            if (element == IntPtr.Zero)
            {
                return false;
            }

            if (manager != this.ownElementsManager)
            {
                this.ownElements.Clear();
                this.ownElementsManager = manager;
            }

            if (!this.ownElements.TryGetValue(element.ToInt64(), out var own))
            {
                own = this.elements.IsElement(element) &&
                      this.reader.TryRead<IntPtr>(element + GameLayout.Element.ManagerOffset, out var owner) && owner == manager;
                if (this.ownElements.Count >= MaxOwnElements)
                {
                    this.ownElements.Clear();
                }

                this.ownElements[element.ToInt64()] = own;
            }

            return own;
        }

        /// <summary>Whether the element's parents end at the root its own UI manager holds (<see cref="LayerSlots.LeadsBack"/>).</summary>
        private bool LeadsToManagerRoot(IntPtr element) => LayerSlots.LeadsBack(
            element.ToInt64(),
            child => this.elements.TryReadElement(new IntPtr(child), out var info) ? info.Raw.ParentPtr.ToInt64() : 0,
            top => this.reader.TryRead<IntPtr>(new IntPtr(top) + GameLayout.Element.ManagerOffset, out var manager) && this.reader.IsValidAddress(manager) ? manager.ToInt64() : 0,
            manager => this.reader.TryRead<IntPtr>(new IntPtr(manager) + GameLayout.UiManager.RootOffset, out var held) ? held.ToInt64() : 0);

        /// <summary>An element whose children carry at least two of the known layer names.</summary>
        private bool IsLayerContainer(IntPtr candidate)
        {
            if (!this.reader.IsValidAddress(candidate) || !this.elements.TryReadElement(candidate, out var info))
            {
                return false;
            }

            var named = 0;
            foreach (var child in this.elements.ReadChildren(info))
            {
                if (this.elements.TryReadElement(child, out var layer) && KnownLayerNames.Contains(layer.Name) && ++named >= MinLayerNames)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Control names: the root or its layers must carry a known name (<paramref name="conclusive"/>: in game, where the HUD always does).</summary>
        public void ProbeNames(IntPtr root, bool conclusive)
        {
            var now = Environment.TickCount64;
            if (this.namesKnown || root == IntPtr.Zero || now - this.namesTriedAt < RetryMs)
            {
                return;
            }

            this.namesTriedAt = now;
            if ((OffsetSearch.First(this.measuredName, Radius, Step, this.NameFits(root), Step) ?? this.WideName(root)) is not { } found)
            {
                if (conclusive)
                {
                    this.report.Missing("element names");
                }

                return;
            }

            var shift = found - this.measuredName;
            GameLayout.Element.NameOffset = found;
            GameLayout.Element.FontOffset = this.measuredFont + shift;
            GameLayout.Element.ManagerOffset = this.measuredManager + shift;
            GameLayout.Element.TooltipsOffset = this.measuredTooltips + shift;
            this.namesKnown = true;
            this.Record("element names", this.measuredName, found);
            this.JudgeManagerField(root);
        }

        /// <summary>
        ///     The manager pointer follows the control name; here it is checked on its own (<see cref="ElementFieldSearch.Manager"/>):
        ///     the first elements of the tree share one pointer there and that object holds their root. Found elsewhere, it is
        ///     read from there; with nothing to judge it on it stays where the name's shift put it, and nothing is said.
        /// </summary>
        private void JudgeManagerField(IntPtr root)
        {
            var blocks = this.WideBlocks(this.NamedNodes(root).Take(MaxManagerNodes), GameLayout.Element.ManagerOffset + IntPtr.Size);
            bool HoldsRoot(long pointer)
            {
                var manager = new IntPtr(pointer);
                return manager != root && this.reader.IsValidAddress(manager) && !this.elements.IsElement(manager) &&
                       Array.IndexOf(this.reader.ReadArray<IntPtr>(manager, ManagerScanBytes / IntPtr.Size), root) >= 0;
            }

            if (ElementFieldSearch.Manager(blocks, HoldsRoot, GameLayout.Element.ManagerOffset, Radius, Step) is { } found)
            {
                GameLayout.Element.ManagerOffset = found;
                this.Record(ManagerFieldPart, this.measuredManager, found);
            }
        }

        /// <summary>Label texts: the tagged text and two equal stripped copies (<see cref="UiElementReader.TryReadText(IntPtr, out string)"/>).</summary>
        public void ProbeText(IntPtr root, bool conclusive)
        {
            var now = Environment.TickCount64;
            if (this.textKnown || root == IntPtr.Zero || now - this.textTriedAt < RetryMs)
            {
                return;
            }

            this.textTriedAt = now;
            if (this.Leaves(root).Where(leaf => this.elements.TryReadText(leaf, out var text) && text.Length > 0).Take(MinTextHits).Count() >= MinTextHits)
            {
                this.textKnown = true;
                this.report.Found("element texts");
                return;
            }

            if ((OffsetSearch.First(0, Radius, Step, this.TextFits(root), -Radius) ?? this.WideTextShift(root)) is not { } found)
            {
                if (conclusive)
                {
                    this.report.Missing("element texts");
                }

                return;
            }

            GameLayout.Element.TextWithTagsOffset = this.measuredTagged + found;
            GameLayout.Element.TextOffset = this.measuredText + found;
            GameLayout.Element.TextCopyOffset = this.measuredCopy + found;
            GameLayout.Element.TextColorOffset = this.measuredColor + found;
            this.textKnown = true;
            this.Record("element texts", this.measuredText, this.measuredText + found);
        }

        /// <summary>
        ///     Image textures: enough images under the root lead through the texture pointer to an art key, and the smaller
        ///     image class's pointer follows it (<paramref name="conclusive"/>: in game, where the HUD always shows images).
        /// </summary>
        public void ProbeTextures(IntPtr root, bool conclusive)
        {
            var now = Environment.TickCount64;
            if (this.texturesKnown || root == IntPtr.Zero || now - this.texturesTriedAt < RetryMs)
            {
                return;
            }

            this.texturesTriedAt = now;
            var fits = this.TextureFits(root);
            if (fits(GameLayout.Image.TextureOffset))
            {
                this.texturesKnown = true;
                this.report.Found("image textures");
                return;
            }

            if ((OffsetSearch.First(this.measuredTexture, Radius, Step, fits, Step) ?? this.WideTexture(root)) is not { } found)
            {
                if (conclusive)
                {
                    this.report.Missing("image textures");
                }

                return;
            }

            GameLayout.Image.TextureOffset = found;
            GameLayout.Image.AltTextureOffset = this.measuredAltTexture + (found - this.measuredTexture);
            this.texturesKnown = true;
            this.Record("image textures", this.measuredTexture, found);
        }

        /// <summary>
        ///     A UI manager holds <paramref name="root"/> (the root of elements that point at it) where measured, or the slot
        ///     that holds it is adopted and the focus, hover and pad fields follow it.
        /// </summary>
        public bool AdoptManager(IntPtr manager, IntPtr root)
        {
            if (root == IntPtr.Zero || !this.reader.IsValidAddress(manager) || this.elements.IsElement(manager))
            {
                return false;
            }

            if (this.reader.TryRead<IntPtr>(manager + GameLayout.UiManager.RootOffset, out var held) && held == root)
            {
                if (!this.managerKnown)
                {
                    this.managerKnown = true;
                    this.report.Found("UI manager");
                }

                return true;
            }

            var slots = this.reader.ReadArray<IntPtr>(manager, ManagerScanBytes / IntPtr.Size);
            if (OffsetSearch.First(this.measuredRoot, ManagerScanBytes, Step, offset => offset / IntPtr.Size < slots.Length && slots[offset / IntPtr.Size] == root) is not { } found)
            {
                return false;
            }

            var shift = found - this.measuredRoot;
            GameLayout.UiManager.RootOffset = found;
            GameLayout.UiManager.CurrentOffset = this.measuredCurrent + shift;
            GameLayout.UiManager.LastFocusedOffset = this.measuredLastFocused + shift;
            GameLayout.UiManager.HoveredOffset = this.measuredHovered + shift;
            GameLayout.UiManager.PadStateOffset = this.measuredPad + shift;
            this.managerKnown = true;
            this.report.Moved("UI manager", this.measuredRoot, found);
            return true;
        }

        /// <summary>
        ///     Entity names (Render component): judged on monsters, characters and players, half of which must read. Settled only
        ///     by a batch that reads (once: a second guess must not flap); lost once two different batches read none.
        /// </summary>
        public void ProbeRenderNames(IReadOnlyList<IntPtr> renders)
        {
            var now = Environment.TickCount64;
            if (this.renderKnown || renders.Count < MinRenders || now - this.renderTriedAt < RetryMs)
            {
                return;
            }

            this.renderTriedAt = now;
            int Named(int offset) => renders.Count(render => this.elements.ReadWStringStructAt(render + offset) is { Length: > 0 } name && name.Any(char.IsLetter));
            bool Fits(int offset) => Named(offset) * 2 >= renders.Count;
            if (OffsetSearch.First(this.measuredRenderName, Radius, Step, Fits, Step) is not { } found)
            {
                // A batch read while its entities were being freed reads no names on a healthy game.
                if (this.renderMisses.Against(LossWitnesses.KeyOf(renders.Select(render => render.ToInt64()))))
                {
                    this.report.Missing("entity names");
                }

                return;
            }

            this.renderKnown = true;
            this.renderMisses.Clear();
            GameLayout.RenderComponent.NameOffset = found;
            this.Record("entity names", this.measuredRenderName, found);
        }

        /// <summary>How far the label text fields are from where they were measured (the base control's growth, most likely).</summary>
        public int TextShift => GameLayout.Element.TextOffset - this.measuredText;

        /// <summary>A new game process: everything is checked again from the measured layout.</summary>
        public void Reset()
        {
            (this.namesKnown, this.textKnown, this.managerKnown, this.renderKnown, this.texturesKnown) = (false, false, false, false, false);
            (this.elementBaseKnown, this.layerRootsKnown, this.focusKnown, this.focusCheck) = (false, false, false, null);
            this.elementBaseLostAt = null;
            this.elementBaseTried.Clear();
            this.ownElements.Clear();
            this.ownElementsManager = IntPtr.Zero;
            this.layerRootsTriedAt = long.MinValue / 2;
            this.namesTriedAt = this.textTriedAt = this.texturesTriedAt = this.renderTriedAt = long.MinValue / 2;
            (this.wideNameTries, this.wideTextTries, this.wideTextureTries) = (0, 0, 0);
            this.renderMisses.Clear();
        }

        /// <summary>
        ///     Research (dev command "layout"): every search started off target (a field moved by <see cref="SelfTestShift"/>)
        ///     must come back to where the layout is now; "FAIL" names a search that would not survive such an update.
        /// </summary>
        public string SelfTest(IntPtr root, IntPtr manager)
        {
            var names = OffsetSearch.First(GameLayout.Element.NameOffset + SelfTestShift, Radius, Step, this.NameFits(root), Step);
            var textShift = GameLayout.Element.TextOffset - this.measuredText;
            var text = OffsetSearch.First(textShift + SelfTestShift, Radius, Step, this.TextFits(root), -Radius);
            var slots = this.reader.ReadArray<IntPtr>(manager, ManagerScanBytes / IntPtr.Size);
            var rootSlot = OffsetSearch.First(GameLayout.UiManager.RootOffset + SelfTestShift, ManagerScanBytes, Step,
                offset => offset / IntPtr.Size < slots.Length && slots[offset / IntPtr.Size] == root);
            var textures = OffsetSearch.First(GameLayout.Image.TextureOffset + SelfTestShift, Radius, Step, this.TextureFits(root), Step);
            return string.Join("; ", SelfTestLine("element names", GameLayout.Element.NameOffset, names), SelfTestLine("element texts", textShift, text),
                SelfTestLine("UI manager", GameLayout.UiManager.RootOffset, rootSlot), SelfTestLine("image textures", GameLayout.Image.TextureOffset, textures),
                ElementBaseSelfTest(root), this.focusKnown ? "UI focus ok" : "UI focus not judged yet (move through a menu)",
                this.layerRootsKnown ? "in-game layer roots ok" : "in-game layer roots not judged yet");
        }

        /// <summary>The host's element base search started <see cref="SelfTestShift"/> off every field must come back where they read now.</summary>
        private static string ElementBaseSelfTest(IntPtr root)
        {
            var measured = GameReader.Utils.UiElementFields.Measured;
            var now = new GameReader.Utils.UiElementFields(
                Follow(measured.Self), Follow(measured.Children), Follow(measured.Parent), Follow(measured.Position), Follow(measured.Flags), Follow(measured.Size));
            var off = new GameReader.Utils.UiElementFields(now.Self + SelfTestShift, now.Children + SelfTestShift, now.Parent + SelfTestShift,
                now.Position + SelfTestShift, now.Flags + SelfTestShift, now.Size + SelfTestShift);
            var found = GameReader.Utils.OffsetHealer.FindUiElementBase(root, off);
            return string.Join("; ", SelfTestLine("element tree", now.Parent, found.Parent), SelfTestLine("element rects", now.Size, found.Size),
                SelfTestLine("element flags", now.Flags, found.Flags));

            static int Follow(int field) => GameReader.Utils.HostStructs.Follow<GameOffsets.Objects.UiElement.UiElementBaseOffset>(field);
        }

        /// <summary>How far a field is pretended to have moved in <see cref="SelfTest"/>.</summary>
        public const int SelfTestShift = 0x18;

        public static string SelfTestLine(string part, int now, int? found) =>
            found == now ? $"{part} ok" : $"{part} FAIL (found {(found is { } at ? $"0x{at:X}" : "nothing")}, is 0x{now:X})";

        /// <summary>The control name far from where measured: the one place where two elements carry two different known names.</summary>
        private int? WideName(IntPtr root)
        {
            if (this.wideNameTries++ >= MaxWideTries)
            {
                return null;
            }

            var blocks = this.WideBlocks(this.NamedNodes(root), this.measuredName + WStringDecoder.Size);
            return ElementFieldSearch.Name(blocks, (block, at) => WStringDecoder.TryDecode(this.reader, block, at, MaxLength, false), KnownLayerNames, this.measuredName, Radius, Step);
        }

        /// <summary>The label texts far from where measured: the one shift under which enough labels read their three fields.</summary>
        private int? WideTextShift(IntPtr root)
        {
            if (this.wideTextTries++ >= MaxWideTries)
            {
                return null;
            }

            var blocks = this.WideBlocks(this.Leaves(root).Take(MaxLeaves), this.measuredCopy + WStringDecoder.Size);
            var last = GameLayout.Element.ProbeBytes - this.measuredCopy - WStringDecoder.Size;
            return ElementFieldSearch.TextShift(blocks, this.TextAt, Radius, Step, -this.measuredTagged, last + Step, MinTextHits);
        }

        /// <summary>The image texture far from where measured: the one place that leads enough images to an art key.</summary>
        private int? WideTexture(IntPtr root)
        {
            if (this.wideTextureTries++ >= MaxWideTries)
            {
                return null;
            }

            var blocks = this.WideBlocks(this.Leaves(root).Take(MaxWideLeaves), this.measuredTexture + IntPtr.Size);
            return ElementFieldSearch.Texture(blocks, pointer => this.elements.ReadTextureKey(new IntPtr(pointer)) != null, this.measuredTexture, Radius, Step, MinTextureHits);
        }

        /// <summary>Each element's bytes up to <see cref="GameLayout.Element.ProbeBytes"/>, as far as they read (at least <paramref name="floor"/>).</summary>
        private List<byte[]> WideBlocks(IEnumerable<IntPtr> elements, int floor)
        {
            var blocks = new List<byte[]>();
            foreach (var element in elements)
            {
                // Host reads are all or nothing: an element near the end of its page reads shorter.
                for (var size = GameLayout.Element.ProbeBytes; size >= floor; size /= 2)
                {
                    if (this.reader.ReadBytes(element, size) is { Length: > 0 } block)
                    {
                        blocks.Add(block);
                        break;
                    }
                }
            }

            return blocks;
        }

        /// <summary>The root or one of its layers carries a known control name at the offset.</summary>
        private Func<int, bool> NameFits(IntPtr root)
        {
            var blocks = this.NamedNodes(root).Select(node => this.reader.ReadBytes(node, this.measuredName + Radius + WStringDecoder.Size)).Where(block => block.Length > 0).ToList();
            return offset => blocks.Any(block => WStringDecoder.TryDecode(this.reader, block, offset, MaxLength, false) is { } name && KnownLayerNames.Contains(name));
        }

        /// <summary>The root, its children and theirs: the elements that carry the known names.</summary>
        private List<IntPtr> NamedNodes(IntPtr root)
        {
            // In game the named layers sit one level lower than at login (root, layer container, layers).
            var nodes = new List<IntPtr> { root };
            if (this.elements.TryReadElement(root, out var rootInfo))
            {
                foreach (var child in this.elements.ReadChildren(rootInfo))
                {
                    nodes.Add(child);
                    if (nodes.Count < MaxNamedNodes && this.elements.TryReadElement(child, out var childInfo))
                    {
                        nodes.AddRange(this.elements.ReadChildren(childInfo).Take(MaxNamedNodes - nodes.Count));
                    }
                }
            }

            return nodes;
        }

        /// <summary>Enough labels under the root read through the text fields moved by the shift.</summary>
        private Func<int, bool> TextFits(IntPtr root)
        {
            var blocks = this.Leaves(root).Take(MaxLeaves).Select(leaf => this.reader.ReadBytes(leaf, this.measuredCopy + Radius + WStringDecoder.Size)).Where(block => block.Length > 0).ToList();
            return shift => blocks.Count(block => this.TextAt(block, shift)) >= MinTextHits;
        }

        /// <summary>
        ///     Enough images under the root lead through the pointer at the offset to an art key, and far fewer through the
        ///     next slot: one step after the smaller class's pointer is the larger class's, which must not be taken for it.
        /// </summary>
        private Func<int, bool> TextureFits(IntPtr root)
        {
            var blocks = this.Leaves(root).Take(MaxLeaves).Select(leaf => this.reader.ReadBytes(leaf, this.measuredTexture + Radius + Step + IntPtr.Size)).Where(block => block.Length > 0).ToList();
            var hits = new Dictionary<int, int>();
            int Hits(int offset)
            {
                if (!hits.TryGetValue(offset, out var count))
                {
                    count = offset < 0 ? 0 : blocks.Count(block => offset + IntPtr.Size <= block.Length &&
                        this.elements.ReadTextureKey(MemoryMarshal.Read<IntPtr>(block.AsSpan(offset))) != null);
                    hits[offset] = count;
                }

                return count;
            }

            return offset => Hits(offset) is var count && count >= MinTextureHits && Hits(offset + Step) * 2 < count;
        }

        private bool TextAt(byte[] block, int shift)
        {
            var tagged = WStringDecoder.TryDecode(this.reader, block, this.measuredTagged + shift, MaxLength, true);
            var text = WStringDecoder.TryDecode(this.reader, block, this.measuredText + shift, MaxLength, false);
            return tagged != null && text != null && text == WStringDecoder.TryDecode(this.reader, block, this.measuredCopy + shift, MaxLength, false);
        }

        /// <summary>Childless elements under <paramref name="root"/>, breadth first and lazily, hidden ones too (their texts stay).</summary>
        private IEnumerable<IntPtr> Leaves(IntPtr root)
        {
            var queue = new Queue<IntPtr>();
            queue.Enqueue(root);
            for (var walked = 0; queue.Count > 0 && walked < MaxWalkNodes; walked++)
            {
                if (!this.elements.TryReadElement(queue.Dequeue(), out var info))
                {
                    continue;
                }

                var children = this.elements.ReadChildren(info);
                if (children.Length == 0)
                {
                    yield return info.Address;
                }

                foreach (var child in children)
                {
                    queue.Enqueue(child);
                }
            }
        }

        private void Record(string part, int measured, int found)
        {
            if (found == measured)
            {
                this.report.Found(part);
            }
            else
            {
                this.report.Moved(part, measured, found);
            }
        }
    }
}
