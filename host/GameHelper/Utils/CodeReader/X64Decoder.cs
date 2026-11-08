// <copyright file="X64Decoder.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils.CodeReader
{
    using System;

    /// <summary>
    ///     A minimal x64 decoder: length, memory operand, branch target and written general registers. An encoding it does not
    ///     know decodes to nothing, so a caller stops instead of guessing (docs/research/exe-layouts.md, "The semantic source").
    /// </summary>
    public static class X64Decoder
    {
        private const byte OperandSize = 0x66;
        private const byte AddressSize = 0x67;
        private const byte Lock = 0xF0;
        private const byte RepNe = 0xF2;
        private const byte Rep = 0xF3;
        private const byte Escape = 0x0F;
        private const byte Escape38 = 0x38;
        private const byte Escape3A = 0x3A;
        private const byte Vex2 = 0xC5;
        private const byte Vex3 = 0xC4;
        private const byte RexFirst = 0x40;
        private const byte RexLast = 0x4F;
        private const int RexW = 8;
        private const int RexR = 4;
        private const int RexX = 2;
        private const int RexB = 1;
        private const int ModRegister = 3;
        private const int SibRm = 4;
        private const int RipRm = 5;
        private const int NoIndex = 4;
        private const int NoBase = 5;

        /// <summary>Decodes the instruction at the start of <paramref name="code"/>; false when the encoding is not one it knows.</summary>
        public static bool TryDecode(ReadOnlySpan<byte> code, long address, out X64Instruction instruction)
        {
            instruction = default;
            if (code.Length > X64.MaxLength)
            {
                code = code[..X64.MaxLength];
            }

            var at = 0;
            var operandSize = false;
            var simd = 0;
            while (at < code.Length && IsPrefix(code[at]))
            {
                switch (code[at])
                {
                    case OperandSize:
                        operandSize = true;
                        break;
                    case RepNe or Rep:
                        simd = code[at];
                        break;
                    case AddressSize:
                        return false;
                }

                at++;
            }

            var rex = 0;
            var hasRex = false;
            if (at < code.Length && code[at] is >= RexFirst and <= RexLast)
            {
                rex = code[at++] & 0xF;
                hasRex = true;
            }

            if (at >= code.Length)
            {
                return false;
            }

            var op = (int)code[at++];
            var map = X64Forms.OneByte;
            var vex = false;
            var vvvv = 0;
            if (op is Vex2 or Vex3)
            {
                if (hasRex || operandSize || simd != 0 || !ReadVex(code, ref at, op, out map, out rex, out vvvv, out var pp))
                {
                    return false;
                }

                (vex, hasRex) = (true, true);
                (operandSize, simd) = pp switch { 1 => (true, (int)OperandSize), 2 => (false, (int)Rep), 3 => (false, (int)RepNe), _ => (false, 0) };
                op = code[at++];
            }
            else if (op == Escape)
            {
                if (at >= code.Length)
                {
                    return false;
                }

                op = code[at++];
                map = X64Forms.Map0F;
                if (op is Escape38 or Escape3A)
                {
                    map = op == Escape38 ? X64Forms.Map0F38 : X64Forms.Map0F3A;
                    if (at >= code.Length)
                    {
                        return false;
                    }

                    op = code[at++];
                }
            }

            if (map != X64Forms.OneByte && !vex && simd == 0 && operandSize)
            {
                simd = OperandSize;
            }

            var hasModRm = X64Forms.HasModRm(map, op);
            if (hasModRm && at >= code.Length)
            {
                return false;
            }

            var modRm = hasModRm ? code[at] : -1;
            if (X64Forms.Describe(map, op, modRm, simd, (rex & RexB) != 0, vex) is not { } form || form.ModRm != hasModRm)
            {
                return false;
            }

            var memory = new MemoryOperand(X64.NoRegister, X64.NoRegister, 1, 0, false);
            var (regField, rmRegister) = (X64.NoRegister, X64.NoRegister);
            if (hasModRm)
            {
                at++;
                var mod = modRm >> 6;
                regField = ((modRm >> 3) & 7) | ((rex & RexR) != 0 ? 8 : 0);
                if (mod == ModRegister)
                {
                    if (form.MemoryOnly)
                    {
                        return false;
                    }

                    rmRegister = (modRm & 7) | ((rex & RexB) != 0 ? 8 : 0);
                }
                else if (form.RegisterOnly || !ReadMemory(code, ref at, modRm, rex, out memory))
                {
                    return false;
                }
            }

            var immediate = ImmediateBytes(form.Imm, operandSize, (rex & RexW) != 0);
            if (operandSize && form.Rel == sizeof(int))
            {
                return false;
            }

            var end = at + immediate + form.Rel;
            if (end > code.Length)
            {
                return false;
            }

            long value = immediate switch
            {
                sizeof(byte) => (sbyte)code[at],
                sizeof(short) => BitConverter.ToInt16(code[at..]),
                sizeof(int) => BitConverter.ToInt32(code[at..]),
                sizeof(long) => BitConverter.ToInt64(code[at..]),
                _ => 0,
            };
            var target = -1L;
            if (form.Rel > 0)
            {
                var rel = form.Rel == 1 ? (sbyte)code[at + immediate] : BitConverter.ToInt32(code[(at + immediate)..]);
                target = address + end + rel;
            }

            if (form.StringBase != 0)
            {
                memory = new MemoryOperand(form.StringBase, X64.NoRegister, 1, 0, true);
            }

            var size = form.Byte ? sizeof(byte)
                : form.VexGpr ? ((rex & RexW) != 0 ? sizeof(long) : sizeof(int))
                : form.Default64 ? (operandSize ? sizeof(short) : sizeof(long))
                : (rex & RexW) != 0 ? sizeof(long) : operandSize ? sizeof(short) : sizeof(int);
            var regGpr = form.RegGpr ? ByteRegister(regField, form.Byte, hasRex) : X64.NoRegister;
            var rmGpr = form.RmGpr && rmRegister >= 0 ? ByteRegister(rmRegister, form.Byte || form.ByteRm, hasRex) : X64.NoRegister;
            var (first, firstRegister, second) = form.First switch
            {
                Slot.Reg => (OperandKind.Register, regGpr, rmGpr),
                Slot.Rm => (memory.Present ? OperandKind.Memory : OperandKind.Register, rmGpr, regGpr),
                Slot.OpcodeReg => (OperandKind.Register, ByteRegister((op & 7) | ((rex & RexB) != 0 ? 8 : 0), form.Byte, hasRex), X64.NoRegister),
                Slot.Accumulator => (OperandKind.Register, X64.Rax, X64.NoRegister),
                Slot.Immediate => (OperandKind.Immediate, X64.NoRegister, X64.NoRegister),
                Slot.Vvvv => (OperandKind.Register, vvvv, rmGpr),
                Slot.StringMemory => (OperandKind.Memory, X64.NoRegister, X64.NoRegister),
                _ => (OperandKind.None, X64.NoRegister, X64.NoRegister),
            };

            // A VEX form's second operand is mostly vvvv, not the ModRM register; no rule needs it, so none is named.
            if (vex && form.First != Slot.Vvvv && !form.WriteBoth)
            {
                second = X64.NoRegister;
            }

            var writes = form.Implicit;
            if (form.StringBase != 0 && simd != 0)
            {
                writes |= 1 << X64.Rcx;
            }

            writes |= Bit(form.WriteFirst ? firstRegister : X64.NoRegister);
            writes |= Bit(form.WriteBoth ? second : X64.NoRegister);
            writes |= Bit(form.WriteVvvv ? vvvv : X64.NoRegister);
            var kind = form.Kind == InstructionKind.Mov && form.Imm == Immediate.V && (rex & RexW) != 0 ? InstructionKind.Other : form.Kind;
            instruction = new X64Instruction(
                address, end, kind, first, firstRegister >= 0 ? size : 0, firstRegister, second,
                memory.Base, memory.Index, memory.Scale, memory.Displacement, target, writes, memory.Present, value);
            return true;
        }

        private static bool IsPrefix(byte value) =>
            value is OperandSize or AddressSize or Lock or RepNe or Rep or 0x2E or 0x3E or 0x26 or 0x36 or 0x64 or 0x65;

        /// <summary>A byte operand without REX names ah, ch, dh or bh for 4 to 7: the high byte of rax to rbx.</summary>
        private static int ByteRegister(int register, bool isByte, bool hasRex) =>
            isByte && !hasRex && register is >= X64.Rsp and <= X64.Rdi ? register - X64.Rsp : register;

        private static ushort Bit(int register) => register is >= 0 and < X64.Registers ? (ushort)(1 << register) : (ushort)0;

        private static int ImmediateBytes(Immediate kind, bool operandSize, bool wide) => kind switch
        {
            Immediate.Byte => sizeof(byte),
            Immediate.Word => sizeof(short),
            Immediate.Z => operandSize ? sizeof(short) : sizeof(int),
            Immediate.V => wide ? sizeof(long) : operandSize ? sizeof(short) : sizeof(int),
            Immediate.WordByte => sizeof(short) + sizeof(byte),
            _ => 0,
        };

        /// <summary>A VEX prefix: its map, its R/X/B/W bits as a REX nibble, its vvvv register and its implied prefix.</summary>
        private static bool ReadVex(ReadOnlySpan<byte> code, ref int at, int op, out int map, out int rex, out int vvvv, out int pp)
        {
            (map, rex, vvvv, pp) = (0, 0, 0, 0);
            var need = op == Vex2 ? 2 : 3;
            if (at + need > code.Length)
            {
                return false;
            }

            var last = code[at + need - 2];
            if (op == Vex2)
            {
                map = X64Forms.Map0F;
                rex = (last & 0x80) == 0 ? RexR : 0;
            }
            else
            {
                var first = code[at];
                map = first & 0x1F;
                rex = ((first & 0x80) == 0 ? RexR : 0) | ((first & 0x40) == 0 ? RexX : 0) | ((first & 0x20) == 0 ? RexB : 0)
                    | ((last & 0x80) != 0 ? RexW : 0);
                if (map is < X64Forms.Map0F or > X64Forms.Map0F3A)
                {
                    return false;
                }
            }

            vvvv = (~last >> 3) & 0xF;
            pp = last & 3;
            at += need - 1;
            return true;
        }

        private static bool ReadMemory(ReadOnlySpan<byte> code, ref int at, int modRm, int rex, out MemoryOperand memory)
        {
            memory = default;
            var mod = modRm >> 6;
            var rm = modRm & 7;
            var (baseRegister, index, scale) = (rm | ((rex & RexB) != 0 ? 8 : 0), X64.NoRegister, 1);
            var displacement = mod == 1 ? sizeof(byte) : mod == 2 ? sizeof(int) : 0;
            if (rm == SibRm)
            {
                if (at >= code.Length)
                {
                    return false;
                }

                var sib = code[at++];
                var indexField = ((sib >> 3) & 7) | ((rex & RexX) != 0 ? 8 : 0);
                if (indexField != NoIndex)
                {
                    (index, scale) = (indexField, 1 << (sib >> 6));
                }

                baseRegister = (sib & 7) | ((rex & RexB) != 0 ? 8 : 0);
                if ((sib & 7) == NoBase && mod == 0)
                {
                    (baseRegister, displacement) = (X64.NoRegister, sizeof(int));
                }
            }
            else if (rm == RipRm && mod == 0)
            {
                (baseRegister, displacement) = (X64.Rip, sizeof(int));
            }

            if (at + displacement > code.Length)
            {
                return false;
            }

            long value = displacement switch
            {
                sizeof(byte) => (sbyte)code[at],
                sizeof(int) => BitConverter.ToInt32(code[at..]),
                _ => 0,
            };
            at += displacement;
            memory = new MemoryOperand(baseRegister, index, scale, value, true);
            return true;
        }

        private readonly record struct MemoryOperand(int Base, int Index, int Scale, long Displacement, bool Present);
    }
}
