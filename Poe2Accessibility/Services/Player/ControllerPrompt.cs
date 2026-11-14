namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     A first start of the game shows the login screen for the keyboard and mouse: no controller hint is drawn and
    ///     nothing says a button of the controller is what turns them on (live 2026-11-14, no settings file). Tells it
    ///     once a session, when the login screen has stood without a hint bar. Pure.
    /// </summary>
    public sealed class ControllerPrompt
    {
        /// <summary>How long the login screen stands without hints before it is told: the bar comes with the screen, within a snapshot or two.</summary>
        public const int SettleMs = 2500;

        private long shownSince = -1;
        private bool done;

        /// <summary>
        ///     Per frame. <paramref name="loginShown"/>: the login screen is drawn; <paramref name="hints"/>: a controller hint
        ///     bar is on screen. True once, when the prompt is to be said.
        /// </summary>
        public bool Tick(long now, bool loginShown, bool hints)
        {
            if (hints)
            {
                // The controller is in use: it stays so unless a key is pressed, which the player then knows of.
                this.done = true;
            }

            if (this.done || !loginShown)
            {
                this.shownSince = -1;
                return false;
            }

            if (this.shownSince < 0)
            {
                this.shownSince = now;
                return false;
            }

            if (now - this.shownSince < SettleMs)
            {
                return false;
            }

            this.done = true;
            return true;
        }
    }
}
