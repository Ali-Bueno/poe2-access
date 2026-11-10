// <copyright file="GameStateLayout.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Natives;
    using GameOffsets.Objects;

    /// <summary>Where the state stack and the state list sit in the game's state object; negative when not found.</summary>
    public readonly record struct GameStateFields(int Stack, int States)
    {
        /// <summary>The offsets the host was written for.</summary>
        public static GameStateFields Measured { get; } = new(
            Marshal.OffsetOf<GameStateOffset>(nameof(GameStateOffset.CurrentStatePtr)).ToInt32(),
            Marshal.OffsetOf<GameStateOffset>(nameof(GameStateOffset.States)).ToInt32());
    }

    /// <summary>
    ///     Finds the game state object's fields by what they hold: a run of slots of (object, control block) pointers,
    ///     distinct and heap-aligned, and a stack of 16-byte entries whose last one is one of those states. How many
    ///     states there are is counted, not assumed: a patch has added one before (0.5.x).
    /// </summary>
    public static class GameStateLayout
    {
        /// <summary>Bytes searched in the state object: both fields were measured in its first 0x120.</summary>
        public const int BlockBytes = 0x200;

        /// <summary>Each stack entry is a shared pointer (object, control block); the current state is the last one's object.</summary>
        public const int StackEntry = 0x10;

        private const long MinPointer = 0x10000;
        private const long MaxPointer = 0x7FFFFFFFFFFF;
        private const int Pointer = 8;
        private const int HeapAlignment = 16;
        private const int MaxStack = 0x400;

        // A state the game has not used yet is null (0.5.5f: two of thirteen).
        private const int MinStates = 8;

        // More slots than this in a row are not a state list but a table of something else.
        private const int MaxStates = 32;

        private static readonly int Slot = System.Runtime.CompilerServices.Unsafe.SizeOf<StdTuple2D<IntPtr>>();

        /// <summary>
        ///     The first <see cref="BlockBytes"/> of the object at <paramref name="address"/>, or as many as its page holds: a read
        ///     fails whole, the object is shorter than the block (0x148 bytes on 0.5.5f) and may be the last thing before memory
        ///     that cannot be read. Empty when nothing can be read.
        /// </summary>
        public static byte[] ReadBlock(IAreaMemory memory, long address)
        {
            var block = memory.Read(address, BlockBytes);
            var inPage = (int)(Environment.SystemPageSize - (address & (Environment.SystemPageSize - 1)));
            return block.Length > 0 || inPage >= BlockBytes ? block : memory.Read(address, inPage);
        }

        /// <summary>
        ///     The fields nearest <paramref name="near"/> that fit together: a list read 8 bytes off pairs each control block
        ///     with the next state and passes for one, but the stack's top is not in it. Both negative when nothing fits. A list
        ///     read a whole slot off fits too and shifts every name by one: only what the states do tells it (<see cref="GameStateRoles.WholeShift"/>).
        /// </summary>
        public static GameStateFields Locate(byte[] block, GameStateFields near, IAreaMemory memory)
        {
            // A stack's top is read from the game once: the game may change it between two looks, and every list is judged
            // against the same one.
            var tops = new Dictionary<int, long>();
            long TopAt(int place) => tops.TryGetValue(place, out var top) ? top : tops[place] = Top(block, place, memory);
            int StackFor(int at)
            {
                var known = States(block, at)?.Where(state => state != 0).ToHashSet();
                return known == null ? -1 : StructShapes.Nearest(near.Stack, Pointer, block.Length - (3 * Pointer), place => known.Contains(TopAt(place)));
            }

            var first = StructShapes.Nearest(near.States, Pointer, block.Length - (Slot * MinStates), at => StackFor(at) >= 0);
            if (first < 0 || first == near.States)
            {
                return first < 0 ? new GameStateFields(-1, -1) : new GameStateFields(StackFor(first), first);
            }

            // Moved: of the list found and those a slot either side, the one with the most states in the slots the mod names
            // (a slot off drops one at an end). Counted to the end, a list read from a pointer pair before it would hold them all.
            var best = new[] { first, first - Slot, first + Slot }.Where(at => StackFor(at) >= 0)
                .OrderByDescending(at => States(block, at)!.Take(GameStateHelper.TOTAL_STATES).Count(state => state != 0)).First();
            return new GameStateFields(StackFor(best), best);
        }

        /// <summary>
        ///     Whether <paramref name="block"/> is the game's state object, for telling it among everything the image's statics
        ///     point at: a list and a stack that fit together, wherever in it, and besides, since an allocator's table of free
        ///     blocks has that shape too (taken for it live on 2026-11-10, while the game was starting): the object and each of
        ///     its states begin with a virtual table inside the image <paramref name="imageStart"/>..<paramref name="imageEnd"/>,
        ///     no two states share one, each being of its own class, and the state the game is in is one of them.
        /// </summary>
        public static bool IsStateObject(byte[] block, IAreaMemory memory, long imageStart, long imageEnd)
        {
            bool InImage(long value) => value >= imageStart && value < imageEnd;
            if (!InImage(Long(block, 0)) || Locate(block, GameStateFields.Measured, memory) is not { Stack: >= 0, States: >= 0 } found)
            {
                return false;
            }

            // A list read from a few slots early takes the pointer pairs before it for states (only what the states do tells
            // where it starts): those are let be, and the states proper must be enough, with the current one among them.
            var (tables, current, proper) = (new HashSet<long>(), Top(block, found.Stack, memory), new HashSet<long>());
            foreach (var state in States(block, found.States)!.Where(state => state != 0))
            {
                var head = memory.Read(state, Pointer);
                if (head.Length == Pointer && InImage(BitConverter.ToInt64(head, 0)))
                {
                    if (!tables.Add(BitConverter.ToInt64(head, 0)))
                    {
                        return false;
                    }

                    proper.Add(state);
                }
            }

            return proper.Count >= MinStates && proper.Contains(current);
        }

        /// <summary>
        ///     The state objects at <paramref name="at"/> in slot order (zero where unused), as many as there are: the list ends
        ///     at the first slot that is no state, and at its last used slot when that is past the thirteen the mod names. Null
        ///     when they are no state list.
        /// </summary>
        public static long[]? States(byte[] block, int at)
        {
            if (at < 0)
            {
                return null;
            }

            var states = new List<long>();
            var seen = new HashSet<long>();
            for (var index = 0; index < MaxStates && at + ((index + 1) * Slot) <= block.Length; index++)
            {
                var (state, control) = (Long(block, at + (index * Slot)), Long(block, at + (index * Slot) + Pointer));
                if (state != 0 && (!IsPointer(state) || state % HeapAlignment != 0 || !IsPointer(control) || !seen.Add(state)))
                {
                    break;
                }

                states.Add(state);
            }

            // Empty slots past the named ones are the memory after the list, not states to come.
            while (states.Count > GameStateHelper.TOTAL_STATES && states[^1] == 0)
            {
                states.RemoveAt(states.Count - 1);
            }

            return seen.Count >= MinStates ? states.ToArray() : null;
        }

        /// <summary>The state on top of the stack at <paramref name="at"/>; zero when it is no stack.</summary>
        public static long Top(byte[] block, int at, IAreaMemory memory)
        {
            var (first, last, end) = (Long(block, at), Long(block, at + Pointer), Long(block, at + (2 * Pointer)));
            var bytes = last - first;
            if (!IsPointer(first) || bytes <= 0 || bytes > MaxStack || bytes % StackEntry != 0 || end < last)
            {
                return 0;
            }

            var top = memory.Read(last - StackEntry, Pointer);
            return top.Length == Pointer ? BitConverter.ToInt64(top, 0) : 0;
        }

        private static bool IsPointer(long value) => value > MinPointer && value < MaxPointer;

        private static long Long(byte[] bytes, int at) => at >= 0 && at + Pointer <= bytes.Length ? BitConverter.ToInt64(bytes, at) : 0;
    }

    /// <summary>
    ///     Which of the game's states plays an area, which loads one and which is the game menu, told by what they do rather
    ///     than by their slot: the state in play leads to an area with a local player in it; the state that was current while the
    ///     in-game state's area pointer changed loaded it (twice the same: a wait after the load holds the new area from its
    ///     start, a menu the old); the game menu is entered from play and left back to it over the same area (twice the same).
    ///     The other states are named from these (<see cref="Names"/>).
    /// </summary>
    public sealed class GameStateRoles
    {
        /// <summary>Area changes that must agree on the loading state before it is named.</summary>
        public const int LoadingProofs = 2;

        private readonly IReadOnlyList<long> states;
        private long present;
        private long areaAtStart;
        private int candidate = -1;
        private int proofs;
        private int enteredFrom = -1;
        private int menuCandidate = -1;
        private int menuProofs;

        /// <summary>Initializes a new instance of the <see cref="GameStateRoles"/> class over the slots' objects.</summary>
        public GameStateRoles(IReadOnlyList<long> states)
        {
            this.states = states;
        }

        /// <summary>Gets the slot of the state in play; negative until one led to an area.</summary>
        public int InGame { get; private set; } = -1;

        /// <summary>Gets the slot of the state that loads areas; negative until proven.</summary>
        public int AreaLoading { get; private set; } = -1;

        /// <summary>Gets the slot of the game menu (escape) state; negative until proven.</summary>
        public int Escape { get; private set; } = -1;

        /// <summary>
        ///     Gets how many slots every state moved when both roles were found that far from their measured slots (a list read a
        ///     whole slot off, or a state inserted before all of them); null otherwise.
        /// </summary>
        public int? WholeShift =>
            this.InGame >= 0 && this.AreaLoading >= 0 && this.InGame - (int)GameStateTypes.InGameState is var shift && shift != 0 &&
            this.AreaLoading - (int)GameStateTypes.AreaLoadingState == shift ? shift : null;

        /// <summary>
        ///     One look at the current state, on every change: <paramref name="area"/> is the area it leads to with a player in
        ///     it (zero when none), <paramref name="inGameArea"/> the area pointer the in-game state holds now (zero when none).
        /// </summary>
        public void See(long current, long area, long inGameArea)
        {
            if (current != this.present)
            {
                var ended = this.IndexOf(this.present);
                if (ended >= 0 && ended != this.InGame && inGameArea != 0 && inGameArea != this.areaAtStart)
                {
                    (this.candidate, this.proofs) = ended == this.candidate ? (ended, this.proofs + 1) : (ended, 1);
                    this.AreaLoading = this.proofs >= LoadingProofs ? ended : this.AreaLoading;
                }

                // Opened in play and closed back to it over the same area: the game menu.
                var next = this.IndexOf(current);
                if (ended >= 0 && this.InGame >= 0 && ended != this.InGame && this.enteredFrom == this.InGame && next == this.InGame &&
                    inGameArea != 0 && inGameArea == this.areaAtStart)
                {
                    (this.menuCandidate, this.menuProofs) = ended == this.menuCandidate ? (ended, this.menuProofs + 1) : (ended, 1);
                    this.Escape = this.menuProofs >= LoadingProofs ? ended : this.Escape;
                }

                (this.present, this.areaAtStart, this.enteredFrom) = (current, inGameArea, ended);
            }

            if (area != 0 && this.IndexOf(current) is var index and >= 0)
            {
                this.InGame = index;
            }
        }

        /// <summary>
        ///     Which state each of <paramref name="slots"/> slots holds (its measured index, negative when unknown), from the roles
        ///     found, each (measured index, slot found, negative while unknown). A state inserted in the game's list moves every one
        ///     after it, so a state after every role takes the shift of the last, one between two roles that agree takes theirs, and
        ///     the rest keep their slot (a role says nothing of the states before it): with no role moved, every one.
        /// </summary>
        public static int[] Names(int slots, int states, IReadOnlyList<(int Measured, int Found)> roles)
        {
            var names = new int[slots];
            Array.Fill(names, -1);
            var known = roles.Where(role => role.Found is >= 0 && role.Found < slots).OrderBy(role => role.Measured).ToList();
            foreach (var (measured, found) in known)
            {
                names[found] = measured;
            }

            for (var state = 0; state < states; state++)
            {
                if (known.Any(role => role.Measured == state))
                {
                    continue;
                }

                var below = known.LastOrDefault(role => role.Measured < state, (Measured: -1, Found: -1));
                var above = known.FirstOrDefault(role => role.Measured > state, (Measured: -1, Found: -1));
                var shift = (below.Measured >= 0, above.Measured >= 0) switch
                {
                    (true, true) when below.Found - below.Measured == above.Found - above.Measured => below.Found - below.Measured,
                    (true, false) => below.Found - below.Measured,
                    _ => 0,
                };
                if (state + shift is var slot && slot >= 0 && slot < slots && names[slot] < 0)
                {
                    names[slot] = state;
                }
            }

            return names;
        }

        private int IndexOf(long state)
        {
            for (var index = 0; state != 0 && index < this.states.Count; index++)
            {
                if (this.states[index] == state)
                {
                    return index;
                }
            }

            return -1;
        }

    }
}
