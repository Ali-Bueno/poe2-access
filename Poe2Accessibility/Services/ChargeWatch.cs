namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     What the player builds up and spends (<see cref="PlayerCharges"/>): a tick per one held as a charge, infusion or
    ///     surge is gained, a lower tick as one goes, and the count said once it settles, so a chain of Killing Palm kills
    ///     is one "3 power charges". Rage, a stat that moves with every hit, is said only as it fills and as it runs out.
    ///     Entering an area takes what is already held silently. The Combo is said and heard by switches of its own; power
    ///     charges and the Combo play recorded stereo sounds instead of ticks, the Combo a semitone higher per point.
    /// </summary>
    public sealed class ChargeWatch
    {
        private const int PollMs = 100;

        /// <summary>The count is said once it has held this long: charges come in bursts.</summary>
        private const int SettleMs = 400;

        private readonly Func<Entity?> subject;
        private readonly AudioCueService? audio;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly Dictionary<ChargeKind, int> held = new();
        private readonly List<ChargeKind> unsaid = new();
        private readonly Dictionary<(ChargeKind, int), float[]> gained = new();
        private readonly Dictionary<ChargeKind, float[]> lost = new();
        private (int Count, int Max) rage;
        private bool rageLogged;
        private bool baseline = true;
        private long changedAt;
        private long lastPoll;

        /// <summary><paramref name="audio"/> null: speech only (the partner's charges must not tick like the player's).</summary>
        public ChargeWatch(Func<Entity?> subject, AudioCueService? audio, IAccessibilityOutputService output, PluginLog log)
        {
            this.subject = subject;
            this.audio = audio;
            this.output = output;
            this.log = log;
        }

        /// <summary>Recorded sounds by key (set by the plugin): a gain with one (<see cref="PlayerCharges.RecordedGain"/>) plays it, else ticks.</summary>
        public Func<string, StereoSound?> RecordedSound { get; set; } = _ => null;

        /// <summary>What is held now, in <see cref="ChargeKind"/> order, for the vitals report (Rage apart).</summary>
        public IEnumerable<(ChargeKind Kind, int Count)> Held =>
            this.held.Where(entry => entry.Value > 0).OrderBy(entry => entry.Key).Select(entry => (entry.Key, entry.Value));

        /// <summary>Rage now and its maximum; a count of 0 for a character without Rage.</summary>
        public (int Count, int Max) Rage => this.rage;

        /// <summary>A new area or character: what is held now is not news.</summary>
        public void Reset()
        {
            this.rage = default;
            this.held.Clear();
            this.unsaid.Clear();
            this.baseline = true;
        }

        /// <summary>Per frame; <paramref name="reading"/> false while dead, paused or in a menu (nothing is said).</summary>
        public void Tick(bool reading, AccessibilitySettings settings)
        {
            var now = Environment.TickCount64;
            if (!reading || now - this.lastPoll < PollMs ||
                this.subject() is not { } entity || !entity.TryGetComponent<Buffs>(out var buffs))
            {
                return;
            }

            this.lastPoll = now;
            var counts = new Dictionary<ChargeKind, int>();
            foreach (var (id, effect) in buffs.StatusEffects)
            {
                if (PlayerCharges.FromBuffId(id) is { } kind)
                {
                    counts[kind] = counts.GetValueOrDefault(kind) + PlayerCharges.Held(effect.Charges);
                }
            }

            foreach (var kind in Enum.GetValues<ChargeKind>().Where(kind => kind != ChargeKind.Rage))
            {
                var before = this.held.GetValueOrDefault(kind);
                var after = counts.GetValueOrDefault(kind);
                if (after == before)
                {
                    continue;
                }

                this.held[kind] = after;
                if (this.baseline)
                {
                    continue;
                }

                this.log.Info($"Charges: {kind} {before} -> {after}");
                var (speech, sound) = Outputs(kind, settings);
                if (sound)
                {
                    if (after > before)
                    {
                        this.PlayGain(kind, after);
                    }
                    else
                    {
                        this.Play(this.Lost(kind), "charge_lost");
                    }
                }

                this.changedAt = now;
                if (speech && !this.unsaid.Contains(kind))
                {
                    this.unsaid.Add(kind);
                }
            }

            this.WatchRage(entity, settings);
            this.baseline = false;
            if (this.unsaid.Count > 0 && now - this.changedAt >= SettleMs)
            {
                foreach (var group in this.unsaid.GroupBy(PlayerCharges.SpeechKindOf))
                {
                    this.output.Say(group.Key, Announcements.Sentences(group.Select(kind => Announcements.Charges(kind, this.held[kind]))));
                }

                this.unsaid.Clear();
            }
        }

        private static (bool Speech, bool Sound) Outputs(ChargeKind kind, AccessibilitySettings settings) =>
            PlayerCharges.SpeechKindOf(kind) == SpeechKind.Combo ? (settings.ComboSpeech, settings.ComboSounds) : (settings.ChargeSpeech, settings.ChargeSounds);

        /// <summary>Stat that a buff or action changed, else one items changed, else 0.</summary>
        private static int Stat(Stats stats, GameStats stat) =>
            stats.StatsChangedByBuffAndActions.TryGetValue(stat, out var value) ? value : stats.StatsChangedByItems.GetValueOrDefault(stat);

        private void WatchRage(Entity entity, AccessibilitySettings settings)
        {
            if (!entity.TryGetComponent<Stats>(out var stats))
            {
                return;
            }

            var max = Stat(stats, GameStats.virtual_maximum_rage) is > 0 and var raised ? raised
                : Stat(stats, GameStats.maximum_rage) is > 0 and var basic ? basic : PlayerCharges.DefaultMaxRage;
            var now = (Stat(stats, GameStats.current_rage), max);
            var before = this.rage.Count;
            this.rage = now;
            if (now.Item1 > 0 && !this.rageLogged)
            {
                this.rageLogged = true;
                this.log.Info($"Rage seen: {now.Item1} of {max}");
            }

            if (this.baseline || PlayerCharges.RageEdge(before, now.Item1, max) is not { } full)
            {
                return;
            }

            this.log.Info($"Rage {(full ? "full" : "empty")}: {before} -> {now.Item1} of {max}");
            var (speech, sound) = Outputs(ChargeKind.Rage, settings);
            if (sound)
            {
                if (full)
                {
                    this.PlayGain(ChargeKind.Rage, 1);
                }
                else
                {
                    this.Play(this.Lost(ChargeKind.Rage), "charge_lost");
                }
            }

            if (speech)
            {
                this.output.Say(SpeechKind.Charges, full ? Announcements.RageFull : Announcements.RageEmpty);
            }
        }

        private void Play(float[] sound, string key) => this.audio?.Play(sound, 0f, 1f, 1f, key);

        /// <summary>The player's own gain, unpanned: its recorded sound in stereo at the gain's pitch, else the ticks.</summary>
        private void PlayGain(ChargeKind kind, int count)
        {
            if (PlayerCharges.RecordedGain(kind) is { } key && this.RecordedSound(key) is { } recorded)
            {
                this.audio?.Play(recorded, PlayerCharges.GainPitch(kind, count), 1f, key);
                return;
            }

            this.Play(this.Gained(kind, count), "charge_gained");
        }

        private float[] Gained(ChargeKind kind, int count)
        {
            if (!this.gained.TryGetValue((kind, count), out var sound))
            {
                sound = this.gained[(kind, count)] = CueSynth.ChargeGained(kind, count, AudioCueService.SampleRate);
            }

            return sound;
        }

        private float[] Lost(ChargeKind kind)
        {
            if (!this.lost.TryGetValue(kind, out var sound))
            {
                sound = this.lost[kind] = CueSynth.ChargeLost(kind, AudioCueService.SampleRate);
            }

            return sound;
        }
    }
}
