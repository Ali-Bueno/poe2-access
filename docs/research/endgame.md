# Endgame (0.5.x): the Atlas, maps, towers, league content (offline research, 2026-11-08)

Offline only: the game's files (bundle index of 2026-11-08, the build after 0.5.5e), the community dat-schema
(`poe-tool-dev/dat-schema`, `dat-schema/poe2/_Core.gql`, committed 2026-11-05: its row sizes match this install's), the
official 0.5.0 patch notes, and guides. The test character is in Act 2, so nothing here was seen live. The UI side is in
[endgame-ui.md](endgame-ui.md) (world map pins, the trials, GameHelper's old atlas offsets).

Confidence: **PROVEN** = read from the game's files or the code; **LIKELY** = consistent with them and the patch notes;
**UNSURE** = a guess to check live; **WEB** = guides only (poe2wiki was not reachable).

## 1. What the 0.5 endgame is

- WEB/official notes: 0.5.0 "Return of the Ancients" (29 May 2026), league "Runes of Aldur"; 0.5.5 "Forbidden Rites"
  (3 Sep 2026). The Atlas was redesigned around fixed places: a start camp, a **Fortress** reached after the first tower
  (Gateway and Citadel maps, Enigma chambers; Fortress maps give the Atlas passive points), Precursor Towers, league
  **hubs** (Breach south, Ritual west, Delirium west/south-west, Vaal temple east, Abyss south-east), Kingsmarch by the sea,
  Ocean regions revealed by logbooks (the league). Masters of the Atlas (Doryani, Hilda, Jado).
- PROVEN, the map of the Atlas is a **skill graph file**: `metadata/atlasskillgraphs/endgamemaplayoutgraph.psg`, 610 nodes,
  800 links, one node per group (its group's centre is its place). Its nodes are PassiveSkills rows
  (`AtlasInsideFortressWhite12`, `AtlasOutsideFortressPath30`, `AtlasLeagueBreachOuterNode3`, `AtlasWallTower4`,
  `AtlasNormalMapsWithRitual7`…, names "[DNT] …"), and `EndgameMapAtlas` (1,242 rows) says what each one is.
  The families' mean places (graph units, y down) put Breach at (-3,322, 7,877), Ritual (-9,440, 1,860), Delirium
  (-4,675, 4,677), the Vaal temple (1,903, 5,244), Abyss (1,907, 9,479), the Fortress north of the start
  (y -453 … -4,751) and the first tower (-2,209, 4,740): with y growing down that is the notes' layout, so the graph's y
  is the screen's (LIKELY; a live hover settles it).
- PROVEN, the other graphs: `atlasskillgraph.psg` = the **Atlas passive tree** (575 nodes, real names: "Magnanimous
  Offerings", "Water Biome: Gold Found"; league sub-trees Breach, Delirium, Ritual, Abyss, Incursion, Expedition…),
  `leagueskillgraphs/chayulatreepassiveskillgraph.psg` = Breach's **Genesis tree** (248 nodes: `BrequelTree` currency,
  rings, amulets, belts), `royalepassiveskillgraph.psg` (6, PvP). The main tree stays `passiveskillgraph.psg`.
- PROVEN, map content (`EndgameMapContent`, 75 rows, every one with a localized Name and Description): Powerful Map Boss,
  Breach, Expedition, Delirium, Ritual, Abyss (and Overrun, Fissure, Depths), Vaal Beacons (Incursion), Irradiated
  (+1 level), Essence variants, Strongbox variants, Shrine variants, Rogue Exile variants, Azmeri spirit variants,
  Summoning Circles, biome influences, Unique Map, Hideout, Merchant, Viridian Wildwood, Simulacrum, "Map Area Modified".
- PROVEN, objectives (`EndgameMapObjectives`, 19): Map Boss, Corrupted Nexus, Checkpoints, Rare Monsters, Breach, Breach
  hives, Expedition, Verisium Remnants, Delirium, Ritual, Abyss, Abyssal Depths, Shrines, Strongboxes, Essences, Rogue
  Exiles, Azmeri Spirits, Summoning Circles, Vaal Beacons: each an objective text, a completion text, an Atlas icon and,
  for 12 of them, the **minimap icon of what it asks for**.
- PROVEN, towers and tablets: `EndgameMapAtlasTowers` (8 towers, each with the Atlas nodes in its reach and a reveal type),
  `EndgameMapTablets` (8 tablets: Overseer/map boss, Breach, Expedition, Delirium, Ritual, generic/Irradiated, Abyss,
  Incursion; base items `Metadata/Items/TowerAugment/*`), `AtlasTowerInventoryLayout`. Tablets go into a tower, not the
  map device (WEB), 3 slots (WEB).
- PROVEN, other tables: `EndgameMaps` (174 endgame areas: pin kind, flavour text, objective description), `EndgameMapPins`
  (65 kinds: DefaultMap, DefaultTower, Iron/Copper/StoneCitadel and Corrupted…, UniqueMap…, BreachHub…, Logbook, WaypointPin),
  `EndgameMapBiomes` (13: Water, Mountain, Grass, Forest, Swamp, Desert, the three cities, Breach Stronghold, Ocean,
  Island, Oriath), `EndgameQuickTravelHubs` (9 quick-travel buttons: town, Arbiter of Ash, Arbiter of Divinity,
  Expedition, Delirium, Ritual, Breach, Incursion, Abyss), `EndgameRegionActionButtons` (Breach, Forest, Ocean, Tower: the
  "quick use" item on a region: breachstone, ritual key, logbook, tower activation), `EndgameMapNodeStats` (a node's
  extra: Delirium fog bank, Grand Mirror, Rite of the Nameless, failed Simulacrum), `EndgameCorruptionMods` /
  `EndgameCleansedMods`, `EndgameSplinterRecipes`, `MapDevices`/`MapDeviceRecipes`.

## 2. The data the mod now extracts (`Poe2UiExtract endgame` → `catalog/endgame.json`, launcher step "endgame")

`tools/Poe2UiExtract/EndgameExtractor.cs` (+ pure `SkillGraphFile`, `GameMarkup`). Column offsets from the schema, each
checked by what it holds (a moved column fails the step and keeps the previous catalog):

| Table | Columns read (row offset) |
|---|---|
| EndgameMapObjectives (0x30) | Id 0x0, ObjectiveText 0x8, CompletionText 0x10, ContentIcon 0x18, MiniMapIcon key 0x20 |
| EndgameMapContent (0x5D) | Id 0x0, Description 0x28, Name 0x30, MapObjective key 0x3C, VisualIdentity key 0x4C |
| EndgameMapContentVisualIdentity (0x2B; schema 0x28) | AtlasIcon 0x8 |
| EndgameMapNodeStats (0x75) | VisualIdentity key 0x44, Name key 0x54, Description key 0x64 (ClientStrings2) |
| EndgameMapBiomes (0xA0) | Id 0x0, Name 0x28 |
| EndgameMaps (0xF1) | WorldArea key 0x0, MapPin key 0x3C |
| EndgameMapAtlas (0x121) | Passives key 0x0, Map list 0x10, Biome list 0x24, MapPin 0x68, BlockedMessage 0x7C, MapObjective 0xA7, ClientString (hint) 0x10D |
| KeywordPopups | Term 0x8, Definition 0x10 (the objective's first keyword: "ContainsBreach" → "Fisura") |
| ClientStrings2, WorldAreas, PassiveSkills | Text 0x8; Name 0x8; graph id u16 0x30, Name 0x32 |

`.psg` v3 layout (PROVEN, every file read to its last byte): u8 version 3, u8 kind, u8 orbit count + one byte each, u32
root count + 8 bytes each, u32 group count; a group = f32 x, f32 y, u32, u32, u8, u32 node count; a node = u32 graph id,
u32 orbit, u32 slot, u32 link count, then per link u32 id + i32 curve. Orbit radii are not in the file, so a tree node
on an orbit gets its group's centre (the drawn element gives the real place). One id is listed in two groups in the Atlas
tree and the layout (the start): its first place is kept.

The JSON: `objectives` {id: icon, keyword}, `contents` {id: objective}, `icons` {art file: name key, text key}, `maps`
{area id: pin}, `nodes` {passive id: graph, x, y, orbit, slot, links, pin, maps, biomes, objective, blocked, hint}, `text`
{language: {key: text}} with keys `objective/`, `done/`, `term/`, `help/`, `content/`, `contentText/`, `biome/`, `client/`,
`area/`, `passive/` (10 languages, ~1,200 texts each; Simplified Chinese has no tables). 1.3 MB.

## 3. In a map: what each objective's object is (PROVEN, `.ot` files)

| Objective | Minimap icon | Object (`.ot`) | Scanner before | Now |
|---|---|---|---|---|
| Breach (+ hives) | Breach | `MiscellaneousObjects/Breach/BreachObject`, `EndgameBreach`, `Brequel/BrequelInitiator`, `BrequelStabiliser*` | not listed (no `/Leagues/`) | Encounters, "Fisura" |
| Abyss | AbyssPitActive → AbyssPitInactive when done, then hidden | `MiscellaneousObjects/Abyss/AbyssNodeBase` | not listed | Encounters while active, "Abismo" |
| Expedition | ExpeditionDetonator | `MiscellaneousObjects/Expedition/ExpeditionDetonator` (WorldDescription title) | not listed | Encounters, its world title |
| Vaal Beacons | IncursionArchitectUpgrade | `MiscellaneousObjects/LeagueIncursionNew/IncursionPedestalEncounter`, `AtziriShrineEncounter` | not listed | Encounters, "Foco vaal" |
| Essences | Essence | `MiscellaneousObjects/Monolith`, `SuperMonolith`, `Monolith_Landmark` (also in the campaign) | plain object at best | Encounters, "Esencia" |
| Delirium | AfflictionInitiator (hidden once used) | `Terrain/Gallows/Leagues/Delirium/Objects/DeliriumInitiator` | Encounters, "Delirium" (folder) | "Delirio" |
| Summoning Circles | RuneMarkersActive → RuneMarkersInactive | `Terrain/Gallows/Leagues/StoneCircle/Objects/RuneRock_Controller` | Encounters (folder name) | "Círculo de invocación" |
| Ritual | RitualRune | `Terrain/Leagues/Ritual/RitualRuneInteractable` | Encounters (done before) | unchanged |
| Strongboxes, Shrines | Strongbox, Shrine | chests, shrines | Chests | unchanged |
| Rogue Exiles | RogueExile | `Monsters/RogueExiles/*` | Enemies (unique) | unchanged (creatures are left alone) |
| Azmeri Spirits | SpiritActivated | `Monsters/TormentedSpirits/TormentedSpiritBase` (a daemon) | not listed unless hostile | UNSURE: left out (a creature); see the plan |

The rule (`EntityCatalog.LeagueDeviceIcon`): an object outside `Metadata/Monsters/` whose current minimap icon is an
objective's is an Encounter; its name, when it has none, is its objective's keyword (`MinimapIconLabel`). A spent object
changes or hides its icon, so it drops out by itself.

## 4. What a blind player needs, step by step, and what the mod does

1. **Reach the Atlas.** Map device in the hideout/town, or the world map's Atlas tab (`WorldScreen` → `act_buttons_layout`,
   endgame-ui.md §5). Generic: the device is a targetable object (scanner Objects), the tab a button. LIKELY works.
2. **Read a node** (cursor over the Atlas). Done (code, UNSURE live): `UiSemantics.Endgame` — when the hovered element's
   graph record (looked for at the main tree's node-record offset ±0x200, learnt per element class, trusted only when the
   PassiveSkills id is a catalogued endgame node) leads to an Atlas node, it says the popup's title (else the data's one
   map, else its biomes, else "Atlas node"), "Contiene: Fisura, Jefe de mapa" from the content **icons** under the node and
   its popup (art file → catalog), the rest of the popup, the node's objective, hint ("Activa el Repetidor precursor") and
   what opens it ("Completa la Entrada occidental para acceder"), then "Conexiones: arriba a la derecha, Torre precursora;
   …" from the graph's links, each side from the drawn nodes' centres (else the graph's own places).
   The popup's own words (map name, tier, completed, modifiers) are read as the game writes them.
3. **Atlas passive tree / Genesis tree.** The same reader: the node's game name ("Magnanimous Offerings"), its tooltip
   (stats, allocated or not as the game says), its links with sides. Allocation state from memory is NOT read (the main
   tree's state object is not assumed to be the same class). UNSURE until hovered.
4. **Map device window.** Waystone slot, tablet/fragment slots, Activate: ordinary focus items and item tooltips (generic).
   LIKELY works. Waystone tooltips (tier, modifiers, revives) read as items.
5. **In the map.** Objectives: the HUD's objective lines are the game's text, read as HUD news (generic, LIKELY); the
   scanner's Encounters now lists what they ask for (§3); the map boss is a unique enemy (Enemies/boss watch). Portals and
   revives: the game's lines.
6. **Towers and tablets.** Tower map nodes are Atlas nodes (step 2); the tower's tablet window (3 slots) LIKELY reads as
   slots; the "Tower" region action button is an ordinary button. Which nodes a tower reaches is in the data
   (`EndgameMapAtlasTowers.AtlasNodes`), not yet said.
7. **League mechanics in play.** Breach: walk into the hand, kill what comes out before it closes (fight cues as usual).
   Delirium: walk into the mirror, the fog moves with the player. Ritual: done. Abyss: pits along a crack. Vaal beacons:
   energise. Essence: free the monster from the monolith (interact). Expedition: **placing explosives** on a chain with a
   cursor is a spatial puzzle the mod does not read (gap). Summoning circle: activate, a boss comes.

## 5. Gaps and plan

| Item | Confidence | Plan |
|---|---|---|
| Atlas node reading (name, contents, links, needs) | code done, UNSURE live | First live hover: `atlasnode` dev command; fix the record search or the canvas walk if the Atlas draws nodes another way. |
| Atlas node state (completed, reachable, corrupted) | UNSURE | Read from the popup's words first; then look for a MiscAnimated key on the node (EndgameMapPins Unavailable/Available/Active/Failed/Complete pins) with `datkeys`. |
| A finder on the Atlas ("nearest tower / citadel / not completed") | LIKELY feasible | The graph and pins are in the catalog; needs node state from memory for "not completed". |
| Which nodes a tower reaches; tablet effect on them | PROVEN data, not used | `EndgameMapAtlasTowers.AtlasNodes` into the catalog; say "in the reach of 2 towers" on a node. |
| Azmeri spirits in the scanner | UNSURE | Check live whether the spirit is hostile/targetable; list it by its icon if it is a friendly creature. |
| Expedition explosive placement | gap | Needs its own reader (placement cursor, chain length, remnants' places): research when reached. |
| Map objectives as quest targets | LIKELY | Match the HUD's objective text to `objective/<id>` and make the icon's objects Quest targets. |
| Map's modifiers on arrival | UNSURE | The area's map stats are not read by the host; the waystone tooltip says them before. |
| Allocation state of the Atlas trees | UNSURE | Compare the trees' state object with the main tree's (`PassiveTreeFields`) once one is open. |

## 6. Live protocol (when a character reaches the endgame)

1. **Scanner, in any map with content.** Open the Encounters category. Expect "Fisura", "Abismo", "Esencia", "Foco vaal",
   "Delirio", the detonator's title. Log: `Endgame: 19 map objectives, 12 with a minimap icon, 1439 graph nodes from the game
   files` at start; no "Minimap mark not listed" for these icons. An Essence monolith in the campaign (Act 2-4) tests it
   early. After a breach/abyss is done, it leaves the list.
2. **Atlas map.** Open the Atlas, move the cursor onto a node. Speech: the popup's title, "Contiene: …", "Conexiones: …".
   Then run dev command `atlasnode` (command file) with a node hovered; the log line `Atlas node: @… class … record +0x…:
   AtlasOutsideFortressPath30 graph endgamemaplayoutgraph …` proves the chain. If it says "no endgame node", run
   `datkeys` and `dump visible` with the node hovered and keep both files.
3. Check one side: from a node, push the stick the way a said link lies; the cursor must reach that node.
4. **Atlas passive tree** (and the Genesis tree): hover a node, expect its name and links; `atlasnode` as above.
5. **Map device:** put a waystone, hover Activate; dump if the slots read nothing.
6. **Tower:** open a cleared tower's tablet window; dump it.
