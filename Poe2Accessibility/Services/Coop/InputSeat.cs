namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     Whose controller and cursor the mod reads in local co-op, 1 or 2 (settings ReadPlayer1, ReadPlayer2; <see cref="Seats"/>).
    ///     Player 1 unless player 2 is read. Player 2 read alone takes over at its first press (before it joins, the
    ///     only controller in use is player 1's). Both read: the player who pressed a button or pushed a stick last, so a
    ///     menu is read for whoever moves in it; holding a stick (walking) is not a new press.
    /// </summary>
    public sealed class InputSeat
    {
        /// <summary>A stick or trigger past this is pushed.</summary>
        public const float Pushed = 0.5f;

        private readonly uint[] lastButtons = new uint[2];
        private readonly bool[] lastPushed = new bool[2];
        private bool rebase;

        public int Seat { get; private set; } = 1;

        /// <summary>
        ///     Another UI manager (game state): its first reading is where the controls are, not a press (an area load
        ///     switched the seat back and forth, 2026-10-25).
        /// </summary>
        public void Baseline() => this.rebase = true;

        /// <param name="first">Player 1's buttons and largest stick or trigger deflection, null when unreadable.</param>
        /// <param name="second">The same for player 2.</param>
        public int Update(bool readFirst, bool readSecond, (uint Buttons, float Deflection)? first, (uint Buttons, float Deflection)? second)
        {
            var pressedFirst = this.Pressed(0, first) && !this.rebase;
            var pressedSecond = this.Pressed(1, second) && !this.rebase;
            this.rebase = false;
            if (!readSecond)
            {
                this.Seat = 1;
            }
            else if (!readFirst)
            {
                this.Seat = pressedSecond ? 2 : this.Seat;
            }
            else if (pressedFirst != pressedSecond)
            {
                this.Seat = pressedSecond ? 2 : 1;
            }

            return this.Seat;
        }

        private bool Pressed(int index, (uint Buttons, float Deflection)? state)
        {
            var (buttons, deflection) = state ?? (0u, 0f);
            var pushed = deflection >= Pushed;
            var pressed = (buttons & ~this.lastButtons[index]) != 0 || (pushed && !this.lastPushed[index]);
            this.lastButtons[index] = buttons;
            this.lastPushed[index] = pushed;
            return pressed;
        }
    }
}
