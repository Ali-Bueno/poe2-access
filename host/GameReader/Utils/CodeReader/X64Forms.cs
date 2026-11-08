// <copyright file="X64Forms.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils.CodeReader
{
    /// <summary>Where an instruction's first operand comes from.</summary>
    internal enum Slot : byte
    {
        None,
        Reg,
        Rm,
        OpcodeReg,
        Accumulator,
        Immediate,
        Vvvv,
        StringMemory,
    }

    /// <summary>The immediate an instruction carries.</summary>
    internal enum Immediate : byte
    {
        None,
        Byte,
        Word,

        /// <summary>A word with the operand-size prefix, a dword otherwise.</summary>
        Z,

        /// <summary>Also a qword with REX.W (<c>mov r64, imm64</c>).</summary>
        V,

        /// <summary><c>enter</c>'s word and byte.</summary>
        WordByte,
    }

    /// <summary>What the decoder needs to know of one opcode: operands, immediate, and what it writes.</summary>
    internal readonly record struct Form
    {
        public InstructionKind Kind { get; init; }

        public Slot First { get; init; }

        public bool ModRm { get; init; }

        public Immediate Imm { get; init; }

        /// <summary>A relative branch's offset bytes: 1, 4 or none.</summary>
        public int Rel { get; init; }

        /// <summary>The ModRM reg field names a general register.</summary>
        public bool RegGpr { get; init; }

        /// <summary>The ModRM r/m field names a general register when it is a register.</summary>
        public bool RmGpr { get; init; }

        public bool WriteFirst { get; init; }

        /// <summary>The other ModRM operand is written too (<c>xchg</c>, <c>xadd</c>).</summary>
        public bool WriteBoth { get; init; }

        /// <summary>The VEX.vvvv register is written too (<c>mulx</c>).</summary>
        public bool WriteVvvv { get; init; }

        public ushort Implicit { get; init; }

        /// <summary>Every operand is a byte (ah, ch, dh, bh without REX).</summary>
        public bool Byte { get; init; }

        /// <summary>Only the r/m operand is a byte (<c>movzx</c>, <c>setcc</c>).</summary>
        public bool ByteRm { get; init; }

        /// <summary>The operand size is 64 bits without REX.W (push, pop, near branches).</summary>
        public bool Default64 { get; init; }

        public bool MemoryOnly { get; init; }

        public bool RegisterOnly { get; init; }

        /// <summary>A string instruction's implicit memory base; with rep it also writes rcx.</summary>
        public int StringBase { get; init; }

        /// <summary>The operand sizes come from VEX.W, as in BMI's general-register forms.</summary>
        public bool VexGpr { get; init; }
    }

    /// <summary>The opcode maps the decoder knows; anything not here is unknown and stops the caller.</summary>
    internal static class X64Forms
    {
        public const int OneByte = 0;
        public const int Map0F = 1;
        public const int Map0F38 = 2;
        public const int Map0F3A = 3;

        private const ushort Rax = 1 << X64.Rax;
        private const ushort Rcx = 1 << X64.Rcx;
        private const ushort Rdx = 1 << X64.Rdx;
        private const ushort Rbx = 1 << X64.Rbx;
        private const ushort Rsp = 1 << X64.Rsp;
        private const ushort Rbp = 1 << X64.Rbp;

        /// <summary>Whether the opcode is followed by a ModRM byte (before the reg field picks a group member).</summary>
        public static bool HasModRm(int map, int op) => map switch
        {
            OneByte => (op < 0x40 && (op & 7) < 4) || op is 0x63 or 0x69 or 0x6B or (>= 0x80 and <= 0x8F) or 0xC0 or 0xC1
                or 0xC6 or 0xC7 or (>= 0xD0 and <= 0xD3) or 0xF6 or 0xF7 or 0xFE or 0xFF,
            Map0F => op is not (0x05 or 0x0B or 0x31 or 0x77 or 0xA2 or (>= 0x80 and <= 0x8F) or (>= 0xC8 and <= 0xCF)),
            _ => true,
        };

        /// <summary>The form of a legacy or VEX-encoded opcode, or null when the decoder does not know it.</summary>
        /// <param name="map">The opcode map.</param>
        /// <param name="op">The opcode.</param>
        /// <param name="modRm">The ModRM byte, or -1 when there is none.</param>
        /// <param name="simd">The mandatory prefix in force: 0, 0x66, 0xF3 or 0xF2.</param>
        /// <param name="rexB">Whether REX.B is set (<c>41 90</c> is <c>xchg r8, rax</c>, not a nop).</param>
        /// <param name="vex">Whether the instruction is VEX-encoded.</param>
        public static Form? Describe(int map, int op, int modRm, int simd, bool rexB, bool vex) => map switch
        {
            OneByte => vex ? null : OneByteForm(op, modRm, simd, rexB),
            Map0F => TwoByteForm(op, modRm, simd, vex),
            Map0F38 => vex ? Vex0F38Form(op, modRm, simd) : Legacy0F38Form(op, simd),
            Map0F3A => ThreeAForm(op, vex),
            _ => null,
        };

        private static int RegOf(int modRm) => (modRm >> 3) & 7;

        /// <summary>Group 1 (0x80 to 0x83): /0 is add, /7 cmp.</summary>
        private static InstructionKind Arithmetic(int reg) => reg switch { 0 => InstructionKind.Add, 7 => InstructionKind.Cmp, _ => InstructionKind.Other };

        private static bool IsRegister(int modRm) => modRm >> 6 == 3;

        private static Form Gpr(InstructionKind kind, Slot first, bool write) => new()
        {
            Kind = kind, First = first, ModRm = true, RegGpr = true, RmGpr = true, WriteFirst = write,
        };

        /// <summary>A group opcode: the reg field picks the operation, so it names no register.</summary>
        private static Form Group(InstructionKind kind, bool write) => Gpr(kind, Slot.Rm, write) with { RegGpr = false };

        private static Form Sse(Slot first) => new() { First = first, ModRm = true };

        private static Form? OneByteForm(int op, int modRm, int simd, bool rexB)
        {
            if (op < 0x40 && (op & 7) < 6)
            {
                var compare = op >> 3 == 7;
                var kind = compare ? InstructionKind.Cmp : op >> 3 == 0 ? InstructionKind.Add : InstructionKind.Other;
                return (op & 7) switch
                {
                    0 => Gpr(kind, Slot.Rm, !compare) with { Byte = true },
                    1 => Gpr(kind, Slot.Rm, !compare),
                    2 => Gpr(kind, Slot.Reg, !compare) with { Byte = true },
                    3 => Gpr(kind, Slot.Reg, !compare),
                    4 => new Form { Kind = kind, First = Slot.Accumulator, Imm = Immediate.Byte, Byte = true, WriteFirst = !compare },
                    _ => new Form { Kind = kind, First = Slot.Accumulator, Imm = Immediate.Z, WriteFirst = !compare },
                };
            }

            var reg = RegOf(modRm);
            return op switch
            {
                >= 0x50 and <= 0x57 => new Form { Kind = InstructionKind.Push, First = Slot.OpcodeReg, Default64 = true, Implicit = Rsp },
                >= 0x58 and <= 0x5F => new Form { Kind = InstructionKind.Pop, First = Slot.OpcodeReg, Default64 = true, WriteFirst = true, Implicit = Rsp },
                0x63 => Gpr(InstructionKind.Other, Slot.Reg, true),
                0x68 => new Form { Kind = InstructionKind.Push, First = Slot.Immediate, Imm = Immediate.Z, Implicit = Rsp },
                0x69 => Gpr(InstructionKind.Other, Slot.Reg, true) with { Imm = Immediate.Z },
                0x6A => new Form { Kind = InstructionKind.Push, First = Slot.Immediate, Imm = Immediate.Byte, Implicit = Rsp },
                0x6B => Gpr(InstructionKind.Other, Slot.Reg, true) with { Imm = Immediate.Byte },
                >= 0x70 and <= 0x7F => new Form { Kind = InstructionKind.Jcc, First = Slot.Immediate, Rel = 1 },
                0x80 => Group(Arithmetic(reg), reg != 7) with { Imm = Immediate.Byte, Byte = true },
                0x81 => Group(Arithmetic(reg), reg != 7) with { Imm = Immediate.Z },
                0x83 => Group(Arithmetic(reg), reg != 7) with { Imm = Immediate.Byte },
                0x84 => Gpr(InstructionKind.Test, Slot.Rm, false) with { Byte = true },
                0x85 => Gpr(InstructionKind.Test, Slot.Rm, false),
                0x86 => Gpr(InstructionKind.Other, Slot.Rm, true) with { Byte = true, WriteBoth = true },
                0x87 => Gpr(InstructionKind.Other, Slot.Rm, true) with { WriteBoth = true },
                0x88 => Gpr(InstructionKind.Mov, Slot.Rm, true) with { Byte = true },
                0x89 => Gpr(InstructionKind.Mov, Slot.Rm, true),
                0x8A => Gpr(InstructionKind.Mov, Slot.Reg, true) with { Byte = true },
                0x8B => Gpr(InstructionKind.Mov, Slot.Reg, true),
                0x8C => Gpr(InstructionKind.Mov, Slot.Rm, true) with { RegGpr = false },
                0x8D => Gpr(InstructionKind.Lea, Slot.Reg, true) with { MemoryOnly = true },
                0x8E => Gpr(InstructionKind.Mov, Slot.Reg, false) with { RegGpr = false },
                0x8F when reg == 0 => Group(InstructionKind.Pop, true) with { Default64 = true, Implicit = Rsp },
                0x90 when rexB => new Form { First = Slot.OpcodeReg, WriteFirst = true, Implicit = Rax },
                0x90 when simd == 0xF3 => new Form(),
                0x90 => new Form { Kind = InstructionKind.Nop },
                >= 0x91 and <= 0x97 => new Form { First = Slot.OpcodeReg, WriteFirst = true, Implicit = Rax },
                0x98 => new Form { Implicit = Rax },
                0x99 => new Form { Implicit = Rax | Rdx },
                0x9C or 0x9D => new Form { Implicit = Rsp },
                0x9E => new Form(),
                0x9F => new Form { Implicit = Rax },
                0xA4 or 0xA5 => new Form { First = Slot.StringMemory, StringBase = X64.Rdi, Implicit = (1 << X64.Rdi) | (1 << X64.Rsi), Byte = op == 0xA4 },
                0xA6 or 0xA7 => new Form { First = Slot.StringMemory, StringBase = X64.Rsi, Implicit = (1 << X64.Rdi) | (1 << X64.Rsi), Byte = op == 0xA6 },
                0xA8 => new Form { Kind = InstructionKind.Test, First = Slot.Accumulator, Imm = Immediate.Byte, Byte = true },
                // Capstone counts these written; the safe side, as a register wrongly kept is a wrong offset.
                0xA9 => new Form { Kind = InstructionKind.Test, First = Slot.Accumulator, Imm = Immediate.Z, Implicit = Rax },
                0xAA or 0xAB => new Form { First = Slot.StringMemory, StringBase = X64.Rdi, Implicit = 1 << X64.Rdi, Byte = op == 0xAA },
                0xAC or 0xAD => new Form { First = Slot.Accumulator, StringBase = X64.Rsi, WriteFirst = true, Implicit = 1 << X64.Rsi, Byte = op == 0xAC },
                0xAE or 0xAF => new Form { First = Slot.Accumulator, StringBase = X64.Rdi, Implicit = 1 << X64.Rdi, Byte = op == 0xAE },
                >= 0xB0 and <= 0xB7 => new Form { Kind = InstructionKind.Mov, First = Slot.OpcodeReg, WriteFirst = true, Imm = Immediate.Byte, Byte = true },
                >= 0xB8 and <= 0xBF => new Form { Kind = InstructionKind.Mov, First = Slot.OpcodeReg, WriteFirst = true, Imm = Immediate.V },
                0xC0 or 0xD0 or 0xD2 => reg == 6 ? null : Group(InstructionKind.Other, true) with { Byte = true, Imm = op == 0xC0 ? Immediate.Byte : Immediate.None },
                0xC1 or 0xD1 or 0xD3 => reg == 6 ? null : Group(InstructionKind.Other, true) with { Imm = op == 0xC1 ? Immediate.Byte : Immediate.None },
                0xC2 => new Form { Kind = InstructionKind.Ret, First = Slot.Immediate, Imm = Immediate.Word, Implicit = Rsp },
                0xC3 => new Form { Kind = InstructionKind.Ret, Implicit = Rsp },
                0xC6 when reg == 0 => Group(InstructionKind.Mov, true) with { Imm = Immediate.Byte, Byte = true },
                0xC7 when reg == 0 => Group(InstructionKind.Mov, true) with { Imm = Immediate.Z },
                0xC8 => new Form { First = Slot.Immediate, Imm = Immediate.WordByte, Implicit = Rsp | Rbp },
                0xC9 => new Form { Implicit = Rsp | Rbp },
                0xCC => new Form { Kind = InstructionKind.Int3 },
                0xCD => new Form { First = Slot.Immediate, Imm = Immediate.Byte },
                >= 0xE0 and <= 0xE2 => new Form { Kind = InstructionKind.Loop, First = Slot.Immediate, Rel = 1, Implicit = Rcx },
                0xE3 => new Form { Kind = InstructionKind.Jcc, First = Slot.Immediate, Rel = 1 },
                0xE8 => new Form { Kind = InstructionKind.Call, First = Slot.Immediate, Rel = 4, Implicit = Rsp },
                0xE9 => new Form { Kind = InstructionKind.Jmp, First = Slot.Immediate, Rel = 4 },
                0xEB => new Form { Kind = InstructionKind.Jmp, First = Slot.Immediate, Rel = 1 },
                0xF5 or 0xF8 or 0xF9 or 0xFC or 0xFD => new Form(),
                0xF6 => reg switch
                {
                    0 or 1 => Group(InstructionKind.Test, false) with { Imm = Immediate.Byte, Byte = true },
                    2 or 3 => Group(InstructionKind.Other, true) with { Byte = true },
                    _ => Group(InstructionKind.Other, false) with { Byte = true, Implicit = Rax },
                },
                0xF7 => reg switch
                {
                    0 or 1 => Group(InstructionKind.Test, false) with { Imm = Immediate.Z },
                    2 or 3 => Group(InstructionKind.Other, true),
                    _ => Group(InstructionKind.Other, false) with { Implicit = Rax | Rdx },
                },
                0xFE when reg <= 1 => Group(InstructionKind.Other, true) with { Byte = true },
                0xFF => reg switch
                {
                    0 or 1 => Group(InstructionKind.Other, true),
                    2 => Group(InstructionKind.Call, false) with { Default64 = true, Implicit = Rsp },
                    4 => Group(InstructionKind.Jmp, false) with { Default64 = true },
                    6 => Group(InstructionKind.Push, false) with { Default64 = true, Implicit = Rsp },
                    _ => null,
                },
                _ => null,
            };
        }

        private static Form? TwoByteForm(int op, int modRm, int simd, bool vex)
        {
            var reg = RegOf(modRm);
            var register = IsRegister(modRm);
            if (vex && op is (>= 0x00 and < 0x10) or (>= 0x18 and < 0x28) or (>= 0x30 and < 0x50) or (>= 0x80 and < 0xC2) or (>= 0xC7 and < 0xD0))
            {
                return null;
            }

            return op switch
            {
                0x0B => new Form(),
                0x0D => Sse(Slot.Rm) with { MemoryOnly = true },
                0x18 => reg < 4 ? Sse(Slot.Rm) with { MemoryOnly = true } : Sse(Slot.Rm) with { Kind = InstructionKind.Nop },
                >= 0x19 and <= 0x1F => Sse(Slot.Rm) with { Kind = InstructionKind.Nop },
                0x10 or 0x12 or 0x14 or 0x15 or 0x16 or 0x28 or 0x2E or 0x2F => Sse(Slot.Reg),
                0x11 or 0x29 => Sse(Slot.Rm),
                0x13 or 0x17 or 0x2B => Sse(Slot.Rm) with { MemoryOnly = true },
                0x2A => Sse(Slot.Reg) with { RmGpr = simd is 0xF3 or 0xF2 },
                0x2C or 0x2D when simd is 0xF3 or 0xF2 => Sse(Slot.Reg) with { RegGpr = true, WriteFirst = true },
                0x2C or 0x2D => Sse(Slot.Reg),
                0x31 => new Form { Implicit = Rax | Rdx },
                >= 0x40 and <= 0x4F => Gpr(InstructionKind.Other, Slot.Reg, true),
                0x50 => Sse(Slot.Reg) with { RegGpr = true, WriteFirst = true, RegisterOnly = true },
                (>= 0x51 and <= 0x6D) or 0x6F or 0x74 or 0x75 or 0x76 or 0x7C or 0x7D => Sse(Slot.Reg),
                0x6E => Sse(Slot.Reg) with { RmGpr = true },
                0x70 => Sse(Slot.Reg) with { Imm = Immediate.Byte },
                >= 0x71 and <= 0x73 => reg is 2 or 6 || (reg == 4 && op != 0x73) || (simd == 0x66 && op == 0x73 && reg is 3 or 7)
                    ? Sse(Slot.Rm) with { Imm = Immediate.Byte, RegisterOnly = true } : null,
                0x77 => new Form(),
                0x7E when simd == 0xF3 => Sse(Slot.Reg),
                0x7E => Sse(Slot.Rm) with { RmGpr = true, WriteFirst = true },
                0x7F => Sse(Slot.Rm),
                >= 0x80 and <= 0x8F => new Form { Kind = InstructionKind.Jcc, First = Slot.Immediate, Rel = 4 },
                >= 0x90 and <= 0x9F => Group(InstructionKind.Other, true) with { ByteRm = true },
                0xA2 => new Form { Implicit = Rax | Rbx | Rcx | Rdx },
                0xA3 => Gpr(InstructionKind.Other, Slot.Rm, false),
                0xAB or 0xB3 or 0xBB => Gpr(InstructionKind.Other, Slot.Rm, true),
                0xA4 or 0xAC => Gpr(InstructionKind.Other, Slot.Rm, true) with { Imm = Immediate.Byte },
                0xA5 or 0xAD => Gpr(InstructionKind.Other, Slot.Rm, true),
                0xAE when !register => Sse(Slot.Rm),
                0xAE when reg >= 5 => new Form { ModRm = true },
                0xAF => Gpr(InstructionKind.Other, Slot.Reg, true),
                0xB0 => Gpr(InstructionKind.Other, Slot.Rm, true) with { Byte = true, Implicit = Rax },
                0xB1 => Gpr(InstructionKind.Other, Slot.Rm, true) with { Implicit = Rax },
                0xB6 or 0xBE => Gpr(InstructionKind.Other, Slot.Reg, true) with { ByteRm = true },
                0xB7 or 0xBF or 0xBC or 0xBD => Gpr(InstructionKind.Other, Slot.Reg, true),
                0xB8 when simd == 0xF3 => Gpr(InstructionKind.Other, Slot.Reg, true),
                0xBA when reg >= 4 => Group(InstructionKind.Other, reg != 4) with { Imm = Immediate.Byte },
                0xC0 => Gpr(InstructionKind.Other, Slot.Rm, true) with { Byte = true, WriteBoth = true },
                0xC1 => Gpr(InstructionKind.Other, Slot.Rm, true) with { WriteBoth = true },
                0xC2 or 0xC6 => Sse(Slot.Reg) with { Imm = Immediate.Byte },
                0xC3 => Gpr(InstructionKind.Other, Slot.Rm, false) with { MemoryOnly = true },
                0xC4 => Sse(Slot.Reg) with { Imm = Immediate.Byte, RmGpr = true },
                0xC5 => Sse(Slot.Reg) with { Imm = Immediate.Byte, RegGpr = true, WriteFirst = true, RegisterOnly = true },
                0xC7 when reg == 1 && !register => Sse(Slot.Rm) with { Implicit = Rax | Rdx },
                0xC7 when reg is 6 or 7 && register => Group(InstructionKind.Other, true),
                >= 0xC8 and <= 0xCF => new Form { First = Slot.OpcodeReg, WriteFirst = true },
                0xD6 when simd == 0x66 => Sse(Slot.Rm),
                0xD7 => Sse(Slot.Reg) with { RegGpr = true, WriteFirst = true, RegisterOnly = true },
                0xE7 => Sse(Slot.Rm) with { MemoryOnly = true },
                (>= 0xD0 and <= 0xFE) and not (0xD6 or 0xF7) => Sse(Slot.Reg),
                _ => null,
            };
        }

        private static Form? Legacy0F38Form(int op, int simd) => op switch
        {
            (>= 0x00 and <= 0x0B) or 0x10 or 0x14 or 0x15 or 0x17 or (>= 0x1C and <= 0x1E) or (>= 0x20 and <= 0x25)
                or (>= 0x28 and <= 0x2B) or (>= 0x30 and <= 0x35) or (>= 0x37 and <= 0x41) => Sse(Slot.Reg),
            0xF0 when simd == 0xF2 => Gpr(InstructionKind.Other, Slot.Reg, true) with { ByteRm = true },
            0xF1 when simd == 0xF2 => Gpr(InstructionKind.Other, Slot.Reg, true),
            0xF0 => Gpr(InstructionKind.Other, Slot.Reg, true) with { MemoryOnly = true },
            0xF1 => Gpr(InstructionKind.Other, Slot.Rm, false) with { MemoryOnly = true },
            _ => null,
        };

        private static Form? Vex0F38Form(int op, int modRm, int simd) => op switch
        {
            (>= 0x00 and <= 0x1E) or (>= 0x20 and <= 0x2B) or (>= 0x30 and <= 0x41) or (>= 0x45 and <= 0x47) or 0x58 or 0x59
                or 0x78 or 0x79 or (>= 0x96 and <= 0xBF) => Sse(Slot.Reg),
            0x2C or 0x2D or 0x8C => Sse(Slot.Reg) with { MemoryOnly = true },
            0x2E or 0x2F or 0x8E => Sse(Slot.Rm) with { MemoryOnly = true },
            0xF2 or 0xF5 or 0xF7 => Gpr(InstructionKind.Other, Slot.Reg, true) with { VexGpr = true },
            0xF3 when RegOf(modRm) is >= 1 and <= 3 => Gpr(InstructionKind.Other, Slot.Vvvv, true) with { VexGpr = true },
            0xF6 when simd == 0xF2 => Gpr(InstructionKind.Other, Slot.Reg, true) with { VexGpr = true, WriteVvvv = true },
            _ => null,
        };

        private static Form? ThreeAForm(int op, bool vex) => op switch
        {
            (>= 0x08 and <= 0x0F) or 0x20 or 0x21 or 0x22 or (>= 0x40 and <= 0x42) or 0x44 or 0x60 or 0x62
                => Sse(Slot.Reg) with { Imm = Immediate.Byte, RmGpr = op is 0x20 or 0x22 },
            0x61 or 0x63 => Sse(Slot.Reg) with { Imm = Immediate.Byte, Implicit = Rcx },
            >= 0x14 and <= 0x17 => Sse(Slot.Rm) with { Imm = Immediate.Byte, RmGpr = true, WriteFirst = true },
            (>= 0x00 and <= 0x06) or 0x18 or 0x38 or 0x46 or (>= 0x4A and <= 0x4C) when vex => Sse(Slot.Reg) with { Imm = Immediate.Byte },
            0x19 or 0x1D or 0x39 when vex => Sse(Slot.Rm) with { Imm = Immediate.Byte },
            0xF0 when vex => Gpr(InstructionKind.Other, Slot.Reg, true) with { Imm = Immediate.Byte, VexGpr = true },
            _ => null,
        };
    }
}
