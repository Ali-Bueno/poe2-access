// <copyright file="CodeFieldSearch.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>One instruction sequence of the game's code, with the field's offset as its wildcarded operand.</summary>
    /// <param name="Name">the field, as struct.field.</param>
    /// <param name="Bytes">the sequence, zero where <paramref name="Fixed"/> is false.</param>
    /// <param name="Fixed">the bytes that must match.</param>
    /// <param name="Operand">where the offset lies in the sequence.</param>
    /// <param name="Size">the operand's bytes: 1, 2 or 4.</param>
    public sealed record CodeField(string Name, byte[] Bytes, bool[] Fixed, int Operand, int Size);

    /// <summary>
    ///     Reads a struct's offset out of the game's code: a compiled <c>mov eax,[rcx+0x8B0]</c> holds it. Signatures that kept to
    ///     one hit across five builds, from September to November, read 0.5.5f's move of AreaInstance with no measuring
    ///     (docs/research/exe-layouts.md). Pure.
    /// </summary>
    public static class CodeFieldSearch
    {
        private const string OperandMark = "^";
        private const string AnyByte = "??";

        /// <summary>Signatures that must agree: a single hit can be another function's lookalike.</summary>
        private const int MinAgreeing = 2;

        /// <summary>A pattern as "48 8B 88 ^ ?? ?? ?? ?? 48 8B 11", the mark before the operand.</summary>
        public static CodeField Parse(string name, string pattern, int size = sizeof(int))
        {
            var tokens = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var operand = Array.IndexOf(tokens, OperandMark);
            var bytes = tokens.Where(token => token != OperandMark).ToArray();
            return new CodeField(
                name,
                bytes.Select(token => token == AnyByte ? (byte)0 : byte.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray(),
                bytes.Select(token => token != AnyByte).ToArray(),
                operand,
                size);
        }

        /// <summary>The value at least two of the sequences read, when none that hit says otherwise; null otherwise.</summary>
        public static int? Agreed(ReadOnlySpan<byte> code, IReadOnlyList<CodeField> fields)
        {
            int? value = null;
            var agreeing = 0;
            foreach (var field in fields)
            {
                if (Read(code, field) is { } read)
                {
                    if (value is { } known && known != read)
                    {
                        return null;
                    }

                    value = read;
                    agreeing++;
                }
            }

            return agreeing >= MinAgreeing ? value : null;
        }

        /// <summary>The offset the code holds, when the sequence appears exactly once; null otherwise, as a second hit could be anything.</summary>
        public static int? Read(ReadOnlySpan<byte> code, CodeField field)
        {
            var (anchorAt, anchorLength) = LongestFixedRun(field.Fixed);
            var anchor = field.Bytes.AsSpan(anchorAt, anchorLength);
            var hit = -1;
            var from = 0;
            while (from < code.Length)
            {
                var found = code[from..].IndexOf(anchor);
                if (found < 0)
                {
                    break;
                }

                var start = from + found - anchorAt;
                from += found + 1;
                if (start >= 0 && start + field.Bytes.Length <= code.Length && Matches(code, start, field))
                {
                    if (hit >= 0)
                    {
                        return null;
                    }

                    hit = start;
                }
            }

            return hit < 0 ? null : Operand(code, hit + field.Operand, field.Size);
        }

        /// <summary>The code section of a PE file, as raw offset and length; empty when the file is not one.</summary>
        public static (int Start, int Length) CodeOf(ReadOnlySpan<byte> file)
        {
            foreach (var section in CodeReader.PeImage.SectionsOf(file))
            {
                if (section.Name == CodeReader.PeImage.CodeName)
                {
                    return section.Size > 0 && section.RawStart + section.Size <= file.Length ? ((int)section.RawStart, (int)section.Size) : default;
                }
            }

            return default;
        }

        private static bool Matches(ReadOnlySpan<byte> code, int start, CodeField field)
        {
            for (var index = 0; index < field.Bytes.Length; index++)
            {
                if (field.Fixed[index] && code[start + index] != field.Bytes[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static int Operand(ReadOnlySpan<byte> code, int at, int size) => size switch
        {
            sizeof(byte) => code[at],
            sizeof(short) => BitConverter.ToInt16(code[at..]),
            _ => BitConverter.ToInt32(code[at..]),
        };

        private static (int At, int Length) LongestFixedRun(IReadOnlyList<bool> mask)
        {
            var (bestAt, bestLength, at) = (0, 0, -1);
            for (var index = 0; index <= mask.Count; index++)
            {
                if (index < mask.Count && mask[index])
                {
                    at = at < 0 ? index : at;
                }
                else if (at >= 0)
                {
                    (bestAt, bestLength) = index - at > bestLength ? (at, index - at) : (bestAt, bestLength);
                    at = -1;
                }
            }

            return (bestAt, bestLength);
        }
    }
}
