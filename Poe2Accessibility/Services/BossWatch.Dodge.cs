namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameOffsets.Natives;
    using GameReader;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>The dodge warning's gate: what each attack aims at, reaches and shows, and when its blow really lands (docs/research/dodge-timing.md).</summary>
    public sealed partial class BossWatch
    {
        /// <summary>Parts of memory the gate reads, as the compatibility report names them, each with its live check.</summary>
        private static readonly string[] FieldParts =
        {
            "dodge: animated object", "dodge: attack clock", "dodge: clip events", "dodge: action types", "dodge: action targets", "dodge: action places",
        };

        private readonly HashSet<string> gateReasons = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FieldTrust> saidTrust = new(StringComparer.Ordinal);
        private Dictionary<uint, IntPtr>? entityAddresses;
        private AttackReader? reader;
        private AttackData attackData = new();
        private string catalogDirectory = string.Empty;

        /// <summary>Warn of every monster's attacks, not only rares' and uniques' (setting DodgeWarningsAllMonsters).</summary>
        public bool AllMonsters { get; set; }

        /// <summary>The plugin's catalog folder: its attack catalogs (<see cref="AttackData"/>) are read the first time it is set.</summary>
        public string CatalogDirectory
        {
            get => this.catalogDirectory;
            set
            {
                if (value.Length == 0 || value == this.catalogDirectory)
                {
                    return;
                }

                this.catalogDirectory = value;
                this.attackData = AttackData.Load(value);
                this.reader = new AttackReader(new HostMemoryReader(this.log), this.elements, this.attackData, this.log);
                this.log.Info($"Dodge gate: {this.attackData.Clips} clips with a reach, {this.attackData.Areas} skill areas, {this.attackData.ActionTypeIds.Count} action types");
            }
        }

        /// <summary>
        ///     A ground telegraph of the monster's attack, by monster id (the telegraph study's run-time path A,
        ///     docs/research/attack-telegraphs.md §4); none until a telegraph source is wired.
        /// </summary>
        public Func<uint, AttackTelegraph?> Telegraph { get; set; } = _ => null;

        /// <summary>The live checks' verdicts that changed since last asked, by compatibility part.</summary>
        public IReadOnlyList<(string Part, FieldTrust Trust)> FieldNews()
        {
            if (this.reader is not { } read)
            {
                return Array.Empty<(string, FieldTrust)>();
            }

            var checks = new[] { read.AnimatedObjects, read.Clock, read.Events, read.Actions, read.Targets, read.Places };
            var news = new List<(string, FieldTrust)>();
            for (var i = 0; i < checks.Length; i++)
            {
                if (checks[i].Trust != FieldTrust.Unjudged && this.saidTrust.GetValueOrDefault(FieldParts[i]) != checks[i].Trust)
                {
                    this.saidTrust[FieldParts[i]] = checks[i].Trust;
                    news.Add((FieldParts[i], checks[i].Trust));
                }
            }

            return news;
        }

        private void WatchAttack(Entity entity, Actor actor, Rarity rarity, Render render, Render player, Vector2 offset, float gap, float worldPerGrid, long now, bool parrying)
        {
            var id = entity.Id;
            var animation = (int)actor.Animation;
            this.attacks.TryGetValue(id, out var attack);
            if (attack == null || attack.Animation != animation)
            {
                if (attack != null && attack.EndedAt == 0)
                {
                    attack = attack with { EndedAt = now }; // a blow can still arrive for it
                    this.attacks[id] = attack;
                    if (attack is { StartPlace: { } start, AimedPlace: { } aimedAt } && this.Timings.LearnMove(attack.Key, start, new Vector2(player.WorldPosition.X, player.WorldPosition.Y) + offset, aimedAt))
                    {
                        this.log.Info($"Dodge: {attack.Key} carries its user to its target: its reach is judged from there");
                    }
                }

                var key = DodgeTimings.Key(entity.Path, animation);
                var threat = this.Timings.Threat(key) ?? this.ThreatFromName(key, animation);
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
                    if (this.DodgeWarnings && attack is { TravelMs: > 0, Warns: true, Warned: false, InReach: true } flying && flying.CanStillHit(now))
                    {
                        // The next attack replaces this record; the projectile it let go is still coming.
                        var (flightPan, flightPitch) = BeaconMath.Voice(ScreenProjection.Direction(render.WorldPosition, offset));
                        this.flights.Add((id, flying.WarnAt, flightPan, flightPitch, this.WarningGain(offset)));
                    }

                    var learnt = this.Timings.Delay(key);

                    // A projectile lands its flight across the gap after the release the files and earlier blows time.
                    var travel = DodgeTimings.TravelMs(gap, this.Timings.ProjectileSpeed(key));
                    attack = new Attack(animation, key, threat, now, DodgeTimings.WarnAt(now, learnt + travel, LeadMs), learnt,
                        this.AllMonsters || rarity is Rarity.Rare or Rarity.Unique, rarity == Rarity.Unique)
                    {
                        Swing = this.Timings.Swing(key), TravelMs = travel, ReleaseAt = learnt is { } release ? now + release : null,
                        ParryBlowAt = learnt is { } blow && this.Timings.CanParry(key) ? now + blow + travel : null,
                        Unblockable = !this.Timings.CanBlock(key),
                        StartPlace = new Vector2(player.WorldPosition.X, player.WorldPosition.Y) + offset,
                    };
                }
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var monster = here + offset;
            var animated = entity.TryGetComponent<Animated>(out var animatedComponent) ? animatedComponent.Address : IntPtr.Zero;
            // About a dozen reads a frame per attack: spent where a cue can follow, or while the live checks still judge.
            var snapshot = attack.Warns || parrying || this.reader?.Judging == true ? this.reader?.Read(id, actor.Address, animated, monster, worldPerGrid, this.EntityAt, now) : null;
            attack = this.FollowClock(attack, snapshot?.Clock, snapshot?.Events, snapshot is { ActionType: not 0 });
            if (attack.AimedPlace == null && snapshot?.Aim?.TargetPoint is { } aimed)
            {
                attack = attack with { AimedPlace = aimed };
            }
            var (blowIn, timing, contact) = this.BlowIn(attack, snapshot, now);
            var facts = this.Facts(attack, snapshot, rarity, render, player, here, monster, gap, blowIn, timing, id);
            var decision = DodgeGate.Decide(facts, LeadMs);
            if (this.DodgeWarnings)
            {
                this.Mechanics?.Attack(id, attack.Unique, attack.StartedAt, decision, snapshot?.SkillId is { } skillId ? this.attackData.Area(skillId) : null,
                    monster, snapshot?.Aim?.TargetPoint ?? here, here, player.ModelBounds.X, player.WorldPosition, blowIn, now);
            }

            this.NoteReason(attack.Key, animation, decision.Reason, facts, snapshot, attack.Warns || parrying);
            var threatens = decision.Verdict != DodgeVerdict.Silent;
            attack = attack with { InReach = threatens, Threatens = threatens, Timing = timing };

            if (this.DodgeWarnings && this.UnblockableCues && attack is { Unblockable: true, UnblockableCued: false, Warns: true } && threatens)
            {
                // At the wind-up, as the game's red glow: the dodge warning still times the roll.
                attack = attack with { UnblockableCued = true };
                var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(render.WorldPosition, offset));
                this.audio.Play(this.unblockable, pan, pitch, this.WarningGain(offset), "unblockable");
                this.log.Debug($"Unblockable attack: monster {id} animation {AnimationNames.Of(animation)}");
            }

            if (this.DodgeWarnings && attack.Warns && !attack.Warned && decision.Verdict == DodgeVerdict.Warn)
            {
                attack = timing == BlowTiming.Nominal && snapshot?.Clock == null && attack.Learnt != null && attack.Swing is { } swing
                    ? attack with { WarnAt = attack.WarnAt + swing, ReleaseAt = attack.ReleaseAt + swing } // a golem's next swing, nothing new written
                    : attack with { Warned = true, WarnedContact = contact };
                var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(render.WorldPosition, offset));
                if (this.Warn(id, pan, pitch, this.WarningGain(offset), now))
                {
                    this.log.Debug($"Dodge warning: monster {id} {attack.Key} animation {AnimationNames.Of(animation)} ({attack.Threat}, {decision.Reason}, gap {gap:F0}, " +
                                   $"aim {facts.Aim}, reach {facts.InReach?.ToString() ?? "?"}, blow in {blowIn?.ToString() ?? "?"} ms" +
                                   $"{(attack.TravelMs > 0 ? $", {attack.TravelMs} ms of flight" : string.Empty)}, clip {snapshot?.Clip ?? "?"})");
                }
                else if (attack.Warned && contact is { } covered)
                {
                    // Too soon after this monster's last warning for a sound of its own: that warning covers this contact, and
                    // the next one is timed in full (AttackClock.NextBlow).
                    attack = attack with { Warned = false, WarnedContact = null, CoveredContact = covered };
                }
            }

            if (parrying && threatens)
            {
                if (timing == BlowTiming.Live && blowIn is > 0 && this.Timings.CanParry(attack.Key))
                {
                    attack = attack with { ParryBlowAt = now + blowIn.Value };
                }

                if (attack.ParryBlowAt is { } blowAt && now >= ParryTiming.StartAt(blowAt, this.ParryLeadMs, this.parryMs) && attack.CanStillHit(now))
                {
                    attack = this.CueParry(id, attack, blowAt, rarity, render, offset, gap, now);
                }
            }

            this.attacks[id] = attack;
        }

        /// <summary>The clip it plays: a new swing (the clip started over or changed) re-arms the warning.</summary>
        private Attack FollowClock(Attack attack, AnimationReading? clock, IReadOnlyList<ClipEvent>? events, bool acting)
        {
            if (clock is not { } now)
            {
                return attack;
            }

            if (attack.StartClip == null)
            {
                return attack with { StartClip = now.Clip, LastClock = now };
            }

            if (attack.LastClock is { } before && AttackClock.NewSwing(before, now))
            {
                attack = attack with { ClipMoved = AttackClock.LeftStartClip(attack.StartClip, now, acting), Warned = false, WarnedContact = null, CoveredContact = null };
            }

            // A clip with several contacts warns again for each one still ahead.
            if (attack.Warned && attack.WarnedContact is { } warned && events != null && NextContact(now, events) is { } next && next > warned)
            {
                attack = attack with { Warned = false };
            }

            return attack with { LastClock = now };
        }

        /// <summary>In how long the blow lands, and from what: the clip's contact, the files' time on the clip, the files' or learnt time from the start.</summary>
        private (int? BlowIn, BlowTiming Timing, float? Contact) BlowIn(Attack attack, AttackSnapshot? snapshot, long now)
        {
            int? release = null;
            var timing = BlowTiming.None;
            float? contact = null;
            if (snapshot?.Clock is { } clock)
            {
                if (snapshot.Events.Any(AttackClock.IsBlow))
                {
                    contact = AttackClock.NextBlow(clock.Position, snapshot.Events, attack.CoveredContact);
                    (release, timing) = (contact is { } at ? AttackClock.MsUntil(clock, at) : 0, BlowTiming.Live);
                }
                else if (!attack.ClipMoved && attack.Learnt is { } hit)
                {
                    (release, timing) = (AttackClock.MsToNominal(clock, hit) ?? 0, BlowTiming.Nominal);
                }
            }

            if (timing == BlowTiming.None && attack.ReleaseAt is { } at2)
            {
                (release, timing) = ((int)Math.Clamp(at2 - now, int.MinValue / 2, int.MaxValue / 2), BlowTiming.Nominal);
            }

            return (release is > 0 ? release + attack.TravelMs : release, timing, contact);
        }

        private DodgeFacts Facts(Attack attack, AttackSnapshot? snapshot, Rarity rarity, Render render, Render player, Vector2 here, Vector2 monster, float gap,
                                 int? blowIn, BlowTiming timing, uint id)
        {
            var playerRadius = player.ModelBounds.X;
            var projectile = this.Timings.ProjectileSpeed(attack.Key) != null;
            var aim = snapshot?.Aim is { } target
                ? AttackAim.Of(target, Seats.Followed.Id, here, playerRadius, monster, 0f, projectile)
                : AimKind.Unknown;

            // The clip's own reach for a blow beside the monster; the skill's danger area where its table draws one.
            bool? inReach = null;
            if (attack.Threat == DodgeThreat.Melee && snapshot is { AnimatedObject: { } ao, Clip: { } clip } && this.attackData.Reach(ao, clip) is { } reach)
            {
                // A leap strikes where it lands (DodgeTimings.Moves): its reach counts from the place it aims at.
                var from = this.Timings.Moves(attack.Key) && snapshot.Aim?.TargetPoint is { } landing ? landing : monster;
                inReach = AttackReach.MeleeReaches(reach, from, here, playerRadius, AttackAim.MarginWorld);
            }

            if (snapshot?.SkillId is { } skill && this.attackData.Area(skill) is { } area &&
                AttackAreas.Catches(area, monster, snapshot.Aim?.TargetPoint ?? here, here, playerRadius, AttackAim.MarginWorld))
            {
                inReach = true; // the sizes' unit is not measured yet: an area only adds a warning, never takes one away
            }

            var fallback = attack.Threat != DodgeThreat.Melee || attack.Unique || gap <= MeleeDangerGapWorld;
            var (seen, seenIn) = Sight(render, player, blowIn, attack.TravelMs, projectile);
            return new DodgeFacts(attack.Threat, aim, inReach, fallback, seen, seenIn, blowIn, timing, this.Telegraph(id));
        }

        /// <summary>Whether the attacker's body is on a 16:9 screen; from off screen, when its projectile comes into view.</summary>
        private static (bool Seen, int? SeenInMs) Sight(Render render, Render player, int? blowIn, int travelMs, bool projectile)
        {
            if (ScreenProjection.Draw(render.WorldPosition) is not { } drawn)
            {
                return (ScreenProjection.InView(render.WorldPosition), null);
            }

            var edge = render.WorldPosition;
            var side = ScreenProjection.Draw(new StdTuple3D<float> { X = edge.X + render.ModelBounds.X, Y = edge.Y, Z = edge.Z });
            var radius = side is { } point ? Vector2.Distance(point, drawn) : 0f;
            if (AttackSight.BodySeen(drawn, radius))
            {
                return (true, null);
            }

            if (!projectile || travelMs <= 0 || blowIn is not > 0 || ScreenProjection.Draw(player.WorldPosition) is not { } target ||
                AttackSight.EntryFraction(drawn, target) is not { } entry)
            {
                return (false, null);
            }

            var release = blowIn.Value - travelMs;
            return (false, (int)Math.Max(0, AttackSight.SeenAt(release, travelMs, entry)));
        }

        private static float? NextContact(AnimationReading clock, IReadOnlyList<ClipEvent> events)
        {
            float? next = null;
            foreach (var clipEvent in events)
            {
                if (AttackClock.IsBlow(clipEvent) && clipEvent.Time >= clock.Position && (next == null || clipEvent.Time < next))
                {
                    next = clipEvent.Time;
                }
            }

            return next;
        }

        /// <summary>
        ///     Each gate reason once per attack kind, for the live audit (docs/research/dodge-timing.md, "Live protocol"). An attack
        ///     no cue follows (a normal monster's with <c>DodgeWarningsAllMonsters</c> off) is not read, and says so: its line was
        ///     "aim Unknown, clip ?" and, logged first, hid the read line of a rare of the same kind (Ogham Manor, 2026-11-08).
        /// </summary>
        private void NoteReason(string key, int animation, string reason, in DodgeFacts facts, AttackSnapshot? snapshot, bool followed)
        {
            if (snapshot == null && !followed)
            {
                if (this.gateReasons.Add($"{key}|{reason}|unread"))
                {
                    this.log.Info($"Dodge gate: {key} {AnimationNames.Of(animation) ?? "?"} -> {reason} (not read: no warning follows this monster's attacks)");
                }

                return;
            }

            if (this.gateReasons.Add($"{key}|{reason}"))
            {
                this.log.Info($"Dodge gate: {key} {AnimationNames.Of(animation) ?? "?"} -> {reason} (aim {facts.Aim}, reach {facts.InReach?.ToString() ?? "?"}, " +
                              $"seen {facts.Seen}, timing {facts.Timing}, clip {snapshot?.Clip ?? "?"}, action {(snapshot is { ActionType: > 0 } acting ? this.attackData.ActionName(acting.ActionType) ?? $"0x{acting.ActionType:X4}" : "-")}, skill {snapshot?.SkillId ?? "-"})");
            }
        }

        /// <summary>The awake entity with that id, to check an action's target; built once a frame when first asked.</summary>
        private IntPtr? EntityAt(uint id)
        {
            this.entityAddresses ??= Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values
                .Where(entity => entity.IsValid).GroupBy(entity => entity.Id).ToDictionary(group => group.Key, group => group.First().Address);
            return this.entityAddresses.TryGetValue(id, out var address) ? address : null;
        }

        /// <summary>
        ///     The kind an attack's animation name gives it, where the game's tables are mute: each such attack is logged once,
        ///     so a patch's gaps in the data show (docs/research/dodge-timing.md, "Kinds from the data").
        /// </summary>
        private DodgeThreat ThreatFromName(string key, int animation)
        {
            var threat = DodgeThreats.Of(animation, AnimationNames.Of);
            if (this.namedThreats.Add(key))
            {
                this.log.Info($"Dodge kind from the animation's name: {key} {AnimationNames.Of(animation) ?? "(no name)"} -> {threat}");
            }

            return threat;
        }

        /// <summary>How many attacks met so far had their kind from the animation's name, not the game's tables.</summary>
        public int NamedThreats => this.namedThreats.Count;

        /// <summary>
        ///     The parry cue for a timed blow worth parrying, at most once per parry and only while its stop still falls
        ///     before the blow (a blow too quick for the swell gets none); a repeating swing is cued again a swing later.
        /// </summary>
        private Attack CueParry(uint id, Attack attack, long blowAt, Rarity rarity, Render render, Vector2 offset, float gap, long now)
        {
            if (ParryTiming.InTime(blowAt, this.parryMs, now) && now - this.lastParryCue >= ParryTiming.RepeatMs &&
                ParryTiming.Worth(attack.Threat, attack.TravelMs > 0, rarity is Rarity.Rare or Rarity.Unique, gap, MeleeDangerGapWorld))
            {
                this.lastParryCue = now;
                var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(render.WorldPosition, offset));
                this.audio.Play(this.parry, pan, pitch, this.WarningGain(offset), "parry");
                this.log.Debug($"Parry cue: monster {id} animation {AnimationNames.Of(attack.Animation)} ({attack.Threat}, gap {gap:F0}, {now - attack.StartedAt} ms in, blow in {blowAt - now} ms)");
            }

            return attack with { ParryBlowAt = attack.Swing is { } swing ? blowAt + swing : null };
        }
    }
}
