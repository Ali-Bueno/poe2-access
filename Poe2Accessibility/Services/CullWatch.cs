namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Culling Strike cue (the Monk's Killing Palm): a falling chime from the side of an enemy that has just come within
    ///     culling range (<see cref="CullThreshold"/>), once until it heals out of it; a rare or unique one is named too. By
    ///     default it plays only while one of the character's skills culls (<see cref="CullSkills"/>).
    /// </summary>
    public sealed class CullWatch
    {
        private const int PollMs = 100;

        /// <summary>About a screen away: the enemies a strike can reach soon.</summary>
        private const float WatchWorld = 1400f;

        private readonly AudioCueService audio;
        private readonly IAccessibilityOutputService output;
        private readonly UiElementReader elements;
        private readonly float[] chime = CueSynth.Cull(AudioCueService.SampleRate);
        private readonly HashSet<uint> cullable = new();
        private readonly HashSet<uint> seen = new();
        private long lastPoll;

        public CullWatch(AudioCueService audio, IAccessibilityOutputService output, UiElementReader elements)
        {
            this.audio = audio;
            this.output = output;
            this.elements = elements;
        }

        /// <summary>Chime gain under the master volume.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>When it plays: one of <see cref="CullThreshold.Modes"/> (setting CullCue).</summary>
        public string Mode { get; set; } = CullThreshold.WithSkill;

        /// <summary>Whether one of the followed player's skills culls now.</summary>
        public Func<bool> HasCullingSkill { get; set; } = () => false;

        /// <summary>A new area: no enemy is known.</summary>
        public void Reset() => this.cullable.Clear();

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs || this.Mode == CullThreshold.Never ||
                (this.Mode == CullThreshold.WithSkill && !this.HasCullingSkill()) || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.lastPoll = now;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            this.seen.Clear();
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || entity.EntityType != EntityTypes.Monster || !EntityCatalog.IsAliveEnemy(entity) ||
                    !entity.TryGetComponent<Render>(out var render) || !entity.TryGetComponent<Life>(out var life))
                {
                    continue;
                }

                var at = new Vector2(render.WorldPosition.X, render.WorldPosition.Y);
                var rarity = entity.TryGetComponent<ObjectMagicProperties>(out var magic) ? magic.Rarity : Rarity.Normal;
                if (Vector2.Distance(at, here) > WatchWorld || !CullThreshold.Reached(rarity, life.Health.Current, life.Health.Total))
                {
                    continue;
                }

                this.seen.Add(entity.Id);
                if (!this.cullable.Add(entity.Id))
                {
                    continue;
                }

                var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, at - here));
                this.audio.Play(this.chime, pan, pitch, this.Gain);
                if (rarity is Rarity.Rare or Rarity.Unique &&
                    this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) is { Length: > 0 } name)
                {
                    this.output.Say(SpeechKind.BossLife, Announcements.Cullable(name), urgent: true);
                }
            }

            // Dead, gone or healed out of range: news again if it drops back in.
            this.cullable.IntersectWith(this.seen);
        }
    }
}
