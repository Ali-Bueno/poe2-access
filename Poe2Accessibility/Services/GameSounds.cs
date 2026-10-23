namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    ///     The game's own sound effects, extracted on the player's machine by <c>tools/Poe2UiExtract sounds</c> into the
    ///     plugin's <c>sounds/</c> folder (never shipped). 16-bit PCM WAV at the mixer rate, folded to mono so the mixer
    ///     can pan them; each is cut to a short cue with a fade so repeats stay clear.
    /// </summary>
    public sealed class GameSounds
    {
        private readonly Dictionary<string, float[]> sounds = new(StringComparer.OrdinalIgnoreCase);

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

            foreach (var path in Directory.EnumerateFiles(directory, "*.wav"))
            {
                try
                {
                    if (ReadWav(File.ReadAllBytes(path), maxSeconds) is { } samples)
                    {
                        set.sounds[Path.GetFileNameWithoutExtension(path)] = samples;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    log.Error($"Game sound {path} unreadable", ex);
                }
            }

            log.Info($"Game sounds loaded: {string.Join(", ", set.sounds.Keys)}");
            return set;
        }

        /// <summary>The cue, or null when it was not extracted.</summary>
        public float[]? Get(string name) => this.sounds.TryGetValue(name, out var samples) ? samples : null;

        /// <summary>Mono samples of a 16-bit PCM WAV at the mixer rate; null for any other format.</summary>
        private static float[]? ReadWav(byte[] data, float maxSeconds)
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
                    var fade = Math.Min(frames, rate / 20);
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
