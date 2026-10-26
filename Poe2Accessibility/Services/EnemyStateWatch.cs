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

    /// <summary>
    ///     Enemy states that skills pay off on (<see cref="EnemyState"/>): each has its own short sound, played from the
    ///     enemy's side as the state begins, and a rare or unique enemy is named with it. Only the states the party's
    ///     skills cause ever happen, so the cues follow the build — except Primed for Stun, which every build's hits
    ///     build up, so it has a switch of its own.
    /// </summary>
    public sealed class EnemyStateWatch
    {
        private const int PollMs = 100;

        /// <summary>About a screen away, as for the culling cue.</summary>
        private const float WatchWorld = 1400f;

        /// <summary>The same state's sound at most this often: a pack primed over a second stays a few chimes.</summary>
        private const int StateGapMs = 200;

        private readonly AudioCueService audio;
        private readonly IAccessibilityOutputService output;
        private readonly UiElementReader elements;
        private readonly HostMemoryReader memory;
        private readonly Dictionary<EnemyState, float[]> cues =
            Enum.GetValues<EnemyState>().ToDictionary(state => state, state => CueSynth.StateCue(state, AudioCueService.SampleRate));

        private readonly Dictionary<EnemyState, long> sounded = new();
        private readonly EnemyStateNews news = new();
        private readonly List<EnemyState> states = new();
        private long lastPoll;

        public EnemyStateWatch(AudioCueService audio, IAccessibilityOutputService output, UiElementReader elements, HostMemoryReader memory)
        {
            this.audio = audio;
            this.output = output;
            this.elements = elements;
            this.memory = memory;
        }

        /// <summary>Cue gain under the master volume.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>A new area: no enemy is known.</summary>
        public void Reset() => this.news.Clear();

        /// <summary>Per frame; <paramref name="others"/> and <paramref name="primed"/> are the two settings.</summary>
        public void Tick(bool playing, bool others, bool primed)
        {
            var now = Environment.TickCount64;
            if (!playing || !(others || primed) || now - this.lastPoll < PollMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.lastPoll = now;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            this.news.BeginPoll();
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || entity.EntityType != EntityTypes.Monster || !EntityCatalog.IsAliveEnemy(entity) ||
                    !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var at = new Vector2(render.WorldPosition.X, render.WorldPosition.Y);
                if (Vector2.Distance(at, here) > WatchWorld)
                {
                    continue;
                }

                var rarity = entity.TryGetComponent<ObjectMagicProperties>(out var magic) ? magic.Rarity : Rarity.Normal;
                this.states.Clear();
                if (others && entity.TryGetComponent<Buffs>(out var buffs))
                {
                    foreach (var id in buffs.StatusEffects.Keys)
                    {
                        if (EnemyStates.FromBuffId(id) is { } state)
                        {
                            this.states.Add(state);
                        }
                    }
                }

                if (entity.TryGetComponent<Actor>(out var actor))
                {
                    if (others && EnemyStates.FromAnimation(Enum.GetName(actor.Animation) ?? string.Empty) is { } shown)
                    {
                        this.states.Add(shown);
                    }

                    if (primed && this.Primed(actor.Address, rarity))
                    {
                        this.states.Add(EnemyState.PrimedForStun);
                    }
                }

                var entered = this.news.Update(entity.Id, this.states, now);
                if (entered.Count == 0)
                {
                    continue;
                }

                var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, at - here));
                foreach (var state in entered)
                {
                    if (!this.sounded.TryGetValue(state, out var last) || now - last >= StateGapMs)
                    {
                        this.sounded[state] = now;
                        this.audio.Play(this.cues[state], pan, pitch, this.Gain);
                    }
                }

                if (rarity is Rarity.Rare or Rarity.Unique &&
                    this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) is { Length: > 0 } name)
                {
                    this.output.Say(SpeechKind.BossLife, Announcements.EnemyEntered(name, entered), urgent: true);
                }
            }

            this.news.EndPoll();
        }

        /// <summary>Heavy stun buildup against its threshold, both in the Actor component (<see cref="GameLayout.ActorComponent"/>).</summary>
        private bool Primed(IntPtr actor, Rarity rarity)
        {
            var threshold = this.memory.ReadBytes(actor + GameLayout.ActorComponent.PoiseThresholdOffset, sizeof(int));
            var buildup = this.memory.ReadBytes(actor + GameLayout.ActorComponent.PoiseOffset, sizeof(int));
            return threshold.Length == sizeof(int) && buildup.Length == sizeof(int) &&
                   PrimedThreshold.Reached(rarity, BitConverter.ToInt32(buildup), BitConverter.ToInt32(threshold));
        }
    }
}
