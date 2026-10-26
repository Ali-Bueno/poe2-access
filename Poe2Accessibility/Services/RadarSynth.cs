namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     Radar cues (mono, D2R radar_synth.cpp recipes): struck tones for an exit (tin) and a wall (tock), a
    ///     filtered-noise breath for open floor that rises ahead, falls behind and holds to the sides. Below the player
    ///     the struck tones drop to <see cref="DownPitch"/>. The second local player's radar has its own sounds (<see cref="Voice"/>):
    ///     plucked strings for the strikes, a whistle for the breath, so the two radars are told apart by timbre.
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

        /// <summary>Second voice: plucked strings in the same order, low wall, middle obstacle, high exit.</summary>
        private const float PluckedWallHz = 196f;
        private const float PluckedWallDecaySeconds = 0.035f;
        private const float PluckedObstacleHz = 494f;
        private const float PluckedObstacleDecaySeconds = 0.015f;
        private const float PluckedExitHz = 1320f;
        private const float PluckedExitDecaySeconds = 0.09f;

        /// <summary>A second-voice cue matches the loudness (RMS) of the first voice's cue it stands for, up to this peak.</summary>
        private const float SecondVoiceMaxPeak = 0.8f;

        /// <summary>The pluck's burst is low-passed twice at this many times its pitch: a soft finger, not a pick.</summary>
        private const float PluckBrightness = 5f;

        /// <summary>Second voice's open way: a whistle rising an octave ahead, falling one behind, level to the sides.</summary>
        private const float WhistleOctaveLevel = 0.15f;
        private const float WhistleAheadStartHz = 600f;
        private const float WhistleAheadEndHz = 1200f;
        private const float WhistleBehindStartHz = 600f;
        private const float WhistleBehindEndHz = 300f;
        private const float WhistleSideHz = 800f;

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

        /// <summary>Wall tones (D2R `[wall_tones]`): two band-pass stages at this Q, a band about half an octave wide.</summary>
        private const double WallToneQ = 1.8;

        /// <summary>A one-pole low-pass rolls off what the band-pass skirts let through above the bands (warmth).</summary>
        private const double WallToneWarmthHz = 1800;

        /// <summary>RMS of one direction at a touching wall (D2R ContactRms, -28 dBFS): a soft bed under the cues.</summary>
        private const float WallToneRms = 0.04f;

        /// <summary>The low band behind the player is lifted this much (Grimdark's walltones, by ear).</summary>
        private const float BehindTrimDb = 3f;

        /// <summary>Loop length, a power of two for the FFT: ~3 s, long enough not to read as a pulse.</summary>
        private const int WallToneSamples = 1 << 17;

        private const double NegligibleMagnitude = 1e-9;

        /// <summary>Whose radar: the second local (couch co-op) player's plucks and whistles.</summary>
        public enum Voice
        {
            First,
            Second,
        }

        /// <summary>Which way a ray points on screen.</summary>
        public enum Direction
        {
            Up,
            Left,
            Right,
            Down,
        }

        /// <summary>The wall cue when what stops the way is a small obstacle the player can step round.</summary>
        public static float[] Obstacle(Direction direction, int sampleRate, Voice voice = Voice.First)
        {
            var pitch = direction == Direction.Down ? DownPitch : 1f;
            var struck = Struck(ObstacleHz * pitch, ObstacleHarmonic, ObstacleDecaySeconds, sampleRate);
            return voice == Voice.First ? struck : LoudAs(Plucked(PluckedObstacleHz * pitch, PluckedObstacleDecaySeconds, sampleRate), struck);
        }

        public static float[] Cue(RadarReading reading, Direction direction, int sampleRate, Voice voice = Voice.First)
        {
            var pitch = direction == Direction.Down ? DownPitch : 1f;
            var first = reading switch
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
            if (voice == Voice.First || first.Length == 0)
            {
                return first;
            }

            return LoudAs(
                reading switch
                {
                    RadarReading.Exit => Plucked(PluckedExitHz * pitch, PluckedExitDecaySeconds, sampleRate),
                    RadarReading.Wall => Plucked(PluckedWallHz * pitch, PluckedWallDecaySeconds, sampleRate),
                    _ => direction switch
                    {
                        Direction.Up => Whistle(WhistleAheadStartHz, WhistleAheadEndHz, sampleRate),
                        Direction.Down => Whistle(WhistleBehindStartHz, WhistleBehindEndHz, sampleRate),
                        _ => Whistle(WhistleSideHz, WhistleSideHz, sampleRate),
                    },
                },
                first);
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

        /// <summary>
        ///     Karplus-Strong: a softened noise burst one period long circulates and is averaged on every pass, so the
        ///     pluck starts bright and mellows; the struck tones' length and release.
        /// </summary>
        private static float[] Plucked(float hz, float decaySeconds, int sampleRate)
        {
            var line = new float[Math.Max(2, (int)MathF.Round((sampleRate / hz) - 0.5f))];
            uint seed = 3;
            for (var i = 0; i < line.Length; i++)
            {
                seed = (seed * 1664525) + 1013904223;
                line[i] = ((seed >> 8) / 8388608f) - 1f;
            }

            var smoothing = 1f - MathF.Exp(-2 * MathF.PI * PluckBrightness * hz / sampleRate);
            for (var pass = 0; pass < 2; pass++)
            {
                var low = line[^1]; // the line is one period: start from its end
                for (var i = 0; i < line.Length; i++)
                {
                    low += smoothing * (line[i] - low);
                    line[i] = low;
                }
            }

            var mean = line.Average();
            for (var i = 0; i < line.Length; i++)
            {
                line[i] -= mean; // the averaging would keep an offset for ever
            }

            var samples = new float[(int)(StruckSeconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var k = i % line.Length;
                var value = line[k];
                line[k] = 0.5f * (value + line[(k + 1) % line.Length]);
                samples[i] = value * MathF.Exp(-t / decaySeconds) * MathF.Min(1f, (StruckSeconds - t) / StruckReleaseSeconds);
            }

            return samples;
        }

        /// <summary>A sine with a soft octave, gliding exponentially under the breath's swell and length.</summary>
        private static float[] Whistle(float startHz, float endHz, int sampleRate)
        {
            var samples = new float[(int)(BreathSeconds * sampleRate)];
            var phase = 0.0;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                phase += startHz * MathF.Pow(endHz / startHz, t / BreathSeconds) / sampleRate;
                var angle = (float)(2 * Math.PI * phase);
                var envelope = MathF.Pow(MathF.Sin(MathF.PI * t / BreathSeconds), 2) * MathF.Min(1f, (BreathSeconds - t) / BreathReleaseSeconds);
                samples[i] = envelope * (MathF.Sin(angle) + (WhistleOctaveLevel * MathF.Sin(2 * angle)));
            }

            return samples;
        }

        private static float[] LoudAs(float[] sound, float[] reference)
        {
            static float Rms(float[] samples) => MathF.Sqrt(samples.Sum(sample => sample * sample) / samples.Length);
            var peak = sound.Max(MathF.Abs);
            Scale(sound, peak > 0 ? MathF.Min(Rms(reference) / Rms(sound), SecondVoiceMaxPeak / peak) : 0f);
            return sound;
        }

        private static void Scale(float[] samples, float factor)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] *= factor;
            }
        }

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

        /// <summary>
        ///     One seamless loop of band-limited noise (D2R wall_tones.cpp): a random-phase spectrum shaped like two
        ///     band-pass stages and a warm low-pass, inverse FFT, scaled to <see cref="WallToneRms"/> (behind, lifted).
        /// </summary>
        public static float[] WallTone(Direction direction, int sampleRate)
        {
            const int n = WallToneSamples;
            var spectrum = new Complex[n];
            var state = 0x9E3779B9u * ((uint)direction + 1);
            for (var k = 1; k < n / 2; k++)
            {
                var magnitude = WallToneMagnitude(WallToneHz(direction), (double)k * sampleRate / n, sampleRate);
                if (magnitude < NegligibleMagnitude)
                {
                    continue;
                }

                state ^= state << 13; // xorshift32
                state ^= state >> 17;
                state ^= state << 5;
                spectrum[k] = Complex.FromPolarCoordinates(magnitude, 2 * Math.PI * (state / 4294967296.0));
                spectrum[n - k] = Complex.Conjugate(spectrum[k]);
            }

            InverseFft(spectrum);
            var energy = 0.0;
            foreach (var value in spectrum)
            {
                energy += value.Real * value.Real;
            }

            var level = WallToneRms * (direction == Direction.Down ? MathF.Pow(10f, BehindTrimDb / 20f) : 1f);
            var scale = energy > 0 ? level / Math.Sqrt(energy / n) : 0;
            var loop = new float[n];
            for (var i = 0; i < n; i++)
            {
                loop[i] = (float)(spectrum[i].Real * scale);
            }

            return loop;
        }

        /// <summary>The wall tone's filter chain at <paramref name="hz"/>: an RBJ band-pass (0 dB peak) twice, then the low-pass.</summary>
        private static double WallToneMagnitude(double centreHz, double hz, int sampleRate)
        {
            var w0 = 2 * Math.PI * centreHz / sampleRate;
            var alpha = Math.Sin(w0) / (2 * WallToneQ);
            var a0 = 1 + alpha;
            var w = 2 * Math.PI * hz / sampleRate;
            var z1 = Complex.FromPolarCoordinates(1, -w);
            var z2 = Complex.FromPolarCoordinates(1, -2 * w);
            var band = Complex.Abs(alpha / a0 * (1 - z2) / (1 + (-2 * Math.Cos(w0) / a0 * z1) + ((1 - alpha) / a0 * z2)));
            return band * band / Math.Sqrt(1 + ((hz / WallToneWarmthHz) * (hz / WallToneWarmthHz)));
        }

        /// <summary>In-place iterative radix-2 inverse FFT without 1/N scaling (the loop is renormalised).</summary>
        private static void InverseFft(Complex[] data)
        {
            var n = data.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                var bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                {
                    j ^= bit;
                }

                j ^= bit;
                if (i < j)
                {
                    (data[i], data[j]) = (data[j], data[i]);
                }
            }

            for (var length = 2; length <= n; length <<= 1)
            {
                var angle = 2 * Math.PI / length;
                var step = new Complex(Math.Cos(angle), Math.Sin(angle));
                for (var start = 0; start < n; start += length)
                {
                    var twiddle = Complex.One;
                    for (var k = 0; k < length / 2; k++)
                    {
                        var even = data[start + k];
                        var odd = data[start + k + (length / 2)] * twiddle;
                        data[start + k] = even + odd;
                        data[start + k + (length / 2)] = even - odd;
                        twiddle *= step;
                    }
                }
            }
        }

        /// <summary>White noise through a state-variable band-pass whose centre glides exponentially.</summary>
        private static float[] Breath(float startHz, float endHz, int sampleRate)
        {
            const float seconds = BreathSeconds;
            var samples = new float[(int)(seconds * sampleRate)];
            uint seed = 1;
            float low = 0, band = 0;
            var peak = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                seed = (seed * 1664525) + 1013904223;
                var noise = ((seed >> 8) / 8388608f) - 1f;
                var centre = startHz * MathF.Pow(endHz / startHz, t / seconds);
                var tuning = 2 * MathF.Sin(MathF.PI * centre / sampleRate);
                var high = noise - low - (band / BreathQ);
                band += tuning * high;
                low += tuning * band;
                var envelope = MathF.Pow(MathF.Sin(MathF.PI * t / seconds), 2) * MathF.Min(1f, (seconds - t) / BreathReleaseSeconds);
                samples[i] = band * envelope;
                peak = MathF.Max(peak, MathF.Abs(samples[i]));
            }

            Scale(samples, peak > 0 ? CueLevel * BreathPeak / peak : 0f);
            return samples;
        }
    }
}
