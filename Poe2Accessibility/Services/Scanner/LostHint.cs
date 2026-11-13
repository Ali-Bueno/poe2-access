namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     A slight hint for a lost player: after a while walking without new ground, the D2R unexplored-room sound plays for a
    ///     few seconds on the side the way to the objective lies, then stops (docs/radar.md). The radar is what explores.
    /// </summary>
    public sealed class LostHint
    {
        /// <summary>The asset shipped with the mod (the D2R mod's "unexplored room", supplied by the user, at the mixer rate).</summary>
        public const string LoopName = "unexplored_room";

        private const int PollMs = 250;

        /// <summary>Long enough to tell its side, short enough to stay a hint.</summary>
        private const int SoundsMs = 4000;

        /// <summary>Moving less than this between polls is standing still: a quarter of a second of walking is about 100.</summary>
        private const float WalkingWorld = 30f;

        /// <summary>Closer than this the sound sits in the middle (ObjectSounds' centre distance).</summary>
        private const float CentreWorld = 300f;

        private readonly AudioCueService audio;
        private readonly EntityCatalog catalog;
        private readonly float[]? loop;
        private readonly PluginLog log;
        private readonly LostWatch watch = new();
        private AudioCueService.Bed? bed;
        private Vector2? target;
        private Vector2 lastPlace;
        private long startedAt;
        private long lastPoll;

        public LostHint(AudioCueService audio, EntityCatalog catalog, float[]? loop, PluginLog log)
        {
            this.log = log;
            this.audio = audio;
            this.catalog = catalog;
            this.loop = loop;
        }

        public float Gain { get; set; } = 1f;

        /// <summary>How long a player walks without new ground before the hint plays.</summary>
        public int LostAfterMs { get; set; } = 180000;

        /// <summary>How long a player walks over ground already seen before the sound points at the nearest unexplored ground that goes on (0: never).</summary>
        public int SeenGroundAfterMs { get; set; } = 30000;

        /// <summary>How long a player moves through an area, new ground or not, before the hint plays (0: never).</summary>
        public int SoundAfterAreaMs { get; set; } = 600000;

        /// <summary>...and before the way on is said in words (0: never).</summary>
        public int SpokenAfterAreaMs { get; set; } = 1200000;

        /// <summary>Says the way on, given the cell where it leaves the seen ground.</summary>
        public Action<Vector2>? SayWay { get; set; }

        /// <summary>A new area: the old way means nothing there.</summary>
        public void Reset()
        {
            this.target = null;
            this.watch.Reset();
        }

        /// <summary>Per frame; <paramref name="playing"/> false (menu, dialogue, town, guide on, setting off) stops it.</summary>
        public void Tick(bool playing, bool inFight)
        {
            if (this.loop == null)
            {
                return;
            }

            var area = GameReader.Core.States.InGameStateObject.CurrentAreaInstance;
            if (!playing || !Seats.Followed.TryGetComponent<Render>(out var player) || area.WorldToGridConvertor <= 0)
            {
                this.target = null;
                this.bed?.Set(0f, 0f);
                return;
            }

            var toWorld = area.WorldToGridConvertor;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var now = System.Environment.TickCount64;
            if (now - this.lastPoll >= PollMs)
            {
                this.lastPoll = now;
                var moving = Vector2.Distance(here, this.lastPlace) >= WalkingWorld;
                this.lastPlace = here;
                var lost = this.watch.Update(now, this.catalog.SeenBlocks, moving && !inFight, this.LostAfterMs, moving);
                var news = this.watch.Explored(this.SoundAfterAreaMs, this.SpokenAfterAreaMs);
                if (news == LostNews.Spoken && this.catalog.WayOnCell() is { } way)
                {
                    this.target = way;
                    this.startedAt = now;
                    this.log.Info($"Lost hint in words toward cell {way.X:0},{way.Y:0}");
                    this.SayWay?.Invoke(way);
                }
                else if ((lost || news != LostNews.None) && this.catalog.LostHintCell() is { } cell)
                {
                    this.target = cell;
                    this.startedAt = now;
                    this.log.Info($"Lost hint toward cell {cell.X:0},{cell.Y:0}{(lost ? string.Empty : " (long in the area)")}");
                }
                else if (this.watch.OverSeenGround(this.SeenGroundAfterMs) && this.catalog.UnexploredCells() is { Count: > 0 } onward)
                {
                    // Walking where all is seen: toward the nearest ground that goes on, not toward the objective.
                    this.target = onward[0];
                    this.startedAt = now;
                    this.log.Info($"Unexplored ground toward cell {onward[0].X:0},{onward[0].Y:0}");
                }
            }

            if (this.target is not { } place || now - this.startedAt >= SoundsMs)
            {
                this.target = null;
                this.bed?.Set(0f, 0f);
                return;
            }

            var offset = (place * toWorld) - here;
            var (pan, pitch) = ScreenProjection.Voice(player.WorldPosition, offset, offset.Length(), CentreWorld);
            this.bed ??= this.audio.AddBed(this.loop, LoopName);
            this.bed.Set(pan, this.Gain, pitch);
        }
    }
}
