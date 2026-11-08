// <copyright file="StructShapes.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     What a field must hold, so it is recognised wherever a game patch left it: facts that cannot hold by chance, such
    ///     as a pointer back to its own component or a chain that ends in a buff's name.
    /// </summary>
    public static class StructShapes
    {
        private const long MinPointer = 0x10000;
        private const long MaxPointer = 0x7FFFFFFFFFFF;
        private const int Pointer = 8;
        private const int VectorSize = 3 * Pointer;
        private const int MaxBuffs = 1024;
        private const int MaxSkills = 512;
        private const int SkillEntry = 0x10;
        private const int CooldownEntry = 0x48;
        private const int MaxStats = 8192;
        private const int StatEntry = 8;
        private const int FriendlyMask = 0x7F;
        private const int Friendly = 1;
        private const int FoesPerFriendAllowed = 5;
        private const int MaxReactions = 3;
        private const int MaxStatKey = 100000;
        private const int MatrixBytes = 64;
        private const float MaxWorld = 1e7f;
        private const string ItemFolder = "Metadata/Items/";

        // A vital's fields were all measured in its first 0x34 bytes; a patch may grow it a little.
        private const int MaxVitalField = 0x60;

        // Far above any vital the game shows (life, mana and shield stay well under 100 000); a float read as an int is not.
        private const int MaxVitalTotal = 10_000_000;
        private const int MinFullVitals = 2;

        /// <summary>The offset nearest <paramref name="start"/> that fits, within 0..<paramref name="limit"/>; negative when none does.</summary>
        /// <summary>Friend or foe as the game writes it, in <see cref="GameOffsets.Objects.Components.PositionedOffsets"/>.</summary>
        public static bool IsFriendly(byte reaction) => (reaction & FriendlyMask) == Friendly;

        /// <summary>
        ///     A byte that says friend for the player and foe for nearly every monster. Nearly: a player's minions are monsters
        ///     too. A few values only: a byte of a pointer or a float differs from monster to monster, and that noise reads as foes.
        /// </summary>
        public static bool IsReaction(byte[] player, IReadOnlyList<byte[]> monsters, int at) =>
            at >= 0 && at < player.Length && IsFriendly(player[at]) &&
            monsters.Count(monster => at < monster.Length && !IsFriendly(monster[at])) * FoesPerFriendAllowed >= monsters.Count * (FoesPerFriendAllowed - 1) &&
            monsters.Where(monster => at < monster.Length).Select(monster => monster[at]).Distinct().Count() <= MaxReactions;

        /// <summary>
        ///     The reaction around <paramref name="near"/>, or -1. The first that fits: a byte 0x1B after it reads the same on the
        ///     player and on monsters (0.5.5f, +0x1FB), and the nearest would take that one after a shift back.
        /// </summary>
        public static int FindReaction(byte[] player, IReadOnlyList<byte[]> monsters, int near, int radius)
        {
            for (var at = near - radius; at <= near + radius; at++)
            {
                if (IsReaction(player, monsters, at))
                {
                    return at;
                }
            }

            return -1;
        }

        public static int Nearest(int start, int step, int limit, Func<int, bool> fits)
        {
            for (var distance = 0; distance <= limit; distance += step)
            {
                if (start + distance <= limit && fits(start + distance))
                {
                    return start + distance;
                }

                if (distance > 0 && start - distance >= 0 && fits(start - distance))
                {
                    return start - distance;
                }
            }

            return -1;
        }

        /// <summary>Every vital in a Life component, in order: each points back at the component.</summary>
        public static List<int> Vitals(byte[] block, long component, int backPointer, int total, int current)
        {
            var found = new List<int>();
            var end = Math.Max(current + sizeof(int), backPointer + Pointer);
            for (var at = 0; at + end <= block.Length; at += Pointer)
            {
                if (BitConverter.ToInt64(block, at + backPointer) == component &&
                    BitConverter.ToInt32(block, at + total) is >= 0 and <= MaxVitalTotal &&
                    BitConverter.ToInt32(block, at + current) >= 0 &&
                    BitConverter.ToInt32(block, at + current) <= BitConverter.ToInt32(block, at + total))
                {
                    found.Add(at);
                }
            }

            return found;
        }

        /// <summary>
        ///     Where a vital keeps its back-pointer and its total (the current follows the total) when the measured places
        ///     find too few vitals: the pair nearest them under which <paramref name="minVitals"/> vitals point back with a
        ///     total above zero; with <paramref name="full"/>, two of them full (as at rest in town), which a total read one field
        ///     late never is. Null when none does.
        /// </summary>
        public static (int BackPointer, int Total)? VitalLayout(byte[] block, long component, int backPointer, int total, int currentAfterTotal, int minVitals, bool full = false)
        {
            var found = ((int, int)?)null;
            Nearest(backPointer, Pointer, MaxVitalField, back =>
            {
                bool Fits(int place)
                {
                    if (place < back + Pointer && place + Pointer > back)
                    {
                        return false;
                    }

                    // A total above zero on enough vitals, and life above zero on one (the player lives): a total read one field
                    // late takes the current for the total and whatever follows for the current.
                    var vitals = Vitals(block, component, back, place, place + currentAfterTotal);
                    return vitals.Count(vital => BitConverter.ToInt32(block, vital + place) > 0) >= minVitals &&
                           vitals.Any(vital => BitConverter.ToInt32(block, vital + place + currentAfterTotal) > 0) &&
                           (!full || Full(block, vitals, place, currentAfterTotal) >= MinFullVitals);
                }

                var at = Nearest(total, sizeof(int), MaxVitalField, Fits);
                found = at < 0 ? null : (back, at);
                return at >= 0;
            });
            return found;
        }

        /// <summary>How many of <paramref name="vitals"/> are full: life above zero and as high as its total.</summary>
        public static int Full(byte[] block, IEnumerable<int> vitals, int total, int currentAfterTotal) =>
            vitals.Count(vital => BitConverter.ToInt32(block, vital + total) > 0 && BitConverter.ToInt32(block, vital + total) == BitConverter.ToInt32(block, vital + total + currentAfterTotal));

        /// <summary>
        ///     Where each measured vital stands among those found (the game has more vitals than GameHelper reads): the
        ///     smallest shift that lands them all on one, else each on the nearest. Empty with no vitals.
        /// </summary>
        public static int[] PlaceVitals(int[] measured, IReadOnlyList<int> vitals)
        {
            if (vitals.Count == 0)
            {
                return Array.Empty<int>();
            }

            var shifts = vitals.Select(vital => vital - measured[0]).OrderBy(Math.Abs);
            foreach (var shift in shifts)
            {
                if (measured.All(field => vitals.Contains(field + shift)))
                {
                    return measured.Select(field => field + shift).ToArray();
                }
            }

            return measured.Select(field => vitals.OrderBy(vital => Math.Abs(vital - field)).First()).ToArray();
        }

        /// <summary>A list of buffs: pointers whose definitions begin with a lower-case id such as "herald_of_thunder".</summary>
        public static bool IsBuffList(byte[] block, int at, IAreaMemory memory, int definition)
        {
            if (!IsVector(block, at, out var first, out var bytes) || bytes <= 0 || bytes % Pointer != 0 || bytes / Pointer > MaxBuffs)
            {
                return false;
            }

            var entry = Long(memory.Read(first, Pointer), 0);
            var row = IsPointer(entry) ? Long(memory.Read(entry + definition, Pointer), 0) : 0;
            var name = IsPointer(row) ? Long(memory.Read(row, Pointer), 0) : 0;
            return IsPointer(name) && IsId(memory.WideText(name));
        }

        /// <summary>The skills (16-byte entries, the first a pointer) with the cooldowns right after them.</summary>
        public static bool IsSkillList(byte[] block, int at, IAreaMemory memory)
        {
            if (!IsVector(block, at, out var first, out var bytes) || bytes <= 0 || bytes % SkillEntry != 0 || bytes / SkillEntry > MaxSkills)
            {
                return false;
            }

            return IsPointer(Long(memory.Read(first, Pointer), 0)) &&
                   IsVector(block, at + VectorSize, out _, out var cooldowns) && cooldowns % CooldownEntry == 0;
        }

        /// <summary>A pointer to a stat list: key and value pairs with small keys.</summary>
        public static bool IsStatsPointer(byte[] block, int at, IAreaMemory memory, int list)
        {
            var pointer = at + Pointer <= block.Length ? BitConverter.ToInt64(block, at) : 0;
            var vector = IsPointer(pointer) ? memory.Read(pointer + list, VectorSize) : Array.Empty<byte>();
            if (vector.Length < VectorSize || !IsVector(vector, 0, out var first, out var bytes) || bytes <= 0 ||
                bytes % StatEntry != 0 || bytes / StatEntry > MaxStats)
            {
                return false;
            }

            var entry = memory.Read(first, StatEntry);
            return entry.Length == StatEntry && BitConverter.ToInt32(entry, 0) is > 0 and < MaxStatKey;
        }

        /// <summary>A pointer to an item's entity, as a ground item holds it: its path lies under <c>Metadata/Items/</c>.</summary>
        public static bool IsItemEntity(byte[] block, int at, IAreaMemory memory) =>
            at >= 0 && at + Pointer <= block.Length && IsPointer(BitConverter.ToInt64(block, at)) &&
            memory.EntityPath(BitConverter.ToInt64(block, at)).StartsWith(ItemFolder, StringComparison.Ordinal);

        /// <summary>The camera matrix: the game keeps it twice, one copy right after the other.</summary>
        public static bool IsCameraMatrix(byte[] block, int at)
        {
            if (at + (2 * MatrixBytes) > block.Length)
            {
                return false;
            }

            var matrix = block.AsSpan(at, MatrixBytes);
            if (!matrix.SequenceEqual(block.AsSpan(at + MatrixBytes, MatrixBytes)))
            {
                return false;
            }

            var cells = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(matrix);
            var nonZero = 0;
            foreach (var cell in cells)
            {
                if (!float.IsFinite(cell))
                {
                    return false;
                }

                nonZero += cell != 0 ? 1 : 0;
            }

            return nonZero >= cells.Length / 2;
        }

        /// <summary>Three floats that could be a world position inside the map.</summary>
        public static bool IsWorldPosition(byte[] block, int at, float mapWidth, float mapHeight)
        {
            if (at + (3 * sizeof(float)) > block.Length)
            {
                return false;
            }

            var (x, y, z) = (BitConverter.ToSingle(block, at), BitConverter.ToSingle(block, at + 4), BitConverter.ToSingle(block, at + 8));
            return float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z) &&
                   x > 0 && x < mapWidth && y > 0 && y < mapHeight && MathF.Abs(z) < MaxWorld;
        }

        private static bool IsPointer(long value) => value > MinPointer && value < MaxPointer;

        private static long Long(byte[] bytes, int at) => bytes.Length >= at + Pointer ? BitConverter.ToInt64(bytes, at) : 0;

        private static bool IsVector(byte[] block, int at, out long first, out long bytes)
        {
            first = 0;
            bytes = 0;
            if (at < 0 || at + VectorSize > block.Length)
            {
                return false;
            }

            first = BitConverter.ToInt64(block, at);
            var last = BitConverter.ToInt64(block, at + Pointer);
            var end = BitConverter.ToInt64(block, at + (2 * Pointer));
            bytes = last - first;
            return first == 0 ? last == 0 && end == 0 : IsPointer(first) && IsPointer(end) && first <= last && last <= end;
        }

        private static bool IsId(string text) =>
            text.Length >= 3 && text.All(letter => letter is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');
    }
}
