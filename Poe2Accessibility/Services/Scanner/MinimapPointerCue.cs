namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The minimap's edge arrow as a sound: while the player walks, the halo loop comes from the boss room or quest mark the
    ///     arrow points to, in a straight line as the arrow does, not along the way (the owner, 2026-11-18; docs/radar.md
    ///     "The minimap's arrow"). The way there is still the player's to find.
    /// </summary>
    public sealed class MinimapPointerCue
    {
        public const string SoundKey = "pointer";

        private const int PollMs = 250;

        /// <summary>It keeps sounding this long after the last step, so a pause between steps does not cut it.</summary>
        private const int WalkHoldMs = 1500;

        private readonly AudioCueService audio;
        private readonly EntityCatalog catalog;
        private readonly PluginLog log;
        private readonly float[] loop;
        private AudioCueService.Bed? bed;
        private MinimapPointer.Mark? mark;
        private Vector2 lastPlace;
        private long walkedAt = long.MinValue / 2;
        private long lastPoll = long.MinValue / 2;

        public MinimapPointerCue(AudioCueService audio, EntityCatalog catalog, PluginLog log)
        {
            this.audio = audio;
            this.catalog = catalog;
            this.log = log;
            this.loop = CueSynth.Halo(AudioCueService.SampleRate);
        }

        public float Gain { get; set; } = 1f;

        /// <summary>A new area: the old arrow means nothing there.</summary>
        public void Reset() => this.mark = null;

        /// <summary>Per frame; <paramref name="playing"/> false (menu, dialogue, town, guide on, setting off) and fights silence it.</summary>
        public void Tick(bool playing, bool inFight)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!playing || inFight || !Seats.Followed.TryGetComponent<Render>(out var player) || area.WorldToGridConvertor <= 0)
            {
                this.Silence();
                return;
            }

            var toWorld = area.WorldToGridConvertor;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var now = Environment.TickCount64;
            if (now - this.lastPoll >= PollMs)
            {
                this.lastPoll = now;
                if (Vector2.Distance(here, this.lastPlace) >= LostHint.WalkingWorld)
                {
                    this.walkedAt = now;
                }

                this.lastPlace = here;
                var picked = MinimapPointer.Pick(this.catalog.PointerMarks(), here / toWorld);
                if (picked?.Id != this.mark?.Id)
                {
                    this.log.Info(picked is { } next ? $"Minimap arrow toward entry {next.Id} at cell {next.Cell.X:0},{next.Cell.Y:0} (reach {next.ReachCells:0})" : "Minimap arrow gone");
                }

                this.mark = picked;
            }

            if (this.mark is not { } target || now - this.walkedAt > WalkHoldMs)
            {
                this.Silence();
                return;
            }

            var offset = (target.Cell * toWorld) - here;
            var (pan, pitch) = ScreenProjection.Voice(player.WorldPosition, offset, offset.Length(), LostHint.CentreWorld);
            this.bed ??= this.audio.AddBed(this.loop, SoundKey);
            this.bed.Set(pan, this.Gain, pitch);
        }

        private void Silence() => this.bed?.Set(0f, 0f);
    }
}
