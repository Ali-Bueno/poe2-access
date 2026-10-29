# What a sighted player sees on the map (2026-10-24, 0.5.5e)

Why the scanner lists what it lists (`EntityCatalog.Sighted`, `EntityCatalog.Entries`).

## Sources
- Web (forum, patch notes, wikis): little is written. Fog of war is real (layout revealed around the player, about a
  screen); content icons "have to be quite close to show" on the minimap (forum 3642449); the act world map marks
  quest areas; 0.5.2 added quest markers to some interactables. No source for the reveal radius or for icons
  through the fog. GGG's developer docs forbid tools that interact with the game (ToS 7b/7c/7i).
- Overlays: GameHelper2 Radar ("Draw Area/Zone Map (maphack)") and POE2Radar ("Reveal Map") show the whole layout
  before exploring; that is beyond what the game shows.
- The game's own table `Data/Balance/MinimapIcons.datc64` (1049 rows, 0x2A bytes), read with LibBundle3:

| Offset | Field | Values seen |
|---|---|---|
| +0x00 | Id (string) | `Waypoint`, `Checkpoint`, `NPC`, `QuestObject`, `MillerActive`… |
| +0x08 / +0x0C | minimap / large-map icon radius | 1–3 |
| +0x10 / +0x11 | two flags | players, portals, shrines 1/0; NPC, waypoint, entrance, stash 0/1; checkpoints and quest landmarks 0/0 |
| +0x12 | pointer max distance (dat-schema `MinimapIconPointerMaxDistance`) | 250 most, 400 Act 1 quest landmarks, 150 shrines and essences |
| +0x16 | draw order | MyPlayer 10, OtherPlayer 11, PartyMember 9, NPC 8, else 0 |
| +0x1A | label (string) | Waypoint, Checkpoint, Stash, Well, and the quest landmarks below |

- Campaign quest landmarks come in pairs, the active one labelled: `MillerActive` "Besieged Encampment",
  `MudBurrowerActive` "Vile Nest", `CroneActive` "Frostblood Ritual", `ClearfellCampsiteActive` "Mysterious
  Campsite", secrets (`GraveyardSecretActive` "Ancient Ruin"…). The `…Inactive` row has no label (done).
- Entities carry the `MinimapIcon` component (GameHelper: +0x20 → that row → its id). Component bytes on a live
  checkpoint: +0x08 owner entity, +0x10 int 1, +0x18/+0x20/+0x28 pointers.

## Live (Riverbank checkpoint, `entities all`)
The client knows little beyond a screen or two: the player, the checkpoint (18 cells, icon `Checkpoint`), a large
chest (102 cells, no icon); after walking, monsters up to ~200 cells, none with an icon. The Miller and his arena
were not in the list, so no far icon could have been drawn. The large map (D-pad down) showed only explored ground
and labels at the player's places. Not verified: whether an icon is drawn over unexplored ground inside that
bubble, and the unit of the pointer distance.

## Scanner rule
- Enemies: while on screen (the minimap draws no campaign monsters).
- Things with a minimap icon (exits, waypoints, checkpoints, NPCs, stash, shrines, quest objects): as soon as the
  client has them; the game draws their icon or the edge pointer.
- Everything else, named tiles included: once on screen or on explored ground.
- Quest category: what the objective names, wherever the game knows it (bubble entities and the area's tiles),
  seen or not — the quest marker (user, 2026-10-24: the scanner had stopped guiding to the objective).

## Labels, signs, boss rooms (2026-10-29, 0.5.5e)
- **Label:** MinimapIcons row +0x1A, a pointer to the label in the client's language (Spanish table: `WifeActive`
  "Corazón de la tumba", `HusbandActive` "Corazón del mausoleo", `CrowbellActive` "El pilar de caza",
  `OghamVillageRenlysToolsActive` "Taller de Renly", `Checkpoint` "Punto de control"). Read live through the component
  (+0x20 → row → +0x1A): "Waypoint|Transportador", "HealingWell|Pozo", "CraftingBenchIcon|Mesa de reciclaje".
  `dat-columns` finds it as the first translated string column (`MinimapIcons.Label`). Rows are 0x2A bytes back to back;
  each `…Active` row has its unlabelled `…Inactive` row beside it (`TitanGrottoEntranceInActive` spells it so).
