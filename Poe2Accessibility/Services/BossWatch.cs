namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteEnums;
    using GameReader.RemoteEnums.Entity;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Bosses and rare monsters near the player. Boss life (D2R boss-life.md) is spoken as the real percentage at
    ///     each step it falls past, and the defeat once. The dodge warning (<see cref="CueSynth.Dodge"/>) on the side of a rare or
    ///     unique monster (every monster with <see cref="AllMonsters"/>) whose attack is aimed at the player, reaches it and
    ///     shows on screen (<see cref="DodgeGate"/>), louder the closer it is, half a second before the blow: the playing
    ///     clip's own event at its real speed (<see cref="AttackClock"/>), else the files' or learnt time (<see cref="DodgeTimings"/>;
    ///     a projectile's flight across the gap added), at the wind-up until then. For a player who can parry, the parry cue
    ///     (<see cref="CueSynth.Parry"/>, a swell into a dead stop) stops <see cref="ParryLeadMs"/> before a timed blow worth
    ///     parrying (<see cref="ParryTiming"/>), from any monster, never one the game marks as impossible to block or parry;
    ///     a rare's or unique's attack that cannot be blocked adds its own cue at the wind-up (<see cref="CueSynth.Unblockable"/>). Animation ids are GameHelper's <c>Actor.Animation</c> (Actor +0x8B0).
    /// </summary>
    public sealed partial class BossWatch
    {
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
        private readonly float[] parry = CueSynth.Parry(AudioCueService.SampleRate);
        private readonly float[] unblockable = CueSynth.Unblockable(AudioCueService.SampleRate);
        private readonly int parryMs;
        private readonly Dictionary<uint, Attack> attacks = new();

        /// <summary>
        ///     Projectiles let go by an attack the monster has already left for its next one (an archer's volley): each
        ///     keeps its warning, with the side and loudness from where it was shot.
        /// </summary>
        private readonly List<(uint Id, long WarnAt, float Pan, float Pitch, float Gain)> flights = new();
        private readonly Dictionary<uint, long> warnedAt = new();
        private readonly HashSet<string> namedThreats = new(StringComparer.Ordinal);
        private int lastPlayerLife = -1;
        private readonly Dictionary<uint, (string Name, int Step, bool Engaged, int Percent)> bosses = new();
        private long lastLifePoll;
        private long lastParryCue = long.MinValue / 2;

        public BossWatch(AudioCueService audio, IAccessibilityOutputService output, UiElementReader elements, PluginLog log)
        {
            this.audio = audio;
            this.output = output;
            this.elements = elements;
            this.log = log;
            this.whoosh = CueSynth.Dodge(AudioCueService.SampleRate);
            this.parryMs = this.parry.Length * 1000 / AudioCueService.SampleRate;
        }

        public bool AnnounceLife { get; set; } = true;

        public bool DodgeWarnings { get; set; } = true;

        public bool ParryCues { get; set; } = true;

        /// <summary>The extra cue at the wind-up of an attack the game marks as impossible to block (with the dodge warnings on).</summary>
        public bool UnblockableCues { get; set; } = true;

        /// <summary>Whether the followed player has Parry now (<see cref="PlayerSkills"/>); asked only with <see cref="ParryCues"/> on.</summary>
        public Func<bool> CanParry { get; set; } = () => false;

        /// <summary>How long before a blow the parry cue stops, the moment to press Parry: time for the parry to come up.</summary>
        public int ParryLeadMs { get; set; } = 150;

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

        /// <summary>A boss fight's turns and the way out of its danger areas, fed from this watch's walk (docs/research/boss-mechanics.md).</summary>
        public BossMechanicsWatch? Mechanics { get; set; }

        /// <summary>Where <see cref="Timings"/> is saved after each blow learnt (the host is often stopped, not closed).</summary>
        public string TimingsPath { get; set; } = string.Empty;

        /// <summary>A new area: no boss is known.</summary>
        public void Reset()
        {
            this.reader?.Reset();
            this.attacks.Clear();
            this.flights.Clear();
            this.warnedAt.Clear();
            this.bosses.Clear();
            this.lastPlayerLife = -1;
            this.entityAddresses = null;
            this.Mechanics?.Reset();
        }

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            var parrying = playing && this.ParryCues && this.CanParry();
            var attacks = this.DodgeWarnings || parrying;
            if (!playing || (!this.AnnounceLife && !attacks && this.Mechanics == null))
            {
                this.lastPlayerLife = -1; // life lost while nothing is watched is no blow
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var now = Environment.TickCount64;
            // The boss's turns (gone, back, phase) are polled whether or not its life is said: muting the life must not mute them.
            var poll = (this.AnnounceLife || this.Mechanics != null) && now - this.lastLifePoll >= LifePollMs;
            if (poll)
            {
                this.lastLifePoll = now;
            }

            try
            {
                this.WatchNear(area, player, now, poll, attacks, parrying);
            }
            finally
            {
                // A throw in the walk must not leave its address map or the mechanics' poll half done for later frames.
                this.entityAddresses = null;
                if (poll)
                {
                    this.Mechanics?.EndPoll(now);
                }
            }

            if (this.DodgeWarnings)
            {
                this.WarnFlights(now);
            }

            if (attacks && Seats.Followed.TryGetComponent<Life>(out var life))
            {
                this.LearnFromBlow(life.Health.Current, life.Health.Total, now);
            }
            else
            {
                this.lastPlayerLife = -1;
            }
        }

        private void WatchNear(AreaInstance area, Render player, long now, bool poll, bool attacks, bool parrying)
        {
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
                if (rarity == Rarity.Unique && poll && this.AnnounceLife)
                {
                    this.WatchLife(entity, render);
                }

                if (poll)
                {
                    this.Mechanics?.Observe(entity, render, rarity, now);
                }

                if (attacks && EntityCatalog.IsAliveEnemy(entity) && !EntityCatalog.IsHidden(entity) && entity.TryGetComponent<Actor>(out var actor))
                {
                    var gap = offset.Length() - render.ModelBounds.X - player.ModelBounds.X;
                    this.WatchAttack(entity, actor, rarity, render, player, offset, gap, area.WorldToGridConvertor, now, parrying);
                }
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
                boss = (name, BossLifeSteps.Full, false, 100);
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

            if (EntityCatalog.IsHidden(entity))
            {
                this.bosses[entity.Id] = (boss.Name, boss.Step, false, boss.Percent);
                return;
            }

            var percent = life.Health.CurrentInPercent();
            var engaged = boss.Engaged || percent < 100;
            var step = BossLifeSteps.Next(boss.Step, percent);
            if (step < boss.Step)
            {
                // The real value, not the step (D2R): a big hit may skip several steps at once.
                this.output.Say(SpeechKind.BossLife, Announcements.BossLife(boss.Name, percent), percent <= UrgentBossPercent);
            }

            this.bosses[entity.Id] = (boss.Name, step, engaged, percent);
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
            this.audio.Play(this.whoosh, pan, pitch, gain, "dodge");
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

            // An attack the clip's own event times needs no learning; one the gate kept quiet did not hit the player.
            var hitters = this.attacks.Where(pair => !pair.Value.Learned && pair.Value.InReach && pair.Value.Timing != BlowTiming.Live &&
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

            this.attacks[id] = attack with { Learned = true };
            if (!this.Timings.Learn(attack.Key, delay))
            {
                this.log.Info($"Dodge timing: {attack.Key} hit after {delay} ms, not believed (the files say {this.Timings.FileHit(attack.Key)} ms)");
                return;
            }

            if (this.TimingsPath.Length > 0)
            {
                this.Timings.Save(this.TimingsPath);
            }

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

            /// <summary>The game marks the attack as impossible to block, so neither a raised shield nor a parry stops it.</summary>
            public bool Unblockable { get; init; }

            public bool UnblockableCued { get; init; }

            /// <summary>When the next blow to parry lands; null for an untimed attack or once its cue has had its turn.</summary>
            public long? ParryBlowAt { get; init; }

            public bool CanStillHit(long now) => DodgeTimings.CanStillHit(this.EndedAt, this.ReleaseAt, this.TravelMs, now);

            /// <summary>The clip playing when the attack began, to time the files' hit from its position while it plays on.</summary>
            public int? StartClip { get; init; }

            /// <summary>The clip's reading on the last frame, to notice a new swing.</summary>
            public AnimationReading? LastClock { get; init; }

            /// <summary>The swing now plays another clip than the one the attack began with (the files' time is for that one).</summary>
            public bool ClipMoved { get; init; }

            /// <summary>The clip time (s) of the contact last warned of, so a clip's next contact warns again.</summary>
            public float? WarnedContact { get; init; }

            /// <summary>The clip time (s) of a contact the last warning covers (too close after it for a sound of its own).</summary>
            public float? CoveredContact { get; init; }

            /// <summary>Where the monster stood when the attack began (world), to see whether the attack carried it.</summary>
            public System.Numerics.Vector2? StartPlace { get; init; }

            /// <summary>The place the attack aimed at when first read (world): a leap's landing.</summary>
            public System.Numerics.Vector2? AimedPlace { get; init; }

            /// <summary>Where the time to the blow came from on the last frame.</summary>
            public BlowTiming Timing { get; init; }

            /// <summary>The gate's last verdict was not silent.</summary>
            public bool Threatens { get; init; }
        }
    }
}
