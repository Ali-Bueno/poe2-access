// <copyright file="SemanticRules.World.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils.CodeReader
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The rules that start from the world a GEAL function reaches, from the game object its argument names, or from a
    ///     function named by the strings it loads (docs/research/exe-layouts.md, "The world fields").
    /// </summary>
    public sealed partial class SemanticRules
    {
        /// <summary>The error of the GEAL helper that turns an argument into its game object: the functions loading it return that object.</summary>
        private const string GameObjectName = "Requires a Game Object (or an Animated Object attached to one)";

        /// <summary>A sweep over a whole function stops here: what these rules look for comes early in it.</summary>
        private const int SweepInstructions = 400;

        /// <summary>The k-th field read through the game object the argument helper returns.</summary>
        private List<long> Object(SemanticRule rule) => this.AfterCall(rule.Name, this.anchors.FunctionsMentioning(GameObjectName), rule.K);

        /// <summary>From the evaluator's argument, follow the first pointer loaded at each level k times; the lowest field read through the last one.</summary>
        private List<long> Deref(SemanticRule rule)
        {
            if (this.anchors.Evaluator(rule.Name) is not { } evaluator || this.Index.AllOf(evaluator) is not { } code)
            {
                return new List<long>();
            }

            var seeds = new Dictionary<int, long> { [Arguments[rule.Argument - 1]] = 0 };
            var fields = RegisterTracker.Track(code.Take(SweepInstructions), seeds, rule.K)
                .Where(access => access.Level == rule.K && Load(access.Instruction) != null).Select(access => access.Field).ToList();
            return fields.Count > 0 ? new List<long> { fields.Min() } : new List<long>();
        }

        /// <summary>
        ///     After <c>GEAL::GetGameWorld()</c>, the first pointer loaded from the world followed k levels down; the lowest field
        ///     read through the last one (<c>GetAreaId</c>: <c>mov rdx,[rax+0xA0]; mov rdx,[rdx]</c>, the area's WorldAreas row and its Id).
        /// </summary>
        private List<long> ThroughWorld(SemanticRule rule)
        {
            if (this.GameWorld() is not { } world || this.anchors.Evaluator(rule.Name) is not { } evaluator || this.Index.AllOf(evaluator) is not { } code)
            {
                return new List<long>();
            }

            var call = code.FindIndex(instruction => instruction.Kind == InstructionKind.Call && instruction.DirectTarget == world);
            if (call < 0)
            {
                return new List<long>();
            }

            var fields = RegisterTracker.Track(this.Index, code[call].End, new Dictionary<int, long> { [X64.Rax] = 0 }, ResultInstructions, rule.Depth)?
                .Where(access => access.Level == rule.Depth && Load(access.Instruction) != null).Select(access => access.Field).ToList();
            return fields is { Count: > 0 } ? new List<long> { fields.Min() } : new List<long>();
        }

        /// <summary>The world's virtual slot the GEAL function calls, read in the vtables of the class printing the label; with then or inline, one step on.</summary>
        private List<long> Slot(SemanticRule rule)
        {
            if (this.GameWorld() is not { } world || this.anchors.Evaluator(rule.Name) is not { } evaluator || this.Index.AllOf(evaluator) is not { } code)
            {
                return new List<long>();
            }

            var from = code.FindIndex(instruction => instruction.Kind == InstructionKind.Call && instruction.DirectTarget == world);
            if (from < 0 || this.WorldCall(code, from + 1, X64.Rax, descend: true) is not var (slot, next))
            {
                return new List<long>();
            }

            var vtables = this.LabelSites(rule.With!, null).Select(site => this.Index.FunctionStart(site)).OfType<long>().Distinct()
                .SelectMany(function => this.fields.VtablesHolding(function)).Distinct().ToList();
            var field = SemanticFieldReader.Agreed(vtables.Select(table => this.SlotField(table, slot)).OfType<long>().ToList());
            if (field is { } value && rule.Then)
            {
                field = CalleeTaking(code, next) is { } callee && LowestRcxRead(this.Index.Body(callee)) is { } lowest ? value + lowest : null;
            }
            else if (field is { } head && rule.Inline)
            {
                field = this.InlineStorage(vtables, head);
            }

            return field is { } found ? new List<long> { found } : new List<long>();
        }

        /// <summary>In the one function loading the string (and the second), the k-th distinct field read through this.</summary>
        private List<long> This(SemanticRule rule)
        {
            var functions = this.anchors.FunctionsLoading(rule.Name);
            if (rule.With is { } other)
            {
                functions = functions.Intersect(this.anchors.FunctionsLoading(other)).ToHashSet();
            }

            if (functions.Count != 1 || this.Index.AllOf(functions.First()) is not { } code)
            {
                return new List<long>();
            }

            var fields = RegisterTracker.Track(code.Take(SweepInstructions), new Dictionary<int, long> { [X64.Rcx] = 0 })
                .Where(access => Load(access.Instruction) != null).Select(access => access.Field).Distinct().ToList();
            return fields.Count > rule.K ? new List<long> { fields[rule.K] } : new List<long>();
        }

        /// <summary>
        ///     The first direct call made with rcx holding what rax held at <paramref name="from"/>: the callee that takes the
        ///     result. The walk stops at a jump unless <paramref name="pastJumps"/>, when it sweeps on in order to the ret.
        /// </summary>
        private static long? CalleeTaking(List<X64Instruction> code, int from, bool pastJumps = false)
        {
            var held = RegisterTracker.Seed(new Dictionary<int, long> { [X64.Rax] = 0 });
            for (var at = from; at < code.Count && held.Count > 0; at++)
            {
                var instruction = code[at];
                if (instruction.Kind == InstructionKind.Call && instruction.DirectTarget > 0 && held.TryGetValue(X64.Rcx, out var argument) && argument.Offset == 0)
                {
                    return instruction.DirectTarget;
                }

                RegisterTracker.Step(instruction, held);
                if (instruction.Kind == InstructionKind.Ret || (instruction.Kind == InstructionKind.Jmp && !pastJumps))
                {
                    break;
                }
            }

            return null;
        }

        /// <summary>
        ///     The first virtual call through the object <paramref name="register"/> holds at <paramref name="from"/>: its vtable
        ///     slot, and the index after the call in <paramref name="code"/>. With <paramref name="descend"/>, looked for in the
        ///     first callee the object is passed to when the code itself makes none.
        /// </summary>
        private (long Slot, int Next)? WorldCall(List<X64Instruction> code, int from, int register, bool descend)
        {
            var held = RegisterTracker.Seed(new Dictionary<int, long> { [register] = 0 });
            var vtables = new HashSet<int>();
            for (var at = from; at < code.Count && (held.Count > 0 || vtables.Count > 0); at++)
            {
                var instruction = code[at];
                if (instruction.Kind == InstructionKind.Call)
                {
                    if (instruction.HasMemory && instruction.Index == X64.NoRegister && vtables.Contains(instruction.Base))
                    {
                        return (instruction.Displacement, at + 1);
                    }

                    if (descend && instruction.DirectTarget > 0 && held.TryGetValue(X64.Rcx, out var argument) && argument.Offset == 0
                        && this.Index.Body(instruction.DirectTarget) is { } body && this.WorldCall(body, 0, X64.Rcx, descend: false) is { } inner)
                    {
                        return (inner.Slot, at + 1);
                    }
                }

                var loadsVtable = RegisterTracker.IsPointerLoad(instruction) && instruction.Index == X64.NoRegister && instruction.Displacement == 0
                    && held.TryGetValue(instruction.Base, out var start) && start.Offset == 0;
                RegisterTracker.Step(instruction, held);
                vtables.RemoveWhere(vtable => instruction.WritesRegister(vtable) || (instruction.Kind == InstructionKind.Call && (X64.Volatile & (1 << vtable)) != 0));
                if (loadsVtable)
                {
                    vtables.Add(instruction.FirstRegister);
                }

                if (instruction.Kind is InstructionKind.Ret or InstructionKind.Jmp)
                {
                    break;
                }
            }

            return null;
        }

        /// <summary>What a vtable's slot reads from this: X for <c>lea rax,[rcx+X]; ret</c>, else the lowest field its first instructions load; null when none.</summary>
        private long? SlotField(long table, long slot)
        {
            var body = this.Index.Body(this.anchors.Image.Pointer(table + slot));
            if (ThisFields.LeaGetter(body) is { } getter)
            {
                return getter;
            }

            var reads = body is null ? null : RegisterTracker.Track(body.Take(CalleeInstructions), new Dictionary<int, long> { [X64.Rcx] = 0 })
                .Where(access => Load(access.Instruction) != null).Select(access => access.Field).ToList();
            return reads is { Count: > 0 } ? reads.Min() : null;
        }

        /// <summary>The this-relative address the class's constructors store into <c>[this+field]</c>: a small vector's inline storage.</summary>
        private long? InlineStorage(IEnumerable<long> vtables, long field)
        {
            var stored = new List<long>();
            var constructors = vtables.SelectMany(table => this.Index.LeaSites(table).ToArray()).Select(site => this.Index.FunctionStart(site)).OfType<long>().Distinct();
            foreach (var function in constructors)
            {
                if (this.Index.AllOf(function) is not { } code)
                {
                    continue;
                }

                var held = RegisterTracker.Seed(new Dictionary<int, long> { [X64.Rcx] = 0 });
                foreach (var instruction in code.Take(SweepInstructions))
                {
                    if (held.Count == 0)
                    {
                        break;
                    }

                    if (instruction.Kind == InstructionKind.Mov && instruction.First == OperandKind.Memory && instruction.Index == X64.NoRegister
                        && held.TryGetValue(instruction.Base, out var at) && at.Offset + instruction.Displacement == field
                        && held.TryGetValue(instruction.SecondRegister, out var address))
                    {
                        stored.Add(address.Offset);
                    }

                    RegisterTracker.Step(instruction, held);
                }
            }

            return SemanticFieldReader.Agreed(stored);
        }
    }
}
