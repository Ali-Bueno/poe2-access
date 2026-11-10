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

        /// <summary>
        ///     An item its tooltip names (magic, rare, unique) waits this long for a tooltip that is not up yet: a worn
        ///     weapon's came about 170 ms after the cursor, and its base name was said, then its own name (2026-11-10).
        /// </summary>
        public const long NameWaitMs = 300;

        private readonly HashSet<IntPtr> settled = new();

        /// <summary>Items that showed no tooltip within <see cref="NameWaitMs"/>: said at once from then on.</summary>
        private readonly HashSet<IntPtr> nameless = new();
        private IntPtr item;
        private int lines;
        private long firstSeenAt;
        private long sameSince;

        /// <summary>The item in hand has shown no tooltip yet (the wait for its name).</summary>
        private bool empty;

        /// <returns>Whether the item's tooltip may still be filling: its reading waits and is taken again.</returns>
        /// <param name="item">The item's element.</param>
        /// <param name="lines">A hash of the tooltip's lines as read now; zero lines are no tooltip yet.</param>
        /// <param name="nameAwaited">
        ///     Whether the tooltip will change the item's name (not of normal rarity, never read): only then is a tooltip
        ///     that is not up yet waited for, so an item whose line would be the same is never held back.
        /// </param>
        public bool Filling(IntPtr item, int lineCount, int lines, long now, bool nameAwaited = false)
        {
            if (item == IntPtr.Zero || this.settled.Contains(item))
            {
                return false;
            }

            if (lineCount == 0)
            {
                if (!nameAwaited || this.nameless.Contains(item))
                {
                    return false;
                }

                if (item != this.item)
                {
                    (this.item, this.lines, this.firstSeenAt, this.sameSince, this.empty) = (item, 0, now, now, true);
                    return true;
                }

                if (this.empty && now - this.firstSeenAt < NameWaitMs)
                {
                    return true;
                }

                if (this.empty)
                {
                    // No tooltip came (an item in the hand, a panel that shows none): its base name is all there is, and
                    // the wait is paid once.
                    if (this.nameless.Count >= MaxSettled)
                    {
                        this.nameless.Clear();
                    }

                    this.nameless.Add(item);
                }

                return false;
            }

            if (item != this.item || this.empty)
            {
                // Also the tooltip that came after the wait for it: its filling is timed from here.
                (this.item, this.lines, this.firstSeenAt, this.sameSince, this.empty) = (item, lines, now, now, false);
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
            this.nameless.Clear();
            this.item = IntPtr.Zero;
        }
    }
}
