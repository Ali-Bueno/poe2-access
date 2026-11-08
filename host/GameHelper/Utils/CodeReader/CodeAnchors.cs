// <copyright file="CodeAnchors.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils.CodeReader
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    ///     What the exe names: its strings in <c>.rdata</c>, the component getters found from a component's name, and the GEAL
    ///     script-function table (<c>{L"Name", factory}</c> rows) with the evaluator each factory builds.
    /// </summary>
    public sealed class CodeAnchors
    {
        /// <summary>A GEAL function's name, to find the table by: its row points at this string.</summary>
        private const string GealTableAnchor = "IsWalkable";

        /// <summary>The getter wrappers are 116 bytes; a fragment this small holding the name is one.</summary>
        private const int MaxGetterBytes = 0xA0;

        private const int GealRowSize = 16;
        private const int MaxGealNameBytes = 160;
        private const char FirstPrintable = ' ';
        private const char LastPrintable = '~';

        /// <summary>The evaluator a GEAL node runs is its vtable's slot 2.</summary>
        private const int EvaluatorSlot = 2 * sizeof(long);

        /// <summary>An evaluator shorter than this is a thunk to the real one.</summary>
        private const int ThunkBytes = 40;

        private readonly Dictionary<string, List<long>> wide = new();
        private readonly Dictionary<string, long?> getters = new();
        private Dictionary<string, long>? geal;

        public CodeAnchors(CodeIndex index) => this.Index = index;

        /// <summary>Gets the code index.</summary>
        public CodeIndex Index { get; }

        /// <summary>Gets the image.</summary>
        public PeImage Image => this.Index.Image;

        /// <summary>Gets the GEAL table: name to factory.</summary>
        public IReadOnlyDictionary<string, long> Geal => this.geal ??= this.ReadGeal();

        /// <summary>The RVAs of a whole NUL-terminated UTF-16 string in <c>.rdata</c>.</summary>
        public IReadOnlyList<long> WideStrings(string text)
        {
            if (!this.wide.TryGetValue(text, out var found))
            {
                found = this.Find(Encoding.Unicode.GetBytes(text + '\0'), needle => needle, (constants, at) =>
                    at % sizeof(char) == 0 && at >= sizeof(char) && constants[(int)at - 1] == 0 && constants[(int)at - 2] == 0);
                this.wide[text] = found;
            }

            return found;
        }

        /// <summary>The RVAs of a whole NUL-terminated ASCII string in <c>.rdata</c>.</summary>
        public IReadOnlyList<long> AsciiStrings(string text) =>
            this.Find(Encoding.ASCII.GetBytes('\0' + text + '\0'), at => at + 1, (_, _) => true);

        /// <summary>The starts of the NUL-terminated ASCII strings in <c>.rdata</c> that contain <paramref name="text"/>.</summary>
        public IReadOnlyList<long> AsciiContaining(string text)
        {
            var constants = this.Image.Bytes(this.Image.Constants.Start, this.Image.Constants.Size);
            var starts = new SortedSet<long>();
            foreach (var at in this.Find(Encoding.ASCII.GetBytes(text), found => found - this.Image.Constants.Start, (_, _) => true))
            {
                var start = (int)at;
                while (start > 0 && constants[start - 1] != 0)
                {
                    start--;
                }

                starts.Add(this.Image.Constants.Start + start);
            }

            return new List<long>(starts);
        }

        /// <summary>
        ///     <c>GetComponent&lt;T&gt;</c> for a component: of the small functions loading its name, the one with the most callers.
        /// </summary>
        public long? Getter(string component)
        {
            if (this.getters.TryGetValue(component, out var known))
            {
                return known;
            }

            var (best, callers) = ((long?)null, 0);
            foreach (var name in this.AsciiStrings(component))
            {
                foreach (var site in this.Index.LeaSites(name))
                {
                    if (this.Index.Fragment(site) is { } fragment && fragment.End - fragment.Start <= MaxGetterBytes
                        && this.Index.Callers(fragment.Start).Length > callers)
                    {
                        (best, callers) = (fragment.Start, this.Index.Callers(fragment.Start).Length);
                    }
                }
            }

            this.getters[component] = best;
            return best;
        }

        /// <summary>The code a GEAL function runs: its factory's node, that node's evaluator, through a thunk; null when it is not there.</summary>
        public long? Evaluator(string name)
        {
            if (!this.Geal.TryGetValue(name, out var factory) || this.Index.Body(factory) is not { } body)
            {
                return null;
            }

            long? node = null;
            foreach (var instruction in body)
            {
                var vtable = instruction.Kind == InstructionKind.Lea ? instruction.RipTarget : -1;
                if (this.Image.Constants.Holds(vtable) && this.Image.IsCode(this.Image.Pointer(vtable)))
                {
                    node = this.Image.Pointer(vtable + EvaluatorSlot);
                    break;
                }
            }

            if (node is not { } evaluator || this.Index.Body(evaluator) is not { } code)
            {
                return null;
            }

            var bytes = 0;
            foreach (var instruction in code)
            {
                bytes += instruction.Length;
            }

            if (bytes < ThunkBytes)
            {
                foreach (var instruction in code)
                {
                    if (instruction.Kind is InstructionKind.Call or InstructionKind.Jmp && instruction.DirectTarget >= 0)
                    {
                        return instruction.DirectTarget;
                    }
                }
            }

            return evaluator;
        }

        private List<long> Find(byte[] needle, Func<long, long> result, Func<ReadOnlySpan<byte>, long, bool> accept)
        {
            var constants = this.Image.Bytes(this.Image.Constants.Start, this.Image.Constants.Size);
            var found = new List<long>();
            for (var at = constants.IndexOf(needle); at >= 0;)
            {
                if (accept(constants, at))
                {
                    found.Add(result(this.Image.Constants.Start + at));
                }

                var next = constants[(at + 1)..].IndexOf(needle);
                at = next < 0 ? -1 : at + 1 + next;
            }

            return found;
        }

        private Dictionary<string, long> ReadGeal()
        {
            var table = new Dictionary<string, long>();
            if (this.WideStrings(GealTableAnchor) is not [var anchor, ..])
            {
                return table;
            }

            var data = this.Image.Bytes(this.Image.Data.Start, this.Image.Data.Size);
            var pointer = BitConverter.GetBytes(anchor + this.Image.ImageBase);
            var row = -1L;
            for (var at = data.IndexOf(pointer); at >= 0;)
            {
                if (at % sizeof(long) == 0)
                {
                    row = this.Image.Data.Start + at;
                    break;
                }

                var next = data[(at + 1)..].IndexOf(pointer);
                at = next < 0 ? -1 : at + 1 + next;
            }

            if (row < 0)
            {
                return table;
            }

            while (this.GealRow(row - GealRowSize) is not null)
            {
                row -= GealRowSize;
            }

            while (this.GealRow(row) is { } entry)
            {
                table[entry.Name] = entry.Factory;
                row += GealRowSize;
            }

            return table;
        }

        private (string Name, long Factory)? GealRow(long row)
        {
            var (text, factory) = (this.Image.Pointer(row), this.Image.Pointer(row + sizeof(long)));
            if (!this.Image.Constants.Holds(text) || !this.Image.IsCode(factory))
            {
                return null;
            }

            var bytes = this.Image.Bytes(text, MaxGealNameBytes);
            var name = new StringBuilder();
            for (var at = 0; at + 1 < bytes.Length && (bytes[at] | bytes[at + 1]) != 0; at += sizeof(char))
            {
                var letter = (char)(bytes[at] | (bytes[at + 1] << 8));
                if (letter is < FirstPrintable or > LastPrintable)
                {
                    return null;
                }

                name.Append(letter);
            }

            return name.Length > 0 ? (name.ToString(), factory) : null;
        }
    }
}