- **Boss rooms:** `Metadata/Monsters/<Boss>/Objects/BossRoomMinimapIcon` (49 .ot files; base
  `Metadata/MiscellaneousObjects/BossRoomMinimapIcon`, icon `LootFilterLargeRedUpsideDownHouse`, turning
  `…GreyUpsideDownHouse` on the shared state `boss_dead`); campaign bosses override the icon (`CrowbellActive` →
  `CrowbellInactive`). The objects have only MinimapIcon and StateMachine. None was seen in the client's lists: at the
  Hunt Stone with the Crowbell alive the awake list held three entities, and the 2864 sleeping ones near Ogham's
  waypoint are decorations; GameHelper drops ids ≥ 0x40000000 (client-only visuals). So the client knows them only
  near, as it knows everything else; the scanner lists one when it shows (`MinimapMarks`), but no live sighting yet.
- **Boss rooms from tiles:** the client has the whole tile layout on arrival (GameHelper `TgtTilesLocations`), and
  GameHelper Radar's table names the campaign arenas ("TombBoss_ArenaFloor" "Asinia", "MausoleumBoss_ArenaFloor",
  "HagWitchArena_0x" "Beira of the Rotten", "CanopyArena" "The King in the Mists"); they were spoken among the exits. Now
  `MinimapMarks.ArenaBoss` makes them "Sala del jefe: …" (file or label with "boss"/"arena", not a way in such as
  "Arena_Transition_01" or "HoltenBossStairsDown_01"), and arena tiles the table lacks are found by file name, one per
  group of pieces (`Clusters`). The Mud Burrow has no arena tile: the Devourer rises from its `path_St_Spawner` paths.
- **Pointer reach:** MinimapIcons +0x12 (after two radii at +0x8, +0xC and two bools at +0x10, +0x11) is how far the
  minimap points to an icon beyond its edge: 250 for most rows, 400 for the campaign boss icons (MillerActive,
  CroneActive, MudBurrowerActive, RustObeliskActive, HusbandActive, WifeActive, FriendlyCauldronCroneActive), 150 for
  league devices (Shrine, Essence, Breach, Sanctum), 100000 for marks shown map-wide (BossSummoningLocationActive,
  RuneMarkersActive). Grid cells: the scanner lists a tile boss room once the player is that near (400 for the radar
  table's arenas, 250 for unnamed ones, the map boss house's value) or has seen its place; checked live in Clearfell
  2026-10-29: "En el mapa: Sala del jefe: Beira of the Rotten (10% de resistencia al frío)" at 4325 world units
  (398 cells). Read with `Poe2UiExtract dat-rows <game> MinimapIcons <id>`.
- **Signs:** story glyphs (`Metadata/NPC/Four_Act1/HuntingGroundsSignpostGlyph` "Pueblo de Ogham", `VillageGlyph1`
  "Decreto del conde", `ClearfellPosting1` "Proclamación") are typed NPC with icon `StoryGlyph`. The scanner took the
  signpost for a character and for the quest's village (user 2026-10-28); now "Letrero: …" among the objects, never a
  quest target. Seen live 2026-10-29: "Letrero: Decreto del conde, A".
- **Quest icons:** `QuestObject`, `QuestItem`, `MissionTarget` mark what the quest wants; such an entity is copied into
  the Quest category whatever the tracker says.
- **Title cards:** the big area-part banners (ClientStrings `TitleCardCrowbellArena`, `TitleCardUnasHome`,
  `TitleCardRenlysWorkshop`…, 70 of them) come from server-only proximity objects (`CrowbellArenaEntranceTitle.ot`:
  radius 50, off once the quest flag is set). None was seen on screen during the 2026-10-29 session, so where the UI
  draws them is not known.
- **World labels:** the names drawn over things in the world are UI labels under `world_ui_layer_parent`, named by the
  entity's metadata path ("Metadata/MiscellaneousObjects/Checkpoint" → "Punto de control"); the minimap itself has no
  UI children.
