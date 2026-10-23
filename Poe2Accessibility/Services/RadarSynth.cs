namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Radar cues (mono, D2R radar_synth.cpp recipes): struck tones for an exit (tin) and a wall (tock), a
    ///     filtered-noise breath for open floor that rises ahead, falls behind and holds to the sides. Below the player
    ///     the struck tones drop to <see cref="DownPitch"/>.
    /// </summary>
    public static class RadarSynth
    {
        public const float CueLevel = 0.48f;
        public const float DownPitch = 0.86f;

        private const float StruckSeconds = 0.12f;
        private const float StruckAttackSeconds = 0.003f;
        private const float StruckReleaseSeconds = 0.015f;
        private const float HarmonicLevel = 0.2f;

        private const float ExitHz = 1100f;
        private const float ExitHarmonic = 2.7f;
        private const float ExitDecaySeconds = 0.045f;

        /// <summary>Small obstacle (D2R): a hollow knock between the tock and the tin, quick decay, inharmonic partial.</summary>
        private const float ObstacleHz = 640f;
        private const float ObstacleHarmonic = 2.76f;
        private const float ObstacleDecaySeconds = 0.012f;

        private const float WallHz = 260f;
        private const float WallHarmonic = 1.5f;
        private const float WallDecaySeconds = 0.025f;

        private const float BreathSeconds = 0.18f;
        private const float BreathReleaseSeconds = 0.015f;
        private const float BreathQ = 3f;
        private const float BreathPeak = 0.7f;
        private const float AheadStartHz = 900f;
        private const float AheadEndHz = 2400f;
        private const float BehindStartHz = 600f;
        private const float BehindEndHz = 220f;
        private const float SideHz = 1200f;

        /// <summary>Wall bump: a sine falling 150 → 70 Hz over a short low-passed noise knock, 1.4× the cue level.</summary>
        private const float BumpSeconds = 0.09f;
        private const float BumpStartHz = 150f;
        private const float BumpEndHz = 70f;
        private const float BumpBodyDecaySeconds = 0.03f;
        private const float BumpNoiseDecaySeconds = 0.006f;
        private const float BumpNoiseCutoffHz = 1800f;
        private const float BumpNoiseLevel = 0.5f;
        private const float BumpAttackSeconds = 0.002f;
        private const float BumpReleaseSeconds = 0.012f;
        private const float BumpLevel = 1.4f;

        /// <summary>Wall tones (D2R `[wall_tones]`): a noise band per screen direction, loud enough under the cues.</summary>
        private const float WallToneQ = 2f;
        private const float WallToneLevel = 0.3f;
        private const float WallToneSeconds = 2f;

        /// <summary>The tail crossfaded into the head so the band loops without a seam.</summary>
        private const float WallToneSeamSeconds = 0.1f;

        /// <summary>Which way a ray points on screen.</summary>
        public enum Direction
        {
            Up,
            Left,
            Right,
            Down,
        }

        /// <summary>The wall cue when what stops the way is a small obstacle the player can step round.</summary>
        public static float[] Obstacle(Direction direction, int sampleRate) =>
            Struck(ObstacleHz * (direction == Direction.Down ? DownPitch : 1f), ObstacleHarmonic, ObstacleDecaySeconds, sampleRate);

        public static float[] Cue(RadarReading reading, Direction direction, int sampleRate)
        {
            var pitch = direction == Direction.Down ? DownPitch : 1f;
            return reading switch
            {
                RadarReading.Exit => Struck(ExitHz * pitch, ExitHarmonic, ExitDecaySeconds, sampleRate),
                RadarReading.Wall => Struck(WallHz * pitch, WallHarmonic, WallDecaySeconds, sampleRate),
                RadarReading.Open => direction switch
                {
                    Direction.Up => Breath(AheadStartHz, AheadEndHz, sampleRate),
                    Direction.Down => Breath(BehindStartHz, BehindEndHz, sampleRate),
                    _ => Breath(SideHz, SideHz, sampleRate),
                },
                _ => Array.Empty<float>(),
            };
        }

        public static float[] Bump(int sampleRate)
        {
            var samples = new float[(int)(BumpSeconds * sampleRate)];
            uint seed = 7;
            var low = 0f;
            var smoothing = 1f - MathF.Exp(-2 * MathF.PI * BumpNoiseCutoffHz / sampleRate);
            var phase = 0.0;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                phase += BumpStartHz * MathF.Pow(BumpEndHz / BumpStartHz, t / BumpSeconds) / sampleRate;
                seed = (seed * 1664525) + 1013904223;
                low += smoothing * ((((seed >> 8) / 8388608f) - 1f) - low);
                var body = MathF.Sin((float)(2 * Math.PI * phase)) * MathF.Exp(-t / BumpBodyDecaySeconds);
                var knock = BumpNoiseLevel * low * MathF.Exp(-t / BumpNoiseDecaySeconds);
                var gate = MathF.Min(1f, t / BumpAttackSeconds) * MathF.Min(1f, (BumpSeconds - t) / BumpReleaseSeconds);
                samples[i] = CueLevel * BumpLevel * gate * (body + knock);
            }

            return samples;
        }

        /// <summary>Stereo position: left and right rays pan to their side, up and down stay centred.</summary>
        public static float Pan(Direction direction) => direction switch
        {
            Direction.Left => -1f,
            Direction.Right => 1f,
            _ => 0f,
        };

        private static float[] Struck(float hz, float harmonic, float decaySeconds, int sampleRate)
        {
            var samples = new float[(int)(StruckSeconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var envelope = MathF.Min(1f, t / StruckAttackSeconds) * MathF.Min(1f, (StruckSeconds - t) / StruckReleaseSeconds) * MathF.Exp(-t / decaySeconds);
                samples[i] = CueLevel * envelope * (MathF.Sin(2 * MathF.PI * hz * t) + (HarmonicLevel * MathF.Sin(2 * MathF.PI * hz * harmonic * t)));
            }

            return samples;
        }

        /// <summary>D2R bands: high ahead (up), middle to the sides, low behind (down).</summary>
        public static float WallToneHz(Direction direction) => direction switch
        {
            Direction.Up => 800f,
            Direction.Down => 250f,
            _ => 450f,
        };

        /// <summary>A wall tone's level: (1 - depth / range)², nothing once the open way reaches the range.</summary>
        public static float WallToneGain(float depthCells, float rangeCells)
        {
            if (rangeCells <= 0 || depthCells >= rangeCells - 1)
            {
                return 0f;
            }

            var closeness = 1f - (Math.Max(depthCells, 0f) / rangeCells);
            return closeness * closeness;
        }

        /// <summary>White noise through a fixed band-pass, looping: the last <see cref="WallToneSeamSeconds"/> fade into the first.</summary>
        public static float[] WallTone(Direction direction, int sampleRate)
        {
            var length = (int)(WallToneSeconds * sampleRate);
            var seam = (int)(WallToneSeamSeconds * sampleRate);
            var raw = new float[length + seam];
            uint seed = (uint)direction + 7;
            float low = 0, band = 0;
            var tuning = 2 * MathF.Sin(MathF.PI * WallToneHz(direction) / sampleRate);
            var peak = 0f;
            for (var i = 0; i < raw.Length; i++)
            {
                seed = (seed * 1664525) + 1013904223;
                var noise = ((seed >> 8) / 8388608f) - 1f;
                var high = noise - low - (band / WallToneQ);
                band += tuning * high;
                low += tuning * band;
                raw[i] = band;
                peak = MathF.Max(peak, MathF.Abs(band));
            }

            var scale = peak > 0 ? WallToneLevel / peak : 0f;
            var loop = new float[length];
            for (var i = 0; i < length; i++)
            {
                loop[i] = raw[i] * scale;
            }

            for (var i = 0; i < seam; i++)
            {
                // After the loop's last sample comes raw[length]: the head starts there and blends into its own.
                var t = (float)i / seam;
                loop[i] = (loop[i] * t) + (raw[length + i] * scale * (1f - t));
            }

            return loop;
        }

        /// <summary>White noise through a state-variable band-pass whose centre glides exponentially.</summary>
        private static float[] Breath(float startHz, float endHz, int sampleRate)
        {
            var samples = new float[(int)(BreathSeconds * sampleRate)];
            uint seed = 1;
            float low = 0, band = 0;
            var peak = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                seed = (seed * 1664525) + 1013904223;
                var noise = ((seed >> 8) / 8388608f) - 1f;
                var centre = startHz * MathF.Pow(endHz / startHz, t / BreathSeconds);
                var tuning = 2 * MathF.Sin(MathF.PI * centre / sampleRate);
                var high = noise - low - (band / BreathQ);
                band += tuning * high;
                low += tuning * band;
                var envelope = MathF.Pow(MathF.Sin(MathF.PI * t / BreathSeconds), 2) * MathF.Min(1f, (BreathSeconds - t) / BreathReleaseSeconds);
                samples[i] = band * envelope;
                peak = MathF.Max(peak, MathF.Abs(samples[i]));
            }

            if (peak > 0)
            {
                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] *= CueLevel * BreathPeak / peak;
                }
            }

            return samples;
        }
    }
}
