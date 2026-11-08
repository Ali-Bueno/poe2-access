// <copyright file="GameStatesSearch.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils
{
    using System;
    using System.Collections.Generic;
    using GameOffsets.Objects;

    /// <summary>
    ///     Finds the game's state list when its byte pattern no longer matches: a static pointer to an object whose state
    ///     stack ends in one of its states, a shape nothing else in the image has.
    /// </summary>
    internal static class GameStatesSearch
    {
        private const int Chunk = 1 << 22;
        private const int Pointer = 8;
        private const int HeapAlignment = 16;
        private const int StackEntry = 0x10;
        private const int MaxStack = 0x400;
        private const int MinStates = 8;

        /// <summary>The address of the pointer to the state list; zero when no place fits.</summary>
        internal static IntPtr Find(SafeMemoryHandle reader, IntPtr module, int size)
        {
            var start = module.ToInt64();
            for (var offset = 0; offset < size; offset += Chunk)
            {
                var bytes = reader.ReadMemoryArray<byte>(new IntPtr(start + offset), Math.Min(Chunk, size - offset));
                for (var at = 0; at + Pointer <= bytes.Length; at += Pointer)
                {
                    var value = BitConverter.ToInt64(bytes, at);
                    if (value % HeapAlignment == 0 && (value < start || value >= start + size) &&
                        SafeMemoryHandle.IsValidAddress(new IntPtr(value)) && IsStateList(reader, new IntPtr(value)))
                    {
                        return new IntPtr(start + offset + at);
                    }
                }
            }

            return IntPtr.Zero;
        }

        private static bool IsStateList(SafeMemoryHandle reader, IntPtr candidate)
        {
            if (!reader.TryReadMemory<GameStateOffset>(candidate, out var data))
            {
                return false;
            }

            var stack = data.CurrentStatePtr;
            var bytes = stack.Last.ToInt64() - stack.First.ToInt64();
            if (stack.First == IntPtr.Zero || bytes <= 0 || bytes > MaxStack || bytes % StackEntry != 0 ||
                stack.End.ToInt64() < stack.Last.ToInt64())
            {
                return false;
            }

            // A state the game has not used yet is null (0.5.5f: two of thirteen).
            var states = new HashSet<IntPtr>();
            for (var index = 0; index < GameStateHelper.TOTAL_STATES; index++)
            {
                var state = data.States[index].X;
                if (state != IntPtr.Zero && (!SafeMemoryHandle.IsValidAddress(state) || !states.Add(state)))
                {
                    return false;
                }
            }

            return states.Count >= MinStates && reader.TryReadMemory<IntPtr>(stack.Last - StackEntry, out var current) && states.Contains(current);
        }
    }
}
