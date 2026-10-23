namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Proximity sounds for things worth walking to (D2R proximity loops): the nearest item, chest or shrine, exit
    ///     or portal, and waypoint or checkpoint on screen each repeat the game's own sound for them, on their side,
    ///     louder and more often as the player gets closer, and stop once the player stands on them. One voice per
    ///     group so loot, chests and exits stay apart; the groups take turns so two never start together.
    /// </summary>
    public sealed class ObjectSounds
    {
        private const int PollMs = 250;

        /// <summary>Heard within about a screen; silent when standing on it (the proximity narrator names it then).</summary>
        private const float RangeWorld = 1300f;
        private const float OnItWorld = 150f;

        /// <summary>Repeat interval near / far, and the gain at the edge of the range.</summary>
        private const int NearIntervalMs = 1300;
        private const int FarIntervalMs = 3200;
        private const float FarGain = 0.3f;

        /// <summary>Groups never start within this time of each other.</summary>
        private const int GroupGapMs = 220;


        private static readonly (string Group, Func<ScanEntry, bool> Member)[] Groups =
        {
            ("loot", entry => entry.Category == ScanCategory.Items),
            ("chests", entry => entry.Category == ScanCategory.Chests),
            ("exits", entry => entry.Category == ScanCategory.Exits || entry.Kind == "portal"),
            ("waypoints", entry => entry.Kind is "waypoint" or "checkpoint"),
        };

        private readonly AudioCueService audio;
        private readonly EntityCatalog catalog;
        private readonly GameSounds sounds;
        private readonly Dictionary<string, long> lastPlayed = new();
        private readonly Dictionary<string, ScanEntry> nearest = new();
        private long lastPoll;
        private long lastAnyPlayed;

        public ObjectSounds(AudioCueService audio, EntityCatalog catalog, GameSounds sounds)
        {
            this.audio = audio;
            this.catalog = catalog;
            this.sounds = sounds;
        }

        public float Gain { get; set; } = 1f;

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            if (!playing || this.sounds.Count == 0)
            {
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!area.Player.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var now = Environment.TickCount64;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            if (now - this.lastPoll >= PollMs)
            {
                this.lastPoll = now;
                this.nearest.Clear();
                var entries = this.catalog.Entries().Where(entry => EntityCatalog.IsEntity(entry) && entry.Category != ScanCategory.Quest).ToList();
                foreach (var (group, member) in Groups)
                {
                    if (entries.Where(member).FirstOrDefault() is { } entry && Distance(here, entry) <= RangeWorld)
                    {
                        this.nearest[group] = entry;
                    }
                }
            }

            foreach (var (group, entry) in this.nearest)
            {
                var distance = Distance(here, entry);
                if (distance <= OnItWorld || distance > RangeWorld || now - this.lastAnyPlayed < GroupGapMs ||
                    this.SoundFor(entry) is not { } samples)
                {
                    continue;
                }

                var closeness = 1f - Math.Clamp(distance / RangeWorld, 0f, 1f);
                var interval = FarIntervalMs - (closeness * (FarIntervalMs - NearIntervalMs));
                if (this.lastPlayed.TryGetValue(group, out var last) && now - last < interval)
                {
                    continue;
                }

                var screen = ScreenProjection.Direction(player.WorldPosition, new Vector2(entry.WorldX, entry.WorldY) - here);
                var pan = screen.LengthSquared() > 0 ? Vector2.Normalize(screen).X : 0f;
                this.audio.Play(samples, pan, 1f, this.Gain * (FarGain + ((1f - FarGain) * closeness)));
                this.lastPlayed[group] = now;
                this.lastAnyPlayed = now;
            }
        }

        private float[]? SoundFor(ScanEntry entry) => entry.Kind switch
        {
            "gold" => this.sounds.Get("gold"),
            "currency" => this.sounds.Get("currency"),
            "quest" => this.sounds.Get("quest_item"),
            "rare" => this.sounds.Get("item_rare"),
            "chest" => this.sounds.Get("chest"),
            "shrine" => this.sounds.Get("shrine"),
            "portal" => this.sounds.Get("portal"),
            "waypoint" => this.sounds.Get("waypoint"),
            "checkpoint" => this.sounds.Get("checkpoint"),
            "exit" => this.sounds.Get("exit"),
            _ => entry.Category == ScanCategory.Items ? this.sounds.Get("item") : null,
        };

        private static float Distance(Vector2 here, ScanEntry entry) => Vector2.Distance(here, new Vector2(entry.WorldX, entry.WorldY));

    }
}
