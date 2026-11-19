namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;
    using GameReader.RemoteObjects.Components;

    /// <summary>A radar cue from small unseen ground the player walks away from (<see cref="LeftBehindWatch"/>, docs/radar.md "Left behind").</summary>
    public sealed class LeftBehind
    {
        /// <summary>The cue's key: its volume and its line in the sound glossary.</summary>
        public const string SoundKey = "radar_left_behind";

        /// <summary>Twice a second: walking two looks away from a nook takes seconds.</summary>
        private const int PollMs = 500;

        /// <summary>Closer than this the sound sits in the middle (ObjectSounds' centre distance).</summary>
        private const float CentreWorld = 300f;

        private readonly AudioCueService audio;
        private readonly EntityCatalog catalog;
        private readonly PluginLog log;
        private readonly LeftBehindWatch watch = new();
        private readonly float[] sound;
        private long lastPoll = long.MinValue / 2;

        public LeftBehind(AudioCueService audio, EntityCatalog catalog, PluginLog log)
        {
            this.audio = audio;
            this.catalog = catalog;
            this.log = log;
            this.sound = CueSynth.LeftBehind(AudioCueService.SampleRate, 0);
        }

        public float Gain { get; set; } = 1f;

        /// <summary>A new area: nothing near is known.</summary>
        public void Reset() => this.watch.Reset();

        /// <summary>Per frame; <paramref name="playing"/> false (menu, dialogue, town, setting off) keeps it silent.</summary>
        public void Tick(bool playing, bool inFight)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.lastPoll = now;
            var toWorld = GameReader.Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var near = this.catalog.SmallUnseenNear(here, LeftBehindWatch.NearCells);
            if (toWorld <= 0 || this.watch.Update(here, near, this.catalog.IsSeen, inFight, now) is not { } cell)
            {
                return;
            }

            var offset = (cell - here) * toWorld;
            var (pan, pitch) = ScreenProjection.Voice(player.WorldPosition, offset, offset.Length(), CentreWorld);
            this.audio.Play(this.sound, pan, pitch, this.Gain, SoundKey);
            this.log.Info($"Small unexplored ground left behind at cell {cell.X:0},{cell.Y:0}");
        }
    }
}
