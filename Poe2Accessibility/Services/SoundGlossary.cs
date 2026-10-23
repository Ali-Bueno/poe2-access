namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     Every sound the mod makes, by key, for the settings menu's last group (D2R sound glossary): landing on a
    ///     sound says its name and meaning, left or right plays it. Synthesized cues are built here; the game's own
    ///     sounds come from <see cref="GameSounds"/> and are listed only when they were extracted.
    /// </summary>
    public sealed class SoundGlossary
    {
        private readonly AudioCueService audio;
        private readonly Dictionary<string, float[]> sounds = new();
        private readonly List<string> keys = new();

        public SoundGlossary(AudioCueService audio, GameSounds gameSounds)
        {
            this.audio = audio;
            var rate = AudioCueService.SampleRate;
            this.Add("beacon", CueSynth.Ping(rate));
            this.Add("dodge", CueSynth.Whoosh(rate));
            this.Add("crosshair", CueSynth.Crosshair(rate));
            this.Add("heartbeat", CueSynth.Heartbeat(rate));
            this.Add("enemy", CueSynth.Enemy(CueSynth.Layer.None, rate));
            this.Add("elite", CueSynth.Enemy(CueSynth.Layer.Elite, rate));
            this.Add("boss", CueSynth.Enemy(CueSynth.Layer.Boss, rate));
            this.Add("friend", CueSynth.Enemy(CueSynth.Layer.Friend, rate));
            this.Add("radar_wall", RadarSynth.Cue(RadarReading.Wall, RadarSynth.Direction.Left, rate));
            this.Add("radar_open", RadarSynth.Cue(RadarReading.Open, RadarSynth.Direction.Left, rate));
            this.Add("radar_exit", RadarSynth.Cue(RadarReading.Exit, RadarSynth.Direction.Left, rate));
            this.Add("bump", RadarSynth.Bump(rate));
            foreach (var game in new[] { "item", "item_rare", "currency", "gold", "quest_item", "chest", "shrine", "exit", "portal", "waypoint", "checkpoint" })
            {
                if (gameSounds.Get(game) is { } samples)
                {
                    this.Add(game, samples);
                }
            }
        }

        /// <summary>Sound keys in menu order.</summary>
        public IReadOnlyList<string> Keys => this.keys;

        public void Play(string key)
        {
            if (this.sounds.TryGetValue(key, out var samples))
            {
                this.audio.Play(samples, 0f, 1f, 1f);
            }
        }

        private void Add(string key, float[] samples)
        {
            this.sounds[key] = samples;
            this.keys.Add(key);
        }
    }
}
