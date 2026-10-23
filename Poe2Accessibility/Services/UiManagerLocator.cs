namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using GameHelper.RemoteEnums;
    using GameOffsets.Objects.States;

    /// <summary>
    ///     Finds the UI manager of the current state (focus, root). Every UI element stores its manager at
    ///     GameLayout.Element.ManagerOffset, so any element pointer held by the state object leads to it;
    ///     in game InGameState holds it directly. Each state that owns UI (login, game, game menu) has its own.
    /// </summary>
    public sealed class UiManagerLocator
    {
        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;
        private readonly PluginLog log;
        private readonly Dictionary<IntPtr, IntPtr> cache = new();

        public UiManagerLocator(HostMemoryReader reader, UiElementReader elements, PluginLog log)
        {
            this.reader = reader;
            this.elements = elements;
            this.log = log;
        }

        public void Reset() => this.cache.Clear();

        /// <summary>Manager address for the given state object, or zero when none can be validated.</summary>
        public IntPtr Locate(GameStateTypes state, IntPtr stateObject)
        {
            if (stateObject == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (this.cache.TryGetValue(stateObject, out var cached) && this.IsManager(cached))
            {
                return cached;
            }

            var how = "InGameState";
            var manager = state == GameStateTypes.InGameState &&
                this.reader.TryRead<IntPtr>(stateObject + GameLayout.InGameState.UiManagerOffset, out var direct) && this.IsManager(direct)
                ? direct
                : IntPtr.Zero;
            if (manager == IntPtr.Zero)
            {
                manager = this.FromStateElements(stateObject, out how);
            }

            if (manager != IntPtr.Zero)
            {
                this.cache[stateObject] = manager;
                this.log.Info($"UI manager for {state}: 0x{manager.ToInt64():X} via {how}");
            }

            return manager;
        }

        /// <summary>
        ///     In game the screen layers are the children of GameHelper's UiRootStruct element (gamepad or mouse variant,
        ///     whichever the game filled); the manager's root is its parent (2026-10-23).
        /// </summary>
        public IntPtr InGameLayerContainer(IntPtr inGameState)
        {
            if (!this.reader.TryRead<InGameStateOffset>(inGameState, out var stateData))
            {
                return IntPtr.Zero;
            }

            return this.elements.IsElement(stateData.GamepadUiRootStructPtr) ? stateData.GamepadUiRootStructPtr
                : this.elements.IsElement(stateData.UiRootStructPtr) ? stateData.UiRootStructPtr
                : IntPtr.Zero;
        }

        /// <summary>The chat layer element in game (never read automatically); zero when unknown.</summary>
        public IntPtr InGameChatLayer(IntPtr inGameState) => this.InGameLayer(inGameState, GameLayout.InGameUi.ChatLayerOffset);

        public IntPtr InGameTutorialLayer(IntPtr inGameState) => this.InGameLayer(inGameState, GameLayout.InGameUi.TutorialLayerOffset);

        public IntPtr InGameHudLayer(IntPtr inGameState) => this.InGameLayer(inGameState, GameLayout.InGameUi.HudLayerOffset);

        /// <summary>A layer the container points to by field; only trusted when it really is one of its children.</summary>
        private IntPtr InGameLayer(IntPtr inGameState, int offset)
        {
            var container = this.InGameLayerContainer(inGameState);
            return container != IntPtr.Zero &&
                this.reader.TryRead<IntPtr>(container + offset, out var layer) &&
                this.elements.TryReadElement(layer, out var info) && info.Raw.ParentPtr == container
                ? layer
                : IntPtr.Zero;
        }

        /// <summary>The first element pointer stored in the state object names the manager.</summary>
        private IntPtr FromStateElements(IntPtr stateObject, out string how)
        {
            how = string.Empty;
            var slots = this.reader.ReadArray<IntPtr>(stateObject, GameLayout.StateObjectScanBytes / IntPtr.Size);
            for (var i = 0; i < slots.Length; i++)
            {
                if (this.reader.IsValidAddress(slots[i]) && this.elements.IsElement(slots[i]) &&
                    this.reader.TryRead<IntPtr>(slots[i] + GameLayout.Element.ManagerOffset, out var manager) && this.IsManager(manager))
                {
                    how = $"element at state+0x{i * IntPtr.Size:X}";
                    return manager;
                }
            }

            return IntPtr.Zero;
        }

        private bool IsManager(IntPtr candidate) =>
            this.reader.IsValidAddress(candidate) &&
            this.reader.TryRead<IntPtr>(candidate + GameLayout.UiManager.RootOffset, out var root) &&
            this.elements.TryReadElement(root, out var info) &&
            info.Raw.ParentPtr == IntPtr.Zero;
    }
}
