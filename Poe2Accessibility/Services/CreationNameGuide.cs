namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Where the cursor stands on character creation's name step.</summary>
    public enum CreationSpot
    {
        /// <summary>Not on the name step: the class choice.</summary>
        Elsewhere,

        /// <summary>The name's frame, the league or Continue: keys type nothing there.</summary>
        Outside,

        /// <summary>Inside the name's text box.</summary>
        Typing,
    }

    /// <summary>What the player needs to hear after a press the game ignored.</summary>
    public enum CreationAdvice
    {
        None,
        TypeOnTheName,
        CharacterRefused,
        NameFull,
        NameTooShort,
        HowToFinish,
    }

    /// <summary>The presses of one tick: a typing key, Enter, and A on the controller.</summary>
    public readonly record struct CreationPresses(bool Typed, bool Enter, bool A);

    /// <summary>Tells apart the presses the name step swallows in silence (docs/research/ui-layers.md, "Character creation").</summary>
    public sealed class CreationNameGuide
    {
        /// <summary>The field shows a typed character within a few frames: the same name by now means it was dropped.</summary>
        private const long RefusedAfterMs = 250;

        /// <summary>A held key or a repeated press says the same advice once in this long.</summary>
        private const long RepeatAfterMs = 1500;

        private readonly int maxLength;
        private CreationSpot lastSpot;
        private string? nameAtPress;
        private long pressedAt;
        private CreationAdvice lastAdvice;
        private long lastAdviceAt;

        public CreationNameGuide(int maxLength) => this.maxLength = maxLength;

        /// <summary>The advice due now, if any. <paramref name="continueGrey"/> is only read when a press depends on it.</summary>
        public CreationAdvice Tick(long now, CreationSpot spot, string name, CreationPresses presses, Func<bool> continueGrey)
        {
            var wasTyping = this.lastSpot == CreationSpot.Typing;
            this.lastSpot = spot;
            if (spot != CreationSpot.Typing)
            {
                this.nameAtPress = null;
            }

            if (spot == CreationSpot.Outside)
            {
                return wasTyping && continueGrey() ? this.Say(CreationAdvice.NameTooShort, now)
                    : presses.Typed ? this.Say(CreationAdvice.TypeOnTheName, now)
                    : CreationAdvice.None;
            }

            if (spot != CreationSpot.Typing)
            {
                return CreationAdvice.None;
            }

            if (wasTyping && (presses.Enter || presses.A))
            {
                return continueGrey() ? this.Say(CreationAdvice.NameTooShort, now)
                    : presses.A ? this.Say(CreationAdvice.HowToFinish, now)
                    : CreationAdvice.None;
            }

            if (presses.Typed && this.nameAtPress == null)
            {
                (this.nameAtPress, this.pressedAt) = (name, now);
            }

            if (this.nameAtPress == null)
            {
                return CreationAdvice.None;
            }

            if (name != this.nameAtPress)
            {
                this.nameAtPress = null;
                return CreationAdvice.None;
            }

            if (now - this.pressedAt < RefusedAfterMs)
            {
                return CreationAdvice.None;
            }

            this.nameAtPress = null;
            return this.Say(name.Length >= this.maxLength ? CreationAdvice.NameFull : CreationAdvice.CharacterRefused, now);
        }

        private CreationAdvice Say(CreationAdvice advice, long now)
        {
            if (advice == this.lastAdvice && now - this.lastAdviceAt < RepeatAfterMs)
            {
                return CreationAdvice.None;
            }

            (this.lastAdvice, this.lastAdviceAt) = (advice, now);
            return advice;
        }
    }
}
