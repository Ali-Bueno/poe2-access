// <copyright file="SemanticRules.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils.CodeReader
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Runs the semantic rules on one exe: each yields one candidate per site it reads, none when anything it needs is not
    ///     there or does not decode. Registers are named only by the calling convention (this in rcx, the result in rax).
    /// </summary>
    public sealed partial class SemanticRules
    {
        /// <summary>The game's own error string inside <c>GEAL::GetGameWorld()</c>, the one function loading it.</summary>
        private const string GameWorldName = "GEAL::GetGameWorld()";

        private const int LabelInstructions = 60;
        private const int LabelBytes = 0x200;
        private const int ResultInstructions = 30;
        private const int ArgumentInstructions = 60;
        private const int CalleeInstructions = 12;
        private const byte FirstPrintable = 0x20;
        private const byte LastPrintable = 0x7E;

        private static readonly int[] Arguments = { X64.Rcx, X64.Rdx, X64.R8, X64.R9 };

        private readonly CodeAnchors anchors;
        private readonly ThisFields fields;
        private long? gameWorld;

        public SemanticRules(CodeAnchors anchors)
        {
            this.anchors = anchors;
            this.fields = new ThisFields(anchors);
        }

        private CodeIndex Index => this.anchors.Index;

        /// <summary>The value each site of the rule reads; empty when the rule finds nothing.</summary>
        public IReadOnlyList<long> Candidates(SemanticRule rule) => rule.Kind switch
        {
            SemanticKind.Label => this.Label(rule),
            SemanticKind.Geal => this.AfterCall(rule.Name, Only(this.anchors.Getter(rule.Component)), rule.K),
            SemanticKind.World => this.AfterCall(rule.Name, Only(this.GameWorld()), rule.K),
            SemanticKind.Callee => this.Callee(rule),
            SemanticKind.Called => this.Called(rule),
            SemanticKind.Field => this.LabelSites(rule.Name, rule.With).Select(site => this.fields.Field(site, rule.K, this.LabelWalk(site)))
                .OfType<long>().ToList(),
            SemanticKind.Getter => this.Getter(rule) is { } value ? new[] { value } : new long[0],
            SemanticKind.Object => this.Object(rule),
            SemanticKind.Deref => this.Deref(rule),
            SemanticKind.Slot => this.Slot(rule),
            SemanticKind.This => this.This(rule),
            _ => new long[0],
        };

        /// <summary>A memory read that is a field: not the stack, not an address, not indexed; its destination a register, or a compare.</summary>
        internal static long? Load(X64Instruction instruction)
        {
            if (!instruction.HasMemory || instruction.Kind is InstructionKind.Lea or InstructionKind.Call or InstructionKind.Jmp
                or InstructionKind.Nop or InstructionKind.Push or InstructionKind.Pop)
            {
                return null;
            }

            if (instruction.Base is X64.Rsp or X64.Rip or X64.NoRegister || instruction.Index != X64.NoRegister)
            {
                return null;
            }

            return instruction.First == OperandKind.Register || instruction.Kind is InstructionKind.Cmp or InstructionKind.Test
                ? instruction.Displacement : null;
        }

        /// <summary>A <c>lea</c> of a wide string starting with a printable letter: where the next label of a dump begins.</summary>
        internal static bool IsLabel(PeImage image, X64Instruction instruction)
        {
            var text = instruction.Kind == InstructionKind.Lea ? instruction.RipTarget : -1;
            return image.Constants.Holds(text) && image.Byte(text + 1) == 0 && image.Byte(text) is >= FirstPrintable and <= LastPrintable;
        }

        /// <summary>Every <c>lea</c> of the label; with <paramref name="with"/>, only in functions that also print that one.</summary>
        internal List<long> LabelSites(string label, string? with)
        {
            var sites = this.anchors.WideStrings(label).SelectMany(text => this.Index.LeaSites(text).ToArray()).Select(site => (long)site).ToList();
            if (with != null)
            {
                var functions = this.anchors.WideStrings(with).SelectMany(text => this.Index.LeaSites(text).ToArray())
                    .Select(site => this.Index.FunctionStart(site)).OfType<long>().ToHashSet();
                sites = sites.Where(site => this.Index.FunctionStart(site) is { } function && functions.Contains(function)).ToList();
            }

            return sites;
        }

        /// <summary>The straight line from a label's <c>lea</c>, to the first ret, int3 or jmp.</summary>
        internal List<X64Instruction>? LabelWalk(long site) => this.Index.Walk(
            site, LabelInstructions, LabelBytes, instruction => instruction.Kind is InstructionKind.Ret or InstructionKind.Int3 or InstructionKind.Jmp);

        private List<long> Label(SemanticRule rule)
        {
            var found = new List<long>();
            foreach (var site in this.LabelSites(rule.Name, rule.With))
            {
                if (this.LabelWalk(site) is not { } code)
                {
                    continue;
                }

                var (calls, loads) = (0, new List<long>());
                foreach (var instruction in code.Skip(1))
                {
                    if (IsLabel(this.anchors.Image, instruction) && loads.Count > rule.K && calls >= 2)
                    {
                        break;
                    }

                    if (instruction.Kind == InstructionKind.Call)
                    {
                        calls++;
                        continue;
                    }

                    var load = calls >= 1 ? Load(instruction) : null;
                    if (load == null && rule.Address && instruction.Kind == InstructionKind.Lea && instruction.Index == X64.NoRegister
                        && instruction.Base is not (X64.Rsp or X64.Rip or X64.NoRegister))
                    {
                        load = instruction.Displacement;
                    }

                    if (load is { } value)
                    {
                        loads.Add(value);
                    }
                }

                if (loads.Count > rule.K)
                {
                    found.Add(loads[rule.K]);
                }
            }

            return found;
        }

        /// <summary>The lowest <c>[rcx+D]</c> a function's first instructions load: the field a callee reads first from its argument.</summary>
        private static long? LowestRcxRead(List<X64Instruction>? body)
        {
            var reads = body?.Take(CalleeInstructions).Where(instruction => instruction.Kind == InstructionKind.Mov && instruction.HasMemory && instruction.Base == X64.Rcx)
                .Select(instruction => instruction.Displacement).ToList();
            return reads is { Count: > 0 } ? reads.Min() : null;
        }

        private static IReadOnlySet<long> Only(long? function) => function is { } start ? new HashSet<long> { start } : new HashSet<long>();

        /// <summary>In a GEAL function's evaluator, the k-th field read through what the first call to one of <paramref name="targets"/> returns.</summary>
        private List<long> AfterCall(string name, IReadOnlySet<long> targets, int k)
        {
            if (targets.Count == 0 || this.anchors.Evaluator(name) is not { } evaluator || this.Index.AllOf(evaluator) is not { } code)
            {
                return new List<long>();
            }

            foreach (var instruction in code)
            {
                if (instruction.Kind == InstructionKind.Call && targets.Contains(instruction.DirectTarget))
                {
                    var reads = RegisterTracker.Track(this.Index, instruction.End, new Dictionary<int, long> { [X64.Rax] = 0 }, ResultInstructions);
                    var through = reads?.Where(access => access.Offset == 0).Select(access => access.Field).ToList();
                    return through is { } values && values.Count > k ? new List<long> { values[k] } : new List<long>();
                }
            }

            return new List<long>();
        }

        private List<long> Callee(SemanticRule rule)
        {
            if (this.anchors.Getter(rule.Component) is not { } getter || this.anchors.Evaluator(rule.Name) is not { } evaluator
                || this.Index.AllOf(evaluator) is not { } code)
            {
                return new List<long>();
            }

            var after = code.SkipWhile(instruction => instruction.Kind != InstructionKind.Call || instruction.DirectTarget != getter).Skip(1);
            var callee = after.FirstOrDefault(instruction => instruction.Kind == InstructionKind.Call && instruction.DirectTarget > 0);
            return callee.Length > 0 && LowestRcxRead(this.Index.Body(callee.DirectTarget)) is { } lowest ? new List<long> { lowest } : new List<long>();
        }

        private List<long> Called(SemanticRule rule)
        {
            var found = new List<long>();
            foreach (var site in this.LabelSites(rule.Name, rule.With))
            {
                var calls = this.LabelWalk(site)?.Where(instruction => instruction.Kind == InstructionKind.Call).ToList();
                if (calls is not { } list || list.Count <= rule.Call || list[rule.Call].DirectTarget <= 0)
                {
                    continue;
                }

                var callee = list[rule.Call].DirectTarget;
                if (rule.Argument == 0)
                {
                    // Counted back from the ret, so the code up to it must all be there.
                    var body = this.Index.Body(callee);
                    var loads = body?.Exists(instruction => instruction.Kind == InstructionKind.Ret) == true
                        ? body.TakeWhile(instruction => instruction.Kind != InstructionKind.Ret).Select(Load).OfType<long>().ToList() : null;
                    if (loads is { } values && values.Count > rule.K)
                    {
                        found.Add(values[values.Count - 1 - rule.K]);
                    }
                }
                else
                {
                    var seeds = new Dictionary<int, long> { [Arguments[rule.Argument - 1]] = 0 };
                    var reads = RegisterTracker.Track(this.Index, callee, seeds, ArgumentInstructions)?.Where(access => access.Offset == 0).ToList();
                    if (reads is { } values && values.Count > rule.K)
                    {
                        found.Add(values[rule.K].Field);
                    }
                }
            }

            return found;
        }

        /// <summary>The getter reads <c>[this+details]</c>, then <c>[details+lookup]</c>, takes <c>lea [lookup+map]</c>, and after the find the components.</summary>
        private long? Getter(SemanticRule rule)
        {
            if (this.anchors.Getter(rule.Component) is not { } getter || this.Index.Body(getter) is not { } code)
            {
                return null;
            }

            var first = code.FindIndex(instruction => instruction.HasMemory && instruction.Base == X64.Rcx);
            if (first < 0)
            {
                return null;
            }

            var details = code[first].Displacement;
            var lookup = code.Skip(first + 1).FirstOrDefault(instruction => instruction.Kind == InstructionKind.Mov && instruction.HasMemory
                && IsHeap(instruction.Base) && instruction.Displacement != details);
            if (lookup.Length == 0 || lookup.First != OperandKind.Register || lookup.FirstRegister < 0)
            {
                return null;
            }

            var map = code.FirstOrDefault(instruction => instruction.Kind == InstructionKind.Lea && instruction.HasMemory && instruction.Base == lookup.FirstRegister);
            var find = map.Length == 0 ? -1 : code.FindIndex(instruction => instruction.Kind == InstructionKind.Call && instruction.Address > map.Address);
            if (find < 0)
            {
                return null;
            }

            var components = code.Skip(find).FirstOrDefault(instruction => instruction.Kind == InstructionKind.Mov && instruction.HasMemory
                && instruction.Index == X64.NoRegister && IsHeap(instruction.Base) && instruction.Base != X64.Rax);
            return rule.Name switch
            {
                SemanticRule.Details => details,
                SemanticRule.Lookup => lookup.Displacement,
                SemanticRule.Map => map.Displacement,
                _ when components.Length > 0 => components.Displacement,
                _ => null,
            };
        }

        private static bool IsHeap(int register) => register is not (X64.Rsp or X64.Rip or X64.NoRegister);

        private long? GameWorld()
        {
            if (this.gameWorld is null)
            {
                var functions = this.anchors.FunctionsMentioning(GameWorldName);
                this.gameWorld = functions.Count == 1 ? functions.First() : -1;
            }

            return this.gameWorld >= 0 ? this.gameWorld : null;
        }
    }
}
