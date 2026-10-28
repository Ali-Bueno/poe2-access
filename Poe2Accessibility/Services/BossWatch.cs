namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Bosses and rare monsters near the player. Boss life (D2R boss-life.md) is spoken as the real percentage at
    ///     each step it falls past, and the defeat once. The dodge warning: a short rising whoosh on the side of a rare or
    ///     unique monster on screen attacking (<see cref="DodgeThreats"/>; a rare's blow only within reach), louder the
    ///     closer it is, half a second before the blow lands as learnt from earlier hits of that attack by any monster of
    ///     its kind or given by the game's files (<see cref="DodgeTimings"/>; a projectile's flight across the gap added, at
    ///     its speed in the files), at the wind-up until then. Animation ids are GameHelper's
    ///     <c>Actor.Animation</c> (Actor +0x8B0).
    /// </summary>
    public sealed class BossWatch
    {
        /// <summary>Boss life steps, percent (D2R: 50, then every 10; 75 and 5 added for long PoE fights).</summary>
        private static readonly int[] LifeSteps = { 75, 50, 40, 30, 20, 10, 5 };

        /// <summary>From here down a boss's life cuts in when its kind is set to "cuts in when urgent".</summary>
        private const int UrgentBossPercent = 30;

        /// <summary>Monsters this close (world units, about a screen) are watched.</summary>
        private const float WatchWorld = 1400f;

        private const int LifePollMs = 150;

        /// <summary>One warning per monster within this time (multi-hit animations restart).</summary>
        private const int WarningGapMs = 600;

        /// <summary>
        ///     A rare's blow warns only within this gap between the bodies (world units): the player's own reach is 40, so
        ///     this leaves a step and a half to roll away; a unique's blows warn from anywhere on screen (slam waves).
        /// </summary>
        private const float MeleeDangerGapWorld = 150f;

        /// <summary>The warning's gain at the edge of the watched range; full at contact (raised from 0.7: a boss's far blows were faint).</summary>
        private const float FarWarningGain = 0.8f;

        /// <summary>The warning comes this long before an attack's learnt blow: time to hear it and roll (B).</summary>
        private const int LeadMs = 500;

        /// <summary>A fall of life this large in one frame is a blow, not damage over time.</summary>
        private const float BlowFraction = 0.04f;

        /// <summary>A blow seen this soon after its attack animation ended still belongs to it (the server's word arrives late).</summary>
        private const int LateBlowMs = 300;

        private readonly AudioCueService audio;
        private readonly IAccessibilityOutputService output;
        private readonly UiElementReader elements;
        private readonly PluginLog log;
        private readonly float[] whoosh;
        private readonly Dictionary<uint, Attack> attacks = new();

        /// <summary>
        ///     Projectiles let go by an attack the monster has already left for its next one (an archer's volley): each
        ///     keeps its warning, with the side and loudness from where it was shot.
        /// </summary>
        private readonly List<(uint Id, long WarnAt, float Pan, float Pitch, float Gain)> flights = new();
        private readonly Dictionary<uint, long> warnedAt = new();
        private int lastPlayerLife = -1;
        private readonly Dictionary<uint, (string Name, int Step, bool Engaged, int Percent)> bosses = new();
        private long lastLifePoll;

        public BossWatch(AudioCueService audio, IAccessibilityOutputService output, UiElementReader elements, PluginLog log)
        {
            this.audio = audio;
            this.output = output;
            this.elements = elements;
            this.log = log;
            this.whoosh = CueSynth.Whoosh(AudioCueService.SampleRate);
        }

        public bool AnnounceLife { get; set; } = true;

        public bool DodgeWarnings { get; set; } = true;

        /// <summary>Warning cue gain under the master volume.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>"El molinero hinchado 40 por ciento" for the vitals command; null without a boss in the fight.</summary>
        public string? Status()
        {
            foreach (var (_, boss) in this.bosses)
            {
                if (boss.Engaged && boss.Step > 0)
                {
                    return Announcements.BossLife(boss.Name, boss.Percent);
                }
            }

            return null;
        }

        /// <summary>When each kind of attack lands (loaded by the plugin).</summary>
        public DodgeTimings Timings { get; set; } = new();

        /// <summary>Where <see cref="Timings"/> is saved after each blow learnt (the host is often stopped, not closed).</summary>
        public string TimingsPath { get; set; } = string.Empty;

        /// <summary>A new area: no boss is known.</summary>
        public void Reset()
        {
            this.attacks.Clear();
            this.flights.Clear();
            this.warnedAt.Clear();
            this.bosses.Clear();
            this.lastPlayerLife = -1;
        }

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            if (!playing || (!this.AnnounceLife && !this.DodgeWarnings))
            {
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var now = Environment.TickCount64;
            var pollLife = this.AnnounceLife && now - this.lastLifePoll >= LifePollMs;
            if (pollLife)
            {
                this.lastLifePoll = now;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            foreach (var entity in area.AwakeEntities.Values)
            {
                // Every monster near teaches when its kind's attacks land; rares and uniques are the ones warned of.
                if (!entity.IsValid || entity.EntityType != EntityTypes.Monster || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var offset = new Vector2(render.WorldPosition.X, render.WorldPosition.Y) - here;
                if (offset.Length() > WatchWorld)
                {
                    continue;
                }

                var rarity = entity.TryGetComponent<ObjectMagicProperties>(out var magic) ? magic.Rarity : Rarity.Normal;
                if (rarity == Rarity.Unique && pollLife)
                {
                    this.WatchLife(entity, render);
                }

                if (this.DodgeWarnings && EntityCatalog.IsAliveEnemy(entity) && entity.TryGetComponent<Actor>(out var actor))
                {
                    var gap = offset.Length() - render.ModelBounds.X - player.ModelBounds.X;
                    this.WatchAttack(entity, (int)actor.Animation, rarity, render, offset, gap, now);
                }
            }

            if (this.DodgeWarnings)
            {
                this.WarnFlights(now);
            }

            if (this.DodgeWarnings && Seats.Followed.TryGetComponent<Life>(out var life))
            {
                this.LearnFromBlow(life.Health.Current, life.Health.Total, now);
            }
        }

        private void WatchLife(Entity entity, Render render)
        {
            if (!entity.TryGetComponent<Life>(out var life) || life.Health.Total <= 0)
            {
                return;
            }

            if (!this.bosses.TryGetValue(entity.Id, out var boss))
            {
                var name = this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty;
                boss = (name, 100, false, 100);
            }

            if (boss.Name.Length == 0)
            {
                return;
            }

            if (!life.IsAlive || EntityCatalog.IsFrozenDead(entity))
            {
                if (boss.Engaged && boss.Step > 0)
                {
                    this.output.Say(SpeechKind.BossLife, Announcements.BossDefeated(boss.Name), true);
                    this.log.Info($"Boss defeated: {boss.Name}");
                }

                this.bosses[entity.Id] = (boss.Name, 0, false, 0);
                return;
            }

            var percent = life.Health.CurrentInPercent();
            var engaged = boss.Engaged || percent < 100;
            var step = boss.Step;
            foreach (var threshold in LifeSteps)
            {
                if (percent < threshold && threshold < step)
                {
                    step = threshold;
                }
            }

            if (step < boss.Step)
            {
                // The real value, not the step (D2R): a big hit may skip several steps at once.
                this.output.Say(SpeechKind.BossLife, Announcements.BossLife(boss.Name, percent), percent <= UrgentBossPercent);
            }

            this.bosses[entity.Id] = (boss.Name, step, engaged, percent);
        }

        private void WatchAttack(Entity entity, int animation, Rarity rarity, Render render, Vector2 offset, float gap, long now)
        {
            var id = entity.Id;
            this.attacks.TryGetValue(id, out var attack);
            if (attack == null || attack.Animation != animation)
            {
                if (attack != null && attack.EndedAt == 0)
                {
                    attack = attack with { EndedAt = now }; // a blow can still arrive for it
                    this.attacks[id] = attack;
                }

                var threat = DodgeThreats.Of(animation, value => Enum.GetName(typeof(Animation), value));
                if (threat == DodgeThreat.None)
                {
                    if (attack == null)
                    {
                        return;
                    }

                    if (now - attack.EndedAt > LateBlowMs + attack.TravelMs)
                    {
                        this.attacks.Remove(id);
                        return;
                    }

                    // A projectile released before the monster turned away still comes: its warning stands.
                    if (!attack.CanStillHit(now))
                    {
                        return;
                    }
                }
                else
                {
                    if (attack is { TravelMs: > 0, Warns: true, Warned: false, InReach: true } flying && flying.CanStillHit(now))
                    {
                        // The next attack replaces this record; the projectile it let go is still coming.
                        var (flightPan, flightPitch) = BeaconMath.Voice(ScreenProjection.Direction(render.WorldPosition, offset));
                        this.flights.Add((id, flying.WarnAt, flightPan, flightPitch, this.WarningGain(offset)));
                    }

                    var key = DodgeTimings.Key(entity.Path, animation);
                    var learnt = this.Timings.Delay(key);

                    // A projectile lands its flight across the gap after the release the files and earlier blows time.
                    var travel = DodgeTimings.TravelMs(gap, this.Timings.ProjectileSpeed(key));
                    attack = new Attack(animation, key, threat, now, DodgeTimings.WarnAt(now, learnt + travel, LeadMs), learnt,
                        rarity is Rarity.Rare or Rarity.Unique, rarity == Rarity.Unique)
                    {
                        Swing = this.Timings.Swing(key), TravelMs = travel, ReleaseAt = learnt is { } release ? now + release : null,
                    };
                }
            }

            // A rare's blow needs it within reach when it would land; a unique's carries (slam waves).
            var inReach = attack.Threat != DodgeThreat.Melee || attack.Unique || gap <= MeleeDangerGapWorld;
            attack = attack with { InReach = inReach };
            if (attack.Warns && !attack.Warned && attack.CanStillHit(now) && now >= attack.WarnAt)
            {
                // The same animation id goes on swinging (a golem's blows a swing apart, nothing new written between
                // them): a timed attack warns again one swing later.
                attack = attack.Learnt != null && attack.Swing is { } swing
                    ? attack with { WarnAt = attack.WarnAt + swing, ReleaseAt = attack.ReleaseAt + swing }
                    : attack with { Warned = true };
                var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(render.WorldPosition, offset));
                if (inReach && this.Warn(id, pan, pitch, this.WarningGain(offset), now))
                {
                    this.log.Debug($"Dodge warning: monster {id} animation {(Animation)animation} ({attack.Threat}, gap {gap:F0}, " +
                                   $"{(attack.Learnt is { } delay ? $"{now - attack.StartedAt} ms into a blow learnt at {delay} ms" : "at the wind-up")}" +
                                   $"{(attack.TravelMs > 0 ? $", {attack.TravelMs} ms of flight" : string.Empty)})");
                }
            }

            this.attacks[id] = attack;
        }

        /// <summary>Louder the nearer the attacker; never below the far level, so a far archer is still heard.</summary>
        private float WarningGain(Vector2 offset)
        {
            var closeness = 1f - Math.Clamp(offset.Length() / WatchWorld, 0f, 1f);
            return this.Gain * (FarWarningGain + ((1f - FarWarningGain) * closeness));
        }

        /// <summary>Plays the whoosh unless this monster warned a moment ago; true when it played.</summary>
        private bool Warn(uint id, float pan, float pitch, float gain, long now)
        {
            if (now - this.warnedAt.GetValueOrDefault(id, long.MinValue / 2) < WarningGapMs)
            {
                return false;
            }

            this.warnedAt[id] = now;
            this.audio.Play(this.whoosh, pan, pitch, gain);
            return true;
        }

        /// <summary>The warnings of projectiles in flight whose attack was replaced by the next one.</summary>
        private void WarnFlights(long now)
        {
            for (var i = this.flights.Count - 1; i >= 0; i--)
            {
                var flight = this.flights[i];
                if (now < flight.WarnAt)
                {
                    continue;
                }

                this.flights.RemoveAt(i);
                if (now - flight.WarnAt <= LeadMs && this.Warn(flight.Id, flight.Pan, flight.Pitch, flight.Gain, now))
                {
                    this.log.Debug($"Dodge warning: monster {flight.Id}, a projectile still in flight from its previous attack");
                }
            }
        }

        /// <summary>
        ///     The player's life fell by a blow: the one monster whose attack is under way in reach dealt it, so that
        ///     attack lands this long after it starts. With two or more under way nothing is learnt.
        /// </summary>
        private void LearnFromBlow(int life, int total, long now)
        {
            var fell = this.lastPlayerLife >= 0 && this.lastPlayerLife - life >= Math.Max(1f, total * BlowFraction);
            this.lastPlayerLife = life;
            if (!fell)
            {
                return;
            }

            var hitters = this.attacks.Where(pair => !pair.Value.Learned && pair.Value.InReach &&
                                                     (pair.Value.Swing != null || now - pair.Value.StartedAt <= DodgeTimings.MaxDelayMs) &&
                                                     (pair.Value.EndedAt == 0 || now - pair.Value.EndedAt <= LateBlowMs + pair.Value.TravelMs)).ToList();
            if (hitters.Count != 1)
            {
                return;
            }

            // A projectile's blow is learnt as its release: the flight differs with the distance each time.
            var (id, attack) = (hitters[0].Key, hitters[0].Value);
            var delay = (int)(now - attack.StartedAt) - attack.TravelMs;
            if (attack.Swing is { } swing && delay > swing)
            {
                delay %= swing; // a later swing of the same animation: its blow lands as far into its own swing
            }

            this.Timings.Learn(attack.Key, delay);
            if (this.TimingsPath.Length > 0)
            {
                this.Timings.Save(this.TimingsPath);
            }

            this.attacks[id] = attack with { Learned = true };
            this.log.Info($"Dodge timing: {attack.Key} hit after {delay} ms (learnt {this.Timings.Delay(attack.Key)} ms)");
        }

        /// <summary>An attack under way: when it began, when to warn, and what was done about it.</summary>
        private sealed record Attack(int Animation, string Key, DodgeThreat Threat, long StartedAt, long WarnAt, int? Learnt, bool Warns, bool Unique)
        {
            public long EndedAt { get; init; }

            public bool InReach { get; init; }

            public bool Warned { get; init; }

            public bool Learned { get; init; }

            /// <summary>One swing's length when the game's files give it: the attack repeats at this period.</summary>
            public int? Swing { get; init; }

            /// <summary>Its projectile's flight to the player from where the attack began; 0 without a projectile.</summary>
            public int TravelMs { get; init; }

            /// <summary>When the swing warned of next lets its blow or projectile go, from the timings; null when unknown.</summary>
            public long? ReleaseAt { get; init; }

            public bool CanStillHit(long now) => DodgeTimings.CanStillHit(this.EndedAt, this.ReleaseAt, this.TravelMs, now);
        }
    }
}
