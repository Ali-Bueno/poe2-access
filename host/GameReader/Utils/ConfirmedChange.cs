namespace GameReader.Utils
{
    /// <summary>
    ///     A value held until another one has been read twice running: a single odd reading is one caught while the game
    ///     wrote the field, and acting on it would throw away what is held for nothing. Pure.
    /// </summary>
    public struct ConfirmedChange
    {
        private uint held;
        private uint pending;
        private bool waiting;

        /// <summary>The value is known anew (the object it belongs to changed): held from now on.</summary>
        public void Set(uint value) => (this.held, this.waiting) = (value, false);

        /// <summary>A reading: true once when it differs from the held value for the second time running, and is then the held one.</summary>
        public bool Offer(uint read)
        {
            if (read == this.held)
            {
                this.waiting = false;
                return false;
            }

            if (this.waiting && read == this.pending)
            {
                this.Set(read);
                return true;
            }

            (this.pending, this.waiting) = (read, true);
            return false;
        }
    }
}
