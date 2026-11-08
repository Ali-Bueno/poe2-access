// <copyright file="X64Instruction.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils.CodeReader
{
    /// <summary>What an instruction does, as far as the semantic rules tell instructions apart.</summary>
    public enum InstructionKind : byte
    {
        /// <summary>Anything else the decoder knows.</summary>
        Other,

        /// <summary><c>mov</c> between registers, memory and immediates (not movzx, movss or the like).</summary>
        Mov,

        /// <summary><c>lea</c>.</summary>
        Lea,

        /// <summary><c>call</c>, direct or not.</summary>
        Call,

        /// <summary><c>jmp</c>, direct or not.</summary>
        Jmp,

        /// <summary>A conditional jump, <c>jrcxz</c> included.</summary>
        Jcc,

        /// <summary><c>loop</c>, <c>loope</c>, <c>loopne</c>.</summary>
        Loop,

        /// <summary><c>ret</c>.</summary>
        Ret,

        /// <summary><c>int3</c>, the padding between functions.</summary>
        Int3,

        /// <summary>A no-operation, of any length.</summary>
        Nop,

        /// <summary><c>push</c>.</summary>
        Push,

        /// <summary><c>pop</c>.</summary>
        Pop,

        /// <summary><c>cmp</c>.</summary>
        Cmp,

        /// <summary><c>add</c>.</summary>
        Add,

        /// <summary><c>test</c>.</summary>
        Test,
    }

    /// <summary>What an instruction's first operand is.</summary>
    public enum OperandKind : byte
    {
        /// <summary>It has none.</summary>
        None,

        /// <summary>A register of any class.</summary>
        Register,

        /// <summary>Memory.</summary>
        Memory,

        /// <summary>An immediate or a branch target.</summary>
        Immediate,
    }

    /// <summary>One decoded instruction: its length, its memory operand, its branch target and the registers it writes.</summary>
    /// <param name="Address">Where it is (an RVA).</param>
    /// <param name="Length">Its bytes.</param>
    /// <param name="Kind">What it does.</param>
    /// <param name="First">What its first operand is.</param>
    /// <param name="FirstSize">The first operand's bytes, for a general register.</param>
    /// <param name="FirstRegister">The first operand's general register, or <see cref="X64.NoRegister"/>.</param>
    /// <param name="SecondRegister">The second operand's general register, or <see cref="X64.NoRegister"/>.</param>
    /// <param name="Base">The memory operand's base, <see cref="X64.Rip"/>, or <see cref="X64.NoRegister"/>.</param>
    /// <param name="Index">The memory operand's index, or <see cref="X64.NoRegister"/>.</param>
    /// <param name="Scale">The index's scale; 1 without an index.</param>
    /// <param name="Displacement">The memory operand's displacement.</param>
    /// <param name="Target">A direct branch's target, or -1.</param>
    /// <param name="Writes">The general registers it writes, one bit each (bit 0 is rax).</param>
    /// <param name="HasMemory">Whether it has a memory operand.</param>
    /// <param name="Immediate">Its immediate, sign-extended, or 0.</param>
    public readonly record struct X64Instruction(
        long Address,
        int Length,
        InstructionKind Kind,
        OperandKind First,
        int FirstSize,
        int FirstRegister,
        int SecondRegister,
        int Base,
        int Index,
        int Scale,
        long Displacement,
        long Target,
        ushort Writes,
        bool HasMemory,
        long Immediate)
    {
        /// <summary>Gets the address a RIP-relative operand points at, or -1.</summary>
        public long RipTarget => this.HasMemory && this.Base == X64.Rip ? this.Address + this.Length + this.Displacement : -1;

        /// <summary>Gets the address after it.</summary>
        public long End => this.Address + this.Length;

        /// <summary>Gets whether it writes the register.</summary>
        public bool WritesRegister(int register) => register >= 0 && register < X64.Registers && (this.Writes & (1 << register)) != 0;

        /// <summary>Gets the direct target of a call, jmp or conditional jump, or -1.</summary>
        public long DirectTarget => this.Kind is InstructionKind.Call or InstructionKind.Jmp or InstructionKind.Jcc ? this.Target : -1;
    }

    /// <summary>The general registers, numbered as the encoding numbers them.</summary>
    public static class X64
    {
        /// <summary>The number of general registers.</summary>
        public const int Registers = 16;

        /// <summary>No register.</summary>
        public const int NoRegister = -1;

        /// <summary>The instruction pointer, as a memory base.</summary>
        public const int Rip = 16;

        public const int Rax = 0;
        public const int Rcx = 1;
        public const int Rdx = 2;
        public const int Rbx = 3;
        public const int Rsp = 4;
        public const int Rbp = 5;
        public const int Rsi = 6;
        public const int Rdi = 7;
        public const int R8 = 8;
        public const int R9 = 9;
        public const int R10 = 10;
        public const int R11 = 11;

        /// <summary>The longest instruction the architecture allows.</summary>
        public const int MaxLength = 15;

        /// <summary>The registers a call may change (the Windows x64 convention).</summary>
        public const ushort Volatile = (1 << Rax) | (1 << Rcx) | (1 << Rdx) | (1 << R8) | (1 << R9) | (1 << R10) | (1 << R11);

        private static readonly string[] Names =
        {
            "rax", "rcx", "rdx", "rbx", "rsp", "rbp", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15", "rip",
        };

        /// <summary>A register's 64-bit name, or an empty string for none.</summary>
        public static string Name(int register) => register >= 0 && register < Names.Length ? Names[register] : string.Empty;

        /// <summary>A register by its 64-bit name, or <see cref="NoRegister"/>.</summary>
        public static int Parse(string name) => System.Array.IndexOf(Names, name);
    }
}
