namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>A league mechanic's object the scanner lists or the league watch follows.</summary>
    public enum LeaguePiece
    {
        VerisiumRemnant,
        ExpeditionMarker,
        ExpeditionRelic,
        ExpeditionDetonator,
        ExpeditionExplosive,
        PlacementIndicator,
        DeliriumMirror,
        ShardBoss,
        ShardMirroredBoss,
        ShardPortal,
        ShardElites,
        ShardPauseFog,
        ShardRareMods,
        ShardGrandMirror,
        GrandMirror,
        Simulacrum,
        AzmeriSpirit,
        BreachInitiator,
        BreachSkill,
        Strongbox,
        RogueExile,
        VaalBeacon,
        RitualAltar,
        AbyssPit,
    }

    /// <summary>What an Expedition charge can dig up, as its marker's minimap icon tells.</summary>
    public enum ExpeditionFind
    {
        Chest,
        Elite,
        Monsters,
        SpecialMonsters,
        Boss,
        Relic,
        Remnant,
    }

    /// <summary>A change of a league object that is news.</summary>
    public enum LeagueLine
    {
        RemnantTome,
        RemnantWaves,
        RemnantReward,
        RemnantSpoiled,
        DetonatorReady,
        Detonated,
        DeliriumBegan,
        InFog,
        OutOfFog,
        SimulacrumWave,
        SimulacrumWaveCleared,
        SimulacrumOver,
        BreachOpen,
        BreachStabilised,
        StableBreachCollapsed,
        UnstableBreachCollapsed,
        StrongboxAmbush,
        StrongboxReady,
        RogueExileFight,
        BeaconReady,
        BeaconDone,
        BeaconPortal,
        RitualDone,
        AbyssClosed,
    }

    /// <summary>A shrine by the buff its use gives the player (Shrines rows 53-66 and 71, all 45 s).</summary>
    public enum ShrineKind
    {
        Meteoric,
        Tempest,
        Freezing,
        Gloom,
        Acceleration,
        Divine,
        Seeking,
        Enduring,
        Replenishing,
        Enlightening,
        Greed,
        Resistance,
        Diamond,
        Corrupting,
        GreedForGear,
    }

    /// <summary>One of a state's values.</summary>
    public readonly record struct StateIs(string State, long[] Values);

    /// <param name="Path">The object's file, or with <paramref name="Family"/> the start of its family's files.</param>
    /// <param name="Script">The script whose states the data step checks, when the path is no file of its own.</param>
    /// <param name="Except">A part of a path the family leaves out.</param>
    /// <param name="ListedWhile">The states that keep it in the scanner; null for an object only followed for its news.</param>
    /// <param name="Icons">The minimap icons it is listed under; null for any.</param>
    /// <param name="WithoutTarget">Listed although the game makes it untargetable (a remnant's waves; no target at all).</param>
    public sealed record LeagueRule(
        LeaguePiece Piece, string Path, bool Family = false, string? Script = null, string? Except = null, StateIs[]? ListedWhile = null,
        string[]? Icons = null, bool WithoutTarget = false)
    {
        public bool Lists => this.ListedWhile != null;
    }

    /// <summary>A state reaching a value is news; <paramref name="From"/> only from that one, <paramref name="Unless"/> not while that state is set.</summary>
    /// <param name="FirstSight">Also when the object is first seen already there.</param>
    /// <param name="Once">At most once per object in an area.</param>
    public sealed record LeagueCue(
        LeaguePiece Piece, string State, long Value, LeagueLine Line, long? From = null, string? Unless = null, bool FirstSight = false, bool Once = false);

    /// <summary>
    ///     The league mechanics' objects by their files, the states that keep one listed, the state changes that are news, the
    ///     player buffs and stat they bring (docs/research/league-mechanics.md §12: the .ot files and tables of 0.5.5f). Pure.
    /// </summary>
    public static class LeaguePieces
    {
        /// <summary>The scanner's kind of an Expedition marker: silent, so dozens of them never take the encounters' voice.</summary>
        public const string MarkerKind = "expedition_marker";

        /// <summary>The client state of what the charge being placed would blow (ExpeditionHighlightable).</summary>
        public const string PlacingRangeState = "in_placing_range";

        /// <summary>The placement indicator's state while the charge cannot go where it stands.</summary>
        public const string BlockedState = "blocked";

        /// <summary>The Simulacrum's wave index, from 0: "wave &lt; 6" picks the music of all but the last of its 7 waves.</summary>
        public const string WaveState = "wave";

        /// <summary>The Simulacrum's waves (AfflictionConstants.SimulacrumWaves).</summary>
        public const int SimulacrumWaves = 7;

        /// <summary>The Delirium mirror's state once walked through: the encounter runs in this area.</summary>
        public const string MirrorUsedState = "interacted";

        /// <summary>The player stat set while inside the Delirium fog (Stats 10258).</summary>
        public const string FogStat = "is_in_affliction_fog";

        /// <summary>The buff of a monster an Azmeri spirit has possessed (BuffDefinitions 2720).</summary>
        public const string PossessedBuff = "tormented_spirit_power";

        /// <summary>The icon a closed Abyss pit turns to, failed or not (AbyssNodeBase on transitions 3 and 4).</summary>
        public const string PitClosedIcon = "AbyssPitInactive";

        /// <summary>The state of a remnant's progress, a detonator or charge blown, a beacon's progress.</summary>
        public const string ActivatedState = "activated";

        private const char InstanceMark = '@';
        private const string EntityMonsters = "Metadata/Monsters/";
        private const string Expedition = "Metadata/MiscellaneousObjects/Expedition/";
        private const string Delirium = "Metadata/MiscellaneousObjects/Delirium/";
        private const string DeliriumArea = "Metadata/Terrain/Gallows/Leagues/Delirium/Objects/";
        private const string Activated = ActivatedState;

        /// <summary>The Expedition markers' icons (MinimapIcons 578-584) and the relic's (856); which kind gets which is LIKELY.</summary>
        private static readonly Dictionary<string, ExpeditionFind> FindIcons = new(StringComparer.Ordinal)
        {
            ["ExpeditionChest1"] = ExpeditionFind.Chest,
            ["ExpeditionChest2"] = ExpeditionFind.Chest,
            ["ExpeditionChest3"] = ExpeditionFind.Chest,
            ["ExpeditionEliteMonster"] = ExpeditionFind.Elite,
            ["ExpeditionRegularMonster"] = ExpeditionFind.Monsters,
            ["ExpeditionLeagueSpecificMonster"] = ExpeditionFind.SpecialMonsters,
            ["ExpeditionBoss"] = ExpeditionFind.Boss,
            ["ExpeditionPOI"] = ExpeditionFind.Relic,
        };

        private static readonly string[] MarkerIcons = FindIcons.Where(pair => pair.Value != ExpeditionFind.Relic).Select(pair => pair.Key).ToArray();

        private static readonly LeagueRule[] Rules =
        {
            // The Verisium Remnant listed through its waves; 3, 4 and 8 spoil it, 7 is done (the script's comment).
            new(LeaguePiece.VerisiumRemnant, "Metadata/MiscellaneousObjects/Expedition2/Expedition2Encounter", ListedWhile: new[] { Is(Activated, 0, 1, 2, 5, 6) }, WithoutTarget: true),
            new(LeaguePiece.VerisiumRemnant, "Metadata/MiscellaneousObjects/Expedition2/Expedition2EncounterQuest", ListedWhile: new[] { Is(Activated, 0, 1, 2, 5, 6) }, WithoutTarget: true),
            new(LeaguePiece.ExpeditionMarker, Expedition + "ExpeditionMarker", ListedWhile: Array.Empty<StateIs>(), Icons: MarkerIcons, WithoutTarget: true),
            new(LeaguePiece.ExpeditionRelic, Expedition + "ExpeditionRelic", ListedWhile: new[] { Is("exploded", 0) }, Icons: new[] { "ExpeditionPOI" }, WithoutTarget: true),
            new(LeaguePiece.ExpeditionDetonator, Expedition + "ExpeditionDetonator", Family: true),
            new(LeaguePiece.ExpeditionExplosive, Expedition + "ExpeditionExplosive"),
            new(LeaguePiece.PlacementIndicator, Expedition + "ExpeditionPlacementIndicator"),
            new(LeaguePiece.DeliriumMirror, DeliriumArea + "DeliriumInitiator"),
            // The shards rise with the fog (visible) and are spent once shattered (detonate); no target of their own.
            Shard(LeaguePiece.ShardBoss, "DeliriumShardBoss"),
            Shard(LeaguePiece.ShardBoss, "DeliriumShardBoss2"),
            Shard(LeaguePiece.ShardMirroredBoss, "DeliriumMirroredBoss"),
            Shard(LeaguePiece.ShardPortal, "DeliriumShardSeethingChyme"),
            Shard(LeaguePiece.ShardElites, "DeliriumShardEliteUpgrade"),
            Shard(LeaguePiece.ShardPauseFog, "DeliriumShardPauseFog"),
            Shard(LeaguePiece.ShardRareMods, "DeliriumAddDeliModsToRares"),
            Shard(LeaguePiece.ShardGrandMirror, "DeliriumBossSpawnsGrandMirror"),
            new(LeaguePiece.GrandMirror, Delirium + "DeliriumGigaMirror", ListedWhile: new[] { Is("enabled", 1) }),
            new(LeaguePiece.Simulacrum, DeliriumArea + "DeliriumSimulacrumObject"),
            // The fleeing wisp has no target; its possessing daemon shares the start of its name.
            new(LeaguePiece.AzmeriSpirit, "Metadata/Monsters/TormentedSpirits/TormentedSpiritofthe", Family: true,
                Script: "Metadata/Monsters/TormentedSpirits/TormentedSpiritBase", Except: "PossesedDaemon", ListedWhile: new[] { Is("active", 1) }, WithoutTarget: true),
            new(LeaguePiece.BreachInitiator, "Metadata/MiscellaneousObjects/Brequel/BrequelInitiator"),
            new(LeaguePiece.BreachSkill, "Metadata/MiscellaneousObjects/Brequel/BrequelSkillObject", ListedWhile: new[] { Is("ready", 1) }),
            new(LeaguePiece.Strongbox, "Metadata/Chests/StrongBoxes/", Family: true, Script: "Metadata/Chests/StrongBoxes/Strongbox"),
            new(LeaguePiece.RogueExile, "Metadata/Monsters/RogueExiles/", Family: true, Script: "Metadata/Monsters/RogueExiles/RogueExileBase"),
            new(LeaguePiece.VaalBeacon, "Metadata/MiscellaneousObjects/LeagueIncursionNew/IncursionPedestalEncounter"),
            new(LeaguePiece.RitualAltar, "Metadata/Terrain/Leagues/Ritual/RitualRuneInteractable"),
            new(LeaguePiece.AbyssPit, "Metadata/MiscellaneousObjects/Abyss/AbyssNodeBase"),
            new(LeaguePiece.AbyssPit, "Metadata/MiscellaneousObjects/Abyss/AbyssFinalNodeBase"),
        };

        /// <summary>The state changes that are news, in the order said when several come at once.</summary>
        public static readonly LeagueCue[] Cues =
        {
            new(LeaguePiece.VerisiumRemnant, Activated, 1, LeagueLine.RemnantTome),
            new(LeaguePiece.VerisiumRemnant, Activated, 5, LeagueLine.RemnantWaves),
            new(LeaguePiece.VerisiumRemnant, Activated, 6, LeagueLine.RemnantReward),
            new(LeaguePiece.VerisiumRemnant, Activated, 3, LeagueLine.RemnantSpoiled),
            new(LeaguePiece.VerisiumRemnant, Activated, 4, LeagueLine.RemnantSpoiled),
            new(LeaguePiece.VerisiumRemnant, Activated, 8, LeagueLine.RemnantSpoiled),
            new(LeaguePiece.ExpeditionDetonator, "explosives_placed", 1, LeagueLine.DetonatorReady),
            new(LeaguePiece.ExpeditionDetonator, Activated, 1, LeagueLine.Detonated),
            new(LeaguePiece.DeliriumMirror, MirrorUsedState, 1, LeagueLine.DeliriumBegan),
            new(LeaguePiece.Simulacrum, "active", 1, LeagueLine.SimulacrumWave),
            new(LeaguePiece.Simulacrum, "active", 0, LeagueLine.SimulacrumWaveCleared, From: 1),
            new(LeaguePiece.Simulacrum, "active", 2, LeagueLine.SimulacrumOver),
            // encounter_state: 1 expanding, 2 enraged (stabilised), 3 closing enraged, 4 closing not enraged (BrequelInitiator).
            new(LeaguePiece.BreachInitiator, "encounter_state", 1, LeagueLine.BreachOpen),
            new(LeaguePiece.BreachInitiator, "encounter_state", 2, LeagueLine.BreachStabilised),
            new(LeaguePiece.BreachInitiator, "encounter_state", 3, LeagueLine.StableBreachCollapsed),
            new(LeaguePiece.BreachInitiator, "encounter_state", 4, LeagueLine.UnstableBreachCollapsed),
            new(LeaguePiece.Strongbox, "started", 1, LeagueLine.StrongboxAmbush),
            new(LeaguePiece.Strongbox, "started", 0, LeagueLine.StrongboxReady, From: 1, Unless: "opened"),
            // The static life bar comes on as the exile engages (RogueExileBase, on_state_boss_life_bar_1).
            new(LeaguePiece.RogueExile, "boss_life_bar", 1, LeagueLine.RogueExileFight, FirstSight: true, Once: true),
            new(LeaguePiece.VaalBeacon, Activated, 2, LeagueLine.BeaconReady),
            new(LeaguePiece.VaalBeacon, Activated, 3, LeagueLine.BeaconDone),
            new(LeaguePiece.VaalBeacon, "open", 1, LeagueLine.BeaconPortal),
            new(LeaguePiece.RitualAltar, "current_state", 3, LeagueLine.RitualDone, From: 2),
        };

        /// <summary>The shrines' buffs (BuffDefinitions; the lesser shrines' older buffs are left out).</summary>
        public static readonly IReadOnlyDictionary<string, ShrineKind> ShrineBuffs = new Dictionary<string, ShrineKind>(StringComparer.Ordinal)
        {
            ["shrine_fire_2"] = ShrineKind.Meteoric,
            ["shrine_lightning_2"] = ShrineKind.Tempest,
            ["shrine_cold_2"] = ShrineKind.Freezing,
            ["shrine_gloom_2"] = ShrineKind.Gloom,
            ["shrine_acceleration_2"] = ShrineKind.Acceleration,
            ["shrine_godmode_2"] = ShrineKind.Divine,
            ["shrine_magicfind_2"] = ShrineKind.Seeking,
            ["shrine_massive_2"] = ShrineKind.Enduring,
            ["shrine_regeneration_2"] = ShrineKind.Replenishing,
            ["shrine_experience_2"] = ShrineKind.Enlightening,
            ["shrine_goldfind_2"] = ShrineKind.Greed,
            ["shrine_resistance_2"] = ShrineKind.Resistance,
            ["shrine_critical_2"] = ShrineKind.Diamond,
            ["shrine_corrupt_2"] = ShrineKind.Corrupting,
            ["shrine_noequipment_2"] = ShrineKind.GreedForGear,
        };

        /// <summary>The monsters of the mechanics, whose buffs the watch logs once to confirm the rules.</summary>
        private static readonly string[] MechanicMonsters =
        {
            "Metadata/Monsters/TormentedSpirits/", "Metadata/Monsters/RogueExiles/", "Metadata/Monsters/Strongbox/", "Metadata/Monsters/LeagueExpedition",
            "Metadata/Monsters/LeagueDelirium/", "Metadata/Monsters/LeagueAffliction/", "Metadata/Monsters/LeagueAbyss/", "Metadata/Monsters/Breach/",
            "Metadata/Monsters/LeagueIncursion", "Metadata/Monsters/LeagueRitual/", "Metadata/Monsters/LeagueAzmeri/",
        };

        private static readonly Dictionary<string, LeagueRule> Files =
            Rules.Where(rule => !rule.Family).ToDictionary(rule => rule.Path, StringComparer.OrdinalIgnoreCase);

        private static readonly LeagueRule[] Families = Rules.Where(rule => rule.Family).ToArray();

        /// <summary>The start of every league sound's key: each mechanic rings with its own recording of the game (docs/research/game-audio.md).</summary>
        public const string SoundPrefix = "league_";

        public const string BreachSound = SoundPrefix + "breach";
        public const string DeliriumSound = SoundPrefix + "delirium";
        public const string EssenceSound = SoundPrefix + "essence";
        public const string ExpeditionSound = SoundPrefix + "expedition";
        public const string AbyssSound = SoundPrefix + "abyss";
        public const string SpiritSound = SoundPrefix + "spirit";
        public const string VaalSound = SoundPrefix + "vaal";
        public const string StrongboxSound = SoundPrefix + "strongbox";

        /// <summary>Every league sound, for the extractor's cues and the glossary.</summary>
        public static readonly string[] Sounds = { BreachSound, DeliriumSound, EssenceSound, ExpeditionSound, AbyssSound, SpiritSound, VaalSound, StrongboxSound };

        /// <summary>
        ///     The sound a league's object rings with, by its piece or, for one the table does not hold, by its folder; empty for
        ///     what has a sound of its own (a Ritual altar) or none (an Expedition marker, an exile).
        /// </summary>
        public static string SoundOf(string path)
        {
            switch (RuleOf(path)?.Piece)
            {
                case LeaguePiece.VerisiumRemnant or LeaguePiece.ExpeditionRelic or LeaguePiece.ExpeditionDetonator:
                    return ExpeditionSound;
                case LeaguePiece.DeliriumMirror or LeaguePiece.ShardBoss or LeaguePiece.ShardMirroredBoss or LeaguePiece.ShardPortal or LeaguePiece.ShardElites
                    or LeaguePiece.ShardPauseFog or LeaguePiece.ShardRareMods or LeaguePiece.ShardGrandMirror or LeaguePiece.GrandMirror or LeaguePiece.Simulacrum:
                    return DeliriumSound;
                case LeaguePiece.AzmeriSpirit:
                    return SpiritSound;
                case LeaguePiece.BreachInitiator or LeaguePiece.BreachSkill:
                    return BreachSound;
                case LeaguePiece.Strongbox:
                    return StrongboxSound;
                case LeaguePiece.VaalBeacon:
                    return VaalSound;
                case LeaguePiece.AbyssPit:
                    return AbyssSound;
                case not null:
                    return string.Empty;
            }

            foreach (var (folder, sound) in FolderSounds)
            {
                if (path.Contains(folder, StringComparison.OrdinalIgnoreCase))
                {
                    return sound;
                }
            }

            return string.Empty;
        }

        /// <summary>A league's folder in an object's path, for the objects the minimap icon lists and the table does not hold.</summary>
        private static readonly (string Folder, string Sound)[] FolderSounds =
        {
            ("/Brequel/", BreachSound), ("/Breach/", BreachSound), ("/Delirium/", DeliriumSound), ("/Essence", EssenceSound),
            ("/Expedition", ExpeditionSound), ("/Abyss/", AbyssSound), ("/Incursion", VaalSound),
        };

        /// <summary>The rule of an object's path, whatever its instance tail; null for anything else.</summary>
        public static LeagueRule? RuleOf(string path)
        {
            var mark = path.IndexOf(InstanceMark);
            var file = mark < 0 ? path : path[..mark];
            if (Files.TryGetValue(file, out var rule))
            {
                return rule;
            }

            foreach (var family in Families)
            {
                if (file.StartsWith(family.Path, StringComparison.OrdinalIgnoreCase) &&
                    (family.Except is not { } except || !file.Contains(except, StringComparison.OrdinalIgnoreCase)))
                {
                    return family;
                }
            }

            return null;
        }

        /// <summary>Whether a listed piece is there to find: its states as its rule asks (silent when one is not read) and its icon.</summary>
        public static bool IsListed(LeagueRule rule, IEnumerable<(string Name, long Value)> states, string icon) =>
            rule.ListedWhile is { } wanted && (rule.Icons is not { } icons || Array.IndexOf(icons, icon) >= 0) &&
            wanted.All(condition => states.Any(state => state.Name == condition.State && Array.IndexOf(condition.Values, state.Value) >= 0));

        /// <summary>What a charge would dig up at a piece: a marker by its icon, a relic, a remnant; null for anything else.</summary>
        public static ExpeditionFind? FindOf(LeaguePiece piece, string icon) => piece switch
        {
            LeaguePiece.ExpeditionMarker => FindIcons.TryGetValue(icon, out var find) && find != ExpeditionFind.Relic ? find : null,
            LeaguePiece.ExpeditionRelic => ExpeditionFind.Relic,
            LeaguePiece.VerisiumRemnant => ExpeditionFind.Remnant,
            _ => null,
        };

        /// <summary>The line an object's icon turning to another tells; null when it tells nothing.</summary>
        public static LeagueLine? IconLine(LeaguePiece piece, string icon) =>
            piece == LeaguePiece.AbyssPit && icon == PitClosedIcon ? LeagueLine.AbyssClosed : null;

        /// <summary>The Simulacrum wave a line speaks of, counted from 1 (the script's index is from 0).</summary>
        public static int WaveOf(IEnumerable<(string Name, long Value)> states) =>
            (int)states.FirstOrDefault(state => state.Name == WaveState).Value + 1;

        /// <summary>A monster of a league mechanic, whose buffs are logged once.</summary>
        public static bool IsMechanicMonster(string path) => MechanicMonsters.Any(folder => path.StartsWith(folder, StringComparison.Ordinal));

        /// <summary>The name of each file, state, icon, buff and monster the rules match, for the data step's check (<see cref="GameNames"/>).</summary>
        public static IEnumerable<GameName> NamesMatched()
        {
            const string rule = nameof(LeaguePieces);
            foreach (var piece in Rules)
            {
                var script = piece.Script ?? piece.Path;
                var states = (piece.ListedWhile ?? Array.Empty<StateIs>()).Select(condition => condition.State)
                    .Concat(Cues.Where(cue => cue.Piece == piece.Piece).SelectMany(cue => cue.Unless is { } unless ? new[] { cue.State, unless } : new[] { cue.State }))
                    .Concat(piece.Piece switch
                    {
                        LeaguePiece.ExpeditionMarker => new[] { PlacingRangeState },
                        LeaguePiece.PlacementIndicator => new[] { BlockedState },
                        LeaguePiece.Simulacrum => new[] { WaveState },
                        _ => Array.Empty<string>(),
                    })
                    .Distinct().ToList();
                if (states.Count == 0)
                {
                    yield return new GameName(GameNameKind.ObjectFile, script, null, rule);
                }

                foreach (var state in states)
                {
                    yield return new GameName(GameNameKind.ObjectFile, script, state, rule);
                }

                if (piece.Family && piece.Path.StartsWith(EntityMonsters, StringComparison.Ordinal) && !piece.Path.EndsWith('/'))
                {
                    yield return new GameName(GameNameKind.Monster, piece.Path[(piece.Path.LastIndexOf('/') + 1)..], null, rule);
                }
            }

            foreach (var icon in FindIcons.Keys.Append(PitClosedIcon))
            {
                yield return new GameName(GameNameKind.MinimapIcon, icon, null, rule);
            }

            foreach (var buff in ShrineBuffs.Keys.Prepend(PossessedBuff))
            {
                yield return new GameName(GameNameKind.Buff, buff, null, rule);
            }
        }

        private static StateIs Is(string state, params long[] values) => new(state, values);

        private static LeagueRule Shard(LeaguePiece piece, string file) =>
            new(piece, Delirium + file, ListedWhile: new[] { Is("visible", 1), Is("detonate", 0) }, WithoutTarget: true);
    }
}
