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
