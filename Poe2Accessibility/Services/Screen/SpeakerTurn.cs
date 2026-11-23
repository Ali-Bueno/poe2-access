namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Pure: who speaks in a dialogue, named before a line only when it is another speaker's turn.</summary>
    public sealed class SpeakerTurn
    {
        /// <summary>The screen reports the new name within a snapshot or two of the line that named it.</summary>
        private const int ScreenLagMs = 1500;

        private string last = string.Empty;
        private (string Name, DateTime At) said = (string.Empty, DateTime.MinValue);

        /// <summary>The dialogue ended or another screen came: its speaker is news again.</summary>
        public void Forget()
        {
            this.last = string.Empty;
            this.said = (string.Empty, DateTime.MinValue);
        }

        /// <summary>The name to say before the line, empty while the same one speaks.</summary>
        public string Next(string speaker)
        {
            if (speaker.Length == 0 || speaker == this.last)
            {
                return string.Empty;
            }

            this.last = speaker;
            return speaker;
        }

        /// <summary>Same, for a line that is being said now.</summary>
        public string Say(string speaker, DateTime now)
        {
            var name = this.Next(speaker);
            this.said = name.Length > 0 ? (name, now) : this.said;
            return name;
        }

        /// <summary>The line named this speaker a moment ago: the label the screen reports is the same news.</summary>
        public bool JustSaid(string name, DateTime now) => name == this.said.Name && (now - this.said.At).TotalMilliseconds < ScreenLagMs;
    }
}
