// <copyright file="GameStatesSearch.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System;
    using System.Collections.Generic;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Finds the static that holds the game's state object without its byte pattern: a pointer in the image's own data to
    ///     an object that <see cref="GameStateLayout"/> takes for the state object, wherever in it the list and the stack are.
    ///     The same judgment says whether the place a pattern gave is the right one.
    /// </summary>
    internal static class GameStatesSearch
    {
        private const int Pointer = 8;
        private const int HeapAlignment = 16;

        /// <summary>Whether the static at <paramref name="address"/> holds the state object now (it does not while the game is still starting).</summary>
        internal static bool Holds(SafeMemoryHandle reader, IntPtr address, IntPtr module, int size) =>
            reader.TryReadMemory<IntPtr>(address, out var state) && IsStateObject(reader, state.ToInt64(), new GameMemory(reader), module.ToInt64(), size);

        /// <summary>
        ///     Every static in <paramref name="ranges"/> of the image that holds a state object, in address order, each with
        ///     the object it holds: several statics may hold the one object, and two objects that both pass settle nothing.
        /// </summary>
        internal static List<(IntPtr Static, long Object)> Find(SafeMemoryHandle reader, IntPtr module, int size, IReadOnlyList<(long Start, long Size)> ranges)
        {
            var (start, memory) = (module.ToInt64(), new GameMemory(reader));
            var (judged, found) = (new Dictionary<long, bool>(), new List<(IntPtr, long)>());
            foreach (var range in ranges)
            {
                // Statics are pointer-aligned, and so is the image.
                var aligned = (range.Start + Pointer - 1) / Pointer * Pointer;
                foreach (var (offset, bytes) in PatternFinder.Read(reader, start, (aligned, range.Start + range.Size - aligned), 0))
                {
                    for (var at = 0; at + Pointer <= bytes.Length; at += Pointer)
                    {
                        var value = BitConverter.ToInt64(bytes, at);
                        if (!IsHeapObject(value, start, size))
                        {
                            continue;
                        }

                        if (!judged.TryGetValue(value, out var fits))
                        {
                            judged[value] = fits = IsStateObject(reader, value, memory, start, size);
                        }

                        if (fits)
                        {
                            found.Add((new IntPtr(start + offset + at), value));
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>Whether <paramref name="value"/> can be a pointer to an object the game allocated: aligned as the heap aligns, and outside the image.</summary>
        internal static bool IsHeapObject(long value, long module, int size) =>
            value % HeapAlignment == 0 && (value < module || value >= module + size) && SafeMemoryHandle.IsValidAddress(new IntPtr(value));

        private static bool IsStateObject(SafeMemoryHandle reader, long address, IAreaMemory memory, long module, int size) =>
            reader.ReadMemoryArray<byte>(new IntPtr(address), GameStateLayout.BlockBytes) is { Length: > 0 } block &&
            GameStateLayout.IsStateObject(block, memory, module, module + size);
    }
}
