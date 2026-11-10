// <copyright file="ImportantUiElements.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.States.InGameStateObjects
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using CoroutineEvents;
    using GameReader.Cache;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;
    using GameOffsets.Objects.UiElement;
    using RemoteEnums;
    using UiElement;

    /// <summary>
    ///     This is actually UiRoot main child which contains
    ///     all the UiElements (100+). Normally it's at index 1 of UiRoot.
    ///     This class is created because traversing childrens of
    ///     UiRoot is a slow process that requires lots of memory reads.
    ///     Drawback:
    ///     1: Every league/patch the offsets needs to be updated.
    ///     Offsets used over here are very unstable.
    ///     2: More UiElements we are tracking = More memory read
    ///     every X seconds just to update IsVisible :(.
    /// </summary>
    public class ImportantUiElements : RemoteObjectBase
    {
        private static readonly int[] WorldMapPanelChildPath = { 22, 0 };
        private static readonly int[] LargeMapViewportChildPath = { 6, 0 };
        private static readonly int[] MiniMapViewportChildPath = { 6, 1 };
        private static readonly int[] PassiveSkillTreeNodesChildPath = { 24, 2 };
        private static readonly int[] Act1PanelChildPath = { 22, 0, 0 };
        private static readonly int[] Act2PanelChildPath = { 22, 0, 1 };
        private static readonly int[] Act3PanelChildPath = { 22, 0, 2 };
        private static readonly int[] Act4PanelChildPath = { 22, 0, 3 };
        private static readonly int[] InterludePanelChildPath = { 22, 0, 5 };
        private static readonly int[] AtlasPanelChildPath = { 22, 0, 6 };
        private static readonly int[] AtlasSkillsPanelChildPath = { 25, 0 };
        private static readonly int[] CurrencyExchangePanelChildPath = { 114, 20, 6, 1 };
        private static readonly int[] GemcuttingPanelChildPath = { 53, 3 };
        private static readonly int[] SupportGemcuttingPanelChildPath = { 54, 3 };
        private static readonly int[] LeftPanelCoopPath = { 22 };
        private static readonly int[] RightPanelCoopPath = { 23 };
        private static readonly int[] TempleConsoleChildPath = { 64, 0 };
        private readonly UiElementParents rootCache;
        private readonly UiElementParents passiveSkillTreeCache;
        /// <summary>
        ///     Passive skill tree node Parent UI element.
        ///     GameUi -> child 24 -> child 2.
        /// </summary>
        private UiElementBase passiveskilltreenodes;

        /// <summary>
        ///     Sekhemas Trial Map panel.
        ///     UiRoot MainChild -> child 1 -> child 84 -> child 0
        /// </summary>
        private UiElementBase sekhemasTrialMapPanel;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ImportantUiElements" /> class.
        /// </summary>
        /// <param name="address">
        ///     UiRoot 1st child address (starting from 0)
        ///     or <see cref="IntPtr.Zero" /> in case UiRoot has no child.
        /// </param>
        internal ImportantUiElements(IntPtr address)
            : base(address)
        {
            this.rootCache = new(null, GameStateTypes.InGameState, GameStateTypes.EscapeState);
            this.passiveSkillTreeCache = new(this.rootCache, GameStateTypes.InGameState, GameStateTypes.EscapeState);

            this.passiveskilltreenodes = new(IntPtr.Zero, this.rootCache);
            this.sekhemasTrialMapPanel = new(IntPtr.Zero, this.rootCache);
            this.LargeMap = new(IntPtr.Zero, this.rootCache);
            this.MiniMap = new(IntPtr.Zero, this.rootCache);
            this.WorldMapPanel = new(IntPtr.Zero, this.rootCache);
            this.Act1 = new(IntPtr.Zero, this.rootCache);
            this.Act2 = new(IntPtr.Zero, this.rootCache);
            this.Act3 = new(IntPtr.Zero, this.rootCache);
            this.Act4 = new(IntPtr.Zero, this.rootCache);
            this.Interlude = new(IntPtr.Zero, this.rootCache);
            this.Atlas = new(IntPtr.Zero, this.rootCache);
            this.AtlasSkillsPanel = new(IntPtr.Zero, this.rootCache);
            this.TempleConsole = new(IntPtr.Zero, this.rootCache);
            this.CurrencyExchangePanel = new(IntPtr.Zero, this.rootCache);
            this.GemcuttingPanel = new(IntPtr.Zero, this.rootCache);
            this.SupportGemcuttingPanel = new(IntPtr.Zero, this.rootCache);
            this.LeftPanel = new(IntPtr.Zero, this.rootCache);
            this.RightPanel = new(IntPtr.Zero, this.rootCache);
            this.ChatParent = new(IntPtr.Zero, this.rootCache);

            this.SkillTreeNodesUiElements = new();
        }

        /// <summary>
        ///     Reads the panels again; the mod reads its own UI tree, so this runs only when the struct sweep asks
        ///     (it used to run every frame).
        /// </summary>
        internal void Refresh()
        {
            if (this.Address != IntPtr.Zero &&
                Core.States.GameCurrentState is GameStateTypes.InGameState or GameStateTypes.EscapeState)
            {
                this.UpdateData(false);
            }
        }

        /// <summary>
        ///     Gets the LargeMap UiElement.
        ///     UiRoot -> MainChild -> 3rd index -> 1nd index.
        /// </summary>
        public MapUiElement LargeMap { get; }

        /// <summary>
        ///     Gets the MiniMap UiElement.
        ///     UiRoot -> MainChild -> 3rd index -> 2nd index.
        /// </summary>
        public MapUiElement MiniMap { get; }

        /// <summary>
        ///     Gets the checkpoint / world-travel map panel UiElement.
        ///     It is only <see cref="UiElementBase.IsVisible" /> while that screen is open
        ///     (opened by interacting with a checkpoint). The in-area LargeMap stays visible
        ///     underneath it, so consumers gate on this to tell the two apart.
        ///     GameUi -> child 22 -> child 0.
        /// </summary>
        public UiElementBase WorldMapPanel { get; }

        /// <summary>
        ///     Gets the Act 1 tab under the checkpoint / world-travel map panel.
        ///     GameUi -> child 22 -> child 0 -> child 0.
        /// </summary>
        public UiElementBase Act1 { get; }

        /// <summary>
        ///     Gets the Act 2 tab under the checkpoint / world-travel map panel.
        ///     GameUi -> child 22 -> child 0 -> child 1.
        /// </summary>
        public UiElementBase Act2 { get; }

        /// <summary>
        ///     Gets the Act 3 tab under the checkpoint / world-travel map panel.
        ///     GameUi -> child 22 -> child 0 -> child 2.
        /// </summary>
        public UiElementBase Act3 { get; }

        /// <summary>
        ///     Gets the Act 4 tab under the checkpoint / world-travel map panel.
        ///     GameUi -> child 22 -> child 0 -> child 3.
        /// </summary>
        public UiElementBase Act4 { get; }

        /// <summary>
        ///     Gets the Interlude tab under the checkpoint / world-travel map panel.
        ///     GameUi -> child 22 -> child 0 -> child 5.
        /// </summary>
        public UiElementBase Interlude { get; }

        /// <summary>
        ///     Gets the Atlas tab under the checkpoint / world-travel map panel.
        ///     GameUi -> child 22 -> child 0 -> child 6.
        /// </summary>
        public UiElementBase Atlas { get; }

        /// <summary>
        ///     Gets the Atlas Skills panel UiElement.
        ///     It is only <see cref="UiElementBase.IsVisible" /> while that panel is open.
        ///     GameUi -> child 25 -> child 0 (child 25 itself is always visible).
        /// </summary>
        public UiElementBase AtlasSkillsPanel { get; }

        /// <summary>
        ///     Gets the Temple Console panel UiElement.
        ///     It is only <see cref="UiElementBase.IsVisible" /> while that panel is open.
        ///     GameUi -> child 64 -> child 0.
        /// </summary>
        public UiElementBase TempleConsole { get; }

        /// <summary>
        ///     Gets the currently-open left-side panel UiElement (character, skills, etc.).
        ///     It is only <see cref="UiElementBase.IsVisible" /> while such a panel is open;
        ///     the backing pointer is null when no left panel is open.
        ///     UiRoot manager -> 0x6D8.
        /// </summary>
        public UiElementBase LeftPanel { get; }

        /// <summary>
        ///     Gets the currently-open right-side panel UiElement (inventory, vendor/shop, stash, etc.).
        ///     It is only <see cref="UiElementBase.IsVisible" /> while such a panel is open;
        ///     the backing pointer is null when no right panel is open.
        ///     UiRoot manager -> 0x6E0.
        /// </summary>
        public UiElementBase RightPanel { get; }

        /// <summary>
        ///     Gets the Currency Exchange item-list panel.
        ///     Its visibility reliably indicates that the Currency Exchange screen is open.
        ///     GameUi -> child 114 -> 20 -> 6 -> 1.
        /// </summary>
        public UiElementBase CurrencyExchangePanel { get; }

        /// <summary>
        ///     Gets the Gemcutting panel visibility gate.
        ///     GameUi -> child 53 -> child 3. Child 53 itself remains visible while the panel is closed.
        /// </summary>
        public UiElementBase GemcuttingPanel { get; }

        /// <summary>
        ///     Gets the Support Gemcutting panel visibility gate.
        ///     GameUi -> child 54 -> child 3. Child 54 itself remains visible while the panel is closed.
        /// </summary>
        public UiElementBase SupportGemcuttingPanel { get; }

        /// <summary>
        ///     Gets the Chat UiElement parent.
        ///     UiRoot -> MainChild -> this index keeps moving around
        /// </summary>
        public ChatParentUiElement ChatParent { get; }

        /// <summary>
        ///     Gets the skill tree nodes UI Elements.
        ///     GameUi -> child 24 -> child 2 -> all children.
        /// </summary>
        public List<SkillTreeNodeUiElement> SkillTreeNodesUiElements { get; }

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            this.passiveskilltreenodes.Address = IntPtr.Zero;
            this.sekhemasTrialMapPanel.Address = IntPtr.Zero;
            this.MiniMap.Address = IntPtr.Zero;
            this.LargeMap.Address = IntPtr.Zero;
            this.WorldMapPanel.Address = IntPtr.Zero;
            this.Act1.Address = IntPtr.Zero;
            this.Act2.Address = IntPtr.Zero;
            this.Act3.Address = IntPtr.Zero;
            this.Act4.Address = IntPtr.Zero;
            this.Interlude.Address = IntPtr.Zero;
            this.Atlas.Address = IntPtr.Zero;
            this.AtlasSkillsPanel.Address = IntPtr.Zero;
            this.TempleConsole.Address = IntPtr.Zero;
            this.CurrencyExchangePanel.Address = IntPtr.Zero;
            this.GemcuttingPanel.Address = IntPtr.Zero;
            this.SupportGemcuttingPanel.Address = IntPtr.Zero;
            this.LeftPanel.Address = IntPtr.Zero;
            this.RightPanel.Address = IntPtr.Zero;
            this.ChatParent.Address = IntPtr.Zero;
            this.SkillTreeNodesUiElements.Clear();
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            this.UpdateParentsCache();
            var reader = Core.Process.Handle;
            var data1 = reader.ReadMemory<ImportantUiElementsOffsets>(this.Address);
            if (Core.GHSettings.EnableControllerMode)
            {
                this.UpdateMapAddresses();
                this.UpdateWorldMapPanelAddresses();
                if (this.IsCoopMode())
                {
                    this.LeftPanel.Address = ResolveChildAddress(this.Address, LeftPanelCoopPath);
                    this.RightPanel.Address = ResolveChildAddress(this.Address, RightPanelCoopPath);
                }
                else
                {
                    this.LeftPanel.Address = IntPtr.Zero;
                    this.RightPanel.Address = IntPtr.Zero;
                }
                this.ChatParent.Address = IntPtr.Zero;
                this.passiveskilltreenodes.Address = IntPtr.Zero;
                this.sekhemasTrialMapPanel.Address = IntPtr.Zero;
            }
            else
            {
                this.UpdateMapAddresses();
                this.UpdateWorldMapPanelAddresses();
                this.LeftPanel.Address = ValidUiElementOrZero(data1.LeftPanelPtr);
                this.RightPanel.Address = ValidUiElementOrZero(data1.RightPanelPtr);
                this.ChatParent.Address = data1.ChatParentPtr;
                this.passiveskilltreenodes.Address = ResolveChildAddress(this.Address, PassiveSkillTreeNodesChildPath);
                this.updatePassiveSkillTreeData();
                {
                    var mgrOff = reader.ReadMemory<UiElementBaseOffset>(this.Address);
                    var parentAddr = reader.ReadMemory<IntPtr>(mgrOff.ChildrensPtr.First + (84 * IntPtr.Size));
                    var parentOff = reader.ReadMemory<UiElementBaseOffset>(parentAddr);
                    this.sekhemasTrialMapPanel.Address =
                      ValidUiElementOrZero(reader.ReadMemory<IntPtr>(parentOff.ChildrensPtr.First));
                }
            }

            this.CurrencyExchangePanel.Address = ResolveChildAddress(this.Address, CurrencyExchangePanelChildPath);
            this.GemcuttingPanel.Address = ResolveChildAddress(this.Address, GemcuttingPanelChildPath);
            this.SupportGemcuttingPanel.Address = ResolveChildAddress(this.Address, SupportGemcuttingPanelChildPath);
        }

        private void UpdateMapAddresses()
        {
            this.LargeMap.Address = ResolveChildAddress(this.Address, LargeMapViewportChildPath);
            this.MiniMap.Address = ResolveChildAddress(this.Address, MiniMapViewportChildPath);
        }

        private void UpdateWorldMapPanelAddresses()
        {
            this.WorldMapPanel.Address = ResolveChildAddress(this.Address, WorldMapPanelChildPath);
            this.Act1.Address = ResolveChildAddress(this.Address, Act1PanelChildPath);
            this.Act2.Address = ResolveChildAddress(this.Address, Act2PanelChildPath);
            this.Act3.Address = ResolveChildAddress(this.Address, Act3PanelChildPath);
            this.Act4.Address = ResolveChildAddress(this.Address, Act4PanelChildPath);
            this.Interlude.Address = ResolveChildAddress(this.Address, InterludePanelChildPath);
            this.Atlas.Address = ResolveChildAddress(this.Address, AtlasPanelChildPath);
            this.AtlasSkillsPanel.Address = ResolveChildAddress(this.Address, AtlasSkillsPanelChildPath);
            this.TempleConsole.Address = ResolveChildAddress(this.Address, TempleConsoleChildPath);
        }

        private bool IsCoopMode()
        {
            if (!Core.GHSettings.EnableControllerMode)
            {
                return false;
            }

            var inGameState = Core.States.InGameStateObject;
            if (inGameState == null)
            {
                return false;
            }

            var currentArea = inGameState.CurrentAreaInstance;
            if (currentArea == null || currentArea.Address == IntPtr.Zero)
            {
                return false;
            }

            foreach (var entity in currentArea.AwakeEntities.Values)
            {
                if (entity.EntitySubtype == GameReader.RemoteEnums.Entity.EntitySubtypes.PlayerOther)
                {
                    return true;
                }
            }

            return false;
        }

        private static IntPtr ResolveChildAddress(IntPtr rootAddress, int[] childPath)
        {
            if (rootAddress == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var reader = Core.Process.Handle;
            var currentAddress = rootAddress;
            foreach (var childIndex in childPath)
            {
                if (childIndex < 0)
                {
                    return IntPtr.Zero;
                }

                var data = reader.ReadMemory<UiElementBaseOffset>(currentAddress);
                var childCount = data.ChildrensPtr.TotalElements(IntPtr.Size);
                if (data.ChildrensPtr.First == IntPtr.Zero || childIndex >= childCount)
                {
                    return IntPtr.Zero;
                }

                currentAddress = reader.ReadMemory<IntPtr>(data.ChildrensPtr.First + (childIndex * IntPtr.Size));
                if (currentAddress == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }
            }

            // Only hand back addresses that are actually Ui elements (see ValidUiElementOrZero).
            return ValidUiElementOrZero(currentAddress);
        }

        // Returns the address only if it points to a real Ui element (its self-pointer matches),
        // otherwise IntPtr.Zero. The panel UiElementBase instances use forceUpdate=true, so assigning
        // a non-Ui address (a stale/garbage pointer, or a fixed child-index path landing on different
        // memory for some screens) makes UiElementBase.UpdateData throw — caught and re-logged by
        // Address.set every frame. Validating with the same self-pointer check keeps that off the log.
        private static IntPtr ValidUiElementOrZero(IntPtr address)
        {
            if (address == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return Core.Process.Handle.TryReadMemory<UiElementBaseOffset>(address, out var data) &&
                   data.Self == address
                ? address
                : IntPtr.Zero;
        }

        private void updatePassiveSkillTreeData()
        {
            if (this.passiveskilltreenodes.IsVisible)
            {
                this.AddOrUpdateSkillNodes();
            }
            else
            {
                this.ClearSkillNodes();
            }
        }

        private void ClearSkillNodes()
        {
            this.SkillTreeNodesUiElements.Clear();
            this.passiveSkillTreeCache.Clear();
        }

        private void AddOrUpdateSkillNodes()
        {
            if (this.SkillTreeNodesUiElements.Count == 0)
            {
                var currentChild = new UiElementBase(IntPtr.Zero, this.passiveSkillTreeCache);
                for (var i = 3; i < this.passiveskilltreenodes.TotalChildrens; i++)
                {
                    currentChild.Address = this.passiveskilltreenodes[i]!.Address;
                    if (!currentChild.IsVisible)
                    {
                        break;
                    }

                    this.AddSkillTreeNodeUiElementRecursive(currentChild);
                }
            }
            else
            {
                Parallel.For(0, this.SkillTreeNodesUiElements.Count, (i) =>
                {
                    this.SkillTreeNodesUiElements[i].Address = this.SkillTreeNodesUiElements[i].Address;
                });
            }
        }

        private void AddSkillTreeNodeUiElementRecursive(UiElementBase uie)
        {
            if (uie.TotalChildrens > 0)
            {
                for (var i = 0; i < uie.TotalChildrens; i++)
                {
                    this.AddSkillTreeNodeUiElementRecursive(uie[i]!);
                }
            }
            else
            {
                var skillNode = new SkillTreeNodeUiElement(uie.Address, this.passiveSkillTreeCache);
                if (skillNode.SkillGraphId != 0)
                {
                    this.SkillTreeNodesUiElements.Add(skillNode);
                }
            }
        }

        private void UpdateParentsCache()
        {
            this.rootCache.UpdateAllParentsParallel();
            this.passiveSkillTreeCache.UpdateAllParentsParallel();
        }
    }
}
