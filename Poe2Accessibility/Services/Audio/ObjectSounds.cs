namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     Proximity sounds for things worth walking to (D2R proximity loops): the nearest item, chest or shrine, exit
    ///     or portal, and waypoint or checkpoint on screen each repeat the game's own sound for them (an item its own drop
    ///     sound, <see cref="ItemDropSounds"/>, with a layer over it for a rare, a unique or a quest item), on their side,
    ///     louder and more often as the player gets closer, higher above and lower below, centred when the player stands
    ///     on them. One voice per group so chests and exits stay apart, one per item for the nearest few (several drops
    ///     were heard as one, user 2026-10-24); voices take turns so two never start together. Every breakable in range
    ///     sounds, each with its material's sound (user 2026-10-27), and every chest and searchable body (user 2026-10-28),
    ///     each crowd staggered only among itself. A portal, exit, waypoint or checkpoint plays its idle bed without a
    ///     break instead (user 2026-10-27), louder as the player nears. The Diablo IV set (<see cref="ItemSoundSet"/>) is
    ///     all loops: each item, chest or shrine heard keeps its own voice going, as in D2R; repeating a piece of those
    ///     recordings was heard to stop and start (user 2026-11-04).
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
        ///     The range of what a clock runs against: a room's crystals stand 2000 to 2800 apart (dump 2026-11-07), so past the first the
        ///     next was out of hearing, and the 20 seconds went on finding it (user 2026-11-07).
        /// </summary>
        private const float TimedRangeWorld = 4000f;

        /// <summary>
        ///     The range of a way that opened: it opens in a fight that has carried the player off, and is looked for afterwards
        ///     (user 2026-11-12). Twice the usual range, not measured against a room.
        /// </summary>
        private const float OpenedWayRangeWorld = 2600f;

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

        /// <summary>The layer under the own sound of a rare, unique or quest item.</summary>
        private const float LayerGain = 0.6f;

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
            ("encounters", 1, entry => entry.Category == ScanCategory.Encounters && entry.Kind != MinimapMarks.RitualDoneKind && SoundKind(entry) != MinimapMarks.QuestObjectKind),
            ("exits", 1, entry => entry.Category == ScanCategory.Exits || entry.Kind == "portal"),
            ("waypoints", 1, entry => entry.Kind is "waypoint" or "checkpoint"),
            ("stashes", 1, entry => entry.Kind == MinimapMarks.StashKind),
            ("wells", 1, entry => entry.Kind == MinimapMarks.WellKind),
            // A sign to read was listed and named but never sounded (user 2026-11-12); one the quest names sounds as a quest object.
            ("signs", 1, entry => entry.Kind == MinimapMarks.SignKind && SoundKind(entry) != MinimapMarks.QuestObjectKind),
            ("opened ways", 1, entry => entry.Kind == OpenedWays.Kind),
            ("quests", 1, entry => entry.Kind == "npc_quest"),
            ("quest objects", 1, entry => SoundKind(entry) == MinimapMarks.QuestObjectKind),
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
        private readonly Dictionary<string, HeardLoop> heardBeds = new();
        private readonly List<string> goneBeds = new();
        private long lastPoll;
        private long lastAnyPlayed;
        private long wayOpenedAt = long.MinValue / 2;

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

        /// <summary>
        ///     A way opened just now: its sound once, at once and in full from wherever it stands, whatever else is sounding;
        ///     after it the entry sounds like any other object, by its distance.
        /// </summary>
        public void WayOpened(ScanEntry entry)
        {
            if (this.SoundFor(entry) is not { } samples || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var (pan, pitch) = ScreenProjection.Voice(player.WorldPosition, new Vector2(entry.WorldX, entry.WorldY) - here, Distance(here, entry), CentreWorld);
            this.audio.Play(samples, pan, pitch, this.Gain, this.SoundKey(entry));
            this.wayOpenedAt = Environment.TickCount64;
        }

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            this.heardBeds.Clear();
            // No early return on an empty game set: the Diablo IV loops and the quest ring do not come from the game's files.
            if (!playing || !Seats.Followed.TryGetComponent<Render>(out var player))
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
                var listed = this.catalog.Entries().Where(EntityCatalog.IsEntity).ToList();
                var wanted = listed.Where(entry => entry.Category == ScanCategory.Quest).Select(entry => entry.Id).ToHashSet();
                var entries = listed.Where(entry => entry.Category != ScanCategory.Quest).Select(entry => WhileWanted(entry, wanted)).ToList();
                var quietLoot = this.QuietLoot();
                foreach (var (group, voices, member) in Groups)
                {
                    foreach (var entry in entries.Where(member)
                                 .Where(entry => !(quietLoot && FightState.QuietsInFight(entry.Category, entry.Kind)) &&
                                                 (group != LootGroup || LootFilter.Allows(this.LootLevel, entry.Loot)))
                                 .Where(entry => Distance(here, entry) <= ReachOf(entry)).Take(voices))
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
                if (distance > ReachOf(entry))
                {
                    continue;
                }

                var closeness = 1f - Math.Clamp(distance / RangeOf(entry), 0f, 1f);
                var (pan, pitch) = ScreenProjection.Voice(player.WorldPosition, new Vector2(entry.WorldX, entry.WorldY) - here, distance, CentreWorld);
                var gain = this.Gain * ProximityFade.Gain(distance, RangeOf(entry), FarGain);
                if (this.sounds.GetLoop(entry.Kind) is { } idle)
                {
                    this.heardBeds[entry.Kind] = new HeardLoop(idle, entry.Kind, pan, pitch, gain, false);
                    continue;
                }

                if (this.DiabloLoop(entry) is { } loop)
                {
                    this.heardBeds[group] = new HeardLoop(loop, this.SoundKey(entry), pan, ItemSoundSet.LoopPitch(pitch), gain, true);
                    continue;
                }

                var interval = FarIntervalMs - (closeness * (FarIntervalMs - NearIntervalMs));
                if (crowd.Length > 0 ? now - this.crowdStarted.GetValueOrDefault(crowd) < CrowdGapMs : now - this.lastAnyPlayed < GroupGapMs)
                {
                    continue;
                }

                if (this.SoundFor(entry) is not { } samples ||
                    (this.lastPlayed.TryGetValue(group, out var last) && now - last < interval) ||
                    (entry.Kind == OpenedWays.Kind && now - this.wayOpenedAt < FarIntervalMs))
                {
                    continue;
                }

                this.audio.Play(samples, pan, pitch, gain, this.SoundKey(entry));
                if (this.SoundSet == ItemSoundSet.Game && (entry.QuestMarked ? MinimapMarks.QuestObjectKind : this.Layer(entry.Kind)) is { } key &&
                    this.sounds.Get(key) is { } layer && layer != samples)
                {
                    this.audio.Play(layer, pan, pitch, gain * LayerGain, key);
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

        /// <summary>Each loop follows what was heard this frame, kept going while it is; the rest fade out and go.</summary>
        private void SetBeds()
        {
            var power = 0f;
            foreach (var voice in this.heardBeds.Values)
            {
                power += voice.InPile ? voice.Gain * voice.Gain : 0f;
            }

            var pile = ItemSoundSet.PileScale(power, this.Gain);
            foreach (var (key, voice) in this.heardBeds)
            {
                if (!this.beds.TryGetValue(key, out var bed))
                {
                    // From anywhere in the recording: two of one sound must not play in step.
                    bed = this.beds[key] = this.audio.AddBed(voice.Loop, voice.Sound, Random.Shared.Next(voice.Loop.Length));
                }

                bed.Set(voice.Pan, voice.InPile ? voice.Gain * pile : voice.Gain, voice.Pitch);
            }

            this.goneBeds.Clear();
            foreach (var (key, bed) in this.beds)
            {
                if (!this.heardBeds.ContainsKey(key))
                {
                    bed.Stop();
                    this.goneBeds.Add(key);
                }
            }

            foreach (var key in this.goneBeds)
            {
                this.beds.Remove(key);
            }
        }

        /// <summary>The Diablo IV loop of an entry when that set is chosen and has one.</summary>
        private float[]? DiabloLoop(ScanEntry entry) =>
            this.SoundSet == ItemSoundSet.DiabloIV &&
            ItemSoundSet.DiabloIVSample(SoundKind(entry), entry.BaseId, entry.Loot, this.drops.SampleName(entry.BaseId)) is { } sample
                ? this.diabloSounds.GetLoop(sample)
                : null;

        /// <summary>
        ///     An object the quest tracker names sounds as a quest object while it does: the Relic Altar has no quest icon, only
        ///     "Coloca tu barya en el Altar de reliquias" (user 2026-11-07). Characters and enemies have their own cues.
        /// </summary>
        private static ScanEntry WhileWanted(ScanEntry entry, HashSet<uint> wanted) =>
            entry.Category is ScanCategory.Objects or ScanCategory.Encounters && wanted.Contains(entry.Id) && entry.Sound.Length == 0 &&
            entry.Kind is not MinimapMarks.RitualDoneKind and not "ritual" and not MinimapMarks.StashKind and not MinimapMarks.WellKind
                ? entry with { Sound = MinimapMarks.QuestObjectKind }
                : entry;

        private static float RangeOf(ScanEntry entry) => entry.Timed ? TimedRangeWorld : entry.Kind == OpenedWays.Kind ? OpenedWayRangeWorld : RangeWorld;

        private static float ReachOf(ScanEntry entry) => RangeOf(entry) + ProximityFade.TailWorld;

        /// <summary>The kind an entry sounds as: a barrier keeps its own for the guide, and it or a lever can sound as a quest object.</summary>
        private static string SoundKind(ScanEntry entry) => entry.Sound == MinimapMarks.QuestObjectKind ? entry.Sound : entry.Kind;

        private float[]? SoundFor(ScanEntry entry) => this.drops.For(entry.BaseId) ?? SoundKind(entry) switch
        {
            "gold" => this.sounds.Get("gold"),
            "currency" => this.sounds.Get("currency"),
            "quest" or MinimapMarks.QuestObjectKind => this.sounds.Get("quest_item"),
            "rare" or "unique" => this.sounds.Get(this.Layer(entry.Kind)!),
            "chest" => this.sounds.Get("chest"),
            "corpse" => this.sounds.Get(GameSounds.Corpse) ?? this.sounds.Get("chest"),
            "breakable" => this.sounds.Get($"{GameSounds.BreakableFolder}/{entry.Sound}") ?? this.sounds.Get(GameSounds.BreakableFallback),
            "switch" => this.sounds.Get("switch"),
            "shrine" => this.sounds.Get("shrine"),
            "ritual" => this.sounds.Get("ritual"),
            "portal" => this.sounds.Get("portal"),
            "waypoint" => this.sounds.Get("waypoint"),
            "checkpoint" => this.sounds.Get("checkpoint"),
            MinimapMarks.StashKind => this.sounds.Get(MinimapMarks.StashKind),
            MinimapMarks.WellKind => this.sounds.Get(MinimapMarks.WellKind),
            MinimapMarks.SignKind => this.sounds.Get(MinimapMarks.SignKind),
            OpenedWays.Kind => this.sounds.Get(OpenedWays.Kind),
            "npc_quest" => this.questRing,
            "exit" => this.sounds.Get("exit"),
            _ => entry.Category == ScanCategory.Items ? this.sounds.Get("item") : null,
        };

        /// <summary>
        ///     The sound laid over an item's own to mark it: a rare's, a unique's (the rare one until its own is extracted) or
        ///     the quest sound, which told a quest item from equipment of its shape (user 2026-11-07); null for another kind.
        /// </summary>
        private string? Layer(string kind) => kind switch
        {
            "rare" => "item_rare",
            "unique" => this.sounds.Get("item_unique") != null ? "item_unique" : "item_rare",
            "quest" => "quest_item",
            _ => null,
        };

        /// <summary>The glossary key whose volume the entry's sound follows: an item by its group (a Diablo IV rare or unique by its rarity), the rest by kind.</summary>
        private string SoundKey(ScanEntry entry)
        {
            if (entry.Category != ScanCategory.Items)
            {
                return SoundKind(entry);
            }

            return this.SoundSet == ItemSoundSet.DiabloIV && this.Layer(entry.Kind) is { } layer ? layer : ItemSoundGroup.Of(entry.Kind, entry.BaseId);
        }

        private static float Distance(Vector2 here, ScanEntry entry) => Vector2.Distance(here, new Vector2(entry.WorldX, entry.WorldY));

        /// <summary>A loop heard this frame; one in a pile shares <see cref="ItemSoundSet.PileScale"/> with the others.</summary>
        private readonly record struct HeardLoop(float[] Loop, string Sound, float Pan, float Pitch, float Gain, bool InPile);
    }
}
