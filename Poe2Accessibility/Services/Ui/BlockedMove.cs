namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     A D-pad press in a menu that moved nothing (a page's only control, the end of a list): the control is said
    ///     again, since silence cannot be told from a mod that stopped reading (the shop's guild page, 2026-11-10). Pure.
    /// </summary>
    public sealed class BlockedMove
    {
        /// <summary>A move is told by then: a highlight the mod searches for is found up to 300 ms late, a screen change a little after.</summary>
        public const long WaitMs = 600;

        private IntPtr control;
        private string line = string.Empty;
        private long pressedAt;

        /// <summary>Whether a press still waits to be told from a move.</summary>
        public bool Waiting { get; private set; }

        /// <summary>A direction went down with the cursor on this control, which says this line; no control, nothing to wait for.</summary>
        public void Pressed(IntPtr control, string line, long now)
        {
            (this.control, this.line, this.pressedAt) = (control, line, now);
            this.Waiting = control != IntPtr.Zero && line.Length > 0;
        }

        /// <returns>
        ///     Whether to say the control again: once, <see cref="WaitMs"/> after the last press, when the cursor still rests
        ///     on it, it still says the same and nothing was said meanwhile.
        /// </returns>
        public bool Due(IntPtr control, string line, bool saidSince, long now)
        {
            if (!this.Waiting)
            {
                return false;
            }

            if (control != this.control || line != this.line || saidSince)
            {
                this.Waiting = false;
                return false;
            }

            if (now - this.pressedAt < WaitMs)
            {
                return false;
            }

            this.Waiting = false;
            return true;
        }
    }
}
