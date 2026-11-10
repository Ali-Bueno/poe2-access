namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using GameReader.Utils.CodeReader;
    using static Check;

    /// <summary>The host's x64 decoder: known encodings, and agreement with capstone on the game's own code.</summary>
    public static class X64DecoderTests
    {
        private const string FixtureName = "x64-capstone.tsv";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(KnownEncodingsDecode), KnownEncodingsDecode),
            (nameof(UnknownEncodingsStop), UnknownEncodingsStop),
            (nameof(DecoderAgreesWithCapstoneOnTheGamesCode), DecoderAgreesWithCapstoneOnTheGamesCode),
        };

        /// <summary>Compares the decoder with a capstone dump (re/decoder_truth.py) and prints the numbers; 0 when every decoded row agrees.</summary>
        public static int CompareFile(string path)
        {
            var result = Compare(File.ReadLines(path));
            Console.WriteLine(result.Summary());
            foreach (var (shape, count) in result.Unknown.OrderByDescending(pair => pair.Value).Take(40))
            {
                Console.WriteLine($"  unknown {shape}: {count}");
            }

            foreach (var line in result.Disagreements.Take(40))
            {
                Console.WriteLine($"  differs {line}");
            }

            return result.Disagreements.Count == 0 ? 0 : 1;
        }

        private static void KnownEncodingsDecode()
        {
            // mov eax,[rax+0x1E0]: ModRM disp32.
            var i = Decode("8B 80 E0 01 00 00");
            Expect(i, 6, InstructionKind.Mov, X64.Rax, X64.NoRegister, 0x1E0, "mov eax,[rax+0x1E0]");
            Equal(OperandKind.Register, i.First, "a load's destination is a register");
            Equal(1 << X64.Rax, (int)i.Writes, "it writes rax");

            // mov rcx,[rcx+8]: REX.W, disp8.
            i = Decode("48 8B 49 08");
            Expect(i, 4, InstructionKind.Mov, X64.Rcx, X64.NoRegister, 8, "mov rcx,[rcx+8]");
            Equal(8, i.FirstSize, "REX.W makes it 64 bits");

            // mov r8,[r12+rbx*8+0x10]: REX.WRXB-style SIB with r12 base.
            i = Decode("4D 8B 44 DC 10");
            Expect(i, 5, InstructionKind.Mov, 12, X64.Rbx, 0x10, "SIB with REX.B");
            Equal(8, i.Scale, "scale 8");
            Equal(8, i.FirstRegister, "REX.R names r8");

            // mov rax,[rsp+0x28]: SIB with no index.
            i = Decode("48 8B 44 24 28");
            Expect(i, 5, InstructionKind.Mov, X64.Rsp, X64.NoRegister, 0x28, "rsp needs a SIB");

            // lea rdx,[rip+0x100]: RIP-relative.
            i = Decode("48 8D 15 00 01 00 00", 0x1000);
            Expect(i, 7, InstructionKind.Lea, X64.Rip, X64.NoRegister, 0x100, "lea rdx,[rip+0x100]");
            Equal(0x1000L + 7 + 0x100, i.RipTarget, "the RIP-relative target");

            // mov rax,gs:[0x58]: SIB with neither base nor index.
            i = Decode("65 48 8B 04 25 58 00 00 00");
            Expect(i, 9, InstructionKind.Mov, X64.NoRegister, X64.NoRegister, 0x58, "an absolute address");

            // call rel32 and jcc rel8 / rel32.
            i = Decode("E8 10 00 00 00", 0x2000);
            Equal(InstructionKind.Call, i.Kind, "call");
            Equal(0x2015L, i.DirectTarget, "call target");
            Equal(true, i.WritesRegister(X64.Rsp), "a call moves rsp");
            Equal(0x2000L + 2 - 2, Decode("74 FE", 0x2000).DirectTarget, "je to itself");
            Equal(0x2000L + 6 + 0x80, Decode("0F 84 80 00 00 00", 0x2000).DirectTarget, "0F map jcc rel32");

            // 66 prefix: mov word ptr [rcx+4], 0x1234 (iw), and cmp ax,[rdx].
            i = Decode("66 C7 41 04 34 12");
            Expect(i, 6, InstructionKind.Mov, X64.Rcx, X64.NoRegister, 4, "66 shrinks the immediate");
            Equal(OperandKind.Memory, i.First, "a store's destination is memory");
            Equal(InstructionKind.Cmp, Decode("66 3B 02").Kind, "cmp ax,[rdx]");

            // F3 / F2 SSE: movss xmm0,[rax+0x138]; movsd [rsp+8],xmm1.
            i = Decode("F3 0F 10 80 38 01 00 00");
            Expect(i, 8, InstructionKind.Other, X64.Rax, X64.NoRegister, 0x138, "movss load");
            Equal(OperandKind.Register, i.First, "movss loads into a register");
            Equal(0, (int)i.Writes, "an xmm write is no general register");
            Equal(OperandKind.Memory, Decode("F2 0F 11 4C 24 08").First, "movsd store");

            // 0F map: movzx eax,byte ptr [rcx+0x69]; cmovne; setcc writes its byte register's full register.
            i = Decode("0F B6 41 69");
            Expect(i, 4, InstructionKind.Other, X64.Rcx, X64.NoRegister, 0x69, "movzx");
            Equal(1 << X64.Rax, (int)Decode("0F 95 C4").Writes, "setne ah writes rax");
            Equal(1 << 12, (int)Decode("41 0F 95 C4").Writes, "setne r12b");

            // 0F 38 / 0F 3A maps: pshufb xmm0,[rax]; pextrd eax,xmm1,2; pinsrd xmm0,[rcx+4],1.
            Equal(5, Decode("66 0F 38 00 00").Length, "0F 38 has no immediate");
            i = Decode("66 0F 3A 16 C8 02");
            Equal(6, i.Length, "0F 3A carries an imm8");
            Equal(1 << X64.Rax, (int)i.Writes, "pextrd writes its general register");
            Expect(Decode("66 0F 3A 22 41 04 01"), 7, InstructionKind.Other, X64.Rcx, X64.NoRegister, 4, "pinsrd");

            // Immediates: imul eax,[rbx+0x10],0x12345678; mov rax,imm64; enter.
            Equal(7, Decode("69 43 10 78 56 34 12").Length, "imul Gv,Ev,iz");
            Equal(10, Decode("48 B8 01 02 03 04 05 06 07 08").Length, "mov r64,imm64");
            Equal(4, Decode("C8 20 00 00").Length, "enter iw,ib");
            Equal(7, Decode("81 7F 08 FF FF 00 00").Length, "cmp dword ptr [rdi+8],imm32");
            Equal(InstructionKind.Cmp, Decode("80 7F 08 01").Kind, "cmp byte ptr [rdi+8],1");

            // The scale rule's shifts and products: shl rdx,6; imul rbp,rcx,0x38; imul rcx,[r8+0x18]; sar rax,6 is not a shl.
            i = Decode("48 C1 E2 06");
            Equal((InstructionKind.Shl, X64.Rdx, 6L), (i.Kind, i.FirstRegister, i.Immediate), "shl rdx,6");
            i = Decode("48 6B E9 38");
            Equal((InstructionKind.Imul, X64.Rbp, X64.Rcx, 0x38L), (i.Kind, i.FirstRegister, i.SecondRegister, i.Immediate), "imul rbp,rcx,0x38");
            Equal(InstructionKind.Imul, Decode("49 0F AF 48 18").Kind, "imul rcx,[r8+0x18]");
            Equal(InstructionKind.Imul, Decode("48 F7 E9").Kind, "imul rcx (rdx:rax)");
            Equal(InstructionKind.Other, Decode("48 C1 F8 06").Kind, "sar rax,6");

            // xchg / xadd write both; div writes rax and rdx; rep stosq writes rdi and rcx.
            Equal((1 << X64.Rax) | (1 << X64.Rcx), (int)Decode("48 87 C8").Writes, "xchg rax,rcx");
            Equal((1 << X64.Rax) | (1 << X64.Rdx), (int)Decode("48 F7 F1").Writes, "div rcx");
            Equal((1 << X64.Rcx) | (1 << X64.Rdi), (int)Decode("F3 48 AB").Writes, "rep stosq");

            // VEX: vmovss xmm0,[rcx+0x1E0] (C5) and vpextrd eax,xmm1,2 (C4).
            Expect(Decode("C5 FA 10 81 E0 01 00 00"), 8, InstructionKind.Other, X64.Rcx, X64.NoRegister, 0x1E0, "VEX2 movss");
            i = Decode("C4 E3 79 16 C8 02");
            Equal(6, i.Length, "VEX3 0F3A carries an imm8");
            Equal(1 << X64.Rax, (int)i.Writes, "vpextrd writes its general register");

            // Nops and padding.
            Equal(InstructionKind.Nop, Decode("66 0F 1F 44 00 00").Kind, "a 6-byte nop");
            Equal(InstructionKind.Int3, Decode("CC").Kind, "int3");
            Equal(InstructionKind.Ret, Decode("C3").Kind, "ret");
        }

        private static void UnknownEncodingsStop()
        {
            foreach (var bytes in new[] { "D9 45 08", "67 8B 00", "62 F1 7C 48 10 00", "0F 0F C1 B4", "8B", "48 8D C0", "0F 05", "E4 60", "66 E8 00 00 00 00" })
            {
                Equal(false, X64Decoder.TryDecode(Bytes(bytes), 0, out _), $"{bytes} is not guessed");
            }
        }

        private static void DecoderAgreesWithCapstoneOnTheGamesCode()
        {
            var directory = AppContext.BaseDirectory;
            while (!File.Exists(Path.Join(directory, "tests", "Poe2Accessibility.Tests", "Fixtures", FixtureName)))
            {
                directory = Path.GetDirectoryName(directory) ?? throw new FileNotFoundException($"{FixtureName} is not under any folder above the tests");
            }

            var result = Compare(File.ReadLines(Path.Join(directory, "tests", "Poe2Accessibility.Tests", "Fixtures", FixtureName)));
            Equal(string.Empty, string.Join("; ", result.Disagreements.Take(5)), "every decoded row agrees with capstone");
            Equal(true, result.Decoded * 100 >= result.Rows * 98, $"most rows decode ({result.Summary()})");
        }

        private static ComparisonResult Compare(IEnumerable<string> lines)
        {
            var result = new ComparisonResult();
            foreach (var line in lines)
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var column = line.Split('\t');
                var address = long.Parse(column[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var bytes = Convert.FromHexString(column[1]);
                result.Rows++;
                if (!X64Decoder.TryDecode(bytes, address, out var i))
                {
                    var shape = $"{column[3]} {column[1][..Math.Min(column[1].Length, 8)]}";
                    result.Unknown[shape] = result.Unknown.GetValueOrDefault(shape) + 1;
                    continue;
                }

                result.Decoded++;
                var why = Differences(i, column);
                if (why.Length > 0)
                {
                    result.Disagreements.Add($"{column[0]} {column[1]} {column[3]}: {why}");
                    continue;
                }

                var theirs = Writes(column[8]);
                result.ExactWrites += theirs == i.Writes ? 1 : 0;
            }

            return result;
        }

        private static string Differences(X64Instruction i, string[] column)
        {
            var why = new List<string>();
            if (i.Length != int.Parse(column[2], CultureInfo.InvariantCulture))
            {
                why.Add($"length {i.Length}");
            }

            var memory = i.HasMemory ? $"{X64.Name(i.Base)},{X64.Name(i.Index)},{i.Scale},{i.Displacement}" : "-";
            if (memory != column[5])
            {
                why.Add($"memory {memory}");
            }

            var branch = i.First != OperandKind.Immediate || i.DirectTarget == -1 ? "-"
                : i.Target < 0 ? "-" + (-i.Target).ToString("X", CultureInfo.InvariantCulture) : i.Target.ToString("X", CultureInfo.InvariantCulture);
            if (branch != column[7])
            {
                why.Add($"branch {branch}");
            }

            var first = i.First switch { OperandKind.Register => "r", OperandKind.Memory => "m", OperandKind.Immediate => "i", _ => "-" };
            if (first != column[4])
            {
                why.Add($"first operand {first}");
            }

            if (KindOf(column[3]) is var kind && kind != i.Kind)
            {
                why.Add($"kind {i.Kind}");
            }

            var theirs = Writes(column[8]);
            if ((theirs & ~i.Writes) != 0)
            {
                why.Add($"writes {i.Writes:X} miss {theirs & ~i.Writes:X}");
            }

            // The register tracker relies on these: a register named must be capstone's, and a mov or lea names both and its size.
            var copies = i.Kind is InstructionKind.Mov or InstructionKind.Lea;
            var firstRegister = i.FirstRegister >= 0 ? X64.Name(i.FirstRegister) : "-";
            if ((i.FirstRegister >= 0 || copies) && firstRegister != column[9])
            {
                why.Add($"first register {firstRegister}");
            }

            var secondRegister = i.SecondRegister >= 0 ? X64.Name(i.SecondRegister) : "-";
            if ((i.SecondRegister >= 0 || i.Kind == InstructionKind.Mov) && secondRegister != column[10])
            {
                why.Add($"second register {secondRegister}");
            }

            if ((copies || i.Kind == InstructionKind.Add) && i.FirstRegister >= 0 && i.FirstSize.ToString(CultureInfo.InvariantCulture) != column[11])
            {
                why.Add($"size {i.FirstSize}");
            }

            // The field rule shifts a register by add's immediate.
            if (i.Kind == InstructionKind.Add && i.FirstSize == sizeof(long) && column[12] != "-"
                && i.Immediate.ToString(CultureInfo.InvariantCulture) != column[12])
            {
                why.Add($"immediate {i.Immediate}");
            }

            // The scale rule multiplies an index by shl's count and the three-operand imul's factor (shl by one carries none).
            if (i.Kind is InstructionKind.Shl or InstructionKind.Imul && i.Immediate != 0 && i.Immediate.ToString(CultureInfo.InvariantCulture) != column[12])
            {
                why.Add($"immediate {i.Immediate}");
            }

            return string.Join(", ", why);
        }

        private static InstructionKind KindOf(string mnemonic) => mnemonic.Replace("lock ", string.Empty, StringComparison.Ordinal) switch
        {
            "mov" => InstructionKind.Mov,
            "lea" => InstructionKind.Lea,
            "call" => InstructionKind.Call,
            "jmp" => InstructionKind.Jmp,
            "loop" or "loope" or "loopne" => InstructionKind.Loop,
            "ret" => InstructionKind.Ret,
            "int3" => InstructionKind.Int3,
            "nop" => InstructionKind.Nop,
            "push" => InstructionKind.Push,
            "pop" => InstructionKind.Pop,
            "cmp" => InstructionKind.Cmp,
            "test" => InstructionKind.Test,
            "add" => InstructionKind.Add,
            "shl" => InstructionKind.Shl,
            "imul" => InstructionKind.Imul,
            _ when mnemonic.StartsWith('j') => InstructionKind.Jcc,
            _ => InstructionKind.Other,
        };

        private static ushort Writes(string names) => names == "-" ? (ushort)0
            : (ushort)names.Split(',').Select(X64.Parse).Where(register => register is >= 0 and < X64.Registers).Aggregate(0, (bits, register) => bits | (1 << register));

        private static void Expect(X64Instruction i, int length, InstructionKind kind, int baseRegister, int index, long displacement, string what)
        {
            Equal(length, i.Length, $"{what}: length");
            Equal(kind, i.Kind, $"{what}: kind");
            Equal(true, i.HasMemory, $"{what}: memory");
            Equal(baseRegister, i.Base, $"{what}: base");
            Equal(index, i.Index, $"{what}: index");
            Equal(displacement, i.Displacement, $"{what}: displacement");
        }

        private static X64Instruction Decode(string hex, long address = 0)
        {
            if (!X64Decoder.TryDecode(Bytes(hex), address, out var instruction))
            {
                throw new InvalidOperationException($"{hex} does not decode");
            }

            return instruction;
        }

        private static byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

        private sealed class ComparisonResult
        {
            public int Rows { get; set; }

            public int Decoded { get; set; }

            public int ExactWrites { get; set; }

            public Dictionary<string, int> Unknown { get; } = new();

            public List<string> Disagreements { get; } = new();

            public string Summary() =>
                $"{this.Rows} rows: {this.Decoded} decoded ({100.0 * this.Decoded / Math.Max(1, this.Rows):F2}%), {this.Rows - this.Decoded} unknown, "
                + $"{this.Disagreements.Count} disagree; written registers exactly capstone's on {this.ExactWrites}, a superset on the rest";
        }
    }
}
