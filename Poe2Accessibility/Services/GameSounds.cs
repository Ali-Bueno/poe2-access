namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    ///     The game's own sound effects, extracted on the player's machine by <c>tools/Poe2UiExtract sounds</c> into the
    ///     plugin's <c>sounds/</c> folder (never shipped). 16-bit PCM WAV at the mixer rate, folded to mono so the mixer
    ///     can pan them; each is cut to a short cue with a fade so repeats stay clear. The idle beds in <c>loops/</c> are
    ///     kept whole and brought to one loudness; the breakables' sounds in <c>breakables/</c> (one per material, got as
    ///     <c>breakables/rock</c>) are dulled and brought low, since many play at once; a searched body's is brought up
    ///     near a chest's, its sample being far quieter. A cue the player's own news plays unpanned is also read with its two
    ///     channels on first use (<see cref="GetStereo"/>).
    /// </summary>
    public sealed class GameSounds
    {
        private const string LoopFolder = "loops";

        public const string BreakableFolder = "breakables";

        /// <summary>The one breakable sound of older extractions, for a material without its own.</summary>
        public const string BreakableFallback = "breakable";

        /// <summary>A body searched for loot: the game's own rummage through a corpse (Corpses/Open_04).</summary>
        public const string Corpse = "corpse";

        /// <summary>The body's sound at most at this RMS and peak (about -20 dBFS): the sample sits 11 dB under the chest's.</summary>
        private const float CorpseRms = 0.1f;
        private const float CorpsePeak = 0.8f;

        /// <summary>The extractor's cap on a loop's length.</summary>
        private const float LoopMaxSeconds = 20f;

        /// <summary>Every loop at this RMS (about -26 dBFS), under the cues: a bed that never stops must not cover them.</summary>
        private const float LoopRms = 0.05f;

        /// <summary>Nor any peak above half scale: a crackling loop brought to that RMS would stand out in bursts.</summary>
        private const float LoopPeak = 0.5f;

        /// <summary>
        ///     Breakables at most at this peak and RMS (about -32 dBFS over the cue), well under a chest: rocks were "ultra
        ///     shrill" at the game's level (user 2026-10-27), and every breakable in range now sounds, not only the nearest.
        /// </summary>
        private const float BreakablePeak = 0.3f;
        private const float BreakableRms = 0.025f;

        /// <summary>One-pole low-pass on the breakables, taking the crack's top edge off: the shrill part, not the material.</summary>
        private const float BreakableCutoffHz = 2800f;

        private readonly Dictionary<string, float[]> sounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float[]> loops = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, StereoSound?> stereo = new(StringComparer.OrdinalIgnoreCase);
        private readonly string directory;
        private readonly float maxSeconds;

        private GameSounds(string directory, float maxSeconds)
        {
            this.directory = directory;
            this.maxSeconds = maxSeconds;
        }

        public int Count => this.sounds.Count;

        /// <summary>Loads every <c>name.wav</c> of <paramref name="directory"/>; a missing folder gives an empty set.</summary>
        public static GameSounds Load(string directory, float maxSeconds, PluginLog log)
        {
            var set = new GameSounds(directory, maxSeconds);
            if (!Directory.Exists(directory))
            {
                log.Info($"No game sounds in {directory} (run tools/Poe2UiExtract sounds); synthesized cues only");
                return set;
            }

            LoadFolder(directory, maxSeconds, true, set.sounds, log);
            var breakableFolder = Path.Join(directory, BreakableFolder);
            var breakables = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(breakableFolder))
            {
                LoadFolder(breakableFolder, maxSeconds, true, breakables, log);
            }

            if (set.sounds.Remove(BreakableFallback, out var fallback))
            {
                breakables[string.Empty] = fallback;
            }

            foreach (var (name, samples) in breakables)
            {
                LowPass(samples, BreakableCutoffHz);
                Normalize(samples, BreakableRms, BreakablePeak);
                set.sounds[name.Length > 0 ? $"{BreakableFolder}/{name}" : BreakableFallback] = samples;
            }

            if (set.sounds.TryGetValue(Corpse, out var corpse))
            {
                Normalize(corpse, CorpseRms, CorpsePeak);
            }

            var loopFolder = Path.Join(directory, LoopFolder);
            if (Directory.Exists(loopFolder))
            {
                LoadFolder(loopFolder, LoopMaxSeconds, false, set.loops, log);
                foreach (var loop in set.loops.Values)
                {
                    Normalize(loop, LoopRms, LoopPeak);
                }
            }

            log.Info($"Game sounds loaded: {string.Join(", ", set.sounds.Keys)}; loops: {string.Join(", ", set.loops.Keys)}");
            return set;
        }

        /// <summary>The cue, or null when it was not extracted.</summary>
        public float[]? Get(string name) => this.sounds.TryGetValue(name, out var samples) ? samples : null;

        /// <summary>The cue with both its channels (a mono file on both sides), read once; null when it was not extracted.</summary>
        public StereoSound? GetStereo(string name)
        {
            lock (this.stereo)
            {
                if (!this.stereo.TryGetValue(name, out var sound))
                {
                    sound = this.stereo[name] = this.sounds.ContainsKey(name) && this.ReadFile(name) is { } channels
                        ? new StereoSound(channels[0], channels[^1])
                        : null;
                }

                return sound;
            }
        }

        /// <summary>The idle bed of a kind of place (portal, exit, waypoint, checkpoint), or null when it was not extracted.</summary>
        public float[]? GetLoop(string name) => this.loops.TryGetValue(name, out var samples) ? samples : null;

        private static void LoadFolder(string folder, float maxSeconds, bool fadeOut, Dictionary<string, float[]> into, PluginLog log)
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.wav"))
            {
                try
                {
                    if (ReadWav(File.ReadAllBytes(path), maxSeconds, fadeOut) is { } samples)
                    {
                        into[Path.GetFileNameWithoutExtension(path)] = samples;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    log.Error($"Game sound {path} unreadable", ex);
                }
            }
        }

        private static void LowPass(float[] samples, float cutoffHz)
        {
            var alpha = 1f - MathF.Exp(-2f * MathF.PI * cutoffHz / AudioCueService.SampleRate);
            var state = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                state += alpha * (samples[i] - state);
                samples[i] = state;
            }
        }

        private static void Normalize(float[] samples, float rms, float peak)
        {
            var sum = 0.0;
            var highest = 0f;
            foreach (var sample in samples)
            {
                sum += sample * sample;
                highest = Math.Max(highest, Math.Abs(sample));
            }

            var current = samples.Length > 0 ? (float)Math.Sqrt(sum / samples.Length) : 0f;
            if (current > 0f)
            {
                var scale = Math.Min(rms / current, peak / highest);
                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] *= scale;
                }
            }
        }

        private float[][]? ReadFile(string name)
        {
            try
            {
                return ReadChannels(File.ReadAllBytes(Path.Join(this.directory, name + ".wav")), this.maxSeconds, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>Mono samples of a 16-bit PCM WAV at the mixer rate; null for any other format.</summary>
        private static float[]? ReadWav(byte[] data, float maxSeconds, bool fadeOut)
        {
            if (ReadChannels(data, maxSeconds, fadeOut) is not { } channels)
            {
                return null;
            }

            if (channels.Length == 1)
            {
                return channels[0];
            }

            var mono = new float[channels[0].Length];
            for (var i = 0; i < mono.Length; i++)
            {
                mono[i] = (channels[0][i] + channels[1][i]) / 2f;
            }

            return mono;
        }

        /// <summary>Each channel's samples of a 16-bit PCM WAV at the mixer rate (one or two); null for any other format.</summary>
        private static float[][]? ReadChannels(byte[] data, float maxSeconds, bool fadeOut)
        {
            if (data.Length < 44 || BitConverter.ToUInt32(data, 0) != 0x46464952 || BitConverter.ToUInt32(data, 8) != 0x45564157)
            {
                return null; // not "RIFF....WAVE"
            }

            int channels = 0, rate = 0, bits = 0, offset = 12;
            while (offset + 8 <= data.Length)
            {
                var id = BitConverter.ToUInt32(data, offset);
                var size = BitConverter.ToInt32(data, offset + 4);
                var body = offset + 8;
                if (id == 0x20746D66)
                {
                    // "fmt "
                    channels = BitConverter.ToInt16(data, body + 2);
                    rate = BitConverter.ToInt32(data, body + 4);
                    bits = BitConverter.ToInt16(data, body + 14);
                }
                else if (id == 0x61746164)
                {
                    // "data"
                    if (channels is < 1 or > 2 || bits != 16 || rate != AudioCueService.SampleRate)
                    {
                        return null;
                    }

                    var frames = Math.Min(Math.Min(size, data.Length - body) / (2 * channels), (int)(maxSeconds * rate));
                    var fade = fadeOut ? Math.Min(frames, rate / 20) : 0;
                    var samples = new float[channels][];
                    for (var c = 0; c < channels; c++)
                    {
                        samples[c] = new float[frames];
                        for (var i = 0; i < frames; i++)
                        {
                            var tail = frames - i;
                            samples[c][i] = BitConverter.ToInt16(data, body + (((i * channels) + c) * 2)) / 32768f * (tail < fade ? (float)tail / fade : 1f);
                        }
                    }

                    return samples;
                }

                offset = body + size + (size & 1);
            }

            return null;
        }
    }
}
