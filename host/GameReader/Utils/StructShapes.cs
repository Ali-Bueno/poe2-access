// <copyright file="StructShapes.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>Where a stat list keeps its vector of pairs, how long a pair is, and where it keeps the stat's key and value.</summary>
    public readonly record struct StatLayout(int List, int Stride, int Key, int Value);

    /// <summary>What a rule that every entity must keep says, over the entities it was read on.</summary>
    public enum InvariantVerdict
    {
        /// <summary>Too few entities, or neither clearly kept nor clearly broken.</summary>
        NotJudged,

        /// <summary>Nearly every entity keeps it.</summary>
        Holds,

        /// <summary>Most entities break it, in more than one area: the field is not where it is read.</summary>
        Fails,
    }

    /// <summary>
    ///     Counts, over the areas of one game process, how many entities keep a rule their field must keep
    ///     (<see cref="StructShapes.IsFlag"/> and the like): the only live check of the fields nothing but the game's code
    ///     places. It can tell, never repair: a rule that fails reports its part and moves nothing. An entity counts once
    ///     per area (a flask is the same entity in every area, a second sweep of one area adds nothing).
    /// </summary>
    public sealed class InvariantTally
    {
        /// <summary>Entities a rule must have been read on before it says anything, and that must break it before it fails.</summary>
        public const int MinEntities = 5;

        /// <summary>Areas the entities that break a rule must come from: one area can hold one odd kind of thing.</summary>
        public const int MinAreas = 2;

        // A rule holds while at most one entity in ten breaks it (an entity freed while it was read).
        private const int KeptPerBroken = 10;
        private const int MaxRemembered = 4096;

        private readonly Dictionary<string, Rule> rules = new(StringComparer.Ordinal);

        /// <summary>Notes whether <paramref name="entity"/>, met in <paramref name="area"/>, keeps the rule; one seen there before is not counted again.</summary>
        public void Add(string rule, long area, long entity, bool keeps)
        {
            lock (this.rules)
            {
                if (!this.rules.TryGetValue(rule, out var tally))
                {
                    this.rules[rule] = tally = new Rule();
                }

                if (tally.Entities.Count >= MaxRemembered || !tally.Entities.Add((area, entity)))
                {
                    return;
                }

                if (keeps)
                {
                    tally.Kept++;
                }
                else
                {
                    tally.Broken++;
                    tally.BrokenAreas.Add(area);
                }
            }
        }

        /// <summary>What the rule says so far, and on how many entities.</summary>
        public (InvariantVerdict Verdict, int Kept, int Seen) Verdict(string rule)
        {
            lock (this.rules)
            {
                if (!this.rules.TryGetValue(rule, out var tally))
                {
                    return (InvariantVerdict.NotJudged, 0, 0);
                }

                var seen = tally.Kept + tally.Broken;
                var verdict = seen < MinEntities ? InvariantVerdict.NotJudged
                    : tally.Broken >= MinEntities && tally.Broken * 2 >= seen && tally.BrokenAreas.Count >= MinAreas ? InvariantVerdict.Fails
                    : tally.Broken * KeptPerBroken <= seen ? InvariantVerdict.Holds
                    : InvariantVerdict.NotJudged;
                return (verdict, tally.Kept, seen);
            }
        }

        /// <summary>A new game process: nothing counted before says anything of it.</summary>
        public void Clear()
        {
            lock (this.rules)
            {
                this.rules.Clear();
            }
        }

        private sealed class Rule
        {
            public HashSet<(long Area, long Entity)> Entities { get; } = new();

            public HashSet<long> BrokenAreas { get; } = new();

            public int Kept { get; set; }

            public int Broken { get; set; }
        }
    }

    /// <summary>
    ///     What a field must hold, so it is recognised wherever a game patch left it: facts that cannot hold by chance, such
    ///     as a pointer back to its own component or a chain that ends in a buff's name.
    /// </summary>
    public static class StructShapes
    {
        // Far above what any flask or charm holds or spends (under a hundred charges in 0.5.5f); a pointer's half is not.
        private const int MaxCharges = 10_000;

        // No body is several screens wide (a screen is about 1400 world units); a pointer's half read as a float is tiny or huge.
        private const float MaxBodySize = 10_000f;

        /// <summary>A C++ bool as the game writes it: 0 or 1. A byte of anything else there is another field.</summary>
        public static bool IsFlag(byte value) => value <= 1;

        /// <summary>A rarity within the game's enum, 0 to <paramref name="last"/> (unique).</summary>
        public static bool IsRarity(int value, int last) => value >= 0 && value <= last;

        /// <summary>A stack holds one item at least and no more than its kind allows (<paramref name="max"/>; under 1 when that was not read).</summary>
        public static bool IsStackCount(int count, int max) => count >= 1 && (max < 1 || count <= max);

        /// <summary>A flask's charges: none or more held, one or more spent per use, neither a pointer's half.</summary>
        public static bool IsCharges(int current, int perUse) => current is >= 0 and <= MaxCharges && perUse is >= 1 and <= MaxCharges;

        /// <summary>A body's three sizes: numbers, none negative or absurd. A weak rule (other floats pass it); it catches a pointer or an id read as sizes.</summary>
        public static bool IsBodySize(float x, float y, float z) =>
            float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z) && x >= 0 && y >= 0 && z >= 0 && x < MaxBodySize && y < MaxBodySize && z < MaxBodySize;

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

        // A pair of ints in 0.5.5f; a patch may add a field or two to it.
        private const int MaxStatEntry = 0x18;

        // A state machine's states: the reader's own bound on their count, a definition 0xC0 bytes in 0.5.5f with its name
        // first, as an MSVC string (text or pointer, then length at +0x10 and capacity at +0x18, 15 characters inline).
        private const int MaxStates = 256;
        private const int MaxStateStride = 0x200;
        private const int MaxStateName = 64;
        private const int StdStringBytes = 0x20;
        private const int StdStringLength = 0x10;
        private const int StdStringCapacity = 0x18;
        private const int StdStringInline = 15;
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

        /// <summary>The offset nearest <paramref name="start"/> that fits, within 0..<paramref name="limit"/>; negative when none does.</summary>
        public static int Nearest(int start, int step, int limit, Func<int, bool> fits)
        {
            for (var distance = 0; distance <= Math.Max(limit, start); distance += step)
            {
                if (start + distance <= limit && fits(start + distance))
                {
                    return start + distance;
                }

                if (distance > 0 && start - distance >= 0 && start - distance <= limit && fits(start - distance))
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

        /// <summary>
        ///     A list of buffs: pointers whose definitions hold, at <paramref name="name"/> (their start in 0.5.5f), a pointer to a
        ///     lower-case id such as "herald_of_thunder".
        /// </summary>
        public static bool IsBuffList(byte[] block, int at, IAreaMemory memory, int definition, int name = 0)
        {
            if (!IsVector(block, at, out var first, out var bytes) || bytes <= 0 || bytes % Pointer != 0 || bytes / Pointer > MaxBuffs)
            {
                return false;
            }

            var entry = Long(memory.Read(first, Pointer), 0);
            var row = IsPointer(entry) ? Long(memory.Read(entry + definition, Pointer), 0) : 0;
            var id = IsPointer(row) ? Long(memory.Read(row + name, Pointer), 0) : 0;
            // As IsBuffId reads an id, capitals too ("AoD_beam_degen"): a healthy list whose first buff is one of those must
            // not fail here and send the sweep looking for the name in another column.
            return IsPointer(id) && memory.WideText(id) is { Length: >= MinIdLength } text && IsBuffId(text);
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

        /// <summary>A pointer to a stat list laid out as <paramref name="layout"/>: key and value pairs with small keys.</summary>
        public static bool IsStatsPointer(byte[] block, int at, IAreaMemory memory, StatLayout layout)
        {
            var pointer = at + Pointer <= block.Length ? BitConverter.ToInt64(block, at) : 0;
            var vector = IsPointer(pointer) ? memory.Read(pointer + layout.List, VectorSize) : Array.Empty<byte>();
            if (vector.Length < VectorSize || !IsVector(vector, 0, out var first, out var bytes) || bytes <= 0 ||
                bytes % layout.Stride != 0 || bytes / layout.Stride > MaxStats)
            {
                return false;
            }

            var entry = memory.Read(first, layout.Stride);
            return entry.Length == layout.Stride && BitConverter.ToInt32(entry, layout.Key) is > 0 and < MaxStatKey;
        }

        /// <summary>
        ///     Where a stat list keeps its vector and how a pair is laid out, nearest <paramref name="near"/> (the measured layout
        ///     first, then the smallest pair that fits, as a multiple of the pair fits too): every key small and distinct, and one
        ///     pair holds stat <paramref name="key"/> with <paramref name="value"/>, a value the game shows elsewhere (the player's
        ///     level, from the Player component). Null when none does.
        /// </summary>
        public static StatLayout? FindStatLayout(byte[] list, IAreaMemory memory, StatLayout near, int key, int value)
        {
            StatLayout? found = null;
            Nearest(near.List, Pointer, list.Length - VectorSize, at =>
            {
                if (!IsVector(list, at, out var first, out var bytes) || bytes <= 0 || bytes > MaxStats * MaxStatEntry)
                {
                    return false;
                }

                var pairs = memory.Read(first, (int)bytes);
                if (pairs.Length < bytes)
                {
                    return false;
                }

                var measured = near with { List = at };
                if (HoldsStat(pairs, measured, key, value))
                {
                    found = measured;
                    return true;
                }

                for (var stride = StatEntry; stride <= MaxStatEntry && found is null; stride += sizeof(int))
                {
                    var size = stride;
                    Nearest(near.Key, sizeof(int), size - sizeof(int), keyAt => Nearest(near.Value, sizeof(int), size - sizeof(int), valueAt =>
                    {
                        var layout = new StatLayout(at, size, keyAt, valueAt);
                        found = keyAt != valueAt && layout != measured && HoldsStat(pairs, layout, key, value) ? layout : null;
                        return found is not null;
                    }) >= 0);
                }

                return found is not null;
            });
            return found;
        }

        /// <summary>
        ///     Where a state machine's states object keeps its list of state definitions and how long a definition is, nearest
        ///     <paramref name="near"/>: every one of the <paramref name="count"/> definitions begins with its name, distinct ASCII
        ///     words such as "activated". Null when none does.
        /// </summary>
        public static (int List, int Stride)? FindStateLayout(byte[] states, IAreaMemory memory, int count, (int List, int Stride) near)
        {
            if (count <= 0 || count > MaxStates)
            {
                return null;
            }

            (int List, int Stride)? found = null;
            Nearest(near.List, Pointer, states.Length - Pointer, at =>
            {
                var first = Long(states, at);
                if (!IsPointer(first))
                {
                    return false;
                }

                // Read as long as each stride makes the list: past the list's end the game may have nothing mapped.
                var stride = Nearest(near.Stride, Pointer, MaxStateStride, size =>
                    size >= StdStringBytes && memory.Read(first, count * size) is { } bytes && bytes.Length == count * size && NamesStates(bytes, size, count, memory));
                found = stride > 0 ? (at, stride) : null;
                return found is not null;
            });
            return found;
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

        /// <summary>A value that can be a pointer into the game's user space.</summary>
        public static bool IsPointer(long value) => value > MinPointer && value < MaxPointer;

        /// <summary>The 8 bytes at <paramref name="at"/>; zero outside <paramref name="bytes"/>.</summary>
        public static long Long(byte[] bytes, int at) => at >= 0 && bytes.Length >= at + Pointer ? BitConverter.ToInt64(bytes, at) : 0;

        /// <summary>The 4 bytes at <paramref name="at"/>; zero outside <paramref name="bytes"/>.</summary>
        public static int Int(byte[] bytes, int at) => at >= 0 && bytes.Length >= at + sizeof(int) ? BitConverter.ToInt32(bytes, at) : 0;

        /// <summary>An MSVC vector (first, last, end) at <paramref name="at"/>, empty or of pointers in order; its first and its size in bytes.</summary>
        public static bool IsVector(byte[] block, int at, out long first, out long bytes)
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

        /// <summary>Whether <paramref name="count"/> definitions of <paramref name="stride"/> bytes each begin with a distinct state name.</summary>
        private static bool NamesStates(byte[] definitions, int stride, int count, IAreaMemory memory)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < count; index++)
            {
                var at = index * stride;
                var length = Long(definitions, at + StdStringLength);
                var capacity = Long(definitions, at + StdStringCapacity);
                if (length is <= 0 or > MaxStateName || capacity < length)
                {
                    return false;
                }

                // An MSVC string keeps up to 15 characters inline, longer ones behind a pointer.
                var text = capacity <= StdStringInline ? definitions.AsSpan(at, (int)length).ToArray() : memory.Read(Long(definitions, at), (int)length);
                if (text.Length != length || !text.All(letter => letter is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'0' and <= (byte)'9') or (byte)'_') ||
                    !names.Add(System.Text.Encoding.ASCII.GetString(text)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Pairs laid out as <paramref name="layout"/>: every key small and distinct, one of them <paramref name="key"/> holding <paramref name="value"/>.</summary>
        private static bool HoldsStat(byte[] pairs, StatLayout layout, int key, int value)
        {
            if (pairs.Length % layout.Stride != 0)
            {
                return false;
            }

            var keys = new HashSet<int>();
            var holds = false;
            for (var at = 0; at < pairs.Length; at += layout.Stride)
            {
                var stat = BitConverter.ToInt32(pairs, at + layout.Key);
                if (stat is <= 0 or >= MaxStatKey || !keys.Add(stat))
                {
                    return false;
                }

                holds |= stat == key && BitConverter.ToInt32(pairs, at + layout.Value) == value;
            }

            return holds;
        }

        /// <summary>
        ///     Whether a text reads as a buff definition's id: letters, digits and underscores, as all 599 ids in
        ///     catalog/buff-names.json (3 to 57 characters, some with capitals such as "AoD_beam_degen").
        /// </summary>
        public static bool IsBuffId(string text) =>
            text.Length > 0 && text.All(letter => char.IsAsciiLetterOrDigit(letter) || letter == '_');

        /// <summary>The shortest id of the game's tables (buff-names.json: 3 characters).</summary>
        private const int MinIdLength = 3;

        private static bool IsId(string text) =>
            text.Length >= MinIdLength && text.All(letter => letter is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');
    }
}
