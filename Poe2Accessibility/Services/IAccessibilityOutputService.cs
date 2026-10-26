namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Single speech sink. Everything the plugin says goes through here so the backend (PRISM) can be
    ///     swapped or mocked in one place. Callers speak on events only, never per frame.
    /// </summary>
    public interface IAccessibilityOutputService : IDisposable
    {
        string BackendName { get; }

        /// <param name="interrupt">true only on context changes (new screen); false for incremental info.</param>
        void Speak(string text, bool interrupt = false);

        /// <summary>
        ///     D2R "Urge": interrupts, then any interrupting phrase in the next <paramref name="holdMs"/> queues behind
        ///     it instead of cutting it off (low life, arrival, a status that began).
        /// </summary>
        void Urge(string text, int holdMs = 1500);

        /// <summary>
        ///     Says a tutorial lesson whole: for <paramref name="holdMs"/> once it starts, what would cut it waits and follows
        ///     it (only the latest of what interrupts).
        /// </summary>
        void Keep(string text, int holdMs, bool interrupt = false) => this.Speak(text, interrupt);

        /// <summary>
        ///     Queues a short warning like <see cref="Speak"/>; an interrupting phrase in the next moment starts with it, so
        ///     the warning is never dropped and nothing waits behind it (life and mana).
        /// </summary>
        void Mention(string text) => this.Speak(text);

        /// <summary>Re-speak the last announcement (user hotkey).</summary>
        void Repeat();

        void Stop();
    }

    /// <summary>Fallback when PRISM cannot be initialised: the plugin keeps logging but stays silent.</summary>
    public sealed class NullOutputService : IAccessibilityOutputService
    {
        public string BackendName => "none";

        public void Speak(string text, bool interrupt = false)
        {
        }

        public void Urge(string text, int holdMs = 1500)
        {
        }

        public void Repeat()
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}
