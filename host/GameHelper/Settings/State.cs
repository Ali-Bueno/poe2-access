// <copyright file="State.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Settings
{
    using System.Collections.Generic;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.Utils;

    /// <summary>
    ///     Game Helper Core Settings.
    /// </summary>
    public class State
    {
        /// <summary>
        ///     Gets a value indicating whether user wants to disable entity processing
        ///     when in town/hideout.
        /// </summary>
        public bool DisableEntityProcessingInTownOrHideout = false;

        /// <summary>
        ///     Gets a value indicating the max degree of parallelism while reading entity list
        ///     in every frame. This is added because some users wanted to limit the cpu usage
        ///     GH uses irrespective of the FPS loss.
        /// </summary>
        public int EntityReaderMaxDegreeOfParallelism = 4;

        /// <summary>
        ///     Gets the value indicating whether to disable all kind of counters and misc features
        ///     to improve the GH performance when running fully juiced + 6 man party maps.
        ///     (even 1 FPS improvement counts at this stage since ppl have 50k entities
        ///     in the network bubble)
        /// </summary>
        public bool DisableAllCounters = true;

        
        /// <summary>
        ///     Gets a value indicating whether user wants to close the Game Helper when
        ///     the game exit or not.
        /// </summary>
        public bool CloseWhenGameExit = true;

        /// <summary>
        ///     Gets a value indicating if the game input is keyboard/mouse OR PS4 controller.
        /// </summary>
        public bool EnableControllerMode = true;

        /// <summary>
        ///     Gets the party leader name.
        /// </summary>
        public string LeaderName = string.Empty;

        /// <summary>
        ///     Gets or sets the important NPC Paths.
        /// </summary>
        public List<string> SpecialNPCPaths = new();

        /// <summary>
        ///     Gets or sets the custom categories (and its defination) for monsters
        /// </summary>
        public List<(EntityFilterType filtertype, string filter, Rarity rarity, GameStats stat, int group)> PoiMonstersCategories2 = new();

        /// <summary>
        ///     Gets or sets the custom categories (and its defination) for MiscellaneousObjects in the game.
        /// </summary>
        public List<(string path, int group)> SpecialMiscObjPaths = new()
        {
            ("Metadata/MiscellaneousObjects/Expedition/ExpeditionMarker", 100),
            ("Metadata/MiscellaneousObjects/Expedition/ExpeditionRelic", 101),
            ("Metadata/Terrain/Gallows/Leagues/Expedition/Objects/Expedition", 100),
            ("Metadata/MiscellaneousObjects/Checkpoints/Checkpoint_Endgame_Boss", 102),
            ("Metadata/MiscellaneousObjects/Expedition2/Expedition2Encounter", 103),
            ("Metadata/Terrain/Leagues/Ritual/RitualRuneObject", 104),
            ("Metadata/MiscellaneousObjects/Abyss", 105),
            ("Metadata/MiscellaneousObjects/Brequel/BrequelInitiator", 106),
            ("Metadata/Terrain/Leagues/Sanctum/Objects/SanctumMote", 107),
        };

        /// <summary>
        ///     Gets or sets a list of monsters path to ignore.
        ///     The condition used is StartsWith so just provide enough to make the path unique.
        /// </summary>
        public List<string> MonstersPathsToIgnore = new()
        {
            "Metadata/Monsters/LeagueIncursionNew/Garrison/VaalTrainingDummy",
            "Metadata/Monsters/PerennialKingStage2/PerennialKingCorpseFriend",
            "Metadata/Monsters/MarakethSamdDjinn/SandDjinn",
            "Metadata/Monsters/MarakethWaterDjinn/WaterDjinn",
            "Metadata/Monsters/MarakethTimeDjinn/FireDjinn",
            "Metadata/Monsters/Monsters/LivingLightningPlayerSummoned",
            "Metadata/Monsters/Pirates/CampExplosion",
            "Metadata/Monsters/CageSkeleton/CageSkeleton",
            "Metadata/Pet",
            "Metadata/Monsters/JellfishNettler/JellyfishNettlerFlying",
            "Metadata/Monsters/MarakethSanctumTrial/Hazards/GroundSpike",
            "Metadata/Monsters/MarakethSanctumTrial/Hazards/ProjectilePortal",
        };

        /// <summary>
        ///     Gets or sets a value indiciating if user wants to process useless entities as well
        /// </summary>
        public bool ProcessAllRenderableEntities = false;

        /// <summary>
        ///     Gets a value indicating if user is running Taiwan client or not.
        /// </summary>
        public bool IsTaiwanClient = false;

        // ── Entity Staleness Fixes (toggleable) ──

        /// <summary>
        ///     When enabled, NPC entities are included in the cleanup logic
        ///     so stale/despawned NPCs are removed from the entity dictionary.
        /// </summary>
        public bool EnableNpcEntityCleanup = true;

        /// <summary>
        ///     When enabled, entities that stay invalid for many consecutive frames
        ///     are removed from the entity dictionary regardless of type.
        /// </summary>
        public bool EnableStaleEntityCleanup = true;

        /// <summary>
        ///     How many consecutive invalid frames before an entity is removed.
        /// </summary>
        public int StaleEntityFrameThreshold = 120;
    }
}
