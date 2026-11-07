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

        /// <summary>What the quest's own icon marks and has no kind of its own; also the key of the sound it makes, whose volume it follows.</summary>
        public const string QuestObjectKind = "quest_item";

        /// <summary>
        ///     A ritual altar's icon, "RitualRuneFinished" once done: the league's altars and the campaign's own (Freythorn's
        ///     CanopyRitualAltar, the King in the Mists' CanopyBossRitualAltar; their .ot files, 2026-10-29).
        /// </summary>
        public const string RitualIcon = "RitualRune";

        public const string RitualDoneIcon = "RitualRuneFinished";

        public const string RitualDoneKind = "ritual_done";

        public static bool IsRitualAltar(string icon) => icon.StartsWith(RitualIcon, StringComparison.Ordinal);

        /// <summary>
        ///     The scanner kind of a league device by its icon. A finished altar has its own: it stays listed, the tribute is
        ///     spent there, but its sound drew the player to nothing (user 2026-11-04).
        /// </summary>
        public static string DeviceKind(string icon) => icon == RitualDoneIcon ? RitualDoneKind : IsRitualAltar(icon) ? "ritual" : IsQuestIcon(icon) ? QuestObjectKind : "encounter";

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
        /// <summary>
        ///     The quest's own, the Death Crystal's, a Trial room's objective, and that of what moves a Trial on: the Relic Altar, its map, the floor
        ///     doors and the rooms' rewards. The tracker stops naming the altar once a run fails (.ot sweep; user 2026-11-07).
        /// </summary>
        private static readonly string[] QuestIcons = { "QuestObject", "QuestItem", "MissionTarget", "SanctumFountain", "MarakethSanctumRoomSelector" };

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

            return label.Length > 0 && !IsSpent(icon) && icon != StoryGlyph ? (IsQuestIcon(icon) ? QuestObjectKind : LandmarkKind, label, false) : null;
        }

        /// <summary>The kind of an entity with this icon: under the quest's own, one without a kind is a quest object, and sounds like one (user 2026-11-07).</summary>
        public static string KindUnder(string icon, string kind) => kind.Length == 0 && IsQuestIcon(icon) ? QuestObjectKind : kind;

        /// <summary>
        ///     A named barrier a campaign area made for itself, such as the Ancient Seal at Balbala's door (Act2/2_2/Objects/BossDoor,
        ///     user 2026-11-07), and not a dungeon's plain door (Metadata/MiscellaneousObjects/Door), a hideout's or a league's.
        /// </summary>
        public static bool IsStoryBarrier(string path) => path.StartsWith("Metadata/Terrain/Gallows/Act", StringComparison.Ordinal);

        /// <summary>
        ///     A cage the quest wants opened by its file (RisuCage, KaruiElderCage): of the 62 objects with a quest icon only these
        ///     and three others have no display_text (sweep of the .ot files, 2026-11-07).
        /// </summary>
        public static bool IsCage(string path) => path.EndsWith("Cage", StringComparison.Ordinal);

        /// <summary>A character's name without their title: "Risu" of "Risu, Desertora faridun".</summary>
        public static string BareName(string name) => name.Split(',')[0].Trim();
    }
}
