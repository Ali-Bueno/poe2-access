// <copyright file="State.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Settings
{
    using System.Collections.Generic;

    /// <summary>
    ///     Game Helper Core Settings.
    /// </summary>
    public class State
    {
        /// <summary>
        ///     Gets a value indicating the max degree of parallelism while reading entity list
        ///     in every frame. This is added because some users wanted to limit the cpu usage
        ///     GH uses irrespective of the FPS loss.
        /// </summary>
        public int EntityReaderMaxDegreeOfParallelism = 4;

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
        ///     Gets or sets the MiscellaneousObjects paths kept as important objects.
        /// </summary>
        public List<string> SpecialMiscObjPaths = new()
        {
            "Metadata/MiscellaneousObjects/Expedition/ExpeditionMarker",
            "Metadata/MiscellaneousObjects/Expedition/ExpeditionRelic",
            "Metadata/Terrain/Gallows/Leagues/Expedition/Objects/Expedition",
            "Metadata/MiscellaneousObjects/Checkpoints/Checkpoint_Endgame_Boss",
            "Metadata/MiscellaneousObjects/Expedition2/Expedition2Encounter",
            "Metadata/Terrain/Leagues/Ritual/RitualRuneObject",
            "Metadata/MiscellaneousObjects/Abyss",
            "Metadata/MiscellaneousObjects/Brequel/BrequelInitiator",
            "Metadata/Terrain/Leagues/Sanctum/Objects/SanctumMote",
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
