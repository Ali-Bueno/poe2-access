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
    ///     or portal, and waypoint or checkpoint on screen each repeat the game's own sound for them (an item its own drop
    ///     sound, <see cref="ItemDropSounds"/>, with a rarity layer over it for a rare or unique), on their side,
    ///     louder and more often as the player gets closer, higher above and lower below, centred when the player stands
    ///     on them. One voice per group so chests and exits stay apart, one per item for the nearest few (several drops
    ///     were heard as one, user 2026-10-24); voices take turns so two never start together. Every breakable in range
    ///     sounds, each with its material's sound (user 2026-10-27), and every chest and searchable body (user 2026-10-28),
    ///     each crowd staggered only among itself. A portal, exit, waypoint or checkpoint plays its idle bed without a
    ///     break instead (user 2026-10-27), louder as the player nears.
    /// </summary>
    public sealed class ObjectSounds
    {
        private const int PollMs = 250;

        /// <summary>
        ///     Heard within about a screen and still on it (user 2026-10-27: it went silent there); this near it slides
        ///     to the centre, or its side would flip with every step over it.
        /// </summary>
        public const float RangeWorld = 1300f;
        private const float CentreWorld = 150f;

        /// <summary>
        ///     Repeat interval near / far, and the gain at the edge of the range. Near, often enough that its side stays
        ///     true while walking (1.3 s was heard as misplaced, user 2026-10-26).
        /// </summary>
        private const int NearIntervalMs = 600;
        private const int FarIntervalMs = 3200;
        private const float FarGain = 0.3f;

        /// <summary>Groups never start within this time of each other.</summary>
        private const int GroupGapMs = 220;

        /// <summary>Items heard at once, nearest first, each with its own sound and turn.</summary>
        private const int MaxLootVoices = 4;

        /// <summary>Breakables heard at once, nearest first: all of a room's urns, not only the nearest (user 2026-10-27).</summary>
        private const int MaxBreakableVoices = 12;

        /// <summary>Chests and bodies heard at once, nearest first: they were heard one at a time (user 2026-10-28).</summary>
        private const int MaxChestVoices = 6;

        /// <summary>A crowd's voices (chests, breakables) never start within this time of each other, so a cluster is heard as several.</summary>
        private const int CrowdGapMs = 70;

        /// <summary>The rarity layer under a rare or unique item's own sound.</summary>
        private const float RareLayerGain = 0.6f;

        private const string LootGroup = "loot";

        /// <summary>
        ///     Each group's members and voices: one voice keeps its turn with the other groups; a crowd of several (chests,
        ///     breakables) staggers only among itself.
        /// </summary>
        private static readonly (string Group, int Voices, Func<ScanEntry, bool> Member)[] Groups =
        {
            (LootGroup, MaxLootVoices, entry => entry.Category == ScanCategory.Items),
            ("chests", MaxChestVoices, entry => entry.Kind is "chest" or "corpse"),
            ("breakables", MaxBreakableVoices, entry => entry.Kind == "breakable"),
            ("shrines", 1, entry => entry.Category == ScanCategory.Chests && entry.Kind is not ("chest" or "corpse" or "breakable")),
            ("encounters", 1, entry => entry.Category == ScanCategory.Encounters),
            ("exits", 1, entry => entry.Category == ScanCategory.Exits || entry.Kind == "portal"),
            ("waypoints", 1, entry => entry.Kind is "waypoint" or "checkpoint"),
            ("quests", 1, entry => entry.Kind == "npc_quest"),
        };

        private readonly AudioCueService audio;
        private readonly EntityCatalog catalog;
        private readonly GameSounds sounds;
        private readonly ItemDropSounds drops;
        private readonly GameSounds diabloSounds;
        private readonly float[] questRing = CueSynth.Ring(AudioCueService.SampleRate);
        private readonly Dictionary<string, long> lastPlayed = new();
        private readonly Dictionary<string, (string Crowd, ScanEntry Entry)> nearest = new();
        private readonly Dictionary<string, long> crowdStarted = new();
        private readonly Dictionary<string, AudioCueService.Bed> beds = new();
        private readonly Dictionary<string, (float Pan, float Pitch, float Gain)> heardBeds = new();
        private long lastPoll;
        private long lastAnyPlayed;

        public ObjectSounds(AudioCueService audio, EntityCatalog catalog, GameSounds sounds, ItemDropSounds drops, GameSounds diabloSounds)
        {
            this.audio = audio;
            this.catalog = catalog;
            this.sounds = sounds;
            this.drops = drops;
            this.diabloSounds = diabloSounds;
        }

        public float Gain { get; set; } = 1f;

        /// <summary>Which items sound (<see cref="LootFilter"/> level, settings).</summary>
        public string LootLevel { get; set; } = LootFilter.All;

        /// <summary>The loot sounds chosen (<see cref="ItemSoundSet"/>, settings).</summary>
        public string SoundSet { get; set; } = ItemSoundSet.Game;

        /// <summary>True while loot keeps quiet (a fight, <see cref="FightWatch"/>; what falls quiet: <see cref="FightState.QuietsInFight"/>).</summary>
        public Func<bool> QuietLoot { get; set; } = () => false;

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            this.heardBeds.Clear();
            if (!playing || this.sounds.Count == 0 || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                this.SetBeds();
                return;
            }

            var now = Environment.TickCount64;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            if (now - this.lastPoll >= PollMs)
            {
                this.lastPoll = now;
                this.nearest.Clear();
                var entries = this.catalog.Entries().Where(entry => EntityCatalog.IsEntity(entry) && entry.Category != ScanCategory.Quest).ToList();
                var quietLoot = this.QuietLoot();
                foreach (var (group, voices, member) in Groups)
                {
                    foreach (var entry in entries.Where(member)
                                 .Where(entry => !(quietLoot && FightState.QuietsInFight(entry.Category, entry.Kind)) &&
                                                 (group != LootGroup || LootFilter.Allows(this.LootLevel, entry.Loot)))
                                 .Where(entry => Distance(here, entry) <= RangeWorld).Take(voices))
                    {
                        // Items take turns with the other groups, one at a time, so each drop is heard apart.
                        var crowd = voices > 1 && group != LootGroup ? group : string.Empty;
                        this.nearest[voices == 1 ? group : $"{group}:{entry.Id}"] = (crowd, entry);
                    }
                }

                foreach (var gone in this.lastPlayed.Keys.Where(key => !this.nearest.ContainsKey(key)).ToList())
                {
                    this.lastPlayed.Remove(gone);
                }
            }

            foreach (var (group, (crowd, entry)) in this.nearest)
            {
                var distance = Distance(here, entry);
                if (distance > RangeWorld)
                {
                    continue;
                }

                var closeness = 1f - Math.Clamp(distance / RangeWorld, 0f, 1f);
                var (side, height) = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, new Vector2(entry.WorldX, entry.WorldY) - here));
                var spread = Math.Min(1f, distance / CentreWorld);
                var (pan, pitch) = (side * spread, 1f + ((height - 1f) * spread));
                var gain = this.Gain * (FarGain + ((1f - FarGain) * closeness));
                if (this.sounds.GetLoop(entry.Kind) != null)
                {
                    this.heardBeds[entry.Kind] = (pan, pitch, gain);
                    continue;
                }

                var interval = FarIntervalMs - (closeness * (FarIntervalMs - NearIntervalMs));
                if (crowd.Length > 0 ? now - this.crowdStarted.GetValueOrDefault(crowd) < CrowdGapMs : now - this.lastAnyPlayed < GroupGapMs)
                {
                    continue;
                }

                if (this.SoundFor(entry) is not { } samples ||
                    (this.lastPlayed.TryGetValue(group, out var last) && now - last < interval))
                {
                    continue;
                }

                this.audio.Play(samples, pan, pitch, gain, this.SoundKey(entry));
                if (this.SoundSet == ItemSoundSet.Game && this.RarityLayer(entry.Kind) is { } key && this.sounds.Get(key) is { } layer && layer != samples)
                {
                    this.audio.Play(layer, pan, pitch, gain * RareLayerGain, key);
                }

                this.lastPlayed[group] = now;
                if (crowd.Length > 0)
                {
                    this.crowdStarted[crowd] = now;
                }
                else
                {
                    this.lastAnyPlayed = now;
                }
            }

            this.SetBeds();
        }

        /// <summary>Each idle bed follows the place heard this frame; the rest fade out.</summary>
        private void SetBeds()
        {
            foreach (var (kind, (pan, pitch, gain)) in this.heardBeds)
            {
                if (!this.beds.ContainsKey(kind) && this.sounds.GetLoop(kind) is { } loop)
                {
                    this.beds[kind] = this.audio.AddBed(loop, kind);
                }
            }

            foreach (var (kind, bed) in this.beds)
            {
                if (this.heardBeds.TryGetValue(kind, out var voice))
                {
                    bed.Set(voice.Pan, voice.Gain, voice.Pitch);
                }
                else
                {
                    bed.Set(0f, 0f);
                }
            }
        }

        /// <summary>The Diablo IV sound of an entry when that set is chosen and has one.</summary>
        private float[]? DiabloSound(ScanEntry entry) =>
            this.SoundSet == ItemSoundSet.DiabloIV &&
            ItemSoundSet.DiabloIVSample(entry.Kind, entry.BaseId, entry.Loot, this.drops.SampleName(entry.BaseId)) is { } sample
                ? this.diabloSounds.Get(sample)
                : null;

        private float[]? SoundFor(ScanEntry entry) => this.DiabloSound(entry) ?? this.drops.For(entry.BaseId) ?? entry.Kind switch
        {
            "gold" => this.sounds.Get("gold"),
            "currency" => this.sounds.Get("currency"),
            "quest" => this.sounds.Get("quest_item"),
            "rare" or "unique" => this.sounds.Get(this.RarityLayer(entry.Kind)!),
            "chest" => this.sounds.Get("chest"),
            "corpse" => this.sounds.Get(GameSounds.Corpse) ?? this.sounds.Get("chest"),
            "breakable" => this.sounds.Get($"{GameSounds.BreakableFolder}/{entry.Sound}") ?? this.sounds.Get(GameSounds.BreakableFallback),
            "switch" => this.sounds.Get("switch"),
            "shrine" => this.sounds.Get("shrine"),
            "ritual" => this.sounds.Get("ritual"),
            "portal" => this.sounds.Get("portal"),
            "waypoint" => this.sounds.Get("waypoint"),
            "checkpoint" => this.sounds.Get("checkpoint"),
            "npc_quest" => this.questRing,
            "exit" => this.sounds.Get("exit"),
            _ => entry.Category == ScanCategory.Items ? this.sounds.Get("item") : null,
        };

        /// <summary>The sound marking a rare or unique item, null for another kind; uniques use the rare one until their own is extracted.</summary>
        private string? RarityLayer(string kind) => kind switch
        {
            "rare" => "item_rare",
            "unique" => this.sounds.Get("item_unique") != null ? "item_unique" : "item_rare",
            _ => null,
        };

        /// <summary>The glossary key whose volume the entry's sound follows: an item by its group (a Diablo IV rare or unique by its rarity), the rest by kind.</summary>
        private string SoundKey(ScanEntry entry)
        {
            if (entry.Category != ScanCategory.Items)
            {
                return entry.Kind;
            }

            return this.SoundSet == ItemSoundSet.DiabloIV && this.RarityLayer(entry.Kind) is { } rarity ? rarity : ItemSoundGroup.Of(entry.Kind, entry.BaseId);
        }

        private static float Distance(Vector2 here, ScanEntry entry) => Vector2.Distance(here, new Vector2(entry.WorldX, entry.WorldY));

    }
}
