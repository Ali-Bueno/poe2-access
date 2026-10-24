namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Gold pick-ups (D2R "Picked up 37 gold"): the inventory panel's gold counter (G+0x378 subtree: the amount
    ///     text whose child image is the coin pile) keeps its value while the panel is hidden, so a rise of it during
    ///     play is gold picked up. With a panel open (sales, quest rewards, stash) only the baseline follows.
    /// </summary>
    public sealed class GoldWatch
    {
        private const int PollMs = 250;
        private const int SearchDepth = 12;
        private const int MaxNodes = 4000;

        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;

        private IntPtr counter;
        private long? amount;
        private long lastPoll;

        public GoldWatch(HostMemoryReader memory, UiElementReader elements, IAccessibilityOutputService output, PluginLog log)
        {
            this.memory = memory;
            this.elements = elements;
            this.output = output;
            this.log = log;
        }

        /// <summary>A new area or character: the panel is rebuilt and the amount is learnt again without a word.</summary>
        public void Reset()
        {
            this.counter = IntPtr.Zero;
            this.amount = null;
        }

        /// <summary>Per frame in game; <paramref name="playing"/> false while a panel or menu is open.</summary>
        public void Tick(IntPtr layerContainer, bool playing, bool enabled)
        {
            var now = Environment.TickCount64;
            if (layerContainer == IntPtr.Zero || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (!this.TryAmount(this.counter, out var value))
            {
                this.counter = this.Find(layerContainer);
                if (!this.TryAmount(this.counter, out value))
                {
                    return;
                }

                this.log.Info($"Gold counter at 0x{this.counter.ToInt64():X} reads {value}");
            }

            if (this.amount is { } before && value > before && playing && enabled)
            {
                this.output.Say(SpeechKind.GroundItems, Announcements.GoldPickedUp(value - before));
            }

            this.amount = value;
        }

        /// <summary>Digits of the counter ("1.234" in Spanish, "1,234" in English); false when it is not an amount.</summary>
        internal static bool TryParseAmount(string text, out long value)
        {
            value = 0;
            var digits = new string(text.Where(char.IsDigit).ToArray());
            return digits.Length > 0 && digits.Length <= 15 && text.All(c => char.IsDigit(c) || c is ',' or '.' or ' ' or ' ') &&
                   long.TryParse(digits, out value);
        }

        private bool TryAmount(IntPtr address, out long value)
        {
            value = 0;
            return address != IntPtr.Zero && this.elements.TryReadText(address, out var text) && TryParseAmount(text, out value);
        }

        /// <summary>
        ///     Depth-first in screen-tree order under the inventory panel: the first amount text with a coin-pile image
        ///     child (about 150 nodes in; breadth-first passes 4000 nodes of item cells before reaching its depth).
        /// </summary>
        private IntPtr Find(IntPtr layerContainer)
        {
            if (!this.memory.TryRead<IntPtr>(layerContainer + GameLayout.InGameUi.InventoryPanelOffset, out var panel) || panel == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var stack = new Stack<(IntPtr Address, int Depth)>();
            stack.Push((panel, 0));
            var visited = new HashSet<IntPtr>();
            while (stack.Count > 0 && visited.Count < MaxNodes)
            {
                var (address, depth) = stack.Pop();
                if (!visited.Add(address) || !this.elements.TryReadElement(address, out var info))
                {
                    continue;
                }

                var children = this.elements.ReadChildren(info);
                if (this.TryAmount(address, out _) && children.Any(this.IsCoin))
                {
                    return address;
                }

                if (depth < SearchDepth)
                {
                    for (var i = children.Length - 1; i >= 0; i--)
                    {
                        stack.Push((children[i], depth + 1));
                    }
                }
            }

            return IntPtr.Zero;
        }

        private bool IsCoin(IntPtr child) =>
            this.elements.TryReadElement(child, out var info) &&
            this.elements.ReadTextureKey(info) is { } key && key.Contains(GameLayout.Image.GoldArtMarker, StringComparison.Ordinal);
    }
}
