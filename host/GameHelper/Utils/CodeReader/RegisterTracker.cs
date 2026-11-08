// <copyright file="RegisterTracker.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils.CodeReader
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>One memory access through a tracked register: the register held the struct's address plus <paramref name="Offset"/>.</summary>
    /// <param name="Instruction">The access.</param>
    /// <param name="Register">The base register.</param>
    /// <param name="Offset">What the register held past the struct's start.</param>
    /// <param name="Level">How many pointers were followed from the seeded struct to reach this one (0: the struct itself).</param>
    public readonly record struct TrackedAccess(X64Instruction Instruction, int Register, long Offset, int Level = 0)
    {
        /// <summary>Gets the field's offset in the struct.</summary>
        public long Field => this.Offset + this.Instruction.Displacement;
    }

    /// <summary>What a register holds: a struct's address plus <paramref name="Offset"/>, the struct being <paramref name="Level"/> pointers from the seeded one.</summary>
    public readonly record struct Held(long Offset, int Level = 0);

    /// <summary>
    ///     Follows which registers hold a struct's address, straight-line from a point: 64-bit register copies and
    ///     <c>lea reg,[reg+d]</c> carry it, any other write drops it, and a call drops the volatile registers.
    /// </summary>
    public static class RegisterTracker
    {
        /// <summary>Where a straight-line walk outside any <c>.pdata</c> fragment stops.</summary>
        private const int LeafWindow = 0x400;

        /// <summary>The accesses through the seeded registers in at most <paramref name="limit"/> instructions; null when the code does not decode.</summary>
        public static List<TrackedAccess>? Track(CodeIndex index, long start, IReadOnlyDictionary<int, long> seeds, int limit)
        {
            var end = index.Fragment(start) is { } fragment ? fragment.End : start + LeafWindow;
            var code = index.Walk(start, limit, end - start, instruction => instruction.Kind is InstructionKind.Ret or InstructionKind.Jmp);
            return code is null ? null : Track(code, seeds);
        }

        /// <summary>
        ///     The accesses through the seeded registers over <paramref name="code"/>, taken in order whatever the jumps. With
        ///     <paramref name="follow"/>, a pointer loaded from a struct is tracked too, one level deeper, up to that many levels:
        ///     the first such load through a level names the field, and later loads of the same field (another branch) reach the same level.
        /// </summary>
        public static List<TrackedAccess> Track(IEnumerable<X64Instruction> code, IReadOnlyDictionary<int, long> seeds, int follow = 0)
        {
            var held = Seed(seeds);
            var followed = new Dictionary<int, long>();
            var accesses = new List<TrackedAccess>();
            foreach (var instruction in code)
            {
                if (held.Count == 0)
                {
                    break;
                }

                var deeper = (Held?)null;
                if (instruction.HasMemory && instruction.Index == X64.NoRegister && held.TryGetValue(instruction.Base, out var at))
                {
                    var access = new TrackedAccess(instruction, instruction.Base, at.Offset, at.Level);
                    accesses.Add(access);
                    if (at.Level < follow && IsPointerLoad(instruction))
                    {
                        if (!followed.TryGetValue(at.Level, out var field))
                        {
                            followed[at.Level] = field = access.Field;
                        }

                        deeper = field == access.Field ? new Held(0, at.Level + 1) : null;
                    }
                }

                Step(instruction, held);
                if (deeper is { } pointer)
                {
                    held[instruction.FirstRegister] = pointer;
                }
            }

            return accesses;
        }

        /// <summary>The seeded registers, each holding its struct at level 0.</summary>
        public static Dictionary<int, Held> Seed(IReadOnlyDictionary<int, long> seeds) =>
            seeds.ToDictionary(seed => seed.Key, seed => new Held(seed.Value));

        /// <summary>One instruction's effect on what the registers hold: a copy or <c>lea</c> carries, any other write drops, a call drops the volatiles.</summary>
        public static void Step(X64Instruction instruction, Dictionary<int, Held> held)
        {
            if (Carries(instruction, held))
            {
                return;
            }

            for (var register = 0; register < X64.Registers; register++)
            {
                if (instruction.WritesRegister(register))
                {
                    held.Remove(register);
                }
            }

            if (instruction.Kind == InstructionKind.Call)
            {
                for (var register = 0; register < X64.Registers; register++)
                {
                    if ((X64.Volatile & (1 << register)) != 0)
                    {
                        held.Remove(register);
                    }
                }
            }
        }

        /// <summary>A 64-bit <c>mov reg,[mem]</c>: what loads a pointer.</summary>
        public static bool IsPointerLoad(X64Instruction instruction) =>
            instruction.Kind == InstructionKind.Mov && instruction.HasMemory && instruction.First == OperandKind.Register
            && instruction.FirstSize == sizeof(long) && instruction.FirstRegister >= 0;

        /// <summary>A 64-bit copy or <c>lea</c> from a held register moves the address into its destination.</summary>
        private static bool Carries(X64Instruction instruction, Dictionary<int, Held> held)
        {
            if (instruction.FirstSize != sizeof(long) || instruction.FirstRegister < 0)
            {
                return false;
            }

            if (instruction.Kind == InstructionKind.Mov && !instruction.HasMemory && held.TryGetValue(instruction.SecondRegister, out var copied))
            {
                held[instruction.FirstRegister] = copied;
                return true;
            }

            if (instruction.Kind == InstructionKind.Lea && instruction.Index == X64.NoRegister && held.TryGetValue(instruction.Base, out var based))
            {
                held[instruction.FirstRegister] = based with { Offset = based.Offset + instruction.Displacement };
                return true;
            }

            return false;
        }
    }
}
