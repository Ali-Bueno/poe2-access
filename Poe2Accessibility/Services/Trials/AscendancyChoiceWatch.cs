namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.Plugin;

    /// <summary>
    ///     Reads the Altar of Ascendancy's choice of class: the stick moves between the cards and the focus stays where it
    ///     was, so the card it is on was never said (docs/research/trials-and-campaign.md §3).
    /// </summary>
    public sealed class AscendancyChoiceWatch
    {
        private const long PollMs = 250;

        /// <summary>The label about the class lies in the card's tree, two levels under the card (dump 2026-11-14).</summary>
        private const int AboutHops = 2;

        /// <summary>The labels lie four levels under the window; its three cards hold some 110 elements down to there.</summary>
        private const int SearchDepth = 4;
        private const int SearchNodes = 600;

        /// <summary>
        ///     The window's title is said, cutting in, some 0.2 s after the panel shows (log 2026-11-14: the first card,
        ///     queued before it, was lost): the first card waits it out.
        /// </summary>
        private const long TitleMs = 750;

        /// <summary>How far under a card the Build Planner's mark is looked for (its text is the mark's child).</summary>
        private const int MarkDepth = 2;

        private readonly UiElementReader elements;
        private readonly Func<TrialCatalog> catalog;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog? log;
        private long lastPoll;
        private IntPtr panel;
        private List<(IntPtr Card, IntPtr About)> cards = new();
        private string said = string.Empty;
        private long openedAt;
        private bool cuts;
        private bool onControl;

        public AscendancyChoiceWatch(UiElementReader elements, Func<TrialCatalog> catalog, IAccessibilityOutputService output, PluginLog? log)
        {
            this.elements = elements;
            this.catalog = catalog;
            this.output = output;
            this.log = log;
        }

        /// <summary>Gets the tree of the class A opened, while it fills the screen; zero otherwise.</summary>
        public IntPtr OpenedTree { get; private set; }

        /// <summary>Per frame, with the panel covering the game (zero: none) and whether a control of it has the focus.</summary>
        public void Tick(IntPtr panelOverGame, bool focusHeld)
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (panelOverGame != this.panel)
            {
                this.panel = panelOverGame;
                this.cards = this.FindCards(panelOverGame);
                this.said = string.Empty;
                this.openedAt = now;
                this.cuts = false;
            }

            // Up or down takes the focus to the Build Planner's button; back on the cards, the one the stick is on is said again.
            var back = this.onControl && !focusHeld;
            this.onControl = focusHeld;
            if (back)
            {
                this.said = string.Empty;
            }

            if (this.cards.Count < 2)
            {
                this.OpenedTree = IntPtr.Zero;
                return;
            }

            if (now - this.openedAt < TitleMs)
            {
                return;
            }

            var ordered = AscendancyChoice.InOrder(this.cards.Select(this.Read).OfType<AscendancyCard>());
            if (ordered.Count != this.cards.Count)
            {
                this.cards = this.FindCards(panelOverGame); // a card the game made anew: looked for again
                return;
            }

            var chosen = AscendancyChoice.Chosen(ordered);
            this.OpenedTree = AscendancyChoice.Opened(ordered, chosen) ? ordered[chosen].Tree : IntPtr.Zero;
            var line = AscendancyChoice.Line(ordered, chosen);
            if (line.Length == 0 || line == this.said)
            {
                return;
            }

            // The first card comes after the window's title; a move of the stick cuts what the last card was saying.
            this.log?.Info($"Ascendancy cards: {string.Join(" | ", ordered.Select(card => $"{card.Name} {card.Width:0}"))}; on {chosen + 1}");
            this.output.Speak(line, this.cuts);
            this.cuts = true;
            this.said = line;
        }

        /// <summary>The cards of the window: each holds a label about its class; none in any other panel.</summary>
        private List<(IntPtr Card, IntPtr About)> FindCards(IntPtr window)
        {
            var found = new List<(IntPtr Card, IntPtr About)>();
            if (window == IntPtr.Zero || !this.elements.TryReadElement(window, out var root))
            {
                return found;
            }

            var budget = SearchNodes;
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((root, 0));
            while (queue.Count > 0 && budget-- > 0)
            {
                var (node, depth) = queue.Dequeue();
                if (node.Name == AscendancyChoice.AboutName)
                {
                    var card = this.elements.ReadAncestors(node, AboutHops);
                    if (card.Count == AboutHops)
                    {
                        found.Add((card[^1].Address, node.Address));
                    }

                    continue;
                }

                if (depth >= SearchDepth)
                {
                    continue;
                }

                foreach (var child in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(child, out var info) && info.IsVisibleFlag)
                    {
                        queue.Enqueue((info, depth + 1));
                    }
                }
            }

            return found;
        }

        private AscendancyCard? Read((IntPtr Card, IntPtr About) card)
        {
            if (!this.elements.TryReadElement(card.Card, out var info) || !info.IsVisibleFlag ||
                !this.elements.TryReadText(card.About, out var about) || about.Length == 0 ||
                !PluginUiElementReflection.TryGetAbsoluteRect(card.Card, out var position, out var size))
            {
                return null;
            }

            var catalog = this.catalog();
            var name = catalog.AscendancyOf(about) is { } key ? catalog.Text(key) : string.Empty;
            var tree = this.elements.TryReadElement(card.About, out var label) ? label.Raw.ParentPtr : IntPtr.Zero;
            return new AscendancyCard(name, about, this.HasMark(info, tree), position.X + (size.X / 2), size.X, tree);
        }

        /// <summary>The Build Planner writes the build's name on the card of its class: the card's only text outside its tree.</summary>
        private bool HasMark(in UiElementReader.ElementInfo card, IntPtr tree)
        {
            var stack = new Stack<(IntPtr Address, int Depth)>();
            foreach (var child in this.elements.ReadChildren(card))
            {
                if (child != tree)
                {
                    stack.Push((child, 1));
                }
            }

            while (stack.Count > 0)
            {
                var (address, depth) = stack.Pop();
                if (!this.elements.TryReadElement(address, out var node) || !node.IsVisibleFlag)
                {
                    continue;
                }

                if (this.elements.TryReadText(address, out var text) && text.Any(char.IsLetter))
                {
                    return true;
                }

                if (depth < MarkDepth)
                {
                    foreach (var child in this.elements.ReadChildren(node))
                    {
                        stack.Push((child, depth + 1));
                    }
                }
            }

            return false;
        }
    }
}
