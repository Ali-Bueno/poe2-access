namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.Utils;
    using GameReader.Utils.CodeReader;

    /// <summary>
    ///     The stick rule's constants read from the game's exe on disk, so a retuned rule is followed instead of kept stale
    ///     (docs/compatibility.md): the stick step loads 0.94, 8.333 and 1.5 in three consecutive movss (RVA 0x9B9E0F in build
    ///     26476662) and its cone lambda compares against 0.7071 then multiplies by 4 (0x9CA139); both sequences hit, with
    ///     the same constants, on the five archived builds from 25244054 on. A sequence's hits must all read the same value, and
    ///     each value must be plausible, else the compiled-in constants stand. Pure.
    /// </summary>
    public static class StickRuleCode
    {
        /// <summary>mov rbx,[r12+0x58]; movss xmm8,[rip+cos]; movss xmm9,[rip+slope]; movss xmm10,[rip+base]: one hit per build.</summary>
        private const string LinkedSignature = "49 8B 5C 24 58 F3 44 0F 10 05 ^ ?? ?? ?? ?? F3 44 0F 10 0D ^ ?? ?? ?? ?? F3 44 0F 10 15 ^ ?? ?? ?? ??";

        /// <summary>mulss xmm6,[rax+4]; mulss xmm5,[rax]; addss xmm6,xmm5; comiss xmm6,[rip+cos]; jbe; mulss xmm6,[rip+slope]: two lambdas per build (stages 2 and 3), the same constants.</summary>
        private const string ConeSignature = "F3 0F 59 70 04 F3 0F 59 28 F3 0F 58 F5 0F 2F 35 ^ ?? ?? ?? ?? 76 ?? F3 0F 59 35 ^ ?? ?? ?? ??";

        private const string OperandMark = "^";
        private const string AnyByte = "??";
        private const int DisplacementBytes = 4;

        /// <summary>A cosine threshold lies strictly inside the unit interval; slopes and bases are positive and small.</summary>
        private const float MaxFactor = 100f;

        /// <summary>A sequence of code with RIP-relative float loads; the marks sit before each displacement.</summary>
        /// <param name="Fixed">The bytes that must match (false for a wildcard or a displacement).</param>
        /// <param name="Operands">Where each displacement starts in the sequence.</param>
        public sealed record Signature(byte[] Bytes, bool[] Fixed, IReadOnlyList<int> Operands);

        /// <summary>The rule's constants as the code holds them.</summary>
        public sealed record Rule(float LinkedCosine, float LinkedSlope, float LinkedBase, float ConeCosine, float ConeSlope);

        public static Signature Parse(string pattern)
        {
            var tokens = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var bytes = new List<byte>();
            var fixedBytes = new List<bool>();
            var operands = new List<int>();
            foreach (var token in tokens)
            {
                if (token == OperandMark)
                {
                    operands.Add(bytes.Count);
                    continue;
                }

                bytes.Add(token == AnyByte ? (byte)0 : Convert.ToByte(token, 16));
                fixedBytes.Add(token != AnyByte);
            }

            return new Signature(bytes.ToArray(), fixedBytes.ToArray(), operands);
        }

        /// <summary>The constants in the exe file, or null when either sequence is missing, ambiguous or implausible.</summary>
        public static Rule? Read(byte[] file)
        {
            var sections = PeImage.SectionsOf(file);
            var code = sections.FirstOrDefault(section => section.Name == PeImage.CodeName);
            if (code.Size <= 0)
            {
                return null;
            }

            float? FloatAt(long rva) => sections.FirstOrDefault(section => rva >= section.Start && rva + sizeof(float) <= section.Start + section.Size) is { Size: > 0 } section
                ? BitConverter.ToSingle(file, (int)(section.RawStart + rva - section.Start))
                : null;
            var linked = Floats(file.AsSpan((int)code.RawStart, (int)code.Size), code.Start, Parse(LinkedSignature), FloatAt);
            var cone = Floats(file.AsSpan((int)code.RawStart, (int)code.Size), code.Start, Parse(ConeSignature), FloatAt);
            if (linked == null || cone == null)
            {
                return null;
            }

            var rule = new Rule(linked[0], linked[1], linked[2], cone[0], cone[1]);
            return IsCosine(rule.LinkedCosine) && IsCosine(rule.ConeCosine) && IsFactor(rule.LinkedSlope) && IsFactor(rule.LinkedBase) && IsFactor(rule.ConeSlope) ? rule : null;
        }

        /// <summary>
        ///     The floats a signature's RIP-relative operands load, when the sequence hits at least once in the code and every
        ///     hit loads the same floats; <paramref name="floatAt"/> reads the file at an RVA (null outside its sections).
        /// </summary>
        public static float[]? Floats(ReadOnlySpan<byte> code, long codeRva, Signature signature, Func<long, float?> floatAt)
        {
            float[]? agreed = null;
            for (var start = 0; start + signature.Bytes.Length <= code.Length; start++)
            {
                if (!Matches(code, start, signature))
                {
                    continue;
                }

                var values = new float[signature.Operands.Count];
                for (var index = 0; index < values.Length; index++)
                {
                    var operand = signature.Operands[index];
                    var displacement = BitConverter.ToInt32(code[(start + operand)..]);
                    if (floatAt(codeRva + start + operand + DisplacementBytes + displacement) is not { } value)
                    {
                        return null;
                    }

                    values[index] = value;
                }

                if (agreed != null && !agreed.SequenceEqual(values))
                {
                    return null;
                }

                agreed = values;
            }

            return agreed;
        }

        private static bool Matches(ReadOnlySpan<byte> code, int start, Signature signature)
        {
            for (var index = 0; index < signature.Bytes.Length; index++)
            {
                if (signature.Fixed[index] && code[start + index] != signature.Bytes[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCosine(float value) => value > 0 && value < 1;

        private static bool IsFactor(float value) => value > 0 && value < MaxFactor;
    }
}
