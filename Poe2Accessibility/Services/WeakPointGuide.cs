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
    ///     Boss weak point: while a boss whose tail is not the part to strike is near (<see cref="WeakPoints"/>), a
    ///     heartbeat from the head's side leads there, faster as it nears and slow and soft at the head; silent while the
    ///     head is out of play (buried).
    /// </summary>
    public sealed class WeakPointGuide
    {
        /// <summary>A boss arena: the Devourer's head surfaces anywhere in it while its tail stays (world units, ~5 s of walking).</summary>
        private const float FightWorld = 2000f;

        /// <summary>At the head the beat drops to this share of its gain, so the fight's own sounds come through.</summary>
        private const float AtPartGain = 0.6f;

        private readonly AudioCueService audio;
        private readonly IAccessibilityOutputService output;
        private readonly UiElementReader elements;
        private readonly HostMemoryReader memory;
        private readonly PluginLog log;
        private readonly float[] beat = CueSynth.Heartbeat(AudioCueService.SampleRate);
        private readonly Dictionary<uint, string> names = new();
        private readonly HashSet<string> introduced = new();
        private readonly List<(BossPart Part, IntPtr Render)> near = new();
        private uint? following;
        private long lastBeat;

        public WeakPointGuide(AudioCueService audio, IAccessibilityOutputService output, UiElementReader elements, HostMemoryReader memory, PluginLog log)
        {
            this.audio = audio;
            this.output = output;
            this.elements = elements;
            this.memory = memory;
            this.log = log;
        }

        /// <summary>Beat gain under the master volume.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>Gap between the bodies within which the player is at the head (the melee reach setting).</summary>
        public float ReachWorld { get; set; } = 40f;

        /// <summary>A new area: no boss is known.</summary>
        public void Reset()
        {
            this.names.Clear();
            this.introduced.Clear();
            this.following = null;
        }

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            if (!playing || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            if (!this.Gather(here))
            {
                this.Lose();
                return;
            }

            var parts = this.near.Select(entry => entry.Part with { Name = this.NameOf(entry.Part.Id, entry.Render) }).ToList();
            if (WeakPoints.Pick(parts, here) is not { } head)
            {
                this.Lose();
                return;
            }

            if (this.following != head.Id)
            {
                this.following = head.Id;
                this.log.Info($"Weak point: {head.Name} ({head.Path})");
                if (this.introduced.Add(head.Name))
                {
                    this.output.Speak(Announcements.WeakPointGuide(head.Name));
                }
            }

            var gap = Vector2.Distance(head.Position, here) - head.Radius - player.ModelBounds.X;
            var now = Environment.TickCount64;
            if (now - this.lastBeat < WeakPoints.IntervalMs(gap, this.ReachWorld))
            {
                return;
            }

            this.lastBeat = now;
            var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, head.Position - here));
            this.audio.Play(this.beat, pan, pitch, this.Gain * (gap <= this.ReachWorld ? AtPartGain : 1f), "weak_point");
        }

        /// <summary>The living monsters within the arena; false (no names read) when none of them is a tail.</summary>
        private bool Gather(Vector2 here)
        {
            this.near.Clear();
            var tail = false;
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || entity.EntityType != EntityTypes.Monster || !EntityCatalog.IsAliveEnemy(entity) ||
                    !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var at = new Vector2(render.WorldPosition.X, render.WorldPosition.Y);
                if (Vector2.Distance(at, here) > FightWorld)
                {
                    continue;
                }

                tail |= WeakPoints.IsTail(entity.Path);
                var unique = entity.TryGetComponent<ObjectMagicProperties>(out var magic) && magic.Rarity == Rarity.Unique;
                var part = new BossPart(entity.Id, string.Empty, entity.Path, at, render.ModelBounds.X, unique, EntityCatalog.IsInPlay(this.memory, entity));
                this.near.Add((part, render.Address));
            }

            return tail;
        }

        private string NameOf(uint id, IntPtr render)
        {
            if (!this.names.TryGetValue(id, out var name) || name.Length == 0)
            {
                name = this.elements.ReadWStringStructAt(render + GameLayout.RenderComponent.NameOffset) ?? string.Empty;
                this.names[id] = name;
            }

            return name;
        }

        private void Lose()
        {
            if (this.following != null)
            {
                this.log.Info("Weak point out of play");
                this.following = null;
            }
        }
    }
}
