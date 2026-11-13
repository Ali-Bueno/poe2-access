namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;

    /// <summary>The self-test and the fit checks of <see cref="LayoutProbe"/>: what a measured field must satisfy to count as found.</summary>
    public sealed partial class LayoutProbe
    {
        /// <summary>
        ///     Research (dev command "layout"): every search started off target (a field moved by <see cref="SelfTestShift"/>)
        ///     must come back to where the layout is now; "FAIL" names a search that would not survive such an update.
        /// </summary>
        public string SelfTest(IntPtr root, IntPtr manager)
        {
            var names = OffsetSearch.First(GameLayout.Element.NameOffset + SelfTestShift, Radius, Step, this.NameFits(root), Step);
            var textShift = GameLayout.Element.TextOffset - this.measuredText;
            var text = OffsetSearch.First(textShift + SelfTestShift, Radius, Step, this.TextFits(root), -Radius);
            var slots = this.reader.ReadArray<IntPtr>(manager, ManagerScanBytes / IntPtr.Size);
            var rootSlot = OffsetSearch.First(GameLayout.UiManager.RootOffset + SelfTestShift, ManagerScanBytes, Step,
                offset => offset / IntPtr.Size < slots.Length && slots[offset / IntPtr.Size] == root);
            var textures = OffsetSearch.First(GameLayout.Image.TextureOffset + SelfTestShift, Radius, Step, this.TextureFits(root), Step);
            return string.Join("; ", SelfTestLine("element names", GameLayout.Element.NameOffset, names), SelfTestLine("element texts", textShift, text),
                SelfTestLine("UI manager", GameLayout.UiManager.RootOffset, rootSlot), SelfTestLine("image textures", GameLayout.Image.TextureOffset, textures),
                ElementBaseSelfTest(root), this.focusKnown ? "UI focus ok" : "UI focus not judged yet (move through a menu)",
                this.layerRootsKnown ? "in-game layer roots ok" : "in-game layer roots not judged yet");
        }

        /// <summary>The host's element base search started <see cref="SelfTestShift"/> off every field must come back where they read now.</summary>
        private static string ElementBaseSelfTest(IntPtr root)
        {
            var measured = GameReader.Utils.UiElementFields.Measured;
            var now = new GameReader.Utils.UiElementFields(
                Follow(measured.Self), Follow(measured.Children), Follow(measured.Parent), Follow(measured.Position), Follow(measured.Flags), Follow(measured.Size));
            var off = new GameReader.Utils.UiElementFields(now.Self + SelfTestShift, now.Children + SelfTestShift, now.Parent + SelfTestShift,
                now.Position + SelfTestShift, now.Flags + SelfTestShift, now.Size + SelfTestShift);
            var found = GameReader.Utils.OffsetHealer.FindUiElementBase(root, off);
            return string.Join("; ", SelfTestLine("element tree", now.Parent, found.Parent), SelfTestLine("element rects", now.Size, found.Size),
                SelfTestLine("element flags", now.Flags, found.Flags));

            static int Follow(int field) => GameReader.Utils.HostStructs.Follow<GameOffsets.Objects.UiElement.UiElementBaseOffset>(field);
        }

        /// <summary>How far a field is pretended to have moved in <see cref="SelfTest"/>.</summary>
        public const int SelfTestShift = 0x18;

        public static string SelfTestLine(string part, int now, int? found) =>
            found == now ? $"{part} ok" : $"{part} FAIL (found {(found is { } at ? $"0x{at:X}" : "nothing")}, is 0x{now:X})";

        /// <summary>The root or one of its layers carries a known control name at the offset.</summary>
        private Func<int, bool> NameFits(IntPtr root)
        {
            var blocks = this.NamedNodes(root).Select(node => this.reader.ReadBytes(node, this.measuredName + Radius + WStringDecoder.Size)).Where(block => block.Length > 0).ToList();
            return offset => blocks.Any(block => WStringDecoder.TryDecode(this.reader, block, offset, MaxLength, false) is { } name && KnownLayerNames.Contains(name));
        }

        /// <summary>The root, its children and theirs: the elements that carry the known names.</summary>
        private List<IntPtr> NamedNodes(IntPtr root)
        {
            // In game the named layers sit one level lower than at login (root, layer container, layers).
            var nodes = new List<IntPtr> { root };
            if (this.elements.TryReadElement(root, out var rootInfo))
            {
                foreach (var child in this.elements.ReadChildren(rootInfo))
                {
                    nodes.Add(child);
                    if (nodes.Count < MaxNamedNodes && this.elements.TryReadElement(child, out var childInfo))
                    {
                        nodes.AddRange(this.elements.ReadChildren(childInfo).Take(MaxNamedNodes - nodes.Count));
                    }
                }
            }

            return nodes;
        }

        /// <summary>Enough labels under the root read through the text fields moved by the shift.</summary>
        private Func<int, bool> TextFits(IntPtr root)
        {
            var blocks = this.Leaves(root).Take(MaxLeaves).Select(leaf => this.reader.ReadBytes(leaf, this.measuredCopy + Radius + WStringDecoder.Size)).Where(block => block.Length > 0).ToList();
            return shift => blocks.Count(block => this.TextAt(block, shift)) >= MinTextHits;
        }

        /// <summary>
        ///     Enough images under the root lead through the pointer at the offset to an art key, and far fewer through the
        ///     next slot: one step after the smaller class's pointer is the larger class's, which must not be taken for it.
        /// </summary>
        private Func<int, bool> TextureFits(IntPtr root)
        {
            var blocks = this.Leaves(root).Take(MaxLeaves).Select(leaf => this.reader.ReadBytes(leaf, this.measuredTexture + Radius + Step + IntPtr.Size)).Where(block => block.Length > 0).ToList();
            var hits = new Dictionary<int, int>();
            int Hits(int offset)
            {
                if (!hits.TryGetValue(offset, out var count))
                {
                    count = offset < 0 ? 0 : blocks.Count(block => offset + IntPtr.Size <= block.Length &&
                        this.elements.ReadTextureKey(MemoryMarshal.Read<IntPtr>(block.AsSpan(offset))) != null);
                    hits[offset] = count;
                }

                return count;
            }

            return offset => Hits(offset) is var count && count >= MinTextureHits && Hits(offset + Step) * 2 < count;
        }

        private bool TextAt(byte[] block, int shift)
        {
            var tagged = WStringDecoder.TryDecode(this.reader, block, this.measuredTagged + shift, MaxLength, true);
            var text = WStringDecoder.TryDecode(this.reader, block, this.measuredText + shift, MaxLength, false);
            return tagged != null && text != null && text == WStringDecoder.TryDecode(this.reader, block, this.measuredCopy + shift, MaxLength, false);
        }

        /// <summary>Childless elements under <paramref name="root"/>, breadth first and lazily, hidden ones too (their texts stay).</summary>
        private IEnumerable<IntPtr> Leaves(IntPtr root)
        {
            var queue = new Queue<IntPtr>();
            queue.Enqueue(root);
            for (var walked = 0; queue.Count > 0 && walked < MaxWalkNodes; walked++)
            {
                if (!this.elements.TryReadElement(queue.Dequeue(), out var info))
                {
                    continue;
                }

                var children = this.elements.ReadChildren(info);
                if (children.Length == 0)
                {
                    yield return info.Address;
                }

                foreach (var child in children)
                {
                    queue.Enqueue(child);
                }
            }
        }

        private void Record(string part, int measured, int found)
        {
            if (found == measured)
            {
                this.report.Found(part);
            }
            else
            {
                this.report.Moved(part, measured, found);
            }
        }
    }
}
