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
    ///     The layout checks run on the first root found (<see cref="LayoutProbe"/>), and the in-game layers are found by
    ///     what they hold (<see cref="InGameFields"/>), so a game update that moves them is followed.
    /// </summary>
    public sealed class UiManagerLocator
    {
        private const int ScanChunkBytes = 0x100;

        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;
        private readonly PluginLog log;
        /// <summary>Elements whose manager must agree before an unproven manager is trusted (a stray pointer is not a manager).</summary>
        private const int MinAgreeingElements = 2;

        /// <summary>Parents climbed to an element's root at most.</summary>
        private const int MaxDepth = 64;

        private readonly Dictionary<IntPtr, IntPtr> cache = new();
        private readonly LayoutProbe probe;

        public UiManagerLocator(HostMemoryReader reader, UiElementReader elements, PluginLog log, LayoutProbe probe, InGameFields fields)
        {
            this.reader = reader;
            this.elements = elements;
            this.log = log;
            this.probe = probe;
            this.Fields = fields;
        }

        /// <summary>The in-game layers by what they hold.</summary>
        public InGameFields Fields { get; }

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
            var inGame = state == GameStateTypes.InGameState;
            if (inGame)
            {
                // Names first: the manager pointer every element holds sits beside its name and moves with it.
                this.probe.ProbeNames(this.RootOf(this.InGameLayerContainer(stateObject)), true);
            }

            // The direct slot is only taken as measured: an object there that merely holds the root is no manager.
            var manager = inGame &&
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
                if (this.reader.TryRead<IntPtr>(manager + GameLayout.UiManager.RootOffset, out var root))
                {
                    // In game the HUD always shows texts and named layers, so a miss there is a real one.
                    this.probe.ProbeNames(root, inGame);
                    this.probe.ProbeText(root, inGame);
                }
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
        public IntPtr InGameChatLayer(IntPtr inGameState) => this.Fields.Find(this.InGameLayerContainer(inGameState), InGameField.Chat);

        public IntPtr InGameTutorialLayer(IntPtr inGameState) => this.Fields.Find(this.InGameLayerContainer(inGameState), InGameField.Tutorial);

        public IntPtr InGameHudLayer(IntPtr inGameState) => this.Fields.Find(this.InGameLayerContainer(inGameState), InGameField.Hud);

        public IntPtr InGameDeathScreen(IntPtr inGameState) => this.Fields.Find(this.InGameLayerContainer(inGameState), InGameField.DeathScreen);

        /// <summary>
        ///     The first element pointer stored in the state object names the manager; when the manager's root slot does
        ///     not hold that element's root (a game update), the manager is trusted only once a second element agrees.
        /// </summary>
        private IntPtr FromStateElements(IntPtr stateObject, out string how)
        {
            how = string.Empty;
            var votes = new Dictionary<IntPtr, int>();
            // Reads are all-or-nothing and a state object can sit near the end of its heap region (SelectCharacterState
            // 0x1CF0 bytes before an unreadable page, 2026-10-24): read it in chunks so the slots that exist are seen.
            for (var chunk = 0; chunk < GameLayout.StateObjectScanBytes; chunk += ScanChunkBytes)
            {
                var slots = this.reader.ReadArray<IntPtr>(stateObject + chunk, ScanChunkBytes / IntPtr.Size);
                for (var i = 0; i < slots.Length; i++)
                {
                    if (!this.reader.IsValidAddress(slots[i]) || !this.elements.IsElement(slots[i]) ||
                        !this.reader.TryRead<IntPtr>(slots[i] + GameLayout.Element.ManagerOffset, out var manager) || !this.reader.IsValidAddress(manager))
                    {
                        continue;
                    }

                    how = $"element at state+0x{chunk + (i * IntPtr.Size):X}";
                    this.probe.ProbeNames(this.RootOf(slots[i]), false);
                    if (this.IsManager(manager))
                    {
                        return manager;
                    }

                    votes[manager] = votes.GetValueOrDefault(manager) + 1;
                    if (votes[manager] >= MinAgreeingElements && this.probe.AdoptManager(manager, this.RootOf(slots[i])) && this.IsManager(manager))
                    {
                        return manager;
                    }
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>The parentless element above <paramref name="element"/>; zero when the chain cannot be read.</summary>
        private IntPtr RootOf(IntPtr element)
        {
            for (var depth = 0; depth < MaxDepth && this.elements.TryReadElement(element, out var info); depth++)
            {
                if (info.Raw.ParentPtr == IntPtr.Zero)
                {
                    return element;
                }

                element = info.Raw.ParentPtr;
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
