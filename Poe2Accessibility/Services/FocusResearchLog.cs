namespace Poe2Accessibility.Services
{
    using System;
    using System.IO;
    using System.Text;
    using GameHelper.RemoteEnums;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Research aid that needs no hotkey: every time the focus lands on an element, its surroundings are
    ///     appended to debug/focus-research.log (ancestors, siblings, children with names and texts), so one
    ///     ordinary play session documents every widget the user visited.
    /// </summary>
    public sealed class FocusResearchLog
    {
        private const int AncestorHops = 6;
        private const int SubtreeDepth = 3;
        private const int MaxLinesPerSubtree = 60;

        private readonly UiElementReader elements;
        private readonly string path;
        private readonly PluginLog log;

        public FocusResearchLog(UiElementReader elements, string outputDirectory, PluginLog log)
        {
            this.elements = elements;
            this.path = Path.Join(outputDirectory, "focus-research.log");
            this.log = log;
        }

        /// <summary>One-line observation appended to the research log (byte watch, probes).</summary>
        public void Note(string line)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(this.path) ?? string.Empty);
                File.AppendAllText(this.path, $"--- {DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                this.log.Error("Focus research note failed", ex);
            }
        }

        public void Record(GameStateTypes state, AccessibleUiNode node)
        {
            try
            {
                if (!this.elements.TryReadElement(node.Address, out var info))
                {
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"=== {DateTime.Now:HH:mm:ss.fff} {state} focus 0x{node.Address.ToInt64():X} name=\"{node.Name}\" role={node.Role} text=\"{node.Text}\" value=\"{node.Value}\" flags=0x{info.Raw.Flags:X}");
                var ancestors = this.elements.ReadAncestors(info, AncestorHops);
                sb.Append("ancestors (nearest first): ");
                foreach (var a in ancestors)
                {
                    sb.Append($"0x{a.Address.ToInt64():X}\"{a.Name}\"({this.elements.ReadChildren(a).Length}) ");
                }

                sb.AppendLine();
                var container = ancestors.Count >= 2 ? ancestors[1] : ancestors.Count == 1 ? ancestors[0] : info;
                sb.AppendLine($"subtree of 0x{container.Address.ToInt64():X} \"{container.Name}\" (2 levels up):");
                var lines = 0;
                this.AppendSubtree(sb, container, 0, node.Address, ref lines);
                this.AppendDeepProbe(sb, node.Address);
                Directory.CreateDirectory(Path.GetDirectoryName(this.path) ?? string.Empty);
                File.AppendAllText(this.path, sb.ToString());
            }
            catch (Exception ex)
            {
                this.log.Error("Focus research log failed", ex);
            }
        }

        /// <summary>Bytes of the focused object to inspect for value storage (combo item lists, selected index, slider value).</summary>
        private const int DeepProbeBytes = 0x1000;

        /// <summary>A combo box has a handful of entries; anything larger is not an item list.</summary>
        private const int MaxVectorItems = 64;

        private static readonly int BaseStructSize = System.Runtime.CompilerServices.Unsafe.SizeOf<GameOffsets.Objects.UiElement.UiElementBaseOffset>();

        /// <summary>
        ///     Research for value-bearing widgets: std::vector&lt;std::wstring&gt; candidates (item lists) and a hex
        ///     dump past the base struct, so two records of the same control with different values can be diffed
        ///     to find where the selected index / value lives.
        /// </summary>
        private void AppendDeepProbe(StringBuilder sb, IntPtr address)
        {
            var block = this.elements.ReadBlock(address, DeepProbeBytes);
            var vectorSize = System.Runtime.CompilerServices.Unsafe.SizeOf<GameOffsets.Natives.StdVector>();
            for (var offset = 0; offset + vectorSize <= block.Length; offset += IntPtr.Size)
            {
                var vector = System.Runtime.InteropServices.MemoryMarshal.Read<GameOffsets.Natives.StdVector>(block.AsSpan(offset));
                var bytes = vector.Last.ToInt64() - vector.First.ToInt64();
                if (bytes <= 0 || bytes % WStringDecoder.Size != 0 || bytes / WStringDecoder.Size > MaxVectorItems ||
                    vector.End.ToInt64() < vector.Last.ToInt64() || !this.elements.IsReadable(vector.First))
                {
                    continue;
                }

                var items = this.elements.ReadStringVector(vector);
                if (items.Count > 0)
                {
                    sb.AppendLine($"vector<wstring>@0x{offset:X}: [{string.Join(" | ", items)}]");
                }
            }

            sb.AppendLine($"hex 0x{BaseStructSize:X}..0x{block.Length:X}:");
            for (var line = BaseStructSize; line < block.Length; line += 32)
            {
                var count = Math.Min(32, block.Length - line);
                sb.AppendLine($"  +0x{line:X4} {Convert.ToHexString(block, line, count)}");
            }
        }

        private void AppendSubtree(StringBuilder sb, in UiElementReader.ElementInfo info, int depth, IntPtr focused, ref int lines)
        {
            if (lines++ >= MaxLinesPerSubtree || depth > SubtreeDepth)
            {
                return;
            }

            var text = this.elements.TryReadText(info.Address, out var t) ? t : string.Empty;
            var marker = info.Address == focused ? " <== FOCUS" : string.Empty;
            sb.Append(' ', depth * 2)
              .AppendLine($"0x{info.Address.ToInt64():X} vis={(info.IsVisibleFlag ? 'V' : '-')} flags=0x{info.Raw.Flags:X} name=\"{info.Name}\" text=\"{text}\"{marker}");
            foreach (var childAddress in this.elements.ReadChildren(info))
            {
                if (this.elements.TryReadElement(childAddress, out var child))
                {
                    this.AppendSubtree(sb, child, depth + 1, focused, ref lines);
                }
            }
        }
    }
}
