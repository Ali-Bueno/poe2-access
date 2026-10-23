namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using GameHelper.Plugin;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States;
    using GameOffsets.Objects.UiElement;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Reads the game's UI element tree using GameHelper2's public offset structs
    ///     (GameOffsets.Objects.UiElement.UiElementBaseOffset) and runtime validation instead of guessed numbers.
    ///     Each element is fetched as ONE memory block and parsed locally, so probing costs no extra reads.
    /// </summary>
    public sealed class UiTreeReader
    {
        /// <summary>No real UI container has more children than this; larger counts mean a bad pointer.</summary>
        private const int MaxChildrenPerNode = 4096;

        /// <summary>Longest label we consider plausible when probing for text fields (host reader caps at 1000).</summary>
        private const int MaxProbeStringLength = 512;

        /// <summary>Heap-backed strings cost one remote read each; bound the per-element research cost.</summary>
        private const int MaxHeapProbesPerNode = 16;

        /// <summary>
        ///     How many bytes of an element we probe for strings. The GameHelper2 struct ends at 0x290 and
        ///     the first live dump (login screen) showed only name/font strings inside it, so the label text
        ///     must sit past it; 0x800 comfortably covers subclass fields without touching the next object
        ///     often (missing chunks are tolerated).
        /// </summary>
        private const int ElementProbeBytes = 0x800;

        /// <summary>
        ///     Byte ranges already identified in UiElementBaseOffset (vtable/self/children vector, parent,
        ///     position, modifier, scale, StringId, flags, size, colour) and by the first live dump (name at
        ///     0x98, font at 0xC0); a std::wstring can never start inside them.
        /// </summary>
        private static readonly (int Start, int End)[] KnownFieldRanges =
        {
            (0x000, 0x028), (0x098, 0x0E0), (0x100, 0x110), (0x118, 0x120), (0x128, 0x148),
            (0x168, 0x16C), (0x172, 0x173), (0x270, 0x278), (0x28C, 0x29C),
        };

        /// <summary>Control name field (std::wstring), validated on the login dump 2026-09-14 (888 nodes, snake_case layout names).</summary>
        private const int NameOffset = 0x98;

        /// <summary>Font face field (std::wstring), validated on the same dump (Fontin / Fontin Smallcaps / OptimusPrincepsSemiBold).</summary>
        private const int FontOffset = 0xC0;

        /// <summary>Host reads are all-or-nothing, so wide scans go page-chunk by page-chunk to survive allocation ends.</summary>
        private const int ScanChunkBytes = 0x100;

        /// <summary>The UI tree is nowhere near this deep; a longer parent chain means a corrupt pointer loop.</summary>
        private const int MaxParentChainDepth = 64;

        private static readonly int ElementBlockSize = Unsafe.SizeOf<UiElementBaseOffset>();

        /// <summary>Raw bytes of any object, chunked (for diagnostic hex dumps); missing chunks are zero-filled and reported.</summary>
        public (byte[] Bytes, List<int> UnreadableChunks) ReadObjectBytes(IntPtr address, int bytes)
        {
            var result = new byte[bytes];
            var unreadable = new List<int>();
            for (var chunk = 0; chunk < bytes; chunk += ScanChunkBytes)
            {
                var part = this.reader.ReadBytes(address + chunk, Math.Min(ScanChunkBytes, bytes - chunk));
                if (part.Length == 0)
                {
                    unreadable.Add(chunk);
                    continue;
                }

                Array.Copy(part, 0, result, chunk, part.Length);
            }

            return (result, unreadable);
        }

        private readonly HostMemoryReader reader;

        public UiTreeReader(HostMemoryReader reader)
        {
            this.reader = reader;
        }

        /// <summary>
        ///     Reads and validates one element. The host accepts a Self pointer that is either zero or the
        ///     element's own address (UiElementBase.UpdateData); anything else is not an element.
        /// </summary>
        public bool TryReadNode(IntPtr address, out UiNode node)
        {
            node = null!;
            var (block, missing) = this.ReadObjectBytes(address, ElementProbeBytes);
            if (missing.Exists(chunk => chunk < ElementBlockSize))
            {
                return false;
            }

            var raw = MemoryMarshal.Read<UiElementBaseOffset>(block);
            if (raw.Self != IntPtr.Zero && raw.Self != address)
            {
                return false;
            }

            node = new UiNode(address, raw)
            {
                Children = this.reader.ReadStdVector<IntPtr>(raw.ChildrensPtr, MaxChildrenPerNode),
                StringId = this.reader.ReadStdWString(raw.StringIdPtr),
                Name = this.ReadWStringAt(block, NameOffset) ?? string.Empty,
                Font = this.ReadWStringAt(block, FontOffset) ?? string.Empty,
                StringCandidates = this.FindStringCandidates(block),
            };

            if (PluginUiElementReflection.TryGetAbsoluteRect(address, out var position, out var size))
            {
                node.HasRect = true;
                node.Position = position;
                node.Size = size;
            }

            return true;
        }

        /// <summary>
        ///     The in-game UI root through the chain GameHelper2 already validated
        ///     (InGameState.UiRootStructPtr → UiRootStruct.UiRootPtr, controller variant when enabled).
        /// </summary>
        public IntPtr ReadInGameUiRoot(IntPtr inGameStateAddress, bool controllerMode)
        {
            if (!this.reader.TryRead<InGameStateOffset>(inGameStateAddress, out var state))
            {
                return IntPtr.Zero;
            }

            var structPtr = controllerMode ? state.GamepadUiRootStructPtr : state.UiRootStructPtr;
            return this.reader.TryRead<UiRootStruct>(structPtr, out var root) ? root.UiRootPtr : IntPtr.Zero;
        }

        /// <summary>One UI element reachable from a state object, and the root its parent chain leads to.</summary>
        public readonly record struct RootCandidate(int Offset, int InnerOffset, IntPtr Element, IntPtr Root, int Depth);

        /// <summary>
        ///     Finds UI roots reachable from a game-state object without knowing its layout: every pointer
        ///     slot (and every pointer slot of what it points to) that lands on a valid element (self
        ///     pointer, sane children vector) is followed up its parent chain to the parentless root.
        ///     Used for the pre-game states (login, character select) whose layouts nobody has published.
        /// </summary>
        public List<RootCandidate> FindUiRootCandidates(IntPtr stateObject, int scanBytes, int innerScanBytes)
        {
            var found = new List<RootCandidate>();
            var checkedPointers = new HashSet<IntPtr>();
            foreach (var (offset, candidate) in this.ReadPointerSlots(stateObject, scanBytes))
            {
                if (!checkedPointers.Add(candidate))
                {
                    continue;
                }

                if (this.TryClimbToRoot(candidate, out var root, out var depth))
                {
                    found.Add(new RootCandidate(offset, -1, candidate, root, depth));
                    continue;
                }

                foreach (var (innerOffset, inner) in this.ReadPointerSlots(candidate, innerScanBytes))
                {
                    if (checkedPointers.Add(inner) && this.TryClimbToRoot(inner, out var innerRoot, out var innerDepth))
                    {
                        found.Add(new RootCandidate(offset, innerOffset, inner, innerRoot, innerDepth));
                    }
                }
            }

            return found;
        }

        /// <summary>Follows ParentPtr up from a valid element until an element without parent; false if any hop is not an element.</summary>
        public bool TryClimbToRoot(IntPtr element, out IntPtr root, out int depth)
        {
            root = IntPtr.Zero;
            depth = 0;
            var current = element;
            for (; depth < MaxParentChainDepth; depth++)
            {
                if (!this.IsElementLike(current, out var raw))
                {
                    return false;
                }

                if (raw.ParentPtr == IntPtr.Zero)
                {
                    root = current;
                    return true;
                }

                current = raw.ParentPtr;
            }

            return false;
        }

        /// <summary>Strict element test for discovery: the game always writes the self pointer, and a children vector must be well formed.</summary>
        private bool IsElementLike(IntPtr address, out UiElementBaseOffset raw)
        {
            if (!this.reader.TryRead<UiElementBaseOffset>(address, out raw) || raw.Self != address)
            {
                return false;
            }

            var span = raw.ChildrensPtr.Last.ToInt64() - raw.ChildrensPtr.First.ToInt64();
            return span >= 0 && span % IntPtr.Size == 0 && span / IntPtr.Size <= MaxChildrenPerNode &&
                   (span == 0 || this.reader.IsValidAddress(raw.ChildrensPtr.First));
        }

        /// <summary>Valid pointer values found in an object, chunked so a partially unreadable range still yields what it can.</summary>
        public IEnumerable<(int Offset, IntPtr Pointer)> ReadPointerSlots(IntPtr address, int bytes)
        {
            if (!this.reader.IsValidAddress(address))
            {
                yield break;
            }

            for (var chunk = 0; chunk < bytes; chunk += ScanChunkBytes)
            {
                var slots = this.reader.ReadArray<IntPtr>(address + chunk, ScanChunkBytes / IntPtr.Size);
                for (var i = 0; i < slots.Length; i++)
                {
                    if (this.reader.IsValidAddress(slots[i]))
                    {
                        yield return (chunk + (i * IntPtr.Size), slots[i]);
                    }
                }
            }
        }

        /// <summary>
        ///     Probes every pointer-aligned slot of an element block for a plausible std::wstring. This is how
        ///     the display-text offset gets discovered on a live screen ("Play", "Options"...) instead of guessed.
        ///     Inline strings are decoded from the block itself; heap strings cost one extra read each.
        /// </summary>
        private List<(int Offset, string Text)> FindStringCandidates(byte[] block)
        {
            var found = new List<(int, string)>();
            var heapProbes = 0;
            for (var offset = 0; offset + WStringDecoder.Size <= block.Length; offset += IntPtr.Size)
            {
                if (IsInsideKnownField(offset))
                {
                    continue;
                }

                var isHeap = WStringDecoder.IsHeap(block, offset);
                if (isHeap && heapProbes >= MaxHeapProbesPerNode)
                {
                    continue;
                }

                var text = WStringDecoder.TryDecode(this.reader, block, offset, MaxProbeStringLength, allowEmpty: false);
                if (text == null)
                {
                    continue;
                }

                if (isHeap)
                {
                    heapProbes++;
                }

                if (IsPrintable(text))
                {
                    found.Add((offset, text));
                }
            }

            return found;
        }

        /// <summary>Reads a std::wstring field at a known offset of an element block; null when it is not a well-formed string.</summary>
        private string? ReadWStringAt(byte[] block, int offset) =>
            WStringDecoder.TryDecode(this.reader, block, offset, MaxProbeStringLength, allowEmpty: false);

        /// <summary>
        ///     Pointer slots of any object that target a valid UI element, with the element's name. Diffing
        ///     this list between two dumps taken with different controls focused reveals hovered/focused
        ///     element pointers without knowing the owner's layout.
        /// </summary>
        public List<(int Offset, IntPtr Element, string Name)> FindElementPointerSlots(IntPtr obj, int bytes)
        {
            var found = new List<(int, IntPtr, string)>();
            foreach (var (offset, pointer) in this.ReadPointerSlots(obj, bytes))
            {
                if (pointer == obj || !this.IsElementLike(pointer, out _))
                {
                    continue;
                }

                var block = this.reader.ReadBytes(pointer, NameOffset + WStringDecoder.Size);
                var name = block.Length == 0 ? string.Empty : this.ReadWStringAt(block, NameOffset) ?? string.Empty;
                found.Add((offset, pointer, name));
            }

            return found;
        }

        private static bool IsInsideKnownField(int offset)
        {
            foreach (var (start, end) in KnownFieldRanges)
            {
                if (offset >= start && offset < end)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPrintable(string text)
        {
            foreach (var c in text)
            {
                if ((char.IsControl(c) && c != '\n' && c != '\r' && c != '\t') || char.IsSurrogate(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
