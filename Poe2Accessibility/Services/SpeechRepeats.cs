namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Whether a line only repeats the one just asked for (pure): a boss of several bodies asks once per body.</summary>
    public sealed class SpeechRepeats
    {
        /// <summary>Identical text arriving within this window is a frame-level duplicate, not new information.</summary>
        public const int WindowMs = 300;

        private string last = string.Empty;
        private DateTime at = DateTime.MinValue;

        public bool IsRepeat(string text, DateTime now) => text == this.last && (now - this.at).TotalMilliseconds < WindowMs;

        public void Note(string text, DateTime now)
        {
            this.last = text;
            this.at = now;
        }
    }
}
