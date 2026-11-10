namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     What <see cref="LayoutProbe"/> does once a look near where a field was measured found nothing: the searches of a
    ///     whole element, the second looks before a part is said lost, and the checks that ask for a pointer to lead back
    ///     (docs/compatibility.md, "The whole object", "When a part is said lost").
    /// </summary>
    public sealed partial class LayoutProbe
    {
        /// <summary>
        ///     The parts that missed where a miss counts (in game) are looked for again here: the locator probes once per state,
        ///     and one look on the first frame in game, when the HUD may not be built yet, is neither a loss nor the last word.
        /// </summary>
        private void LookAgain(IntPtr manager)
        {
            var now = Environment.TickCount64;
            if (manager == IntPtr.Zero || now < this.nextLookAgainAt ||
                ((!this.missedInGame || (this.namesKnown && this.textKnown && this.texturesKnown)) && (this.pendingState == IntPtr.Zero || this.layerRootsKnown)))
            {
                return;
            }

            // Only in play is a miss a miss: the manager given then is the in-game one, whose root always shows the HUD.
            this.nextLookAgainAt = now + LookAgainEveryMs;
            if (GameReader.Core.States.GameCurrentState != GameReader.RemoteEnums.GameStateTypes.InGameState)
            {
                return;
            }

            if (this.missedInGame && this.reader.TryRead<IntPtr>(manager + GameLayout.UiManager.RootOffset, out var root) && root != IntPtr.Zero)
            {
                this.ProbeNames(root, true);
                this.ProbeText(root, true);
                this.ProbeTextures(root, true);
            }

            if (this.pendingState != IntPtr.Zero && GameReader.Core.States.InGameStateObject.Address == this.pendingState)
            {
                this.ProbeLayerRoots(this.pendingState);
            }
        }

        /// <summary>A conclusive look that found nothing: said lost once a second look, <see cref="RetryMs"/> later or more, agrees.</summary>
        private void Missed(string part, LossWitnesses misses)
        {
            this.missedInGame = true;
            if (misses.Against(++this.looks))
            {
                this.report.Missing(part);
            }
        }

        /// <summary>Whether the element's parents end at the root its own UI manager holds (<see cref="LayerSlots.LeadsBack"/>).</summary>
        private bool LeadsToManagerRoot(IntPtr element) => LayerSlots.LeadsBack(
            element.ToInt64(),
            child => this.elements.TryReadElement(new IntPtr(child), out var info) ? info.Raw.ParentPtr.ToInt64() : 0,
            top => this.reader.TryRead<IntPtr>(new IntPtr(top) + GameLayout.Element.ManagerOffset, out var manager) && this.reader.IsValidAddress(manager) ? manager.ToInt64() : 0,
            manager => this.reader.TryRead<IntPtr>(new IntPtr(manager) + GameLayout.UiManager.RootOffset, out var held) ? held.ToInt64() : 0);

        /// <summary>
        ///     The manager pointer follows the control name; here it is checked on its own (<see cref="ElementFieldSearch.Manager"/>):
        ///     the first elements of the tree share one pointer there and that object holds their root. Found elsewhere, it is
        ///     read from there; with nothing to judge it on it stays where the name's shift put it, and nothing is said.
        /// </summary>
        private void JudgeManagerField(IntPtr root)
        {
            var blocks = this.WideBlocks(this.NamedNodes(root).Take(MaxManagerNodes), GameLayout.Element.ManagerOffset + IntPtr.Size);
            bool HoldsRoot(long pointer)
            {
                var manager = new IntPtr(pointer);
                return manager != root && this.reader.IsValidAddress(manager) && !this.elements.IsElement(manager) &&
                       Array.IndexOf(this.reader.ReadArray<IntPtr>(manager, ManagerScanBytes / IntPtr.Size), root) >= 0;
            }

            if (ElementFieldSearch.Manager(blocks, HoldsRoot, GameLayout.Element.ManagerOffset, Radius, Step) is { } found)
            {
                GameLayout.Element.ManagerOffset = found;
                this.Record(ManagerFieldPart, this.measuredManager, found);
            }
        }

        /// <summary>The control name far from where measured: the one place where two elements carry two different known names.</summary>
        private int? WideName(IntPtr root)
        {
            if (this.wideNameTries++ >= MaxWideTries)
            {
                return null;
            }

            var blocks = this.WideBlocks(this.NamedNodes(root), this.measuredName + WStringDecoder.Size);
            return ElementFieldSearch.Name(blocks, (block, at) => WStringDecoder.TryDecode(this.reader, block, at, MaxLength, false), KnownLayerNames, this.measuredName, Radius, Step);
        }

        /// <summary>The label texts far from where measured: the one shift under which enough labels read their three fields.</summary>
        private int? WideTextShift(IntPtr root)
        {
            if (this.wideTextTries++ >= MaxWideTries)
            {
                return null;
            }

            var blocks = this.WideBlocks(this.Leaves(root).Take(MaxLeaves), this.measuredCopy + WStringDecoder.Size);
            var last = GameLayout.Element.ProbeBytes - this.measuredCopy - WStringDecoder.Size;
            return ElementFieldSearch.TextShift(blocks, this.TextAt, Radius, Step, -this.measuredTagged, last + Step, MinTextHits);
        }

        /// <summary>The image texture far from where measured: the one place that leads enough images to an art key.</summary>
        private int? WideTexture(IntPtr root)
        {
            if (this.wideTextureTries++ >= MaxWideTries)
            {
                return null;
            }

            var blocks = this.WideBlocks(this.Leaves(root).Take(MaxWideLeaves), this.measuredTexture + IntPtr.Size);
            return ElementFieldSearch.Texture(blocks, pointer => this.elements.ReadTextureKey(new IntPtr(pointer)) != null, this.measuredTexture, Radius, Step, MinTextureHits);
        }

        /// <summary>Each element's bytes up to <see cref="GameLayout.Element.ProbeBytes"/>, as far as they read (at least <paramref name="floor"/>).</summary>
        private List<byte[]> WideBlocks(IEnumerable<IntPtr> elements, int floor)
        {
            var blocks = new List<byte[]>();
            foreach (var element in elements)
            {
                // Host reads are all or nothing: an element near the end of its page reads shorter.
                for (var size = GameLayout.Element.ProbeBytes; size >= floor; size /= 2)
                {
                    if (this.reader.ReadBytes(element, size) is { Length: > 0 } block)
                    {
                        blocks.Add(block);
                        break;
                    }
                }
            }

            return blocks;
        }
    }
}
