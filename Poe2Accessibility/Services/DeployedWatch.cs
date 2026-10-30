namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     What the player's skills placed and left standing, to be found again and hit: Tempest Bell, Frozen Locus's ice
    ///     crystal (<see cref="DeployedObjects"/>). Its own game sound (else a small bell) from where it stands, more often
    ///     and louder as the player gets closer; several each on its own, never at the same moment. Each kind is logged once as it is first heard,
    ///     and each new most standing at once.
    /// </summary>
    public sealed class DeployedWatch
    {
        private const int PollMs = 100;

        private readonly AudioCueService audio;
        private readonly PluginLog log;
        private readonly float[] ping = CueSynth.Deployed(AudioCueService.SampleRate);
        private readonly Dictionary<uint, (Vector2 At, long Since)> placed = new();
        private readonly HashSet<uint> moving = new();
        private readonly Dictionary<uint, long> lastPlayed = new();
        private readonly HashSet<string> loggedPaths = new(StringComparer.Ordinal);
        private long lastPoll;
        private long lastAnyPlayed;
        private int mostStanding;

        public DeployedWatch(AudioCueService audio, PluginLog log)
        {
            this.audio = audio;
            this.log = log;
        }

        /// <summary>Gain under the master volume.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>The game's sounds by cue key (set by the plugin); null for one not extracted, which gets the small bell.</summary>
        public Func<string, float[]?> GameSound { get; set; } = _ => null;

        /// <summary>A new area: nothing placed is known.</summary>
        public void Reset()
        {
            this.placed.Clear();
            this.moving.Clear();
            this.lastPlayed.Clear();
            this.mostStanding = 0;
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
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var seen = new HashSet<uint>();
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || !DeployedObjects.IsPlaced(entity.Path) || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                seen.Add(entity.Id);
                var at = new Vector2(render.WorldPosition.X, render.WorldPosition.Y);
                if (!this.placed.TryGetValue(entity.Id, out var first))
                {
                    this.placed[entity.Id] = (at, now);
                    if (this.loggedPaths.Add(entity.Path))
                    {
                        this.log.Info($"Placed by a skill: {entity.Path}");
                    }

                    continue;
                }

                if (this.moving.Contains(entity.Id) || DeployedObjects.Moved(first.At, at))
                {
                    this.moving.Add(entity.Id);
                    continue;
                }

                var distance = Vector2.Distance(here, at);
                if (now - first.Since < DeployedObjects.SettleMs || distance > DeployedObjects.RangeWorld || now - this.lastAnyPlayed < DeployedObjects.GapMs ||
                    (this.lastPlayed.TryGetValue(entity.Id, out var last) && now - last < DeployedObjects.IntervalMs(distance)))
                {
                    continue;
                }

                this.lastPlayed[entity.Id] = now;
                this.lastAnyPlayed = now;
                var (side, height) = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, at - here));
                var spread = Math.Min(1f, distance / DeployedObjects.CentreWorld);
                var key = DeployedObjects.SoundKey(entity.Path);
                var (sound, name) = this.GameSound(key) is { } game ? (game, key) : (this.ping, "deployed");
                this.audio.Play(sound, side * spread, 1f + ((height - 1f) * spread), this.Gain * DeployedObjects.GainAt(distance), name);
            }

            if (seen.Count > this.mostStanding)
            {
                this.mostStanding = seen.Count;
                this.log.Info($"Placed by skills, standing at once: {seen.Count}");
            }

            foreach (var gone in this.placed.Keys.Where(id => !seen.Contains(id)).ToList())
            {
                this.placed.Remove(gone);
                this.moving.Remove(gone);
                this.lastPlayed.Remove(gone);
            }
        }
    }
}
