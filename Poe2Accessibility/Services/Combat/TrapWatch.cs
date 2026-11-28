namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The dodge warning from where a Trial's trap is about to strike (<see cref="TrialTraps"/>): spikes under the rumbling
    ///     ground, a laser firing, a mine primed. Every trap and state met is logged once, for the first run through them.
    /// </summary>
    public sealed class TrapWatch
    {
        private const int PollMs = 100;

        /// <summary>Near enough to be caught; a first guess, no trap has been timed live.</summary>
        private const float WarnWorld = 400f;

        /// <summary>A row of spikes rumbles at once: one warning for it, not one each.</summary>
        private const int MinGapMs = 300;

        private readonly AudioCueService audio;
        private readonly PluginLog log;
        private readonly float[] warning = CueSynth.Dodge(AudioCueService.SampleRate);
        private readonly TrapStrikes strikes = new();
        private readonly DistinctLog logged;

        /// <summary>Each petrifying statue's last animation, and the player's petrification buffs, logged as they change (first run, 2026-11-19).</summary>
        private readonly Dictionary<uint, int> statueAnimations = new();
        private string gazeLogged = string.Empty;

        /// <summary>The petrifying statues: eyes latched open or shut, where each stands now, the line each looks along once met.</summary>
        private readonly Dictionary<uint, bool> eyesOpen = new();
        private readonly Dictionary<uint, Render> statues = new();
        private readonly Dictionary<uint, Vector2> facings = new();
        private readonly HashSet<uint> inLine = new();
        private readonly Dictionary<int, float[]> gazePulses = new();
        private readonly IAccessibilityOutputService output;
        private long lastGazePulse = long.MinValue / 2;
        private bool gazeSaid;
        private bool stone;
        private long lastPoll;
        private long lastWarned;

        public TrapWatch(AudioCueService audio, IAccessibilityOutputService output, PluginLog log)
        {
            this.audio = audio;
            this.output = output;
            this.log = log;
            this.logged = new DistinctLog(log, "Trial trap seen");
        }

        /// <summary>The statue's own beam starting (the owner's pick 2026-11-19, tools/Poe2UiExtract/SoundExtractor.cs).</summary>
        public const string StatueOpenSound = "statue_open";

        /// <summary>Gets or sets the game's recordings by key: a statue opening its eyes (<see cref="StatueOpenSound"/>).</summary>
        public Func<string, float[]?> GameSound { get; set; } = _ => null;

        /// <summary>Gain under the master volume.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>A new area: no trap is known to be firing.</summary>
        public void Reset()
        {
            this.strikes.Reset();
            this.statueAnimations.Clear();
            this.gazeLogged = string.Empty;
            this.eyesOpen.Clear();
            this.statues.Clear();
            this.facings.Clear();
            this.inLine.Clear();
            (this.gazeSaid, this.stone) = (false, false);
        }

        /// <summary>Per frame; <paramref name="playing"/> false while dead, paused or in a menu.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.lastPoll = now;
            this.LogGaze();
            this.statues.Clear();
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                var path = entity.Path ?? string.Empty;
                if (!entity.IsValid || TrialTraps.KindOf(path) == null || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var states = entity.TryGetComponent<StateMachine>(out var machine)
                    ? machine.States.Select(state => (state.Name, state.Value)).ToList()
                    : new List<(string Name, long Value)>();
                this.LogOnce(path, states);
                var offset = new Vector2(render.WorldPosition.X - player.WorldPosition.X, render.WorldPosition.Y - player.WorldPosition.Y);
                if (TrialTraps.KindOf(path) == TrialTrap.PetrifyingStatue)
                {
                    this.LogStatue(entity, offset.Length());
                    this.statues[entity.Id] = render;
                    this.Eyes(entity, render, offset);
                }

                if (this.strikes.Update(entity.Id, TrialTraps.IsFiring(path, states), offset.Length() <= WarnWorld))
                {
                    this.Warn(render, offset, now);
                }
            }

            this.Gaze(player, now);
        }

        /// <summary>A statue's eyes opening near: its own beam starting, from where it stands.</summary>
        private void Eyes(GameReader.RemoteObjects.States.InGameStateObjects.Entity entity, Render statue, Vector2 offset)
        {
            if (!entity.TryGetComponent<Actor>(out var actor))
            {
                return;
            }

            var was = this.eyesOpen.TryGetValue(entity.Id, out var open) && open;
            var opened = PetrifyingStatues.Eyes((int)actor.Animation, was);
            this.eyesOpen[entity.Id] = opened;
            if (opened && !was && offset.Length() <= PetrifyingStatues.OpenHeardWorld && this.GameSound(StatueOpenSound) is { } sound)
            {
                var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(statue.WorldPosition, offset));
                this.audio.Play(sound, pan, pitch, this.Gain, StatueOpenSound);
            }
        }

        /// <summary>
        ///     A statue looking at the player: the file over stone from its side, quicker and higher as the stone builds (the owner's
        ///     pick); said once an area what to do. Its line is learned from where the player stood, and stepping into a known line
        ///     while its eyes are shut is said. Turned to stone is said, with no sound (the owner: the reader and the rising rasp suffice).
        /// </summary>
        private void Gaze(Render player, long now)
        {
            if (!Seats.Followed.TryGetComponent<Buffs>(out var buffs))
            {
                return;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var stoneNow = PetrifyingStatues.IsStone(buffs.StatusEffects.Keys);
            if (stoneNow && !this.stone)
            {
                this.output.Urge(Announcements.Petrified);
            }

            this.stone = stoneNow;
            var gaze = PetrifyingStatues.Gaze(buffs.StatusEffects.Select(pair => (pair.Key, pair.Value.SourceEntityId, (int)pair.Value.Charges)));
            if (gaze is { } looking && !stoneNow)
            {
                var statue = this.statues.TryGetValue(looking.Statue, out var seen) ? seen : null;
                if (statue != null && !this.facings.ContainsKey(looking.Statue) &&
                    PetrifyingStatues.Facing(new Vector2(statue.WorldPosition.X, statue.WorldPosition.Y), here) is { } facing)
                {
                    this.facings[looking.Statue] = facing;
                    this.log.Info($"Statue {looking.Statue} looks along {facing.X:0.00},{facing.Y:0.00}, the player {Vector2.Distance(new Vector2(statue.WorldPosition.X, statue.WorldPosition.Y), here):0} away");
                }

                if (!this.gazeSaid)
                {
                    this.gazeSaid = true;
                    this.output.Urge(Announcements.StatueGaze);
                }

                if (now - this.lastGazePulse >= PetrifyingStatues.PulseMs(looking.Stacks))
                {
                    this.lastGazePulse = now;
                    if (!this.gazePulses.TryGetValue(looking.Stacks, out var pulse))
                    {
                        pulse = CueSynth.Gaze(looking.Stacks, AudioCueService.SampleRate);
                        this.gazePulses[looking.Stacks] = pulse;
                    }

                    var (pan, pitch) = statue == null ? (0f, 1f)
                        : BeaconMath.Voice(ScreenProjection.Direction(statue.WorldPosition, new Vector2(statue.WorldPosition.X, statue.WorldPosition.Y) - here));
                    this.audio.Play(pulse, pan, pitch, this.Gain, CueSynth.GazeSound);
                }

                return;
            }

            // A known line with the eyes shut: stepping into it is said, before they open again.
            foreach (var (id, facing) in this.facings)
            {
                var standing = this.statues.TryGetValue(id, out var render) &&
                               PetrifyingStatues.InLine(new Vector2(render.WorldPosition.X, render.WorldPosition.Y), facing, here);
                if (standing && this.inLine.Add(id) && !(this.eyesOpen.TryGetValue(id, out var open) && open))
                {
                    this.output.Say(SpeechKind.StatusBegins, Announcements.InStatueLine);
                }
                else if (!standing)
                {
                    this.inLine.Remove(id);
                }
            }
        }

        private void Warn(Render trap, Vector2 offset, long now)
        {
            if (now - this.lastWarned < MinGapMs)
            {
                return;
            }

            this.lastWarned = now;
            var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(trap.WorldPosition, offset));
            this.audio.Play(this.warning, pan, pitch, this.Gain, "dodge");
        }

        /// <summary>Research: a statue's eyes as its animation changes, with how far it is (world units), to time and range its gaze.</summary>
        private void LogStatue(GameReader.RemoteObjects.States.InGameStateObjects.Entity entity, float distance)
        {
            if (!entity.TryGetComponent<Actor>(out var actor))
            {
                return;
            }

            var animation = (int)actor.Animation;
            if (!this.statueAnimations.TryGetValue(entity.Id, out var was) || was != animation)
            {
                this.statueAnimations[entity.Id] = animation;
                this.log.Info($"Statue {entity.Id}: animation {actor.Animation} ({animation}), {distance:0} away");
            }
        }

        /// <summary>Research: the player's petrification buffs (the gaze's build-up and the stone) as they change.</summary>
        private void LogGaze()
        {
            if (!Seats.Followed.TryGetComponent<Buffs>(out var buffs))
            {
                return;
            }

            var line = string.Join("; ", buffs.StatusEffects.Where(pair => pair.Key.Contains("petrif", StringComparison.OrdinalIgnoreCase))
                .Select(pair => $"{pair.Key} charges {pair.Value.Charges} stage {pair.Value.RawStage} left {pair.Value.TimeLeft:0.0}/{pair.Value.TotalTime:0.0} from {pair.Value.SourceEntityId}"));
            var key = System.Text.RegularExpressions.Regex.Replace(line, @"left [^ ]+", string.Empty);
            if (key != this.gazeLogged)
            {
                this.gazeLogged = key;
                this.log.Info($"Petrification buffs: {(line.Length > 0 ? line : "none")}");
            }
        }

        private void LogOnce(string path, List<(string Name, long Value)> states)
        {
            this.logged.Note($"{path}: {string.Join(", ", states.Where(state => state.Value > 0).Select(state => $"{state.Name}={state.Value}"))}");
        }
    }
}
