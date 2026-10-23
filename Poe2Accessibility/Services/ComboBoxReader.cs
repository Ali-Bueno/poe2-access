namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    ///     Reads the selected entry of a combo box. The entry vector and the selected index are known
    ///     (GameLayout.ComboBox); what the 16-byte entries contain is not, so the first read tries every
    ///     plausible layout live, keeps the one that yields text, and records the evidence in the log.
    /// </summary>
    public sealed class ComboBoxReader
    {
        /// <summary>Longest dropdown entry we expect when reading a raw UTF-16 buffer.</summary>
        private const int MaxEntryChars = 128;

        private readonly UiElementReader elements;
        private readonly PluginLog log;
        private readonly HashSet<IntPtr> reported = new();
        private Strategy? working;

        public ComboBoxReader(UiElementReader elements, PluginLog log)
        {
            this.elements = elements;
            this.log = log;
        }

        private enum Strategy
        {
            /// <summary>Entry = pointer (qword 0) to a std::wstring struct.</summary>
            PointerToWString,

            /// <summary>Entry = pointer (qword 0) to a NUL-terminated UTF-16 buffer.</summary>
            PointerToUtf16,

            /// <summary>Entry = pointer (qword 0) to a UI element (dropdown label) whose text is readable.</summary>
            PointerToElement,

            /// <summary>Same three shapes but from qword 1 of the entry.</summary>
            SecondQwordToWString,
            SecondQwordToUtf16,
            SecondQwordToElement,

            /// <summary>Same shapes but on the parallel vector's entries.</summary>
            ParallelToWString,
            ParallelToUtf16,
            ParallelToElement,
        }

        public readonly record struct ComboValue(string Selected, string Highlighted, int SelectedIndex, int HighlightIndex, int Count)
        {
            public bool IsOpenOnOtherEntry => this.HighlightIndex != this.SelectedIndex;
        }

        /// <summary>Selected (committed) and highlighted entry texts; empty texts when the layout is still unknown.</summary>
        public ComboValue Read(IntPtr comboBox)
        {
            var state = this.elements.ReadComboBoxState(comboBox);
            if (state == null)
            {
                return new ComboValue(string.Empty, string.Empty, -1, -1, 0);
            }

            var (index, highlight, entries, parallel) = state.Value;
            var strategy = this.working ?? this.Resolve(comboBox, entries, parallel, index);
            if (strategy == null)
            {
                return new ComboValue(string.Empty, string.Empty, index, highlight, entries.Length);
            }

            var selected = this.Decode(strategy.Value, entries, parallel, index) ?? string.Empty;
            var highlighted = highlight == index ? selected : this.Decode(strategy.Value, entries, parallel, highlight) ?? string.Empty;
            return new ComboValue(selected, highlighted, index, highlight, entries.Length);
        }

        private Strategy? Resolve(IntPtr comboBox, byte[][] entries, byte[][] parallel, int index)
        {
            foreach (Strategy strategy in Enum.GetValues<Strategy>())
            {
                var text = this.Decode(strategy, entries, parallel, index);
                if (text != null)
                {
                    this.working = strategy;
                    this.log.Info($"Combo entry layout resolved: {strategy} (combo 0x{comboBox.ToInt64():X}, {entries.Length} entries, selected {index} = \"{text}\")");
                    return strategy;
                }
            }

            if (this.reported.Add(comboBox))
            {
                this.log.Warn($"Combo 0x{comboBox.ToInt64():X}: {entries.Length} entries, selected {index}; entry[{index}] = {Convert.ToHexString(entries[index])}, parallel = {(parallel.Length > index ? Convert.ToHexString(parallel[index]) : "n/a")} — no strategy decoded it");
            }

            return null;
        }

        private string? Decode(Strategy strategy, byte[][] entries, byte[][] parallel, int index)
        {
            var source = strategy >= Strategy.ParallelToWString ? parallel : entries;
            if (index >= source.Length)
            {
                return null;
            }

            var qwordIndex = strategy is Strategy.SecondQwordToWString or Strategy.SecondQwordToUtf16 or Strategy.SecondQwordToElement ? 1 : 0;
            var pointer = (IntPtr)MemoryMarshal.Read<long>(source[index].AsSpan(qwordIndex * 8));
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            string? text = strategy switch
            {
                Strategy.PointerToWString or Strategy.SecondQwordToWString or Strategy.ParallelToWString => this.elements.ReadWStringStructAt(pointer),
                Strategy.PointerToUtf16 or Strategy.SecondQwordToUtf16 or Strategy.ParallelToUtf16 => this.elements.ReadUtf16BufferAt(pointer, MaxEntryChars),
                _ => this.elements.IsElement(pointer) && this.elements.TryReadText(pointer, out var t) ? t : null,
            };

            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
