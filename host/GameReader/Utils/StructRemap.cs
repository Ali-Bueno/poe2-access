// <copyright file="StructRemap.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>From this offset of a struct up to the next segment, its bytes sit <paramref name="Shift"/> away in the game.</summary>
    public readonly record struct RemapSegment(int From, int Shift);

    /// <summary>Rebuilds a struct whose fields a game patch moved, so every read keeps its compile-time layout.</summary>
    public static class StructRemapper
    {
        /// <summary>The segments in order, dropping those that continue the shift before them.</summary>
        public static RemapSegment[] Segments(IEnumerable<RemapSegment> moved)
        {
            var result = new List<RemapSegment>();
            foreach (var segment in moved.OrderBy(segment => segment.From))
            {
                if ((result.Count == 0 && segment.Shift != 0) || (result.Count > 0 && result[^1].Shift != segment.Shift))
                {
                    result.Add(segment);
                }
            }

            return result.ToArray();
        }

        /// <summary>Where a field measured at <paramref name="offset"/> reads now.</summary>
        public static int Follow(RemapSegment[]? segments, int offset) =>
            offset + (segments?.LastOrDefault(segment => segment.From <= offset).Shift ?? 0);

        /// <summary>How many bytes to read for a struct of <paramref name="size"/> bytes.</summary>
        public static int ReadSize(int size, RemapSegment[] segments) => size + Math.Max(0, segments.Max(segment => segment.Shift));

        /// <summary>Fills <paramref name="target"/> from <paramref name="raw"/>, the game's bytes from the struct's start.</summary>
        public static void Apply(ReadOnlySpan<byte> raw, Span<byte> target, RemapSegment[] segments)
        {
            raw[..target.Length].CopyTo(target);
            for (var index = 0; index < segments.Length; index++)
            {
                var (from, shift) = (segments[index].From, segments[index].Shift);
                var to = index + 1 < segments.Length ? segments[index + 1].From : target.Length;
                raw.Slice(from + shift, to - from).CopyTo(target[from..]);
            }
        }
    }

    /// <summary>Reads the game's structs from bytes already read, through the remap when a patch moved their fields.</summary>
    public static class HostStructs
    {
        /// <summary>
        ///     The struct in <paramref name="raw"/>, the game's bytes from its start, through the remap; false when they are fewer
        ///     than the struct takes as the game lays it out now (<see cref="Size{T}"/>), rather than reading fields from where
        ///     they no longer are.
        /// </summary>
        public static bool TryRead<T>(ReadOnlySpan<byte> raw, out T value)
            where T : unmanaged
        {
            value = default;
            if (raw.Length < Size<T>())
            {
                return false;
            }

            if (StructRemap<T>.Segments is not { } moved)
            {
                value = System.Runtime.InteropServices.MemoryMarshal.Read<T>(raw);
                return true;
            }

            StructRemapper.Apply(raw, System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref value, 1)), moved);
            return true;
        }

        /// <summary>Where a field of <typeparamref name="T"/> measured at <paramref name="offset"/> reads now.</summary>
        public static int Follow<T>(int offset)
            where T : unmanaged => StructRemapper.Follow(StructRemap<T>.Segments, offset);

        /// <summary>How many bytes hold <typeparamref name="T"/> as the game lays it out now.</summary>
        public static int Size<T>()
            where T : unmanaged
        {
            var size = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            return StructRemap<T>.Segments is { } moved ? StructRemapper.ReadSize(size, moved) : size;
        }
    }

    /// <summary>The segments of <typeparamref name="T"/>; null while the struct stands where it was measured.</summary>
    /// <typeparam name="T">the struct read from the game.</typeparam>
    internal static class StructRemap<T>
        where T : unmanaged
    {
        internal static RemapSegment[]? Segments;

        internal static void Set(RemapSegment[] segments) => Segments = segments.Length == 0 ? null : segments;
    }
}
