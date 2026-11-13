namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameOffsets.Objects.States;

    /// <summary>The name, text, texture and render-name probes of <see cref="LayoutProbe"/>, with the adoption of a found manager.</summary>
    public sealed partial class LayoutProbe
    {
        /// <summary>Control names: the root or its layers must carry a known name (<paramref name="conclusive"/>: in game, where the HUD always does).</summary>
        public void ProbeNames(IntPtr root, bool conclusive)
        {
            var now = Environment.TickCount64;
            if (this.namesKnown || root == IntPtr.Zero || now - this.namesTriedAt < RetryMs)
            {
                return;
            }

            this.namesTriedAt = now;
            // The whole-object searches are kept for in game, where the evidence is always there (a few tries per game process).
            if ((OffsetSearch.First(this.measuredName, Radius, Step, this.NameFits(root), Step) ?? (conclusive ? this.WideName(root) : null)) is not { } found)
            {
                if (conclusive)
                {
                    this.Missed("element names", this.nameMisses);
                }

                return;
            }

            var shift = found - this.measuredName;
            GameLayout.Element.NameOffset = found;
            GameLayout.Element.FontOffset = this.measuredFont + shift;
            GameLayout.Element.ManagerOffset = this.measuredManager + shift;
            GameLayout.Element.TooltipsOffset = this.measuredTooltips + shift;
            this.namesKnown = true;
            this.Record("element names", this.measuredName, found);
            this.JudgeManagerField(root);
        }

        /// <summary>Label texts: the tagged text and two equal stripped copies (<see cref="UiElementReader.TryReadText(IntPtr, out string)"/>).</summary>
        public void ProbeText(IntPtr root, bool conclusive)
        {
            var now = Environment.TickCount64;
            if (this.textKnown || root == IntPtr.Zero || now - this.textTriedAt < RetryMs)
            {
                return;
            }

            this.textTriedAt = now;
            if (this.Leaves(root).Where(leaf => this.elements.TryReadText(leaf, out var text) && text.Length > 0).Take(MinTextHits).Count() >= MinTextHits)
            {
                this.textKnown = true;
                this.report.Found("element texts");
                return;
            }

            if ((OffsetSearch.First(0, Radius, Step, this.TextFits(root), -Radius) ?? (conclusive ? this.WideTextShift(root) : null)) is not { } found)
            {
                if (conclusive)
                {
                    this.Missed("element texts", this.textMisses);
                }

                return;
            }

            GameLayout.Element.TextWithTagsOffset = this.measuredTagged + found;
            GameLayout.Element.TextOffset = this.measuredText + found;
            GameLayout.Element.TextCopyOffset = this.measuredCopy + found;
            GameLayout.Element.TextColorOffset = this.measuredColor + found;
            this.textKnown = true;
            this.Record("element texts", this.measuredText, this.measuredText + found);
        }

        /// <summary>
        ///     Image textures: enough images under the root lead through the texture pointer to an art key, and the smaller
        ///     image class's pointer follows it (<paramref name="conclusive"/>: in game, where the HUD always shows images).
        /// </summary>
        public void ProbeTextures(IntPtr root, bool conclusive)
        {
            var now = Environment.TickCount64;
            if (this.texturesKnown || root == IntPtr.Zero || now - this.texturesTriedAt < RetryMs)
            {
                return;
            }

            this.texturesTriedAt = now;
            var fits = this.TextureFits(root);
            if (fits(GameLayout.Image.TextureOffset))
            {
                this.texturesKnown = true;
                this.report.Found("image textures");
                return;
            }

            if ((OffsetSearch.First(this.measuredTexture, Radius, Step, fits, Step) ?? (conclusive ? this.WideTexture(root) : null)) is not { } found)
            {
                if (conclusive)
                {
                    this.Missed("image textures", this.textureMisses);
                }

                return;
            }

            GameLayout.Image.TextureOffset = found;
            GameLayout.Image.AltTextureOffset = this.measuredAltTexture + (found - this.measuredTexture);
            this.texturesKnown = true;
            this.Record("image textures", this.measuredTexture, found);
        }

        /// <summary>
        ///     A UI manager holds <paramref name="root"/> (the root of elements that point at it) where measured, or the slot
        ///     that holds it is adopted and the focus, hover and pad fields follow it.
        /// </summary>
        public bool AdoptManager(IntPtr manager, IntPtr root)
        {
            if (root == IntPtr.Zero || !this.reader.IsValidAddress(manager) || this.elements.IsElement(manager))
            {
                return false;
            }

            if (this.reader.TryRead<IntPtr>(manager + GameLayout.UiManager.RootOffset, out var held) && held == root)
            {
                if (!this.managerKnown)
                {
                    this.managerKnown = true;
                    this.report.Found("UI manager");
                }

                return true;
            }

            var slots = this.reader.ReadArray<IntPtr>(manager, ManagerScanBytes / IntPtr.Size);
            if (OffsetSearch.First(this.measuredRoot, ManagerScanBytes, Step, offset => offset / IntPtr.Size < slots.Length && slots[offset / IntPtr.Size] == root) is not { } found)
            {
                return false;
            }

            var shift = found - this.measuredRoot;
            GameLayout.UiManager.RootOffset = found;
            GameLayout.UiManager.CurrentOffset = this.measuredCurrent + shift;
            GameLayout.UiManager.LastFocusedOffset = this.measuredLastFocused + shift;
            GameLayout.UiManager.HoveredOffset = this.measuredHovered + shift;
            GameLayout.UiManager.PadStateOffset = this.measuredPad + shift;
            this.managerKnown = true;
            this.report.Moved("UI manager", this.measuredRoot, found);
            return true;
        }

        /// <summary>
        ///     Entity names (Render component): judged on monsters, characters and players, half of which must read. Settled only
        ///     by a batch that reads (once: a second guess must not flap); lost once two different batches read none.
        /// </summary>
        public void ProbeRenderNames(IReadOnlyList<IntPtr> renders)
        {
            var now = Environment.TickCount64;
            if (this.renderKnown || renders.Count < MinRenders || now - this.renderTriedAt < RetryMs)
            {
                return;
            }

            this.renderTriedAt = now;
            int Named(int offset) => renders.Count(render => this.elements.ReadWStringStructAt(render + offset) is { Length: > 0 } name && name.Any(char.IsLetter));
            bool Fits(int offset) => Named(offset) * 2 >= renders.Count;
            if (OffsetSearch.First(this.measuredRenderName, Radius, Step, Fits, Step) is not { } found)
            {
                // A batch read while its entities were being freed reads no names on a healthy game.
                if (this.renderMisses.Against(LossWitnesses.KeyOf(renders.Select(render => render.ToInt64()))))
                {
                    this.report.Missing("entity names");
                }

                return;
            }

            this.renderKnown = true;
            this.renderMisses.Clear();
            GameLayout.RenderComponent.NameOffset = found;
            this.Record("entity names", this.measuredRenderName, found);
        }
    }
}
