namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Names what the right stick aims at (D2R target-sight: the game's own target, its name and life), once per
    ///     new target while the stick is pushed. The target is the game's mouse-over entity, which follows the
    ///     controller's aim (GameHelper <c>InGameState.MouseOverEntity</c>). Sweeping across a pack interrupts the
    ///     previous name; a first name waits behind what is being said.
    /// </summary>
    public sealed class TargetNarrator
    {
        private const int PollMs = 100;

        /// <summary>A target must stay under the aim this long before it is named (the aim sweeps over others).</summary>
        private const int SettleMs = 150;

        /// <summary>The same target is named again only after this long without it.</summary>
        private const int RepeatMs = 3000;

        /// <summary>Names within this long of the previous one cut it off (sweeping); later ones queue.</summary>
        private const int SweepMs = 2000;

        /// <summary>D2R crosshair: a monster beeped for stays quiet for this long of being the target.</summary>
        private const int BeepQuietMs = 3000;

        private readonly AudioCueService audio;
        private readonly float[] beep;
        private readonly System.Collections.Generic.Dictionary<IntPtr, long> beeped = new();
        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private long lastPoll;
        private IntPtr candidate;
        private long candidateSince;
        private IntPtr spoken;
        private long spokenAt = long.MinValue / 2;

        public TargetNarrator(AudioCueService audio, HostMemoryReader memory, UiElementReader elements, IAccessibilityOutputService output, PluginLog log)
        {
            this.audio = audio;
            this.beep = CueSynth.Crosshair(AudioCueService.SampleRate);
            this.memory = memory;
            this.elements = elements;
            this.output = output;
            this.log = log;
        }

        /// <summary>Crosshair beep gain (settings).</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>Beep when the game's target lands on a living enemy (D2R crosshair).</summary>
        public bool Beep { get; set; } = true;

        /// <summary>Speak the target's name while aiming.</summary>
        public bool Names { get; set; } = true;

        /// <summary>Per frame; <paramref name="aiming"/> while the right stick is pushed (and L3, the scanner, is not held).</summary>
        public void Tick(bool playing, bool aiming)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            var hovered = Core.States.InGameStateObject.MouseOverEntity.Address;
            if (hovered != this.candidate)
            {
                (this.candidate, this.candidateSince) = (hovered, now);
                return;
            }

            this.BeepOnEnemy(hovered, now);
            if (!this.Names || !aiming || hovered == IntPtr.Zero || now - this.candidateSince < SettleMs ||
                (hovered == this.spoken && now - this.spokenAt < RepeatMs))
            {
                if (hovered == this.spoken)
                {
                    this.spokenAt = now; // still on it: keep it from being named again when the aim comes back
                }

                return;
            }

            var entity = Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values.FirstOrDefault(candidate => candidate.Address == hovered);
            if (entity == null || entity.EntitySubtype == EntitySubtypes.PlayerSelf || this.Describe(entity) is not { } spoken)
            {
                return;
            }

            this.output.Speak(spoken, now - this.spokenAt < SweepMs);
            this.log.Debug($"Target -> {entity.Path} \"{spoken}\"");
            (this.spoken, this.spokenAt) = (hovered, now);
        }

        /// <summary>Two blips the first time a living enemy becomes the target, then quiet for it while it stays one.</summary>
        private void BeepOnEnemy(IntPtr hovered, long now)
        {
            if (!this.Beep || hovered == IntPtr.Zero)
            {
                return;
            }

            if (this.beeped.TryGetValue(hovered, out var at) && now - at < BeepQuietMs)
            {
                this.beeped[hovered] = now; // still the target: stays quiet
                return;
            }

            var entity = Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values.FirstOrDefault(candidate => candidate.Address == hovered);
            if (entity == null || entity.EntityType != EntityTypes.Monster || !EntityCatalog.IsAliveEnemy(entity))
            {
                return;
            }

            this.beeped[hovered] = now;
            if (this.beeped.Count > 64)
            {
                foreach (var stale in this.beeped.Where(pair => now - pair.Value > BeepQuietMs).Select(pair => pair.Key).ToList())
                {
                    this.beeped.Remove(stale);
                }
            }

            var pan = 0f;
            if (entity.TryGetComponent<Render>(out var render) &&
                Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<Render>(out var player))
            {
                var offset = new System.Numerics.Vector2(render.WorldPosition.X - player.WorldPosition.X, render.WorldPosition.Y - player.WorldPosition.Y);
                pan = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, offset)).Pan;
            }

            this.audio.Play(this.beep, pan, 1f, this.Gain);
        }

        /// <summary>Name, rarity of a magic or better monster, life when hurt; null for unnamed things and corpses.</summary>
        private string? Describe(Entity entity)
        {
            if (!entity.TryGetComponent<Render>(out var render) ||
                this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) is not { Length: > 0 } name)
            {
                return null;
            }

            if (entity.EntityType != EntityTypes.Monster)
            {
                return EntityCatalog.IsInPlay(this.memory, entity) ? name : null;
            }

            if (!EntityCatalog.IsAliveEnemy(entity) || !entity.TryGetComponent<Life>(out var life))
            {
                return null;
            }

            if (entity.TryGetComponent<ObjectMagicProperties>(out var magic) && magic.Rarity != Rarity.Normal)
            {
                name = Announcements.EnemyWithRarity(name, magic.Rarity);
            }

            return Announcements.Target(name, life.Health.Total > 0 ? life.Health.CurrentInPercent() : null);
        }
    }
}
