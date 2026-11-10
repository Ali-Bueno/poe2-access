// <copyright file="GameStates.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Coroutine;
    using CoroutineEvents;
    using GameOffsets.Objects;
    using GameOffsets.Objects.States;
    using RemoteEnums;
    using States.InGameStateObjects;
    using States;
    using Utils;

    /// <summary>
    ///     Reads and stores the global states of the game.
    /// </summary>
    public class GameStates : RemoteObjectBase
    {
        /// <summary>The part that says which state plays an area.</summary>
        public const string InGamePart = "in-game state";

        /// <summary>The part that says which state loads an area.</summary>
        public const string AreaLoadingPart = "area loading state";

        /// <summary>The part that is the game menu (told by being opened in play and closed back to it).</summary>
        public const string EscapePart = "game menu state";

        // The names that are states of the game's list: every slot up to the one 0.5.x added unnamed.
        private const int StateNames = (int)GameStateTypes.UnknownState + 1;

        private const string LocalPlayerFolder = "Metadata/Characters/";

        // While no state has led to an area, the current one is looked at this often.
        private const long LookEveryMs = 1000;

        // A state list not found at attach (the game still starting) is looked for again this often, and said lost only after
        // two minutes of it, as the plugin waits for a first state (GameStateService.StatesUnreadMs).
        private const long LayoutRetryMs = 2000;
        private const int LayoutTries = 60;

        private static readonly int AreaPointer = Marshal.OffsetOf<InGameStateOffset>(nameof(InGameStateOffset.AreaInstanceData)).ToInt32();

        private readonly object gameStatesLock = new();
        private long[] slots = Array.Empty<long>();
        private GameStateRoles roles = new(Array.Empty<long>());
        private long nextLookAt;
        private int layoutTried;
        private long nextLayoutAt;
        private bool layoutPending;
        private int[] named = Array.Empty<int>();
        private IntPtr currentStateAddress = IntPtr.Zero;
        private GameStateTypes currentStateName = GameStateTypes.GameNotLoaded;
        private GameStateStaticOffset myStaticObj;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GameStates" /> class.
        /// </summary>
        /// <param name="address">address of the remote memory object.</param>
        internal GameStates(IntPtr address)
            : base(address)
        {
            CoroutineHandler.Start(this.OnPerFrame(), priority: int.MaxValue);
        }

        /// <summary>
        ///     Gets a dictionary containing all the Game States addresses.
        /// </summary>
        public Dictionary<IntPtr, GameStateTypes> AllStates { get; } = new();

        /// <summary>
        ///     Gets the AreaLoadingState object.
        /// </summary>
        public AreaLoadingState AreaLoading { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the InGameState Object.
        /// </summary>
        public InGameState InGameStateObject { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the object of the state the game is in; zero before one is read. It is known even for a state the mod
        ///     has no name for, so what hangs from the current state (its UI) does not depend on the naming.
        /// </summary>
        public IntPtr CurrentStateAddress => this.currentStateAddress;

        /// <summary>
        ///     Gets a value indicating whether the static is known and still held no state object when last read: the game
        ///     is up but has not made its states yet (a client patching itself in its own window), which is no fault of a layout.
        /// </summary>
        public bool AwaitsStateObject => this.Address != IntPtr.Zero && this.myStaticObj.GameState == IntPtr.Zero;

        /// <summary>
        ///     Gets what the judgments of the state object found: its stack, its state list, the in-game and loading states.
        /// </summary>
        public IReadOnlyList<StructOutcome> LayoutOutcomes { get; private set; } = Array.Empty<StructOutcome>();

        /// <summary>
        ///     Gets the current state the game is in.
        /// </summary>
        public GameStateTypes GameCurrentState
        {
            get => this.currentStateName;
            private set
            {
                if (this.currentStateName != value)
                {
                    this.currentStateName = value;
                    if (value != GameStateTypes.GameNotLoaded)
                    {
                        CoroutineHandler.RaiseEvent(RemoteEvents.StateChanged);
                    }
                }
            }
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            if (hasAddressChanged)
            {
                lock (this.gameStatesLock)
                {
                    // While the game starts the static is empty, then holds an object with no states yet: neither is an error.
                    this.myStaticObj = reader.TryReadMemory<GameStateStaticOffset>(this.Address, out var held) ? held : default;
                    this.LayoutOutcomes = this.JudgeLayout(reader, out var counted);
                    var data = this.myStaticObj.GameState != IntPtr.Zero && reader.TryReadMemory<GameStateOffset>(this.myStaticObj.GameState, out var read) ? read : default;

                    // As many states as the game has; while its list is not found, the thirteen where they were measured.
                    this.slots = new long[Math.Max(counted.Length, GameStateHelper.TOTAL_STATES)];
                    for (var i = 0; i < this.slots.Length; i++)
                    {
                        this.slots[i] = counted.Length > 0 ? (i < counted.Length ? counted[i] : 0)
                            : i < GameStateHelper.TOTAL_STATES ? data.States[i].X.ToInt64() : 0;
                    }

                    // A slot past the ones the mod has a name for is a state all the same: an unknown one, not "game not loaded".
                    this.AllStates.Clear();
                    this.named = Enumerable.Range(0, this.slots.Length).Select(slot => slot < StateNames ? slot : -1).ToArray();
                    for (var i = 0; i < this.slots.Length; i++)
                    {
                        this.AllStates[new IntPtr(this.slots[i])] = this.named[i] >= 0 ? (GameStateTypes)this.named[i] : GameStateTypes.UnknownState;
                    }

                    if (counted.Length > 0 && counted.Length != GameStateHelper.TOTAL_STATES)
                    {
                        Console.WriteLine($"[GameStates] the game has {counted.Length} states, {GameStateHelper.TOTAL_STATES} when measured.");
                    }

                    this.roles = new GameStateRoles(this.slots);
                    this.AreaLoading.Address = new IntPtr(this.slots[(int)GameStateTypes.AreaLoadingState]);
                    this.InGameStateObject.Address = new IntPtr(this.slots[(int)GameStateTypes.InGameState]);
                }
            }
            else
            {
                // Caller (OnPerFrame) takes gameStatesLock around this call.
                if (this.layoutPending && Environment.TickCount64 >= this.nextLayoutAt)
                {
                    this.nextLayoutAt = Environment.TickCount64 + LayoutRetryMs;
                    this.UpdateData(true);
                }

                if (this.myStaticObj.GameState == IntPtr.Zero)
                {
                    return; // the game has not made its state object yet
                }

                var data = reader.ReadMemory<GameStateOffset>(this.myStaticObj.GameState);
                var cStateAddr = reader.ReadMemory<IntPtr>(data.CurrentStatePtr.Last - GameStateLayout.StackEntry); // the last entry's object
                var changed = cStateAddr != IntPtr.Zero && cStateAddr != this.currentStateAddress;
                if (changed || (this.roles.InGame < 0 && cStateAddr != IntPtr.Zero && Environment.TickCount64 >= this.nextLookAt))
                {
                    this.nextLookAt = Environment.TickCount64 + LookEveryMs;
                    this.roles.See(cStateAddr.ToInt64(), this.AreaOf(reader, cStateAddr), this.InGameArea(reader));
                    this.ApplyRoles();

                    // Named at every look, not only when the state changes: the look may just have renamed it.
                    this.currentStateAddress = cStateAddr;
                    if (changed && !this.AllStates.ContainsKey(cStateAddr))
                    {
                        Console.WriteLine(
                            $"[GameStates] Current state 0x{cStateAddr.ToInt64():X} " +
                            "is absent from the configured state table; GameStateOffset may have shifted.");
                    }

                    this.GameCurrentState = GameStateRoles.CurrentName(this.AllStates, cStateAddr, changed, this.currentStateName);
                }
            }
        }

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            lock (this.gameStatesLock)
            {
                this.myStaticObj = default;
                this.currentStateAddress = IntPtr.Zero;
                this.GameCurrentState = GameStateTypes.GameNotLoaded;
                this.AllStates.Clear();
                this.AreaLoading.Address = IntPtr.Zero;
                this.InGameStateObject.Address = IntPtr.Zero;
                this.slots = Array.Empty<long>();
                this.roles = new GameStateRoles(this.slots);
                this.LayoutOutcomes = Array.Empty<StructOutcome>();
                (this.layoutTried, this.layoutPending, this.named) = (0, false, Array.Empty<int>());
            }
        }

        /// <summary>Reads the state object again from its static, as on attach: the static was just proven to hold it.</summary>
        internal void Rejudge()
        {
            if (this.Address == IntPtr.Zero)
            {
                return;
            }

            lock (this.gameStatesLock)
            {
                this.layoutTried = 0;
                this.UpdateData(true);
            }
        }

        /// <summary>Where the state object keeps its stack and its list, judged on every attach (<see cref="GameStateLayout"/>).</summary>
        private IReadOnlyList<StructOutcome> JudgeLayout(SafeMemoryHandle reader, out long[] states)
        {
            states = Array.Empty<long>();
            var memory = new GameMemory(reader);
            var block = GameStateLayout.ReadBlock(memory, this.myStaticObj.GameState.ToInt64());
            if (block.Length == 0)
            {
                // No object yet (the static is still empty, live on 2026-11-10 with the mod started before the game): looked at
                // again like a list not built yet, or the states would never be read.
                this.layoutPending = ++this.layoutTried < LayoutTries;
                return Array.Empty<StructOutcome>();
            }

            var found = GameStateLayout.Locate(block, GameStateFields.Measured, memory);
            states = GameStateLayout.States(block, found.States) ?? Array.Empty<long>();
            var outcomes = OffsetHealer.PlaceGameStates(found);
            this.layoutPending = outcomes.Any(outcome => outcome.Health == StructHealth.Lost) && ++this.layoutTried < LayoutTries;
            return this.layoutPending
                ? outcomes.Select(outcome => outcome.Health == StructHealth.Lost ? outcome with { Health = StructHealth.NotJudged, Detail = "no state list yet" } : outcome).ToList()
                : outcomes;
        }

        /// <summary>The area pointer the in-game state holds now (the slot judged in play, else the measured one); zero when none.</summary>
        private long InGameArea(SafeMemoryHandle reader)
        {
            var slot = this.roles.InGame >= 0 ? this.roles.InGame : (int)GameStateTypes.InGameState;
            return slot < this.slots.Length && reader.TryReadMemory<IntPtr>(new IntPtr(this.slots[slot]) + OffsetHealer.Follow(typeof(InGameStateOffset), AreaPointer), out var area)
                ? area.ToInt64() : 0;
        }

        /// <summary>The area <paramref name="state"/> leads to when a local player stands in it; zero otherwise.</summary>
        private long AreaOf(SafeMemoryHandle reader, IntPtr state)
        {
            if (!reader.TryReadMemory<IntPtr>(state + OffsetHealer.Follow(typeof(InGameStateOffset), AreaPointer), out var area) ||
                !reader.TryReadMemory<IntPtr>(area + this.InGameStateObject.CurrentAreaInstance.Layout.Fields.LocalPlayer, out var player))
            {
                return 0;
            }

            return new GameMemory(reader).EntityPath(player.ToInt64()).StartsWith(LocalPlayerFolder, StringComparison.Ordinal) ? area.ToInt64() : 0;
        }

        /// <summary>
        ///     Names the states from the roles they were seen in (<see cref="GameStateRoles.Names"/>): a state inserted in the game's
        ///     list moves the names after it, so the roles found on both sides of a state tell its slot.
        /// </summary>
        private void ApplyRoles()
        {
            var roles = new[]
            {
                ((int)GameStateTypes.AreaLoadingState, this.roles.AreaLoading),
                ((int)GameStateTypes.EscapeState, this.roles.Escape),
                ((int)GameStateTypes.InGameState, this.roles.InGame),
            };
            var names = GameStateRoles.Names(this.slots.Length, StateNames, roles);
            if (!names.SequenceEqual(this.named))
            {
                this.named = names;
                for (var slot = 0; slot < this.slots.Length; slot++)
                {
                    this.AllStates[new IntPtr(this.slots[slot])] = names[slot] >= 0 ? (GameStateTypes)names[slot] : GameStateTypes.UnknownState;
                }

                var moved = names.Select((name, slot) => (name, slot)).Where(pair => pair.name != pair.slot)
                    .Select(pair => $"slot {pair.slot} {(pair.name >= 0 ? ((GameStateTypes)pair.name).ToString() : "unknown")}");
                Console.WriteLine($"[GameStates] states named by their roles: {string.Join(", ", moved)}");
            }

            var outcomes = this.LayoutOutcomes.Where(outcome => outcome.Name is not (InGamePart or AreaLoadingPart or EscapePart)).ToList();
            outcomes.Add(this.Role(InGamePart, this.roles.InGame, GameStateTypes.InGameState, this.InGameStateObject));
            outcomes.Add(this.Role(AreaLoadingPart, this.roles.AreaLoading, GameStateTypes.AreaLoadingState, this.AreaLoading));
            outcomes.Add(this.Role(EscapePart, this.roles.Escape, GameStateTypes.EscapeState, null));
            this.LayoutOutcomes = outcomes;
        }

        /// <summary>The outcome of a state's role; a state seen at it in another slot gives its object to the holder.</summary>
        private StructOutcome Role(string part, int found, GameStateTypes type, RemoteObjectBase? holder)
        {
            var measured = (int)type;
            if (found < 0)
            {
                return new StructOutcome(part, StructHealth.NotJudged, measured, 0, "not seen at work yet");
            }

            var address = new IntPtr(this.slots[found]);
            if (holder is not null && holder.Address != address)
            {
                holder.Address = address;
                Console.WriteLine($"[GameStates] {part}: slot {measured} -> {found}");
            }

            return new StructOutcome(part, found == measured ? StructHealth.Intact : StructHealth.Moved, measured, found, string.Empty);
        }

        private IEnumerator<Wait> OnPerFrame()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.PerFrameDataUpdate);
                try
                {
                    if (this.Address != IntPtr.Zero)
                    {
                        lock (this.gameStatesLock)
                        {
                            this.UpdateData(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameStates.OnPerFrame] {ex}");
                }
            }
        }
    }
}
