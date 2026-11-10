namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Says what the other side of a trade window offers. Disenchanting is one (the game calls it a trade), and the pad's
    ///     cursor never reaches the other side's grid, so what the player would receive went unread (docs/menu-speech-rules.md).
    /// </summary>
    public sealed class TradeOfferWatch
    {
        private const long PollMs = 250;
        private const long ClosedWindowMs = 1500;

        // The window's body: the other side's grid, the player's grid, then each one's title in the same order.
        private const int TitleAfterGrid = 2;
        private const int MinChildren = 4;

        private readonly UiElementReader elements;
        private readonly InventoryReader inventory;
        private readonly Action<string> speak;
        private long lastPoll;
        private string? said;
        private string? waiting;
        private long closedAt = -ClosedWindowMs;

        public TradeOfferWatch(UiElementReader elements, InventoryReader inventory, Action<string> speak)
        {
            this.elements = elements;
            this.inventory = inventory;
            this.speak = speak;
        }

        /// <summary>Gets a value indicating whether a trade window is open or closed a moment ago: its items leave as it closes.</summary>
        public bool Open => this.said != null || Environment.TickCount64 - this.closedAt < ClosedWindowMs;

        /// <summary>Per frame, with the element that has the focus (zero: none, so the window closed).</summary>
        public void Tick(IntPtr focus)
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (this.waiting is { } line)
            {
                // One poll later, behind the cell's own line: said at once, that line would cut it.
                this.waiting = null;
                this.speak(line);
            }

            if (focus == IntPtr.Zero)
            {
                this.closedAt = this.said != null ? now : this.closedAt;
                this.said = null;
            }
            else if (this.ReadOffer(focus) is { } offer)
            {
                this.waiting = TradeOfferNews.Line(offer.Title, offer.Items, Announcements.Nothing, ref this.said);
            }
        }

        private (string Title, IReadOnlyList<string> Items)? ReadOffer(IntPtr grid)
        {
            if (!this.elements.TryReadElement(grid, out var cell) || !this.elements.TryReadElement(cell.Raw.ParentPtr, out var body))
            {
                return null;
            }

            var parts = this.elements.ReadChildren(body);
            var mine = Array.IndexOf(parts, grid);
            if (parts.Length < MinChildren || mine is < 0 or >= TitleAfterGrid || !this.inventory.TryReadGrid(grid, out _, out _, out _))
            {
                return null;
            }

            var theirs = TitleAfterGrid - 1 - mine;
            if (!this.elements.TryReadText(parts[theirs + TitleAfterGrid], out var title) || title.Length == 0 ||
                !this.elements.TryReadText(parts[mine + TitleAfterGrid], out var ownTitle) || ownTitle.Length == 0 ||
                !this.elements.TryReadElement(parts[theirs], out var other))
            {
                return null;
            }

            return (title, this.elements.ReadChildren(other).Select(item => this.inventory.DescribeItem(item)).OfType<string>().ToList());
        }
    }
}
