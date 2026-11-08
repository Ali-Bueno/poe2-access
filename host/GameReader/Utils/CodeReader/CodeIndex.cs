// <copyright file="CodeIndex.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils.CodeReader
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;

    /// <summary>
    ///     The exe's code as functions and cross references: the <c>.pdata</c> fragments, every direct call by target and every
    ///     RIP-relative <c>lea</c> by target, built in one pass, and decoding that stops at the first unknown encoding.
    /// </summary>
    public sealed class CodeIndex
    {
        private const int FunctionRowSize = 12;
        private const byte CallOpcode = 0xE8;
        private const int CallLength = 5;
        private const byte LeaOpcode = 0x8D;
        private const int LeaLength = 7;

        // REX.W with or without REX.R (48, 4C), and a ModRM of [rip+disp32] (mod 0, rm 5).
        private const byte RexWMask = 0xFB;
        private const byte RexW = 0x48;
        private const byte RipModRmMask = 0xC7;
        private const byte RipModRm = 0x05;

        // UNWIND_INFO: flags in the top five bits of byte 0, the code count in byte 2, codes from byte 4, two bytes each.
        private const int UnwindFlagShift = 3;
        private const int ChainInfoFlag = 4;
        private const int UnwindCountAt = 2;
        private const int UnwindCodesAt = 4;
        private const int UnwindCodeSize = 2;
        private const int MaxChain = 16;

        /// <summary>How many fragments after a function's first <see cref="AllOf"/> looks at for its chained parts.</summary>
        private const int ChainedFragmentsAfter = 300;
        private const int ChainedFragmentsBefore = 5;

        /// <summary>The longest leaf function (no <c>.pdata</c> row) decoded up to its <c>ret</c>.</summary>
        private const int MaxLeafBytes = 0x3000;

        private readonly int[] begin;
        private readonly int[] end;
        private readonly int[] unwind;
        private readonly long[] callTargets;
        private readonly int[] callSites;
        private readonly long[] leaTargets;
        private readonly int[] leaSites;

        public CodeIndex(PeImage image)
        {
            this.Image = image;
            (this.begin, this.end, this.unwind) = this.Functions();
            (this.callTargets, this.callSites, this.leaTargets, this.leaSites) = this.References();
        }

        /// <summary>Gets the image indexed.</summary>
        public PeImage Image { get; }

        /// <summary>Gets the number of <c>.pdata</c> fragments.</summary>
        public int FragmentCount => this.begin.Length;

        /// <summary>Gets the RVAs of every RIP-relative <c>lea</c> target, sorted (with repeats).</summary>
        public ReadOnlySpan<long> LeaTargets => this.leaTargets;

        /// <summary>The <c>.pdata</c> fragment at a position of the sorted table.</summary>
        public (long Start, long End) FragmentAt(int index) => (this.begin[index], this.end[index]);

        /// <summary>The direct calls to <paramref name="target"/>.</summary>
        public ReadOnlySpan<int> Callers(long target) => Sites(this.callTargets, this.callSites, target);

        /// <summary>The RIP-relative <c>lea</c> instructions loading <paramref name="target"/>.</summary>
        public ReadOnlySpan<int> LeaSites(long target) => Sites(this.leaTargets, this.leaSites, target);

        /// <summary>The <c>.pdata</c> fragment holding the RVA, as start and end; null when none does (a leaf).</summary>
        public (long Start, long End)? Fragment(long rva) => this.FragmentIndex(rva) is var index and >= 0 ? (this.begin[index], this.end[index]) : null;

        /// <summary>The start of the function holding the RVA, through chained unwind data; null for a leaf.</summary>
        public long? FunctionStart(long rva)
        {
            var index = this.FragmentIndex(rva);
            if (index < 0)
            {
                return null;
            }

            var (start, info) = ((long)this.begin[index], (long)this.unwind[index]);
            for (var depth = 0; depth < MaxChain && (info & 1) == 0 && ((this.Image.Byte(info) >> UnwindFlagShift) & ChainInfoFlag) != 0; depth++)
            {
                var count = this.Image.Byte(info + UnwindCountAt);
                var parent = info + UnwindCodesAt + (((count + 1) & ~1) * UnwindCodeSize);
                (start, info) = (this.Image.UInt32(parent), this.Image.UInt32(parent + (2 * sizeof(uint))));
            }

            return start;
        }

        /// <summary>
        ///     The instructions from <paramref name="start"/> to <paramref name="stop"/>, up to the first switch table the code
        ///     indexes ahead of itself (<c>[base+index*4+table]</c>). At an unknown encoding they end after the last instruction
        ///     that does not fall through, the rest taken as data; null when there is none. Either way what is returned is complete.
        /// </summary>
        public List<X64Instruction>? Decode(long start, long stop)
        {
            var list = new List<X64Instruction>();
            for (var at = start; at < stop;)
            {
                if (!X64Decoder.TryDecode(this.Image.Bytes(at, Math.Min(X64.MaxLength, stop - at)), at, out var instruction))
                {
                    var last = list.FindLastIndex(known => known.Kind is InstructionKind.Ret or InstructionKind.Jmp or InstructionKind.Int3);
                    if (last < 0)
                    {
                        return null;
                    }

                    list.RemoveRange(last + 1, list.Count - last - 1);
                    return list;
                }

                list.Add(instruction);
                at = instruction.End;
                if (instruction.HasMemory && instruction.Index != X64.NoRegister && instruction.Displacement > at && instruction.Displacement < stop)
                {
                    stop = instruction.Displacement;
                }
            }

            return list;
        }

        /// <summary>
        ///     Up to <paramref name="count"/> instructions from <paramref name="start"/>, ending after the first that
        ///     <paramref name="stop"/> accepts (or before an int3, with <paramref name="beforeInt3"/>); null when an unknown encoding comes first.
        /// </summary>
        public List<X64Instruction>? Walk(long start, int count, long maxBytes, Func<X64Instruction, bool> stop, bool beforeInt3 = false)
        {
            var list = new List<X64Instruction>();
            for (var at = start; list.Count < count && at < start + maxBytes;)
            {
                var left = start + maxBytes - at;
                if (!X64Decoder.TryDecode(this.Image.Bytes(at, Math.Min(X64.MaxLength, left)), at, out var instruction))
                {
                    // An instruction cut by the window's end ends the walk; an unknown one inside it voids the walk.
                    return left < X64.MaxLength ? list : null;
                }

                if (beforeInt3 && instruction.Kind == InstructionKind.Int3)
                {
                    break;
                }

                list.Add(instruction);
                at = instruction.End;
                if (stop(instruction))
                {
                    break;
                }
            }

            return list;
        }

        /// <summary>The fragment starting at <paramref name="start"/>, or a leaf up to its <c>ret</c>, int3 or indirect jump.</summary>
        public List<X64Instruction>? Body(long start) =>
            this.Fragment(start) is { } fragment && fragment.Start == start
                ? this.Decode(fragment.Start, fragment.End)
                : this.Walk(start, int.MaxValue, MaxLeafBytes, IsLeafEnd, beforeInt3: true);

        /// <summary>Every fragment of the function starting at <paramref name="start"/>, chained ones included, or its <see cref="Body"/>.</summary>
        public List<X64Instruction>? AllOf(long start)
        {
            var first = this.FragmentIndex(start);
            if (first < 0)
            {
                return this.Body(start);
            }

            var all = new List<X64Instruction>();
            var last = Math.Min(this.begin.Length, first + ChainedFragmentsAfter);
            for (var index = Math.Max(0, first - ChainedFragmentsBefore); index < last; index++)
            {
                if (this.FunctionStart(this.begin[index]) == start)
                {
                    if (this.Decode(this.begin[index], this.end[index]) is not { } part)
                    {
                        return null;
                    }

                    all.AddRange(part);
                }
            }

            return all.Count > 0 ? all : this.Body(start);
        }

        private static bool IsLeafEnd(X64Instruction instruction) =>
            instruction.Kind == InstructionKind.Ret || (instruction.Kind == InstructionKind.Jmp && instruction.First != OperandKind.Immediate);

        private static ReadOnlySpan<int> Sites(long[] targets, int[] sites, long target)
        {
            var low = LowerBound(targets, target);
            var high = low;
            while (high < targets.Length && targets[high] == target)
            {
                high++;
            }

            return sites.AsSpan(low, high - low);
        }

        private static int LowerBound(long[] sorted, long value)
        {
            var (low, high) = (0, sorted.Length);
            while (low < high)
            {
                var middle = (low + high) >>> 1;
                (low, high) = sorted[middle] < value ? (middle + 1, high) : (low, middle);
            }

            return low;
        }

        private int FragmentIndex(long rva)
        {
            var (low, high) = (0, this.begin.Length);
            while (low < high)
            {
                var middle = (low + high) >>> 1;
                (low, high) = this.begin[middle] <= rva ? (middle + 1, high) : (low, middle);
            }

            return low > 0 && rva < this.end[low - 1] ? low - 1 : -1;
        }

        private (int[] Begin, int[] End, int[] Unwind) Functions()
        {
            var table = this.Image.Bytes(this.Image.Exceptions.Start, this.Image.Exceptions.VirtualSize);
            var rows = new List<(int Begin, int End, int Unwind)>(table.Length / FunctionRowSize);
            for (var at = 0; at + FunctionRowSize <= table.Length; at += FunctionRowSize)
            {
                var row = table.Slice(at, FunctionRowSize);
                var start = BinaryPrimitives.ReadInt32LittleEndian(row);
                if (start != 0)
                {
                    rows.Add((start, BinaryPrimitives.ReadInt32LittleEndian(row[sizeof(int)..]), BinaryPrimitives.ReadInt32LittleEndian(row[(2 * sizeof(int))..])));
                }
            }

            rows.Sort((left, right) => left.Begin.CompareTo(right.Begin));
            var (begins, ends, unwinds) = (new int[rows.Count], new int[rows.Count], new int[rows.Count]);
            for (var index = 0; index < rows.Count; index++)
            {
                (begins[index], ends[index], unwinds[index]) = rows[index];
            }

            return (begins, ends, unwinds);
        }

        private (long[] CallTargets, int[] CallSites, long[] LeaTargets, int[] LeaSites) References()
        {
            var code = this.Image.Bytes(this.Image.Code.Start, this.Image.Code.Size);
            var origin = this.Image.Code.Start;
            var (callTargets, callSites) = (new List<long>(), new List<int>());
            for (var at = code.IndexOf(CallOpcode); at >= 0 && at + CallLength <= code.Length;)
            {
                callTargets.Add(origin + at + CallLength + BinaryPrimitives.ReadInt32LittleEndian(code[(at + 1)..]));
                callSites.Add((int)(origin + at));
                var next = code[(at + 1)..].IndexOf(CallOpcode);
                at = next < 0 ? -1 : at + 1 + next;
            }

            var (leaTargets, leaSites) = (new List<long>(), new List<int>());
            for (var at = code.IndexOf(LeaOpcode); at >= 0 && at + LeaLength - 1 <= code.Length;)
            {
                if (at > 0 && (code[at - 1] & RexWMask) == RexW && (code[at + 1] & RipModRmMask) == RipModRm)
                {
                    leaTargets.Add(origin + at - 1 + LeaLength + BinaryPrimitives.ReadInt32LittleEndian(code[(at + 2)..]));
                    leaSites.Add((int)(origin + at - 1));
                }

                var next = code[(at + 1)..].IndexOf(LeaOpcode);
                at = next < 0 ? -1 : at + 1 + next;
            }

            var (calls, callAt) = (callTargets.ToArray(), callSites.ToArray());
            var (leas, leaAt) = (leaTargets.ToArray(), leaSites.ToArray());
            SortByTarget(calls, callAt);
            SortByTarget(leas, leaAt);
            return (calls, callAt, leas, leaAt);
        }

        /// <summary>Sorts by target, and the sites of one target in address order, as the rules take a label's sites in order.</summary>
        private static void SortByTarget(long[] targets, int[] sites)
        {
            Array.Sort(targets, sites);
            for (var run = 0; run < targets.Length;)
            {
                var next = run + 1;
                while (next < targets.Length && targets[next] == targets[run])
                {
                    next++;
                }

                Array.Sort(sites, run, next - run);
                run = next;
            }
        }
    }
}
