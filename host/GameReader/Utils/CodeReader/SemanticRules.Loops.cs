// <copyright file="SemanticRules.Loops.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils.CodeReader
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The rules that read a loop over a vector (its stride and the fields of the entries), and the fields a world
    ///     slot's callee reads (docs/research/exe-layouts.md, "The entry fields").
    /// </summary>
    public sealed partial class SemanticRules
    {
        /// <summary>A vector's begin is loaded and compared with its end (the next pointer) within this many instructions.</summary>
        private const int BeginToEnd = 6;

        /// <summary>The iterator's add is followed by the compare with the end within this many instructions.</summary>
        private const int AddToEnd = 3;

        /// <summary>The end pointer of a std::vector follows its begin.</summary>
        private const int VectorEnd = sizeof(long);

        /// <summary>One level down from the iterator: the object each entry points at.</summary>
        private const int EntryLevel = 1;

        /// <summary>The immediate the loop over the first vector read after the rule's site adds to its iterator.</summary>
        private List<long> Stride(SemanticRule rule) => this.Loops(rule).Select(loop => LoopStride(loop.Code, loop.Begin)).OfType<long>().ToList();

        /// <summary>The k-th distinct field that loop reads through the pointer each entry holds.</summary>
        private List<long> Entry(SemanticRule rule) => this.Loops(rule).Select(loop => EntryField(loop.Code, loop.Begin, rule.K)).OfType<long>().ToList();

        /// <summary>After <c>GEAL::GetGameWorld()</c> and the world's virtual slot, the k-th distinct field the callee taking the result reads through it.</summary>
        private List<long> Leaf(SemanticRule rule)
        {
            if (this.GameWorld() is not { } world || this.anchors.Evaluator(rule.Name) is not { } evaluator || this.Index.AllOf(evaluator) is not { } code)
            {
                return new List<long>();
            }

            var from = code.FindIndex(instruction => instruction.Kind == InstructionKind.Call && instruction.DirectTarget == world);
            // The evaluator short-circuits with a jump before it calls the leaf, so the sweep goes past jumps.
            if (from < 0 || this.WorldCall(code, from + 1, X64.Rax, descend: true) is not var (_, next) || CalleeTaking(code, next, pastJumps: true) is not { } callee
                || this.Index.Body(callee) is not { } body)
            {
                return new List<long>();
            }

            var fields = FieldsThrough(body, X64.Rcx);
            return fields.Count > rule.K ? new List<long> { fields[rule.K] } : new List<long>();
        }

        /// <summary>Every distinct field (indexed reads included) the code reads through the register and its copies, in order.</summary>
        private static List<long> FieldsThrough(List<X64Instruction> code, int register)
        {
            var held = RegisterTracker.Seed(new Dictionary<int, long> { [register] = 0 });
            var fields = new List<long>();
            foreach (var instruction in code)
            {
                if (held.Count == 0)
                {
                    break;
                }

                var reads = instruction.HasMemory && instruction.Kind is not (InstructionKind.Lea or InstructionKind.Call or InstructionKind.Jmp
                    or InstructionKind.Nop or InstructionKind.Push or InstructionKind.Pop);
                if (reads && held.TryGetValue(instruction.Base, out var at) && !fields.Contains(at.Offset + instruction.Displacement))
                {
                    fields.Add(at.Offset + instruction.Displacement);
                }

                RegisterTracker.Step(instruction, held);
            }

            return fields;
        }

        /// <summary>The functions the rule names (a label's dumps, or a GEAL evaluator) with the first vector loop after its site.</summary>
        private IEnumerable<(List<X64Instruction> Code, int Begin)> Loops(SemanticRule rule)
        {
            var starts = new List<(long Function, long Site)>();
            if (rule.Source == SemanticRule.FromLabel)
            {
                starts.AddRange(this.LabelSites(rule.Name, null).Select(site => (this.Index.FunctionStart(site), site))
                    .Where(start => start.Item1 is { }).Select(start => (start.Item1!.Value, start.site)));
            }
            else if (this.anchors.Evaluator(rule.Name) is { } evaluator)
            {
                starts.Add((evaluator, evaluator));
            }

            foreach (var (function, site) in starts)
            {
                var code = this.Index.AllOf(function);
                var at = code?.FindIndex(instruction => instruction.Address == site) ?? -1;
                if (code != null && at >= 0 && FindBegin(code, at) is { } begin)
                {
                    yield return (code, begin);
                }
            }
        }

        /// <summary>The first pointer load from a field that is compared with the next pointer soon after: a vector's begin and end.</summary>
        private static int? FindBegin(List<X64Instruction> code, int from)
        {
            for (var at = from; at < code.Count; at++)
            {
                var load = code[at];
                if (!RegisterTracker.IsPointerLoad(load) || !IsHeap(load.Base) || load.Index != X64.NoRegister)
                {
                    continue;
                }

                for (var next = at + 1; next < code.Count && next <= at + BeginToEnd && !code[next].WritesRegister(load.FirstRegister); next++)
                {
                    if (ComparesWith(code[next], load.FirstRegister, load.Displacement + VectorEnd))
                    {
                        return at;
                    }
                }
            }

            return null;
        }

        /// <summary>The immediate added to the iterator right before it is compared with the end again.</summary>
        private static long? LoopStride(List<X64Instruction> code, int begin)
        {
            var (iterator, end) = (code[begin].FirstRegister, code[begin].Displacement + VectorEnd);
            for (var at = begin + 1; at < code.Count; at++)
            {
                var add = code[at];
                if (add.Kind != InstructionKind.Add || add.HasMemory || add.SecondRegister >= 0 || add.FirstRegister != iterator || add.FirstSize != sizeof(long))
                {
                    continue;
                }

                for (var next = at + 1; next < code.Count && next <= at + AddToEnd; next++)
                {
                    if (ComparesWith(code[next], iterator, end))
                    {
                        return add.Immediate;
                    }
                }
            }

            return null;
        }

        /// <summary>The k-th distinct field the loop reads through the pointer loaded from each entry.</summary>
        private static long? EntryField(List<X64Instruction> code, int begin, int k)
        {
            var seeds = new Dictionary<int, long> { [code[begin].FirstRegister] = 0 };
            var fields = RegisterTracker.Track(code.Skip(begin + 1), seeds, EntryLevel)
                .Where(access => access.Level == EntryLevel && Load(access.Instruction) != null).Select(access => access.Field).Distinct().ToList();
            return fields.Count > k ? fields[k] : null;
        }

        /// <summary>A compare of the register with the memory at the displacement (either operand order).</summary>
        private static bool ComparesWith(X64Instruction instruction, int register, long displacement) =>
            instruction.Kind == InstructionKind.Cmp && instruction.HasMemory && instruction.Index == X64.NoRegister && instruction.Displacement == displacement
            && (instruction.FirstRegister == register || instruction.SecondRegister == register);
    }
}
