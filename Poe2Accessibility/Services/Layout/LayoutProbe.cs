namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using GameOffsets.Objects.States;

    /// <summary>
    ///     Finds the memory layout a game update can move (docs/compatibility.md). Each field is first tried where it was
    ///     measured and only looked for around it when it no longer holds what it must; fields of one structure that
    ///     move together follow their anchor (font, manager and tooltips the control name, label fields the text, manager
    ///     fields the root pointer); the element base itself is the host's judgment (<see cref="ProbeElementBase"/>).
    ///     Outcomes go to the <see cref="CompatibilityReport"/>.
    /// </summary>
    public sealed partial class LayoutProbe
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

        /// <summary>
        ///     Looks, each <see cref="RetryMs"/> after the last, that must miss the layer roots before they are said lost: the
        ///     first look comes on the first frame in game, when the layers may not be built yet.
        /// </summary>
        private const int LayerRootLooksToSay = 3;

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
        private readonly LossWitnesses nameMisses = new();
        private readonly LossWitnesses textMisses = new();
        private readonly LossWitnesses textureMisses = new();
        private readonly LossWitnesses layerRootMisses = new(LayerRootLooksToSay);
        private const int LookAgainEveryMs = 1000;
        private bool missedInGame;
        private IntPtr pendingState;
        private long nextLookAgainAt;
        private long looks;
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
            (this.missedInGame, this.pendingState, this.nextLookAgainAt) = (false, IntPtr.Zero, 0);
            foreach (var misses in new[] { this.nameMisses, this.textMisses, this.textureMisses, this.layerRootMisses })
            {
                misses.Clear();
            }
            this.renderMisses.Clear();
        }
    }
}
