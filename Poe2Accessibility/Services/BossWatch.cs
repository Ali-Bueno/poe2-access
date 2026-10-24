namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Bosses and rare monsters near the player. Boss life (D2R boss-life.md) is spoken as the real percentage at
    ///     each step it falls past, and the defeat once. The dodge warning: when a rare or unique monster on screen
    ///     starts an attack or spell (its animation leaves idle/run/hit), a short rising whoosh plays on its side —
    ///     the wind-up is the moment to dodge (B). Monsters read from GameHelper's awake entities; the animation id is
    ///     GameHelper's <c>Actor.Animation</c> (Actor +0x8B0).
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


        private readonly AudioCueService audio;
        private readonly IAccessibilityOutputService output;
        private readonly UiElementReader elements;
        private readonly PluginLog log;
        private readonly float[] whoosh;
        private readonly Dictionary<uint, (int Animation, long WarnedAt)> animations = new();
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

        /// <summary>A new area: no boss is known.</summary>
        public void Reset()
        {
            this.animations.Clear();
            this.bosses.Clear();
        }

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            if (!playing || (!this.AnnounceLife && !this.DodgeWarnings))
            {
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!area.Player.TryGetComponent<Render>(out var player))
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
                if (!entity.IsValid || entity.EntityType != EntityTypes.Monster ||
                    !entity.TryGetComponent<ObjectMagicProperties>(out var magic) || magic.Rarity is not (Rarity.Rare or Rarity.Unique) ||
                    !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var offset = new Vector2(render.WorldPosition.X, render.WorldPosition.Y) - here;
                if (offset.Length() > WatchWorld)
                {
                    continue;
                }

                if (magic.Rarity == Rarity.Unique && pollLife)
                {
                    this.WatchLife(entity, render);
                }

                if (this.DodgeWarnings && EntityCatalog.IsAliveEnemy(entity) && entity.TryGetComponent<Actor>(out var actor))
                {
                    this.WatchAttack(entity.Id, (int)actor.Animation, render, offset, now);
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
                boss = (name, 100, false, 100);
            }

            if (boss.Name.Length == 0)
            {
                return;
            }

            if (!life.IsAlive)
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

        private void WatchAttack(uint id, int animation, Render render, Vector2 offset, long now)
        {
            this.animations.TryGetValue(id, out var last);
            this.animations[id] = (animation, last.WarnedAt);
            if (animation == last.Animation || !IsAttack(animation) || now - last.WarnedAt < WarningGapMs)
            {
                return;
            }

            this.animations[id] = (animation, now);
            var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(render.WorldPosition, offset));
            this.audio.Play(this.whoosh, pan, pitch, this.Gain);
            this.log.Debug($"Dodge warning: monster {id} animation {(Animation)animation}");
        }

        /// <summary>Anything but standing, moving, flinching, dying or appearing is an attack or a spell.</summary>
        public static bool IsAttack(int animation)
        {
            if (animation <= 0)
            {
                return false;
            }

            var name = Enum.GetName(typeof(Animation), animation);
            if (name == null)
            {
                return true;
            }

            foreach (var calm in CalmAnimations)
            {
                if (name.Contains(calm, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static readonly string[] CalmAnimations =
        {
            "Idle", "Run", "Walk", "TakeHit", "Hit", "Death", "Dead", "Stun", "Spawn", "Emerge", "Flinch", "Knockback", "Turn", "Taunt", "Sleep", "Rise",
            "Buff", // a self buff hurts no one (the Bloated Miller's SpellBuff warned like his slams, 2026-10-24)
        };
    }
}
