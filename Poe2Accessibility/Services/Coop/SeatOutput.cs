namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     One local player's speech while both are read (<see cref="Seats"/>): each line starts with the character's name,
    ///     "Yarkbruja: life 30 percent"; without a name it passes through unchanged.
    /// </summary>
    public sealed class SeatOutput : IAccessibilityOutputService
    {
        private readonly IAccessibilityOutputService inner;
        private readonly Func<string> name;

        public SeatOutput(IAccessibilityOutputService inner, Func<string> name)
        {
            this.inner = inner;
            this.name = name;
        }

        public string BackendName => this.inner.BackendName;

        public void Speak(string text, bool interrupt = false) => this.inner.Speak(this.Named(text), interrupt);

        /// <summary>A reading stays one behind the seat's name, so it ages like the first player's.</summary>
        public void SpeakReading(string text, bool interrupt = false) => this.inner.SpeakReading(this.Named(text), interrupt);

        public void Urge(string text, int holdMs = 1500) => this.inner.Urge(this.Named(text), holdMs);

        public void Keep(string text, int holdMs, bool interrupt = false) => this.inner.Keep(this.Named(text), holdMs, interrupt);

        public void TakeFloor(string text) => this.inner.TakeFloor(this.Named(text));

        public void StopBefore(DateTime since) => this.inner.StopBefore(since);

        public void Mention(string text) => this.inner.Mention(this.Named(text));

        public void Repeat() => this.inner.Repeat();

        public void Stop() => this.inner.Stop();

        public bool SaidSince(string fragment, DateTime since) => this.inner.SaidSince(fragment, since);

        /// <summary>The plugin's output is not this wrapper's to release.</summary>
        public void Dispose()
        {
        }

        private string Named(string text) => this.name() is { Length: > 0 } speaker ? $"{speaker}: {text}" : text;
    }
}
