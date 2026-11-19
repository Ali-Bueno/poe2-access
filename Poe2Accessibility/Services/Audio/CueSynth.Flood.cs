namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The pulse of a boss arena's flood (Mektul's molten gold), from its nearest edge and faster as it nears: short, low
    ///     and falling, apart from the ground alarm's grinding stabs, the shelter's bright chord and the heartbeat. Candidates
    ///     until the owner picks one by ear.
    /// </summary>
    public static partial class CueSynth
    {
        private const float FloodLevel = 0.8f;
        private const int FloodSeed = 4141;

        /// <summary>The flood's pulse as played.</summary>
        public static float[] Flood(int sampleRate) => FloodBubble(sampleRate);

        /// <summary>A: a bubble of lava bursting, a low pop falling 150 to 60 Hz with a little sizzle after it, 0.28 s.</summary>
        public static float[] FloodBubble(int sampleRate)
        {
            var samples = new float[(int)(0.28f * sampleRate)];
            var random = new Random(FloodSeed);
            var phase = 0f;
            var hiss = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var hz = 60f + (90f * MathF.Exp(-t / 0.04f));
                phase += 2f * MathF.PI * hz / sampleRate;
                var pop = MathF.Sin(phase) * MathF.Min(1f, t / 0.004f) * MathF.Exp(-t / 0.07f);
                hiss += 0.25f * ((float)((random.NextDouble() * 2) - 1) - hiss);
                var sizzle = hiss * 0.35f * MathF.Exp(-MathF.Abs(t - 0.06f) / 0.05f);
                samples[i] = pop + sizzle;
            }

            return Normalized(samples, FloodLevel);
        }

        /// <summary>B: molten crackle, sparse sharp crackles over a low rumble at 55 Hz, 0.3 s.</summary>
        public static float[] FloodCrackle(int sampleRate)
        {
            var samples = new float[(int)(0.3f * sampleRate)];
            var random = new Random(FloodSeed);
            var crackle = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var fade = MathF.Min(1f, t / 0.01f) * MathF.Exp(-t / 0.11f);
                var rumble = 0.6f * MathF.Sin(2f * MathF.PI * 55f * t) + (0.3f * MathF.Sin(2f * MathF.PI * 110f * t));
                if (random.NextDouble() < 0.004)
                {
                    crackle = (float)((random.NextDouble() * 2) - 1);
                }

                crackle *= 0.93f;
                samples[i] = fade * (rumble + (1.4f * crackle));
            }

            return Normalized(samples, FloodLevel);
        }

        /// <summary>C: a muffled thump of magma, 50 and 100 Hz falling slightly under dull noise, 0.25 s.</summary>
        public static float[] FloodThump(int sampleRate)
        {
            var samples = new float[(int)(0.25f * sampleRate)];
            var random = new Random(FloodSeed);
            var phase = 0f;
            var dull = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                phase += 2f * MathF.PI * (50f + (15f * MathF.Exp(-t / 0.03f))) / sampleRate;
                var body = (MathF.Sin(phase) + (0.5f * MathF.Sin(2f * phase))) * MathF.Min(1f, t / 0.003f) * MathF.Exp(-t / 0.08f);
                dull += 0.05f * ((float)((random.NextDouble() * 2) - 1) - dull);
                samples[i] = body + (3f * dull * MathF.Exp(-t / 0.05f));
            }

            return Normalized(samples, FloodLevel);
        }

        /// <summary>D: steam off the gold, a hiss swelling in and dying over a low hum at 70 Hz, 0.3 s.</summary>
        public static float[] FloodHiss(int sampleRate)
        {
            var samples = new float[(int)(0.3f * sampleRate)];
            var random = new Random(FloodSeed);
            var (low, previous) = (0f, 0f);
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var noise = (float)((random.NextDouble() * 2) - 1);
                low += 0.35f * (noise - low);
                var band = low - previous;
                previous = low;
                var envelope = MathF.Min(1f, t / 0.03f) * MathF.Exp(-t / 0.09f);
                samples[i] = envelope * ((1.6f * band) + (0.4f * MathF.Sin(2f * MathF.PI * 70f * t)));
            }

            return Normalized(samples, FloodLevel);
        }
    }
}
