namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     What an entity's minimap icon (a MinimapIcons.dat row) or a tile of the area's layout tells the scanner about the
    ///     map's marks: signs, quest targets, boss rooms (docs/research/minimap.md).
    /// </summary>
    public static class MinimapMarks
    {
        /// <summary>Readable signs and plaques ("Pueblo de Ogham", "Proclamación de Asinia"): typed NPC by the game, never characters.</summary>
        public const string StoryGlyph = "StoryGlyph";

        public const string SignKind = "sign";

        public const string BossRoomKind = "boss_room";

        public const string LandmarkKind = "landmark";

        /// <summary>
        ///     A ritual altar's icon, "RitualRuneFinished" once done: the league's altars and the campaign's own (Freythorn's
        ///     CanopyRitualAltar, the King in the Mists' CanopyBossRitualAltar; their .ot files, 2026-10-29).
        /// </summary>
        public const string RitualIcon = "RitualRune";

        public const string RitualDoneIcon = "RitualRuneFinished";

        public static bool IsRitualAltar(string icon) => icon.StartsWith(RitualIcon, StringComparison.Ordinal);

        /// <summary>
        ///     How far (grid cells) the minimap points to a campaign boss's room: MinimapIcons +0x12 (the pointer's reach) is
        ///     400 for MillerActive, CroneActive, MudBurrowerActive, HusbandActive, WifeActive; the radar table names these arenas.
        /// </summary>
        public const float CampaignBossPointerCells = 400;

        /// <summary>The same for a map boss's house (LootFilterLargeRedUpsideDownHouse, the arenas the table does not name): 250.</summary>
        public const float MapBossPointerCells = 250;

        /// <summary>The object marking a boss's arena (Metadata/Monsters/Crowbell/Objects/BossRoomMinimapIcon); its icon turns grey once the boss is dead.</summary>
        private const string BossRoomObject = "/BossRoomMinimapIcon";

        /// <summary>Icons the game draws on what the quest wants used or picked up (MinimapIcons rows 5, 6, 15).</summary>
        private static readonly string[] QuestIcons = { "QuestObject", "QuestItem", "MissionTarget" };

        /// <summary>A radar-table label "Boss - Captain Hartlin": the name follows.</summary>
        private const string BossLabelPrefix = "Boss - ";

        /// <summary>Words of a tile file or label that mean a boss's arena (the radar table and the game's own tile names).</summary>
        private static readonly string[] ArenaWords = { "boss", "arena" };

        /// <summary>Words of a tile file that lead to an arena or sit beside it rather than hold it.</summary>
        private static readonly string[] WayWords = { "transition", "stairs", "door", "entrance", "_side", "medallion" };

        /// <summary>Radar-table labels of an arena-named tile that is not where the boss is fought.</summary>
        private static readonly string[] NotBosses = { "entrance", "transition", "beacon", "medallion", "stairs", "boss stairs" };

        /// <summary>Radar-table labels that call the place a boss room and name no one.</summary>
        private static readonly string[] BareLabels = { "boss", "bosses", "arena", "boss room" };

        /// <summary>The icon of a boss already slain or a secret already found: the "…Inactive" rows (unlabelled) and a map boss's grey house.</summary>
        public static bool IsSpent(string icon) =>
            icon.EndsWith("Inactive", StringComparison.Ordinal) || icon.EndsWith("InActive", StringComparison.Ordinal) ||
            icon.Contains("Grey", StringComparison.Ordinal);

        public static bool IsBossRoom(string path) => path.Contains(BossRoomObject, StringComparison.OrdinalIgnoreCase);

        public static bool IsQuestIcon(string icon) => Array.IndexOf(QuestIcons, icon) >= 0;

        /// <summary>
        ///     The boss in a radar-table label of a tile ("Asinia", "Beira of the Rotten (10% Cold Res)"), empty when the label
        ///     names no one ("Boss", "Bosses", the tile's own file name); null when the tile is no boss's arena: the file
        ///     names none ("TombBoss_ArenaFloor", "HagWitchArena_03", "Qimah_bossroom") and neither does the label
        ///     ("Crowbell Arena (+2 Passive)" on DolmenDeAzutan_03), or the tile is a way in ("Arena_Transition_01",
        ///     "HoltenBossStairsDown_01") or a thing beside it ("Medallion", "Beacon").
        /// </summary>
        public static string? ArenaBoss(string tileKey, string label)
        {
            var file = TileFile(tileKey);
            var trimmed = label.Trim();
            if (HasAny(file, WayWords) || Array.IndexOf(NotBosses, trimmed.ToLowerInvariant()) >= 0 ||
                !(HasAny(file, ArenaWords) || HasAny(trimmed, ArenaWords)))
            {
                return null;
            }

            if (Array.IndexOf(BareLabels, trimmed.ToLowerInvariant()) >= 0 || trimmed == file || (trimmed.Contains('_') && !trimmed.Contains(' ')))
            {
                return string.Empty;
            }

            return trimmed.StartsWith(BossLabelPrefix, StringComparison.OrdinalIgnoreCase) ? trimmed[BossLabelPrefix.Length..].Trim() : trimmed;
        }

        /// <summary>A label without the radar table's reward note: "Beira of the Rotten (10% Cold Res)" → "Beira of the Rotten".</summary>
        public static string WithoutNote(string label)
        {
            var note = label.IndexOf(" (", StringComparison.Ordinal);
            return (note > 0 ? label[..note] : label).Trim();
        }

        /// <summary>Whether a tile the radar table does not name holds a boss's arena, from its file alone.</summary>
        public static bool IsArenaTile(string tileKey) => ArenaBoss(tileKey, string.Empty) != null;

        /// <summary>The tile's file name without folder, extension or sub-tile ("…/TombBoss_ArenaFloor.tdtx:2-y:2" → "TombBoss_ArenaFloor").</summary>
        public static string TileFile(string tileKey)
        {
            var start = tileKey.LastIndexOf('/') + 1;
            var end = tileKey.IndexOf(".tdt", start, StringComparison.OrdinalIgnoreCase);
            return end < 0 ? tileKey[start..] : tileKey[start..end];
        }

        /// <summary>
        ///     The groups <paramref name="spots"/> fall into (indices into it), a spot within <paramref name="reach"/> of
        ///     another joining its group: one arena's pieces lie side by side, two arenas tiles apart.
        /// </summary>
        public static List<List<int>> Groups(IReadOnlyList<Vector2> spots, float reach)
        {
            var grouped = new bool[spots.Count];
            var groups = new List<List<int>>();
            for (var seed = 0; seed < spots.Count; seed++)
            {
                if (grouped[seed])
                {
                    continue;
                }

                var members = new List<int> { seed };
                grouped[seed] = true;
                for (var next = 0; next < members.Count; next++)
                {
                    for (var other = 0; other < spots.Count; other++)
                    {
                        if (!grouped[other] && Vector2.Distance(spots[members[next]], spots[other]) <= reach)
                        {
                            grouped[other] = true;
                            members.Add(other);
                        }
                    }
                }

                groups.Add(members);
            }

            return groups;
        }

        private static bool HasAny(string text, string[] words) => words.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        ///     The scanner's kind for a mark the minimap draws with a label or as a boss's arena, with its label and whether the
        ///     boss is slain; null for what is no point of interest (unlabelled icons, a secret already found, a sign).
        /// </summary>
        public static (string Kind, string Label, bool Defeated)? PointOfInterest(string path, string icon, string label)
        {
            if (IsBossRoom(path))
            {
                return (BossRoomKind, label, IsSpent(icon));
            }

            return label.Length > 0 && !IsSpent(icon) && icon != StoryGlyph ? (LandmarkKind, label, false) : null;
        }
    }
}
