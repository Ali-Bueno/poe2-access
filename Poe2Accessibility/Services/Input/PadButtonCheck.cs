namespace Poe2Accessibility.Services
{
    using System.Linq;

    /// <summary>
    ///     Whether the pad state's button bits are where they were measured (GameLayout.PadButtons), judged on the left
    ///     trigger, which the game reports both as a pressure and as a button: while it is down and the right one up its
    ///     bit is set, and clear while both are up. A renumbering of the buttons moves them all as far (docs/compatibility.md). Pure.
    /// </summary>
    public sealed class PadButtonCheck
    {
        private const int FramesToJudge = 20;
        private const float Down = 0.8f;
        private const float Up = 0.05f;

        /// <summary>The bit is set in nearly all frames with the trigger down and in nearly none with it up.</summary>
        private const double Share = 0.9;

        private const int Bits = 32;

        private readonly int[] pressed = new int[Bits];
        private readonly int[] released = new int[Bits];
        private int pressedFrames;
        private int releasedFrames;

        /// <summary>True once judged; <see cref="Shift"/> then holds the verdict.</summary>
        public bool Judged { get; private set; }

        /// <summary>How far the left trigger's bit is from where it was measured; null when no bit follows the trigger.</summary>
        public int? Shift { get; private set; }

        /// <summary>One frame of the pad state; true on the frame the check settles.</summary>
        public bool Observe(uint buttons, float leftTrigger, float rightTrigger, int measuredBit)
        {
            if (this.Judged)
            {
                return false;
            }

            var counts = leftTrigger >= Down && rightTrigger <= Up ? this.pressed : leftTrigger <= Up && rightTrigger <= Up ? this.released : null;
            if (counts == null)
            {
                return false;
            }

            if (counts == this.pressed)
            {
                this.pressedFrames++;
            }
            else
            {
                this.releasedFrames++;
            }

            for (var bit = 0; bit < Bits; bit++)
            {
                counts[bit] += (buttons & (1u << bit)) != 0 ? 1 : 0;
            }

            if (this.pressedFrames < FramesToJudge || this.releasedFrames < FramesToJudge)
            {
                return false;
            }

            this.Judged = true;
            this.Shift = OffsetSearch.Outward(measuredBit, Bits, 1).Where(bit => bit < Bits)
                .Where(bit => this.pressed[bit] >= Share * this.pressedFrames && this.released[bit] <= (1 - Share) * this.releasedFrames)
                .Select(bit => (int?)(bit - measuredBit)).FirstOrDefault();
            return true;
        }

        /// <summary>A new game process: judged again.</summary>
        public void Reset()
        {
            System.Array.Clear(this.pressed);
            System.Array.Clear(this.released);
            (this.pressedFrames, this.releasedFrames, this.Judged, this.Shift) = (0, 0, false, null);
        }
    }
}
