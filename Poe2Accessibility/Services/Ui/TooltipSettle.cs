namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     An item's tooltip the first time it shows: the game fills it over a few frames, and one read meanwhile lacks its
    ///     modifier lines (a worn amulet read with its implicit alone, a belt whose build lines were all "missing",
    ///     2026-11-10). It is read again until it holds still. Pure.
    /// </summary>
    public sealed class TooltipSettle
    {
        /// <summary>A first tooltip is taken as whole once it read the same this long apart; the fill itself was not timed (a few frames).</summary>
        public const long HoldMs = 100;

        /// <summary>A tooltip that never holds still (a countdown on it) is said anyway after this long.</summary>
        public const long GiveUpMs = 500;

        /// <summary>Tooltips seen whole are kept for this many items; a panel's items are forgotten with it.</summary>
        private const int MaxSettled = 512;

        private readonly HashSet<IntPtr> settled = new();
        private IntPtr item;
        private int lines;
        private long firstSeenAt;
        private long sameSince;

        /// <returns>Whether the item's tooltip may still be filling: its reading waits and is taken again.</returns>
        /// <param name="item">The item's element.</param>
        /// <param name="lines">A hash of the tooltip's lines as read now; zero lines are no tooltip yet and never wait.</param>
        public bool Filling(IntPtr item, int lineCount, int lines, long now)
        {
            if (item == IntPtr.Zero || lineCount == 0 || this.settled.Contains(item))
            {
                return false;
            }

            if (item != this.item)
            {
                (this.item, this.lines, this.firstSeenAt, this.sameSince) = (item, lines, now, now);
                return true;
            }

            if (lines != this.lines)
            {
                (this.lines, this.sameSince) = (lines, now);
            }

            if (now - this.sameSince < HoldMs && now - this.firstSeenAt < GiveUpMs)
            {
                return true;
            }

            if (this.settled.Count >= MaxSettled)
            {
                this.settled.Clear();
            }

            this.settled.Add(item);
            return false;
        }

        /// <summary>The panel's elements are gone: their addresses will name other items.</summary>
        public void Forget()
        {
            this.settled.Clear();
            this.item = IntPtr.Zero;
        }
    }
}
