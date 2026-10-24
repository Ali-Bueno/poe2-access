namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The in-game screen parts the plugin reaches through the layer container's fields (GameLayout.InGameUi).</summary>
    public enum InGameField
    {
        Hud,
        DeathScreen,
        InventoryPanel,
        Chat,
        Tutorial,
        SkillBar,
        BindDisplay,
    }

    /// <summary>
    ///     Finds each <see cref="InGameField"/> in the layer container (docs/compatibility.md): where it was measured
    ///     if the element there is what it must be — its name ("HUD", "death_screen"), or its shape (the inventory holds
    ///     "panels_parent", the chat a text field and a scroll bar) — else the nearest field that holds such an element.
    ///     The tutorial box has neither, so it follows the shift of the nearest field that was found. Layers are direct
    ///     children of the container; the child order changes between sessions, so it is never used.
    /// </summary>
    public sealed class InGameFields
    {
        /// <summary>How far from where it was measured a field is looked for, and the container bytes read to do it.</summary>
        private const int Radius = 0x800;

        private const int Step = 8;

        /// <summary>A field not found is looked for again after this long (it may not exist yet).</summary>
        private const int RetryMs = 5000;

        /// <summary>Misses in a row before a field counts as lost (fields are empty for a moment while an area loads).</summary>
        private const int MissesToReport = 3;

        private static readonly Dictionary<InGameField, Spec> Specs = new()
        {
            [InGameField.Hud] = new(GameLayout.InGameUi.HudLayerOffset, true, Named("HUD")),
            [InGameField.DeathScreen] = new(GameLayout.InGameUi.DeathScreenOffset, true, Named("death_screen")),
            [InGameField.InventoryPanel] = new(GameLayout.InGameUi.InventoryPanelOffset, true, (fields, info) => fields.HasChild(info, 1, "panels_parent")),
            [InGameField.Chat] = new(GameLayout.InGameUi.ChatLayerOffset, true, (fields, info) => fields.HasChild(info, 2, "textField") && fields.HasChild(info, 2, "scrollbar")),
            [InGameField.Tutorial] = new(GameLayout.InGameUi.TutorialLayerOffset, true, null),
            [InGameField.SkillBar] = new(GameLayout.InGameUi.SkillBarOffset, false, Named(GameLayout.InGameUi.SkillBarName)),
            [InGameField.BindDisplay] = new(GameLayout.InGameUi.BindDisplayOffset, false, Named(GameLayout.InGameUi.BindDisplayName)),
        };

        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;
        private readonly CompatibilityReport report;
        private readonly Dictionary<InGameField, int> offsets = new();
        private readonly Dictionary<InGameField, long> missedAt = new();
        private readonly Dictionary<InGameField, int> misses = new();
        private IntPtr container;

        public InGameFields(HostMemoryReader reader, UiElementReader elements, CompatibilityReport report)
        {
            this.reader = reader;
            this.elements = elements;
            this.report = report;
        }

        /// <summary>The element of <paramref name="field"/> in <paramref name="layerContainer"/>, or zero when it cannot be found.</summary>
        public IntPtr Find(IntPtr layerContainer, InGameField field)
        {
            if (layerContainer == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (layerContainer != this.container)
            {
                this.container = layerContainer;
                this.missedAt.Clear(); // the offsets found stay: they belong to the game's code, not to this container
            }

            var spec = Specs[field];
            if (this.offsets.TryGetValue(field, out var known) && this.Holds(known, spec) is var held && held != IntPtr.Zero)
            {
                return held;
            }

            var now = Environment.TickCount64;
            if (this.missedAt.TryGetValue(field, out var missed) && now - missed < RetryMs)
            {
                return IntPtr.Zero;
            }

            if (this.Locate(field, spec) is { } offset)
            {
                this.offsets[field] = offset;
                this.missedAt.Remove(field);
                this.misses.Remove(field);
                if (offset == spec.Expected)
                {
                    this.report.Found(Part(field));
                }
                else
                {
                    this.report.Moved(Part(field), spec.Expected, offset);
                }

                return this.Holds(offset, spec);
            }

            this.missedAt[field] = now;
            this.misses[field] = this.misses.GetValueOrDefault(field) + 1;
            if (field != InGameField.BindDisplay && this.misses[field] == MissesToReport)
            {
                this.report.Missing(Part(field)); // the bind screen's display exists only once it was opened
            }

            return IntPtr.Zero;
        }

        /// <summary>
        ///     Research (dev command "layout"): each field with a name or shape is looked for from a wrong place
        ///     (<see cref="LayoutProbe.SelfTestShift"/> away) and must be found where it is now.
        /// </summary>
        public string SelfTest(IntPtr layerContainer)
        {
            this.container = layerContainer;
            var lines = new List<string>();
            foreach (var (field, spec) in Specs)
            {
                if (spec.Mark == null || this.Locate(field, spec) is not { } now)
                {
                    continue;
                }

                // Another field holding the same element is as good: it is the element that is read.
                var found = this.Locate(field, spec with { Expected = now + LayoutProbe.SelfTestShift });
                var same = found is { } at && this.Holds(at, spec) == this.Holds(now, spec);
                lines.Add(LayoutProbe.SelfTestLine(Part(field), now, same ? now : found));
            }

            return string.Join("; ", lines);
        }

        /// <summary>A new game process (perhaps a patched one): every field is looked for again.</summary>
        public void Reset()
        {
            this.offsets.Clear();
            this.missedAt.Clear();
            this.misses.Clear();
            this.container = IntPtr.Zero;
        }

        private static Func<InGameFields, UiElementReader.ElementInfo, bool> Named(string name) => (_, info) => info.Name == name;

        private static string Part(InGameField field) => field switch
        {
            InGameField.Hud => "HUD",
            InGameField.DeathScreen => "death screen",
            InGameField.InventoryPanel => "inventory panel",
            InGameField.Chat => "chat",
            InGameField.Tutorial => "objective box",
            InGameField.SkillBar => "skill bar",
            _ => "bind screen",
        };

        private int? Locate(InGameField field, Spec spec)
        {
            if (this.Holds(spec.Expected, spec) != IntPtr.Zero)
            {
                return spec.Expected;
            }

            if (spec.Mark == null)
            {
                // No name or shape to recognise it by: it moved with the nearest field that was found, or it is lost.
                var anchor = this.offsets.Where(pair => Specs[pair.Key].Mark != null)
                    .OrderBy(pair => Math.Abs(Specs[pair.Key].Expected - spec.Expected))
                    .Select(pair => (int?)(pair.Value - Specs[pair.Key].Expected))
                    .FirstOrDefault();
                return anchor is { } shift and not 0 && this.Holds(spec.Expected + shift, spec) != IntPtr.Zero ? spec.Expected + shift : null;
            }

            var start = Math.Max(0, spec.Expected - Radius);
            var slots = this.reader.ReadArray<IntPtr>(this.container + start, (2 * Radius) / IntPtr.Size);
            return OffsetSearch.First(spec.Expected, Radius, Step, offset =>
                (offset - start) / IntPtr.Size is var index && index < slots.Length && this.Fits(slots[index], spec), Step);
        }

        /// <summary>The element at <paramref name="offset"/> of the container when it is what the field must hold.</summary>
        private IntPtr Holds(int offset, Spec spec) =>
            this.reader.TryRead<IntPtr>(this.container + offset, out var element) && this.Fits(element, spec) ? element : IntPtr.Zero;

        private bool Fits(IntPtr element, Spec spec) =>
            element != IntPtr.Zero && this.reader.IsValidAddress(element) && this.elements.TryReadElement(element, out var info) &&
            (!spec.Child || info.Raw.ParentPtr == this.container) && (spec.Mark == null || spec.Mark(this, info));

        /// <summary>An element named <paramref name="name"/> within <paramref name="depth"/> levels under <paramref name="info"/>.</summary>
        private bool HasChild(UiElementReader.ElementInfo info, int depth, string name)
        {
            foreach (var child in this.elements.ReadChildren(info))
            {
                if (this.elements.TryReadElement(child, out var childInfo) &&
                    (childInfo.Name == name || (depth > 1 && this.HasChild(childInfo, depth - 1, name))))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed record Spec(int Expected, bool Child, Func<InGameFields, UiElementReader.ElementInfo, bool>? Mark);
    }
}
