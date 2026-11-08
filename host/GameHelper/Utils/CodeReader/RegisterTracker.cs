// <copyright file="RegisterTracker.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils.CodeReader
{
    using System.Collections.Generic;

    /// <summary>One memory access through a tracked register: the register held the struct's address plus <paramref name="Offset"/>.</summary>
    /// <param name="Instruction">The access.</param>
    /// <param name="Register">The base register.</param>
    /// <param name="Offset">What the register held past the struct's start.</param>
    public readonly record struct TrackedAccess(X64Instruction Instruction, int Register, long Offset)
    {
        /// <summary>Gets the field's offset in the struct.</summary>
        public long Field => this.Offset + this.Instruction.Displacement;
    }

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
            if (index.Walk(start, limit, end - start, instruction => instruction.Kind is InstructionKind.Ret or InstructionKind.Jmp) is not { } code)
            {
                return null;
            }

            var held = new Dictionary<int, long>(seeds);
            var accesses = new List<TrackedAccess>();
            foreach (var instruction in code)
            {
                if (held.Count == 0)
                {
                    break;
                }

                if (instruction.HasMemory && instruction.Index == X64.NoRegister && held.TryGetValue(instruction.Base, out var offset))
                {
                    accesses.Add(new TrackedAccess(instruction, instruction.Base, offset));
                }

                if (Carries(instruction, held))
                {
                    continue;
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

            return accesses;
        }

        /// <summary>A 64-bit copy or <c>lea</c> from a held register moves the address into its destination.</summary>
        private static bool Carries(X64Instruction instruction, Dictionary<int, long> held)
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
                held[instruction.FirstRegister] = based + instruction.Displacement;
                return true;
            }

            return false;
        }
    }
}
