// <copyright file="GameStates.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects
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

        private const string LocalPlayerFolder = "Metadata/Characters/";

        // While no state has led to an area, the current one is looked at this often.
        private const long LookEveryMs = 1000;

        private static readonly int AreaPointer = Marshal.OffsetOf<InGameStateOffset>(nameof(InGameStateOffset.AreaInstanceData)).ToInt32();

        private readonly object gameStatesLock = new();
        private long[] slots = Array.Empty<long>();
        private GameStateRoles roles = new(Array.Empty<long>());
        private long nextLookAt;
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
                    this.myStaticObj = reader.ReadMemory<GameStateStaticOffset>(this.Address);
                    this.LayoutOutcomes = this.JudgeLayout(reader);
                    var data = reader.ReadMemory<GameStateOffset>(this.myStaticObj.GameState);
                    this.slots = new long[GameStateHelper.TOTAL_STATES];
                    for (var i = 0; i < GameStateHelper.TOTAL_STATES; i++)
                    {
                        this.AllStates[data.States[i].X] = (GameStateTypes)i;
                        this.slots[i] = data.States[i].X.ToInt64();
                    }

                    this.roles = new GameStateRoles(this.slots);
                    this.AreaLoading.Address = data.States[(int)GameStateTypes.AreaLoadingState].X;
                    this.InGameStateObject.Address = data.States[(int)GameStateTypes.InGameState].X;
                }
            }
            else
            {
                // Caller (OnPerFrame) takes gameStatesLock around this call.
                var data = reader.ReadMemory<GameStateOffset>(this.myStaticObj.GameState);
                var cStateAddr = reader.ReadMemory<IntPtr>(data.CurrentStatePtr.Last - GameStateLayout.StackEntry); // the last entry's object
                var changed = cStateAddr != IntPtr.Zero && cStateAddr != this.currentStateAddress;
                if (changed || (this.roles.InGame < 0 && cStateAddr != IntPtr.Zero && Environment.TickCount64 >= this.nextLookAt))
                {
                    this.nextLookAt = Environment.TickCount64 + LookEveryMs;
                    this.roles.See(cStateAddr.ToInt64(), this.AreaOf(reader, cStateAddr));
                    this.ApplyRoles();
                }

                if (changed)
                {
                    this.currentStateAddress = cStateAddr;
                    if (this.AllStates.TryGetValue(this.currentStateAddress, out var currentState))
                    {
                        this.GameCurrentState = currentState;
                    }
                    else
                    {
                        this.GameCurrentState = GameStateTypes.GameNotLoaded;
                        Console.WriteLine(
                            $"[GameStates] Current state 0x{this.currentStateAddress.ToInt64():X} " +
                            "is absent from the configured state table; GameStateOffset may have shifted.");
                    }
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
            }
        }

        /// <summary>Where the state object keeps its stack and its list, judged on every attach (<see cref="GameStateLayout"/>).</summary>
        private IReadOnlyList<StructOutcome> JudgeLayout(SafeMemoryHandle reader)
        {
            var block = reader.ReadMemoryArray<byte>(this.myStaticObj.GameState, GameStateLayout.BlockBytes);
            if (block.Length == 0)
            {
                return Array.Empty<StructOutcome>();
            }

            var found = GameStateLayout.Locate(block, GameStateFields.Measured, new GameMemory(reader));
            return OffsetHealer.PlaceGameStates(found);
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

        /// <summary>Names the states by what they were seen doing when that differs from their slot.</summary>
        private void ApplyRoles()
        {
            var outcomes = this.LayoutOutcomes.Where(outcome => outcome.Name is not (InGamePart or AreaLoadingPart)).ToList();
            outcomes.Add(this.Role(InGamePart, this.roles.InGame, GameStateTypes.InGameState, this.InGameStateObject));
            outcomes.Add(this.Role(AreaLoadingPart, this.roles.AreaLoading, GameStateTypes.AreaLoadingState, this.AreaLoading));
            this.LayoutOutcomes = outcomes;
        }

        /// <summary>The outcome of a state's role; a state seen at it in another slot takes its name and its object.</summary>
        private StructOutcome Role(string part, int found, GameStateTypes type, RemoteObjectBase holder)
        {
            var measured = (int)type;
            if (found < 0)
            {
                return new StructOutcome(part, StructHealth.NotJudged, measured, 0, "not seen at work yet");
            }

            var address = new IntPtr(this.slots[found]);
            if (holder.Address != address)
            {
                foreach (var stale in this.AllStates.Where(pair => pair.Value == type).Select(pair => pair.Key).ToList())
                {
                    this.AllStates[stale] = GameStateTypes.UnknownState;
                }

                this.AllStates[address] = type;
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
