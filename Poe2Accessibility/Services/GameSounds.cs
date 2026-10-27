namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    ///     The game's own sound effects, extracted on the player's machine by <c>tools/Poe2UiExtract sounds</c> into the
    ///     plugin's <c>sounds/</c> folder (never shipped). 16-bit PCM WAV at the mixer rate, folded to mono so the mixer
    ///     can pan them; each is cut to a short cue with a fade so repeats stay clear. The idle beds in <c>loops/</c> are
    ///     kept whole and brought to one loudness.
    /// </summary>
    public sealed class GameSounds
    {
        private const string LoopFolder = "loops";

        /// <summary>The extractor's cap on a loop's length.</summary>
        private const float LoopMaxSeconds = 20f;

        /// <summary>Every loop at this RMS (about -26 dBFS), under the cues: a bed that never stops must not cover them.</summary>
        private const float LoopRms = 0.05f;

        /// <summary>Nor any peak above half scale: a crackling loop brought to that RMS would stand out in bursts.</summary>
        private const float LoopPeak = 0.5f;

        private readonly Dictionary<string, float[]> sounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float[]> loops = new(StringComparer.OrdinalIgnoreCase);

        private GameSounds()
        {
        }

        public int Count => this.sounds.Count;

        /// <summary>Loads every <c>name.wav</c> of <paramref name="directory"/>; a missing folder gives an empty set.</summary>
        public static GameSounds Load(string directory, float maxSeconds, PluginLog log)
        {
            var set = new GameSounds();
            if (!Directory.Exists(directory))
            {
                log.Info($"No game sounds in {directory} (run tools/Poe2UiExtract sounds); synthesized cues only");
                return set;
            }

            LoadFolder(directory, maxSeconds, true, set.sounds, log);
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

        /// <summary>Mono samples of a 16-bit PCM WAV at the mixer rate; null for any other format.</summary>
        private static float[]? ReadWav(byte[] data, float maxSeconds, bool fadeOut)
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
                    var samples = new float[frames];
                    for (var i = 0; i < frames; i++)
                    {
                        var sum = 0f;
                        for (var c = 0; c < channels; c++)
                        {
                            sum += BitConverter.ToInt16(data, body + (((i * channels) + c) * 2)) / 32768f;
                        }

                        var tail = frames - i;
                        samples[i] = sum / channels * (tail < fade ? (float)tail / fade : 1f);
                    }

                    return samples;
                }

                offset = body + size + (size & 1);
            }

            return null;
        }
    }
}
