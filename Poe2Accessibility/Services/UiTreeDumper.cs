namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Research tool (EXP-003/004): writes the current screen's UI tree to debug/ui-tree-*.txt so the
    ///     text / focus / hover offsets can be derived from real data and compared between screens.
    /// </summary>
    public sealed class UiTreeDumper
    {
        /// <summary>How far into a game-state object we look for pointers to its UI. InGameState's known
        ///     UiRoot pointer sits at 0x2F0 and its struct is 0x370 (GameOffsets InGameStateOffset); other
        ///     states are assumed no larger than this bound, which is generous compared to that.</summary>
        private const int StateObjectScanBytes = 0x2000;

        /// <summary>The known in-game chain hops through a UiRootStruct whose GameUi pointers sit at 0xBE0/0xBE8
        ///     (GameOffsets UiRootStruct); scan intermediate objects a bit beyond that.</summary>
        private const int IntermediateObjectScanBytes = 0x1000;

        /// <summary>Bytes per line in the diagnostic hex dump.</summary>
        private const int HexBytesPerLine = 16;

        /// <summary>Root candidates to expand per dump (more than this means the heuristic misfired).</summary>
        private const int MaxRootsPerDump = 4;

        private readonly UiTreeReader reader;
        private readonly string outputDirectory;
        private readonly PluginLog log;

        public UiTreeDumper(UiTreeReader reader, string outputDirectory, PluginLog log)
        {
            this.reader = reader;
            this.outputDirectory = outputDirectory;
            this.log = log;
        }

        /// <summary>Returns the written file path, or null when no UI root could be found (a diagnostic file is still written).</summary>
        public string? Dump(GameStateTypes state, IntPtr stateObject, string rootOverrideHex, bool visibleOnly, int maxDepth, int maxNodes)
        {
            Directory.CreateDirectory(this.outputDirectory);
            var roots = this.CollectRoots(state, stateObject, rootOverrideHex, out var rootReport);
            if (roots.Count == 0)
            {
                this.log.Warn($"No UI root found for {state} (state object 0x{stateObject.ToInt64():X})");
                this.WriteStateDiagnostic(state, stateObject, rootReport);
                return null;
            }

            var path = Path.Join(this.outputDirectory, $"ui-tree-{state}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"# UI tree dump — {DateTime.Now:O}");
            sb.AppendLine($"# GameHelper {Core.GetVersion()}, state {state}, state object 0x{stateObject.ToInt64():X}, controller mode {Core.GHSettings.EnableControllerMode}");
            sb.AppendLine($"# visibleOnly={visibleOnly} maxDepth={maxDepth} maxNodes={maxNodes}");
            sb.AppendLine("# line format: [child index path] address vis=<own flag> flags=<hex> kids=<n> name=\"<+0x98>\" font=\"<+0xC0>\" rect=(x,y w×h) str@<offset>=\"<probed std::wstring>\"");
            sb.AppendLine("# rect=(none) means the host could not place it: an invisible ancestor OR a zero unscaled size. Known flag bits: 0x0B visible, 0x0A modify-pos, 0x1B interactive widget, 0x10 text input (login dump 2026-09-14).");
            sb.AppendLine(rootReport);

            var budget = Math.Max(1, maxNodes);
            var visited = new HashSet<IntPtr>();
            var histogram = new Dictionary<int, StringOffsetStats>();
            foreach (var root in roots)
            {
                sb.AppendLine();
                sb.AppendLine($"## root 0x{root.ToInt64():X}");
                this.DumpSubtree(sb, root, "0", 0, visibleOnly, maxDepth, visited, histogram, ref budget);
                if (budget <= 0)
                {
                    sb.AppendLine("# node budget exhausted");
                    break;
                }
            }

            AppendHistogram(sb, histogram);
            this.AppendElementPointerSlots(sb, stateObject, roots);
            File.WriteAllText(path, sb.ToString());
            this.log.Info($"UI tree written to {path} ({visited.Count} nodes, {Math.Max(1, maxNodes) - budget} lines)");
            return path;
        }

        private List<IntPtr> CollectRoots(GameStateTypes state, IntPtr stateObject, string rootOverrideHex, out string report)
        {
            var roots = new List<IntPtr>();
            var sb = new StringBuilder();
            if (TryParseAddress(rootOverrideHex, out var manual))
            {
                sb.AppendLine($"# manual root override: 0x{manual.ToInt64():X}");
                roots.Add(manual);
            }

            // The InGameState object exists in every state; if its root chain resolves outside gameplay the
            // UI root is global and pre-game screens hang off it too.
            var inGameState = Core.States.InGameStateObject.Address;
            var known = inGameState == IntPtr.Zero
                ? IntPtr.Zero
                : this.reader.ReadInGameUiRoot(inGameState, Core.GHSettings.EnableControllerMode);
            sb.AppendLine($"# in-game root via InGameStateOffset chain (state object 0x{inGameState.ToInt64():X}): 0x{known.ToInt64():X}");
            if (known != IntPtr.Zero && !roots.Contains(known))
            {
                roots.Add(known);
            }

            var candidates = this.reader.FindUiRootCandidates(stateObject, StateObjectScanBytes, IntermediateObjectScanBytes);
            sb.AppendLine($"# elements reachable from the state object ({state}): {candidates.Count}");
            foreach (var c in candidates)
            {
                var hop = c.InnerOffset < 0 ? string.Empty : $" -> +0x{c.InnerOffset:X}";
                sb.AppendLine($"#   state+0x{c.Offset:X}{hop} = element 0x{c.Element.ToInt64():X}, root 0x{c.Root.ToInt64():X} ({c.Depth} hops up)");
                if (!roots.Contains(c.Root) && roots.Count < MaxRootsPerDump)
                {
                    roots.Add(c.Root);
                }
            }

            report = sb.ToString();
            return roots;
        }

        private void DumpSubtree(StringBuilder sb, IntPtr address, string path, int depth, bool visibleOnly, int maxDepth, HashSet<IntPtr> visited, Dictionary<int, StringOffsetStats> histogram, ref int budget)
        {
            if (budget <= 0 || depth > maxDepth)
            {
                return;
            }

            if (!visited.Add(address))
            {
                budget--;
                sb.Append(' ', depth * 2).AppendLine($"[{path}] 0x{address.ToInt64():X} (already dumped — cycle or shared node)");
                return;
            }

            if (!this.reader.TryReadNode(address, out var node) || (visibleOnly && !node.IsVisibleFlag))
            {
                visited.Remove(address);
                return;
            }

            budget--;
            sb.Append(' ', depth * 2).Append(FormatNode(node, path));
            foreach (var (offset, text) in node.StringCandidates)
            {
                sb.Append($" str@0x{offset:X}=\"{Escape(text)}\"");
                if (!histogram.TryGetValue(offset, out var stats))
                {
                    stats = new StringOffsetStats();
                    histogram[offset] = stats;
                }

                stats.Record(text);
            }

            sb.AppendLine();
            for (var i = 0; i < node.Children.Length; i++)
            {
                this.DumpSubtree(sb, node.Children[i], $"{path},{i}", depth + 1, visibleOnly, maxDepth, visited, histogram, ref budget);
            }
        }

        /// <summary>A real field sits at a fixed offset on many elements; a one-off hit at a unique offset is noise.</summary>
        private static void AppendHistogram(StringBuilder sb, Dictionary<int, StringOffsetStats> histogram)
        {
            sb.AppendLine();
            sb.AppendLine("## string offset histogram (offset: nodes, distinct texts, samples)");
            var offsets = new List<int>(histogram.Keys);
            offsets.Sort();
            foreach (var offset in offsets)
            {
                var stats = histogram[offset];
                sb.AppendLine($"0x{offset:X}: {stats.Count} nodes, {stats.Distinct.Count} distinct, e.g. {string.Join(" | ", stats.Samples.ConvertAll(Escape))}");
            }
        }

        private sealed class StringOffsetStats
        {
            private const int MaxSamples = 5;

            public int Count { get; private set; }

            public HashSet<string> Distinct { get; } = new();

            public List<string> Samples { get; } = new();

            public void Record(string text)
            {
                this.Count++;
                if (this.Distinct.Add(text) && this.Samples.Count < MaxSamples)
                {
                    this.Samples.Add(text);
                }
            }
        }

        private static string FormatNode(UiNode node, string path)
        {
            var rect = node.HasRect
                ? $"({node.Position.X:F0},{node.Position.Y:F0} {node.Size.X:F0}x{node.Size.Y:F0})"
                : "(none)";
            var font = node.Font.Length == 0 ? string.Empty : $" font=\"{Escape(node.Font)}\"";
            return $"[{path}] 0x{node.Address.ToInt64():X} vis={(node.IsVisibleFlag ? 'V' : '-')} flags=0x{node.Flags:X} kids={node.Children.Length} name=\"{Escape(node.Name)}\"{font} rect={rect}";
        }

        /// <summary>Element-pointer slots of the objects most likely to hold hover/focus state (diff between dumps).</summary>
        private void AppendElementPointerSlots(StringBuilder sb, IntPtr stateObject, IEnumerable<IntPtr> roots)
        {
            sb.AppendLine();
            sb.AppendLine("## element pointer slots (owner+offset = element name) — compare between dumps with different focus");
            this.AppendSlotsOf(sb, "state", stateObject, StateObjectScanBytes);
            foreach (var (offset, pointer) in this.reader.ReadPointerSlots(stateObject, IntermediateObjectScanBytes))
            {
                if (this.reader.FindElementPointerSlots(pointer, IntermediateObjectScanBytes).Count > 0)
                {
                    this.AppendSlotsOf(sb, $"state+0x{offset:X}->", pointer, IntermediateObjectScanBytes);
                }
            }

            foreach (var root in roots)
            {
                this.AppendSlotsOf(sb, $"root 0x{root.ToInt64():X}", root, IntermediateObjectScanBytes);
            }
        }

        private void AppendSlotsOf(StringBuilder sb, string owner, IntPtr obj, int bytes)
        {
            foreach (var (offset, element, name) in this.reader.FindElementPointerSlots(obj, bytes))
            {
                sb.AppendLine($"{owner}+0x{offset:X} = 0x{element.ToInt64():X} \"{Escape(name)}\"");
            }
        }

        /// <summary>
        ///     When no root is found, keep the evidence: a hex dump of the state object plus every slot that
        ///     points at readable memory (with the first bytes of the target), for offline analysis.
        /// </summary>
        private void WriteStateDiagnostic(GameStateTypes state, IntPtr stateObject, string rootReport)
        {
            var path = Path.Join(this.outputDirectory, $"state-object-{state}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"# State object diagnostic — {DateTime.Now:O}");
            sb.AppendLine($"# GameHelper {Core.GetVersion()}, state {state}, state object 0x{stateObject.ToInt64():X}");
            sb.AppendLine(rootReport);

            var (bytes, unreadable) = this.reader.ReadObjectBytes(stateObject, StateObjectScanBytes);
            sb.AppendLine($"# unreadable chunks: {unreadable.Count} (offsets: {string.Join(", ", unreadable.ConvertAll(o => $"0x{o:X}"))})");
            sb.AppendLine();
            sb.AppendLine("## pointer slots that target readable memory (offset = value : first 32 bytes of target)");
            foreach (var (offset, pointer) in this.reader.ReadPointerSlots(stateObject, StateObjectScanBytes))
            {
                var (head, missing) = this.reader.ReadObjectBytes(pointer, 2 * HexBytesPerLine);
                if (missing.Count > 0)
                {
                    continue;
                }

                sb.AppendLine($"+0x{offset:X4} = 0x{pointer.ToInt64():X} : {Convert.ToHexString(head)}");
            }

            sb.AppendLine();
            sb.AppendLine("## hex dump of the state object");
            for (var line = 0; line < bytes.Length; line += HexBytesPerLine)
            {
                var count = Math.Min(HexBytesPerLine, bytes.Length - line);
                sb.AppendLine($"+0x{line:X4}: {Convert.ToHexString(bytes, line, count)}");
            }

            File.WriteAllText(path, sb.ToString());
            this.log.Info($"State object diagnostic written to {path}");
        }

        private static bool TryParseAddress(string hex, out IntPtr address)
        {
            address = IntPtr.Zero;
            var trimmed = hex.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[2..];
            }

            if (trimmed.Length == 0 || !long.TryParse(trimmed, NumberStyles.HexNumber, null, out var value))
            {
                return false;
            }

            address = (IntPtr)value;
            return true;
        }

        private static string Escape(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\"", "\\\"");
    }
}
