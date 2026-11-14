namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameOffsets.Objects.States.InGameState;
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

        /// <summary>
        ///     A step of the tick longer than this is logged by name: the tick froze for most of a second at a session's
        ///     first monsters and nothing in it explains that (docs/research/performance.md).
        /// </summary>
        private const int SlowStepMs = 50;

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

        /// <summary>An attack whose monster has not been seen this long (dead, gone, out of range) is forgotten.</summary>
        private const int ForgetUnseenMs = 2000;

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
        private readonly List<(uint Id, long WarnAt, float Pan, float Pitch, float Gain, int Rank, float Distance)> flights = new();
        private readonly Dictionary<uint, long> warnedAt = new();

        /// <summary>This frame's warnings, played or covered together once every monster is seen (<see cref="DodgeCueMerge"/>).</summary>
        private readonly List<(DodgeCueMerge.Candidate Warning, float Pan, float Pitch, float Gain, string Line)> pending = new();
        private readonly DodgeCueMerge merge = new();
        private readonly HashSet<string> namedThreats = new(StringComparer.Ordinal);
        private int lastPlayerLife = -1;
        /// <summary>
        ///     <c>DeadSince</c>: when it first read not alive with no living read since (0: alive), <see cref="BossLifeSteps.Defeated"/>;
        ///     <c>ChangedAt</c>: when its life last read different. <c>Percent</c> is the last poll's: what is said is read again.
        /// </summary>
        private readonly Dictionary<uint, BossLife> bosses = new();
        private long? playerDiedAt;
        private long lastLifePoll;
        private long lastParryCue = long.MinValue / 2;
        private bool saveFailureLogged;
        private BossMechanicsWatch? mechanics;

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

        /// <summary>Raised with a boss's name when its defeat is said: the scanner marks its room, and drops the room's reward note.</summary>
        public Action<string>? Defeated { get; set; }

        /// <summary>
        ///     "El molinero hinchado 40 por ciento" for the vitals command, its life read from the game as it is asked; null
        ///     without a boss in the fight that is there to read (<see cref="BossLifeSteps.Spoken"/>).
        /// </summary>
        public string? Status()
        {
            var engaged = this.bosses.Where(pair => pair.Value.Engaged && pair.Value.Step > 0).ToList();
            if (engaged.Count == 0)
            {
                return null;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var read = engaged.Select(pair => (Percent: LifeNow(area, pair.Key), pair.Value.ChangedAt, pair.Value.HurtAt)).ToList();
            if (BossLifeSteps.Spoken(read, this.playerDiedAt) is { } index && read[index].Percent is { } percent)
            {
                return Announcements.BossLife(engaged[index].Value.Name, percent);
            }

            this.log.Info($"Boss life not said: {string.Join(", ", engaged.Select(pair => $"{pair.Value.Name} (last polled {pair.Value.Percent}%)"))} not there to read, or reset by the game");
            return null;
        }

        /// <summary>The boss's life as the game holds it now; null when it is not among the entities, dead or set aside.</summary>
        private static int? LifeNow(AreaInstance area, uint id) =>
            area.AwakeEntities.TryGetValue(new EntityNodeKey { id = id }, out var entity) && entity.IsValid && entity.Id == id &&
            entity.TryGetComponent<Life>(out var life) && life.Health.Total > 0 && life.IsAlive &&
            !EntityCatalog.IsFrozenDead(entity) && !EntityCatalog.IsHidden(entity)
                ? life.Health.CurrentInPercent()
                : null;

        /// <summary>When each kind of attack lands (loaded by the plugin).</summary>
        public DodgeTimings Timings { get; set; } = new();

        /// <summary>A boss fight's turns and the way out of its danger areas, fed from this watch's walk (docs/research/boss-mechanics.md).</summary>
        public BossMechanicsWatch? Mechanics
        {
            get => this.mechanics;
            set
            {
                this.mechanics = value;
                if (value != null)
                {
                    value.AreaOf = skill => this.attackData.Area(skill);
                    value.HelperCue = this.WarnFrom;
                }
            }
        }

        /// <summary>Where <see cref="Timings"/> is saved after each blow learnt (the host is often stopped, not closed).</summary>
        public string TimingsPath { get; set; } = string.Empty;

        /// <summary>A new area: no boss is known.</summary>
        public void Reset()
        {
            this.reader?.Reset();
            this.attacks.Clear();
            this.flights.Clear();
            this.warnedAt.Clear();
            this.pending.Clear();
            this.merge.Reset();
            this.bosses.Clear();
            this.playerDiedAt = null;
            this.lastPlayerLife = -1;
            this.entityAddresses = null;
            this.Mechanics?.Reset();
        }

        /// <summary>The player died: an engaged boss seen back at full life afterwards has had its fight reset by the game.</summary>
        public void PlayerDied()
        {
            this.playerDiedAt = Environment.TickCount64;
            this.Mechanics?.PlayerDied(this.playerDiedAt.Value);
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

            var step = System.Diagnostics.Stopwatch.GetTimestamp();
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
                    this.ConfirmDefeats(now);
                }
            }

            this.NoteStep("the monsters near and the poll's end", ref step);
            if (this.DodgeWarnings)
            {
                this.WarnFlights(now);
            }

            this.PlayWarnings(now);
            this.NoteStep("the warnings", ref step);

            this.ForgetUnseen(now);
            if (attacks && Seats.Followed.TryGetComponent<Life>(out var life))
            {
                this.LearnFromBlow(life.Health.Current, life.Health.Total, now);
            }
            else
            {
                this.lastPlayerLife = -1;
            }

            this.NoteStep("the blows learnt", ref step);
        }

        /// <summary>Logs a step that took long and starts the clock of the next.</summary>
        private void NoteStep(string name, ref long started)
        {
            var ms = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (ms >= SlowStepMs)
            {
                this.log.Info($"Boss watch: {name} took {ms:0} ms");
            }

            started = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        private void WatchNear(AreaInstance area, Render player, long now, bool poll, bool attacks, bool parrying)
        {
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var (step, last) = (System.Diagnostics.Stopwatch.GetTimestamp(), string.Empty);
            foreach (var entity in area.AwakeEntities.Values)
            {
                this.NoteStep(last, ref step);
                last = entity.Path;

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

                if (!attacks || !EntityCatalog.IsAliveEnemy(entity) || !entity.TryGetComponent<Actor>(out var actor))
                {
                    continue;
                }

                // A fought boss strikes while the game hides it, and through invisible helpers: those blows are its own.
                var forBoss = this.Mechanics?.StrikesForBoss(entity) == true;
                if (forBoss || !EntityCatalog.IsHidden(entity))
                {
                    var gap = offset.Length() - render.ModelBounds.X - player.ModelBounds.X;
                    this.WatchAttack(entity, actor, forBoss ? Rarity.Unique : rarity, render, player, offset, gap, area.WorldToGridConvertor, now, parrying);
                }
            }

            this.NoteStep(last, ref step);
        }

        private void WatchLife(Entity entity, Render render)
        {
            // An allied unique (a companion, a spectre) is no boss: its life and its death are not the fight's.
            if (!entity.TryGetComponent<Life>(out var life) || life.Health.Total <= 0 ||
                (entity.TryGetComponent<Positioned>(out var positioned) && positioned.IsFriendly))
            {
                return;
            }

            if (!this.bosses.TryGetValue(entity.Id, out var boss))
            {
                boss = BossLife.Fresh(this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty);
            }

            if (boss.Name.Length == 0)
            {
                return;
            }

            // One reading not alive is no death: said by ConfirmDefeats once no living reading came for a while.
            var alive = life.IsAlive && !EntityCatalog.IsFrozenDead(entity);
            var reading = BossLifeSteps.Read(boss, alive, alive && EntityCatalog.IsHidden(entity), alive ? life.Health.CurrentInPercent() : 0, this.playerDiedAt, Environment.TickCount64);
            if (reading.Risen)
            {
                this.log.Info($"Boss risen: {boss.Name} reads alive after its defeat was said");
            }

            if (reading.Restarted)
            {
                this.log.Info($"Boss fight restarted: {boss.Name} back to full life after the player's death");
            }

            if (reading.Said is { } percent)
            {
                // The real value, not the step (D2R): a big hit may skip several steps at once.
                this.output.Say(SpeechKind.BossLife, Announcements.BossLife(boss.Name, percent), percent <= UrgentBossPercent);
            }

            this.bosses[entity.Id] = reading.Boss;
        }

        /// <summary>The bosses read not alive and not seen alive since: defeated, said once (a corpse the game removed counts too).</summary>
        private void ConfirmDefeats(long now)
        {
            foreach (var (id, boss) in this.bosses.Where(pair => pair.Value.Step > 0 && BossLifeSteps.Defeated(pair.Value.DeadSince, now)).ToList())
            {
                if (boss.Engaged)
                {
                    this.output.Say(SpeechKind.BossLife, Announcements.BossDefeated(boss.Name), true);
                    this.log.Info($"Boss defeated: {boss.Name}");
                    this.Defeated?.Invoke(boss.Name);
                }

                this.bosses[id] = boss with { Step = 0, Engaged = false, Percent = 0 };
            }
        }

        /// <summary>Louder the nearer the attacker; never below the far level, so a far archer is still heard.</summary>
        private float WarningGain(Vector2 offset)
        {
            var closeness = 1f - Math.Clamp(offset.Length() / WatchWorld, 0f, 1f);
            return this.Gain * (FarWarningGain + ((1f - FarWarningGain) * closeness));
        }

        /// <summary>The dodge warning from a place (world) that is no monster's attack: a boss's helper upon the player.</summary>
        private bool WarnFrom(Vector2 place)
        {
            if (!this.DodgeWarnings || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return false;
            }

            var offset = place - new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, offset));
            this.audio.Play(this.whoosh, pan, pitch, this.WarningGain(offset), "dodge");
            return true;
        }

        /// <summary>
        ///     Queues the whoosh for this frame unless this monster warned a moment ago; true when queued. <paramref name="line"/> is
        ///     logged if it plays; a warning another cue covers is logged as merged.
        /// </summary>
        private bool Warn(uint id, float pan, float pitch, float gain, long now, DodgeCueMerge.Candidate warning, string line)
        {
            if (now - this.warnedAt.GetValueOrDefault(id, long.MinValue / 2) < WarningGapMs)
            {
                return false;
            }

            this.warnedAt[id] = now;
            this.pending.Add((warning, pan, pitch, gain, line));
            return true;
        }

        /// <summary>The frame's warnings: one cue for blows that land together, the most dangerous one's.</summary>
        private void PlayWarnings(long now)
        {
            if (this.pending.Count == 0)
            {
                return;
            }

            foreach (var outcome in this.merge.Resolve(this.pending.Select(cue => cue.Warning), now))
            {
                var cue = this.pending.First(queued => queued.Warning == outcome.Warning);
                if (outcome.CoveredBy is { } cover)
                {
                    this.log.Debug($"Dodge merged: {cue.Line} covered by monster {cover}");
                    continue;
                }

                this.audio.Play(this.whoosh, cue.Pan, cue.Pitch, cue.Gain, "dodge");
                this.log.Debug($"Dodge warning: {cue.Line}");
            }

            this.pending.Clear();
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
                if (now - flight.WarnAt <= LeadMs)
                {
                    this.Warn(flight.Id, flight.Pan, flight.Pitch, flight.Gain, now, new DodgeCueMerge.Candidate(flight.Id, flight.Rank, flight.Distance, flight.WarnAt + LeadMs, now),
                        $"monster {flight.Id}, a projectile still in flight from its previous attack");
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

            // An attack the clip's own event times needs no learning; one the gate kept quiet did not hit the player; one whose
            // monster is no longer seen cannot be told from the blow's real source.
            var hitters = this.attacks.Where(pair => !pair.Value.Learned && pair.Value.InReach && pair.Value.Timing != BlowTiming.Live &&
                                                     now - pair.Value.SeenAt <= LateBlowMs + pair.Value.TravelMs &&
                                                     (pair.Value.Swing != null || now - pair.Value.StartedAt <= DodgeTimings.MaxDelayMs) &&
                                                     (pair.Value.EndedAt == 0 || now - pair.Value.EndedAt <= LateBlowMs + pair.Value.TravelMs)).ToList();
            if (hitters.Count != 1)
            {
                return;
            }

            // A projectile's blow is learnt as its release: the flight differs with the distance each time. Where the clip
            // was read, the blow is its place in the clip, which is how it is replayed; else the real time since the start.
            var (id, attack) = (hitters[0].Key, hitters[0].Value);
            var real = (int)(now - attack.StartedAt) - attack.TravelMs;
            var delay = attack.BlowClock is var (clock, readAt) ? AttackClock.NominalMsAt(clock, readAt, now - attack.TravelMs) : real;
            if (attack.Swing is { } swing && delay > swing)
            {
                delay %= swing; // a later swing of the same animation: its blow lands as far into its own swing
            }

            var basis = attack.BlowClock is { } read
                ? $", clip time at rate {AttackClock.Rate(read.Reading).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} ({real} ms real)"
                : ", real time (no clock read)";

            this.attacks[id] = attack with { Learned = true };
            if (!this.Timings.Learn(attack.Key, delay))
            {
                this.log.Info($"Dodge timing: {attack.Key} hit after {delay} ms, not believed (the files say {this.Timings.FileHit(attack.Key)} ms){basis}");
                return;
            }

            if (this.TimingsPath.Length > 0 && this.Timings.Save(this.TimingsPath) is { } failure && !this.saveFailureLogged)
            {
                this.saveFailureLogged = true;
                this.log.Warn($"Dodge timings not saved to {this.TimingsPath}: {failure} (kept for this session, tried again with each blow learnt)");
            }

            this.log.Info($"Dodge timing: {attack.Key} hit after {delay} ms (learnt {this.Timings.Delay(attack.Key)} ms){basis}");
        }

        /// <summary>Drops the attacks of monsters the walk has not met for a while, so they neither linger nor teach.</summary>
        private void ForgetUnseen(long now)
        {
            foreach (var id in this.attacks.Where(pair => now - pair.Value.SeenAt > ForgetUnseenMs).Select(pair => pair.Key).ToList())
            {
                this.attacks.Remove(id);
                this.warnedAt.Remove(id);
            }
        }

        /// <summary>An attack under way: when it began, when to warn, and what was done about it.</summary>
        private sealed record Attack(int Animation, string Key, DodgeThreat Threat, long StartedAt, long WarnAt, int? Learnt, bool Warns, bool Unique)
        {
            public long EndedAt { get; init; }

            /// <summary>When the walk last met its monster.</summary>
            public long SeenAt { get; init; }

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

            /// <summary>
            ///     The attack's clip as last read while its animation ran, and when: where in it a blow felt later fell
            ///     (<see cref="AttackClock.NominalMsAt"/>). Null when no clock was read or the swing left the clip.
            /// </summary>
            public (AnimationReading Reading, long At)? BlowClock { get; init; }

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
        }
    }
}
