// <copyright file="ThisFields.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils.CodeReader
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The field rule: which field of a dump's <c>this</c> a label prints, when it prints through a sub-object. A linear
    ///     sweep of the dump from its entry (rcx = this) follows copies, <c>lea</c> and <c>add</c> by a constant, and a virtual
    ///     call through this's own vtable returns this+X when that slot is <c>lea rax,[rcx+X]; ret</c> in every vtable holding the dump.
    /// </summary>
    internal sealed class ThisFields
    {
        private const int Instructions = 100;
        private const int SubObjectGetter = 2;

        private readonly CodeAnchors anchors;
        private readonly Dictionary<long, List<long>> vtables = new();

        public ThisFields(CodeAnchors anchors) => this.anchors = anchors;

        private CodeIndex Index => this.anchors.Index;

        private PeImage Image => this.anchors.Image;

        /// <summary>The k-th distinct this-field the label at <paramref name="site"/> prints, up to the next label; null when there is none.</summary>
        public long? Field(long site, int k, List<X64Instruction>? walk)
        {
            // The sweep must reach the label through decoded code: a gap before it would leave a register's state stale.
            if (walk is not { Count: > 0 } || this.Index.FunctionStart(site) is not { } function || this.Index.AllOf(function) is not { } code
                || !code.Exists(instruction => instruction.Address == site))
            {
                return null;
            }

            var state = new State();
            state.Held[X64.Rcx] = 0;
            foreach (var instruction in code.Where(instruction => instruction.Address < site).OrderBy(instruction => instruction.Address))
            {
                this.Step(state, instruction, function);
            }

            this.Step(state, walk[0], function);
            var (calls, fields) = (0, new List<long>());
            foreach (var instruction in walk.Skip(1))
            {
                if (SemanticRules.IsLabel(this.Image, instruction) && calls >= 2 && fields.Count > k)
                {
                    break;
                }

                if (calls >= 1 && SemanticRules.Load(instruction) is { } displacement && state.Held.TryGetValue(instruction.Base, out var offset)
                    && !fields.Contains(offset + displacement))
                {
                    fields.Add(offset + displacement);
                }

                calls += instruction.Kind == InstructionKind.Call ? 1 : 0;
                this.Step(state, instruction, function);
            }

            return fields.Count > k ? fields[k] : null;
        }

        /// <summary>One instruction's effect on which registers hold this+X and which hold this's vtable.</summary>
        private void Step(State state, X64Instruction instruction, long function)
        {
            var (held, vtable) = (state.Held, state.Vtable);
            var destination = instruction.First == OperandKind.Register ? instruction.FirstRegister : X64.NoRegister;
            if (instruction.Kind == InstructionKind.Call)
            {
                var slot = instruction.HasMemory && instruction.Index == X64.NoRegister && vtable.Contains(instruction.Base) ? instruction.Displacement : (long?)null;
                for (var register = 0; register < X64.Registers; register++)
                {
                    if ((X64.Volatile & (1 << register)) != 0)
                    {
                        held.Remove(register);
                        vtable.Remove(register);
                    }
                }

                if (slot is { } index && this.SubObject(function, index) is { } sub)
                {
                    held[X64.Rax] = sub;
                }

                return;
            }

            var wide = destination >= 0 && instruction.FirstSize == sizeof(long);
            if (wide && instruction.Kind == InstructionKind.Mov && !instruction.HasMemory && instruction.SecondRegister >= 0)
            {
                var source = instruction.SecondRegister;
                var (copied, isVtable) = (held.TryGetValue(source, out var x) ? x : (long?)null, vtable.Contains(source));
                held.Remove(destination);
                vtable.Remove(destination);
                if (copied is { } value)
                {
                    held[destination] = value;
                }

                if (isVtable)
                {
                    vtable.Add(destination);
                }

                return;
            }

            if (wide && instruction.Kind == InstructionKind.Mov && instruction.HasMemory && instruction.Index == X64.NoRegister && instruction.Displacement == 0
                && held.TryGetValue(instruction.Base, out var start) && start == 0)
            {
                held.Remove(destination);
                vtable.Add(destination);
                return;
            }

            if (wide && instruction.Kind == InstructionKind.Lea && instruction.Index == X64.NoRegister && held.TryGetValue(instruction.Base, out var based))
            {
                held[destination] = based + instruction.Displacement;
                vtable.Remove(destination);
                return;
            }

            // add r64, imm: an immediate form names no second register and reads no memory.
            if (wide && instruction.Kind == InstructionKind.Add && !instruction.HasMemory && instruction.SecondRegister < 0 && held.ContainsKey(destination))
            {
                held[destination] += instruction.Immediate;
                return;
            }

            for (var register = 0; register < X64.Registers; register++)
            {
                if (instruction.WritesRegister(register))
                {
                    held.Remove(register);
                    vtable.Remove(register);
                }
            }
        }

        /// <summary>X when the slot is <c>lea rax,[rcx+X]; ret</c> in every vtable holding the function that has a getter there.</summary>
        private long? SubObject(long function, long slot)
        {
            var offsets = new HashSet<long>();
            foreach (var table in this.VtablesHolding(function))
            {
                if (LeaGetter(this.Index.Body(this.Image.Pointer(table + slot))) is { } offset)
                {
                    offsets.Add(offset);
                }
            }

            return offsets.Count == 1 ? offsets.First() : null;
        }

        /// <summary>X when the function is <c>lea rax,[rcx+X]; ret</c>, a sub-object getter; null for anything else.</summary>
        internal static long? LeaGetter(List<X64Instruction>? code) =>
            code is { Count: >= SubObjectGetter } && code[0].Kind == InstructionKind.Lea && code[0].FirstRegister == X64.Rax && code[0].Base == X64.Rcx
            && code[0].Index == X64.NoRegister && code[1].Kind == InstructionKind.Ret ? code[0].Displacement : null;

        /// <summary>The vtables with the function as a slot: each <c>.rdata</c> pointer to it, walked back over code pointers to an address some <c>lea</c> loads.</summary>
        internal List<long> VtablesHolding(long function)
        {
            if (this.vtables.TryGetValue(function, out var known))
            {
                return known;
            }

            var found = new List<long>();
            var constants = this.Image.Bytes(this.Image.Constants.Start, this.Image.Constants.Size);
            var pointer = BitConverter.GetBytes(function + this.Image.ImageBase);
            for (var at = constants.IndexOf(pointer); at >= 0;)
            {
                if (at % sizeof(long) == 0)
                {
                    var table = this.Image.Constants.Start + at;
                    while (!this.IsLoaded(table) && this.Image.IsCode(this.Image.Pointer(table - sizeof(long))))
                    {
                        table -= sizeof(long);
                    }

                    if (this.IsLoaded(table))
                    {
                        found.Add(table);
                    }
                }

                var next = constants[(at + 1)..].IndexOf(pointer);
                at = next < 0 ? -1 : at + 1 + next;
            }

            this.vtables[function] = found;
            return found;
        }

        private bool IsLoaded(long address) => this.Index.LeaTargets.BinarySearch(address) >= 0;

        private sealed class State
        {
            public Dictionary<int, long> Held { get; } = new();

            public HashSet<int> Vtable { get; } = new();
        }
    }
}
