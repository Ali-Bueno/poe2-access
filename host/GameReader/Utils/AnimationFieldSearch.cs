// <copyright file="AnimationFieldSearch.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;

    /// <summary>
    ///     Finds an Actor's current animation by watching actors act. One reading says nothing: the id is a small number, zero
    ///     at rest. But the game keeps a base layer far from the animation that shows, and the two agree unless a second layer
    ///     plays on top (0.5.5f: +0x380 and +0x8B0; a crossbow's shot shows on the idle base layer, which is why they differ a third of the
    ///     time while shooting and never for a run or a monk's blow; docs/compatibility.md). Pure.
    /// </summary>
    public sealed class AnimationFieldSearch
    {
        // An id is a row of the Animation table, some 1100 of them in 0.5.5f.
        private const int RowLimit = 0x1000;

        // At rest, in action and at rest again.
        private const int MinChanges = 2;

        // A flag or a speed stored twice agrees too, with one value; the id of a run and of a blow do not.
        private const int MinValues = 2;
        private const int MaxValues = 8;

        // Readings in action, and neighbours are one value stored twice, not two copies of it.
        private const int MinAgreed = 20;
        private const int MinGap = 0x100;

        // Confirming the measured place is cheap; moving a healthy field is not, so a move takes far more.
        private const int MinAgreedToMove = 200;
        private const int MinValuesToMove = 3;

        // A layer on top parts them for a while (a third of the readings while shooting), and no more.
        private const int AgreedPerDiffered = 1;
        private const int ReadingsPerOutside = 100;

        // The upper half of a pointer is a small number too, after a large one.
        private const int PointerHalfFrom = 0x100;
        private const uint PointerLowFrom = 0x10000;

        // With this many actors seen acting over this many readings, some minutes of play, a pair must have shown. Something in
        // eight actors changes within half a second of arriving (a false alarm, live 2026-11-06): the readings are what count.
        private const int EnoughActors = 8;
        private const int EnoughReadings = 60000;
        private const int MaxRemembered = 256;

        private readonly Dictionary<long, int[]> last = new();
        private readonly List<Pair> pairs = new();
        private readonly HashSet<long> acted = new();
        private readonly List<int> live = new();
        private int[] changes = Array.Empty<int>();
        private int[] outside = Array.Empty<int>();
        private int[] nonZero = Array.Empty<int>();
        private int[] pointerHalves = Array.Empty<int>();
        private HashSet<int>[] values = Array.Empty<HashSet<int>>();
        private int readings;
        private int evidence;

        /// <summary>Gets a value indicating whether enough actors acted for the pair to have shown, if there is one.</summary>
        public bool Enough => this.acted.Count >= EnoughActors && this.evidence >= EnoughReadings;

        /// <summary>
        ///     One reading of one actor, from the struct's start. <paramref name="counts"/> is false for a reading that helps find the
        ///     pair but says nothing of its absence: a player who only runs shows a single id, however long.
        /// </summary>
        public void Add(long actor, ReadOnlySpan<byte> block, bool counts = true)
        {
            var fields = MemoryMarshal.Cast<byte, int>(block);
            if (fields.Length < 2 || (fields[0] == 0 && fields[1] == 0))
            {
                return; // no longer an actor: its table pointer is gone
            }

            this.Size(fields.Length);
            this.readings++;
            this.evidence += counts ? 1 : 0;
            this.last.TryGetValue(actor, out var before);
            for (var index = 0; index < fields.Length && (before == null || index < before.Length); index++)
            {
                this.Read(actor, index, fields, before, counts);
            }

            foreach (var pair in this.pairs)
            {
                if (pair.First < fields.Length && pair.Second < fields.Length && (fields[pair.First] != 0 || fields[pair.Second] != 0))
                {
                    pair.Agreed += fields[pair.First] == fields[pair.Second] ? 1 : 0;
                    pair.Differed += fields[pair.First] == fields[pair.Second] ? 0 : 1;
                }
            }

            if (this.last.Count >= MaxRemembered)
            {
                this.last.Clear();
            }

            this.last[actor] = fields.ToArray();
        }

        /// <summary>The copy nearest <paramref name="near"/>; -1 until exactly one set of fields has agreed. Both read the same.</summary>
        public int Find(int near)
        {
            var agreeing = this.pairs.Where(this.Agrees).ToList();
            var fields = agreeing.SelectMany(pair => new[] { pair.First, pair.Second }).Distinct().ToList();
            if (fields.Count == 0 || agreeing.Count != fields.Count * (fields.Count - 1) / 2)
            {
                return -1;
            }

            return fields.Select(field => field * sizeof(int)).OrderBy(offset => Math.Abs(offset - near)).First();
        }

        /// <summary>What the search has seen so far, for the log.</summary>
        public string Describe()
        {
            var best = this.pairs.OrderByDescending(pair => pair.Agreed).Take(6)
                .Select(pair => $"+0x{pair.First * sizeof(int):X}/+0x{pair.Second * sizeof(int):X} {pair.Agreed}/{pair.Differed} values {this.values[pair.First].Count}");
            return $"{this.readings} readings, {this.evidence} of enemies, {this.acted.Count} acted, {this.live.Count} live fields, {this.pairs.Count} pairs: {string.Join("; ", best)}";
        }

        /// <summary>Whether what the pair at <paramref name="offset"/> showed is enough to move a field: a healthy one must not move on a coincidence.</summary>
        public bool Proven(int offset)
        {
            var field = offset / sizeof(int);
            return this.pairs.Any(pair => (pair.First == field || pair.Second == field) && this.Agrees(pair) && pair.Agreed >= MinAgreedToMove &&
                                          this.values[pair.First].Count >= MinValuesToMove);
        }

        /// <summary>Whether the field at <paramref name="offset"/> behaves as an id on its own, for a game that stopped keeping a copy.</summary>
        public bool Behaves(int offset)
        {
            var field = offset / sizeof(int);
            return field < this.changes.Length && this.Fits(field) && this.changes[field] >= MinChanges && this.values[field].Count >= MinValues;
        }

        private void Size(int length)
        {
            if (this.changes.Length < length)
            {
                var known = this.changes.Length;
                Array.Resize(ref this.changes, length);
                Array.Resize(ref this.outside, length);
                Array.Resize(ref this.nonZero, length);
                Array.Resize(ref this.pointerHalves, length);
                Array.Resize(ref this.values, length);
                for (var index = known; index < length; index++)
                {
                    this.values[index] = new HashSet<int>();
                }
            }
        }

        private void Read(long actor, int index, ReadOnlySpan<int> fields, int[]? before, bool counts)
        {
            var value = fields[index];
            if (value is < 0 or >= RowLimit)
            {
                this.outside[index]++;
            }
            else if (value != 0)
            {
                this.nonZero[index]++;
                this.pointerHalves[index] += value >= PointerHalfFrom && index > 0 && (uint)fields[index - 1] >= PointerLowFrom ? 1 : 0;
                if (this.values[index].Count < MaxValues)
                {
                    this.values[index].Add(value);
                }
            }

            if (before != null && (before[index] == 0) != (value == 0) && ++this.changes[index] >= MinChanges && this.Fits(index))
            {
                this.Watch(index);
                if (counts)
                {
                    this.acted.Add(actor);
                }
            }
        }

        private void Watch(int index)
        {
            if (!this.live.Contains(index))
            {
                foreach (var other in this.live)
                {
                    this.pairs.Add(new Pair(Math.Min(index, other), Math.Max(index, other)));
                }

                this.live.Add(index);
            }
        }

        private bool Agrees(Pair pair) =>
            (pair.Second - pair.First) * sizeof(int) >= MinGap &&
            pair.Agreed >= MinAgreed &&
            pair.Differed * AgreedPerDiffered <= pair.Agreed &&
            this.Fits(pair.First) && this.Fits(pair.Second) &&
            this.values[pair.First].Count >= MinValues && this.values[pair.Second].Count >= MinValues;

        private bool Fits(int field) =>
            this.outside[field] * ReadingsPerOutside <= this.readings &&
            this.pointerHalves[field] * 2 <= this.nonZero[field];

        /// <summary>Two fields watched together, and how often they agreed while either was not at rest.</summary>
        private sealed class Pair
        {
            public Pair(int first, int second) => (this.First, this.Second) = (first, second);

            public int First { get; }

            public int Second { get; }

            public int Agreed { get; set; }

            public int Differed { get; set; }
        }
    }
}
