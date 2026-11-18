namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>The minimap arrow's loop of <see cref="CueSynth"/> (docs/research/game-audio.md "The minimap's arrow").</summary>
    public static partial class CueSynth
    {
        /// <summary>
        ///     The halo, the owner's pick (2026-11-18): the quest sound's register (Item_Quest is strongest at 830 and 1040 Hz), two
        ///     pairs of tones a few hertz apart beating slowly under a light tremolo, a faint shimmer at the quest sound's 2.5 and
        ///     3.6 kHz bands swelling over it. Every frequency makes whole cycles in the loop, so it repeats with no seam.
        /// </summary>
        private const float HaloSeconds = 4f;
        private const float HaloLevel = 0.35f;
        private static readonly float[] HaloHz = { 830f, 832.5f, 1040f, 1043f };
        private static readonly float[] HaloAmplitudes = { 1f, 0.8f, 0.7f, 0.5f };
        private const float HaloTremoloHz = 6f;
        private const float HaloTremoloDepth = 0.25f;
        private static readonly float[] HaloShimmerHz = { 2490f, 3600f };
        private static readonly float[] HaloShimmerAmplitudes = { 0.12f, 0.08f };
        private static readonly float[] HaloShimmerSwellHz = { 1.5f, 1.25f };

        public static float[] Halo(int sampleRate)
        {
            var samples = new float[(int)(HaloSeconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var body = 0f;
                for (var p = 0; p < HaloHz.Length; p++)
                {
                    body += HaloAmplitudes[p] * LoopTone(HaloHz[p], t);
                }

                var shimmer = 0f;
                for (var p = 0; p < HaloShimmerHz.Length; p++)
                {
                    shimmer += HaloShimmerAmplitudes[p] * LoopTone(HaloShimmerHz[p], t) * (0.5f + (0.5f * LoopTone(HaloShimmerSwellHz[p], t)));
                }

                samples[i] = (body * (1f - HaloTremoloDepth + (HaloTremoloDepth * LoopTone(HaloTremoloHz, t)))) + shimmer;
            }

            return Normalized(samples, HaloLevel);
        }

        /// <summary>A sine at the nearest frequency that fits whole cycles in the halo's loop.</summary>
        private static float LoopTone(float hz, float t) => MathF.Sin(2 * MathF.PI * (MathF.Round(hz * HaloSeconds) / HaloSeconds) * t);
    }
}
