namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     D2R's unexplored-room loop: the nearest place where unseen ground begins plays the loop where it lies, panned and
    ///     pitched like the guide and quieter with distance, so the player always hears which way is left to explore
    ///     (docs/radar.md). Silent in towns, in menus and dialogue, and while the guide leads elsewhere.
    /// </summary>
    public sealed class UnexploredLoop
    {
        /// <summary>The asset shipped with the mod (the D2R mod's "unexplored room", supplied by the user, at the mixer rate).</summary>
        public const string LoopName = "unexplored_room";

        private const int PollMs = 250;

        /// <summary>Heard up to about sixty steps away; beyond, the scanner's Unexplored category still lists it.</summary>
        private const float RangeWorld = 6000f;

        /// <summary>Level at the edge of the range, as a share of the near level (ObjectSounds' far gain).</summary>
        private const float FarGain = 0.25f;

        /// <summary>Closer than this the loop sits in the middle (ObjectSounds' centre distance).</summary>
        private const float CentreWorld = 300f;

        private readonly AudioCueService audio;
        private readonly EntityCatalog catalog;
        private readonly float[]? loop;
        private readonly PluginLog log;
        private AudioCueService.Bed? bed;
        private Vector2? target;
        private long lastPoll;

        public UnexploredLoop(AudioCueService audio, EntityCatalog catalog, float[]? loop, PluginLog log)
        {
            this.log = log;
            this.audio = audio;
            this.catalog = catalog;
            this.loop = loop;
        }

        public float Gain { get; set; } = 1f;

        /// <summary>A new area: the old target means nothing there.</summary>
        public void Reset() => this.target = null;

        /// <summary>Per frame; <paramref name="playing"/> false (menu, dialogue, town, guide on, setting off) fades it out.</summary>
        public void Tick(bool playing)
        {
            if (this.loop == null)
            {
                return;
            }

            var area = GameHelper.Core.States.InGameStateObject.CurrentAreaInstance;
            if (!playing || !Seats.Followed.TryGetComponent<Render>(out var player) || area.WorldToGridConvertor <= 0)
            {
                this.bed?.Set(0f, 0f);
                return;
            }

            var toWorld = area.WorldToGridConvertor;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var now = Environment.TickCount64;
            if (now - this.lastPoll >= PollMs)
            {
                this.lastPoll = now;
                var next = ExplorationMap.Follow(this.target, this.catalog.UnexploredCells(), here / toWorld);
                if (next != this.target && (next is not { } moved || this.target is not { } last || Vector2.Distance(moved, last) > ExplorationMap.SamePlaceCells))
                {
                    this.log.Debug(next is { } place ? $"Unexplored loop at cell {place.X:0},{place.Y:0}, {Vector2.Distance(here, place * toWorld):0} units away" : "Unexplored loop: nothing left");
                }

                this.target = next;
            }

            if (this.target is not { } cell || Vector2.Distance(here, cell * toWorld) > RangeWorld)
            {
                this.bed?.Set(0f, 0f);
                return;
            }

            var offset = (cell * toWorld) - here;
            var distance = offset.Length();
            var closeness = 1f - Math.Clamp(distance / RangeWorld, 0f, 1f);
            var (side, height) = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, offset));
            var spread = Math.Min(1f, distance / CentreWorld);
            this.bed ??= this.audio.AddBed(this.loop);
            this.bed.Set(side * spread, this.Gain * (FarGain + ((1f - FarGain) * closeness)), 1f + ((height - 1f) * spread));
        }
    }
}
