namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The closed barrier's cue of <see cref="CueSynth"/>: a way the story keeps shut (the Matlan Waterways' water, a beam,
    ///     a gate), heard in a loop where it stands so the player stops walking into it. The owner's pick of four (2026-11-19,
    ///     audition page https://claude.ai/artifact/Wufj2EggLMhkoAmC7YwcD9): a heavy gate that does not give, "el último".
    /// </summary>
    public static partial class CueSynth
    {
        public const string BarrierSound = "closed_barrier";

        /// <summary>2 dB over the first 0.6 at the owner's word (2026-11-19: "está muy bajo, pero no tanto").</summary>
        private const float BarrierLevel = 0.75f;

        /// <summary>One turn of the loop lasts this long: the two blows, then quiet.</summary>
        private const float BarrierLoopSeconds = 1.6f;

        /// <summary>Two very low, dulled blows, the second a little lower: pushing a heavy gate that does not give.</summary>
        public static float[] Barrier(int sampleRate) =>
            Normalized(Room(Damped(Sum(Modal(70f, new[] { 1f, 1.5f, 2.3f }, new[] { 1f, 0.5f, 0.25f }, 0.12f, 0.45f, sampleRate),
                Delayed(Modal(62f, new[] { 1f, 1.5f, 2.3f }, new[] { 0.9f, 0.45f, 0.2f }, 0.12f, 0.45f, sampleRate), 0.22f, sampleRate)), 900f, sampleRate), sampleRate, 0.35f), BarrierLevel);

        /// <summary>The blows and their quiet, to loop while the barrier stays shut (as the opened way's sound repeats).</summary>
        public static float[] BarrierLoop(int sampleRate)
        {
            var blows = Barrier(sampleRate);
            var loop = new float[Math.Max(blows.Length, (int)(BarrierLoopSeconds * sampleRate))];
            Array.Copy(blows, loop, blows.Length);
            return loop;
        }

        /// <summary>The sound started <paramref name="seconds"/> late, its length grown to hold it.</summary>
        private static float[] Delayed(float[] sound, float seconds, int sampleRate)
        {
            var shift = (int)(seconds * sampleRate);
            var samples = new float[sound.Length + shift];
            Array.Copy(sound, 0, samples, shift, sound.Length);
            return samples;
        }
    }
}
