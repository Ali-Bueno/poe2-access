# Passive skill tree: nodes, names, allocation, edges (PoE2 0.5.5e, 2026-10-23)

Read live, read-only (`tools/computer_use/peek.py` `Process` plus scratch scripts; input only through
`desktop.py`: radial menu to open, left stick to move, Circle to close, nothing allocated), Spanish client,
controller mode, Mercenary "yarkey" level 2 with 0 unspent points, PID 30604, InGameState S = `0x46678E53F10`.
UI dumps `debug/ui-tree-InGameState-20261023-202041.txt` and `-203026.txt` (tree open, visible-only).
Confidence: **MEASURED** = read live and checked against game output; **INFERRED** = consistent, not proven
on every case; **OPEN** = not known.

## Elements (G = `S+0x318`, M = `S+0x310`)

- MEASURED: **`G+0x3C0` = passive panel P** (title `+0x2E8` = "Pasivas"; child 2 of `panels_parent`, which is
  child 2 of the shared panels layer `G+0x378`). The pointer, the panel and everything below it **persist while
  the tree is closed**; closed = P's visible bit (0x0B of `+0x168`) clear, `M+0x760` = 0 (checked after Circle).
  GameHelper's `PassiveSkillTreeNodesChildPath {24, 2}` and `ImportantUiElementsOffsets.PassiveSkillTreePanel`
  (`G+0x730`) are stale in 0.5.5e (G child 24 has one unrelated child; `G+0x730` is another element).
- MEASURED: **canvas = P child 0** (14030×14039, pans with the view). Its **1372 children are groups** (first 4
  empty), each group's children are the node elements: **4477 elements of one class** (one vtable — take it at
  runtime), every one with a node record at `+0x2C8` (below); 3 other-class children carry none. Other P
  children: 1 `edge_fade_overlay`, 7 zoom legend ("Zoom", "x1,0"), 8 `expanded_frame` (search box, points,
  attributes), **10 = the cursor reticle** (83×83, screen centre).
- MEASURED: on-screen rect = the element's ordinary UI rect (GameHelper `UiElementBase` position/size, as the
  dumper prints it). Sizes by kind: small 39×39 (2940), notable or jewel socket 57×58 (925+12), keystone 82×84
  (33), icon-only "Maestría …" 113×113 (347). 220 elements are hidden (visible bit clear): 197 ascendancy nodes
  (`row+0x167` Ascendancy FK set) and 23 others (special jewel sockets among them).
- MEASURED: **hovered node = `M+0x760`** (already used by the plugin). The view pans so the hovered node sits
  under the reticle: in both dumps hovered rect centre = reticle centre = (1279.5, 781.5). The game's left-stick
  movement is geometric, not along edges: the stick went Armadura y evasión (59779) → Velocidad de ataque (97)
  → Armadura y evasión 12% → Regeneración de vida, and the third is not linked to the second.
- MEASURED: only hovered nodes have a `+0x120` tooltip (4 of 4477 after 4 hovers), so text must come from the
  dat rows. Tooltip texts: title, "Name\n(English name)", stat lines, "\nSin asignar" (unallocated only).

## Node → record → localized name (every hop checked by the FK's table name)

Foreign key = (row ptr, file ptr), file `+8` = std::wstring table name (same check as the skill bar).

- MEASURED: **node element `+0x2C8` → node record** (a PassiveSkillGraph node, 0xE0-byte objects). GameHelper's
  `SkillTreeNodeUiElement.SkillInfo` `+0x2A0` is null now (stale), and its `+0x08` row hop is `+0x00` here.
- MEASURED: **record `+0x00` = FK to "Data/Balance/PassiveSkills.dat"** (4477/4477). Record `+0x10` u32 = index
  in the PSG node table (below; 7633/7633), `+0x30` → PSG content object, **`+0x50/+0x58` = std::vector of edge
  pointers, out-edges only** (edge `from` == this record in 5385/5386; one self-loop).
- MEASURED: **PassiveSkills row** (unaligned, stride 0x1E7 from consecutive rows): `+0x00` Id
  ("armour_and_evasion2"), `+0x08` icon path, `+0x10` stats count + `+0x18` array of 16-byte FKs to
  "Data/Balance/Stats.dat" (Stats `+0x00` Id "base_physical_damage_reduction"), `+0x20..+0x2C` i32 stat values
  (10, 8 ↔ "+10 a la armadura / +8 a la evasión"; values past the count are junk), **`+0x30` u16
  PassiveSkillGraphId** (unique across 7632 rows; 59779), **`+0x32` Name = localized name** (equals the tooltip
  title on all 4 hovered nodes), `+0x3A` Characters list (count, ptr → FKs to "Data/Balance/Characters.dat"),
  **`+0x4A` IsKeystone** (33, = the 82×84 elements), **`+0x4B` IsNotable** (984), **`+0x54` icon-only** (368 of
  the 375 "Maestría …" rows, no stats; INFERRED decoration), **`+0x65` IsJewelSocket** (17/17, 0 others),
  `+0x12F` attribute node (293/293 "Atributo", 0 others), `+0xAC` FK PassiveSkillMasteryGroups, **`+0x167` FK
  "Data/Balance/Ascendancy.dat"** (ascendancy nodes), `+0x178` FK PassiveSkillTreeNodeFrameArt, `+0x1B3` FK
  PassiveSkillVariantTypes.
- MEASURED: stat lines are not cheap: the row holds Stats ids and raw values only; the text needs the stat
  description files (12 per minute is shown "0.2% de vida por segundo"). Use the hover tooltip for stats.

## Allocation

- MEASURED: **tree state object T = canvas `+0x3D8`** (`+0x3E0` = T−0x10, a make_shared control block). Checks:
  `T+0x160` → file "Metadata/PassiveSkillGraph.psg", `T+0x170` → "Data/Balance/PassiveSkillTrees.dat",
  `T+0x178` ClassPassiveSkillOverrides.dat, `T+0x180` AscendancyPassiveSkillOverrides.dat, `T+0x190`
  PassiveSkillVariants.dat. It persists while the tree is closed.
- MEASURED (one allocated node; it is all this character has): **`T+0x1A8` = std::vector of 4-byte entries
  whose low u16 is the PassiveSkillGraphId of an allocated node**: one entry `83 E9 00 56` = 59779, the only
  node whose tooltip lacks "Sin asignar". INFERRED: entry = {u16 id, u8 (weapon set?), pad}; the empty vectors
  at `T+0x1C0`/`T+0x1D8` may be the weapon-set lists (OPEN). Not found in PlayerServerData (`AreaInstance+0x5B0`
  → `+0x48`[0]) within two hops.
- MEASURED: **node element `+0x64C` byte = 1 when allocated** (exactly 59779 of 4477) and `+0x648` = 0 on it, 4
  on every other node, including the three nodes linked to it. So the UI holds no "can allocate" state, at
  least with 0 points (OPEN: values with points to spend).
- MEASURED: **class start nodes are not UI elements** (the portrait): PSG records whose row has a non-empty
  Characters list: MERODEADOR 47175, BRUJA 54447, EXPLORADORA 50459, DUELISTA 50986 (Duelista, Mercenario),
  TEMPLARIO 61525, SEIS 44683. **Own start** = the one listing a Characters row whose `+0x00` Id equals the player
  entity's metadata path ("Metadata/Characters/StrDex/StrDexFourb"; Characters `+0x08` = "Mercenario"); the
  allocated node 59779 is linked to DUELISTA, which confirms it.
- INFERRED: state = allocated (in the list), **reachable** (not allocated, linked to an allocated node or the own
  start), else **locked**; "can allocate now" = reachable and points > 0. OPEN: jewels that allow unconnected
  allocation, weapon-set points, ascendancy.

## Edges

- MEASURED: **PSG content C = `[T+0x160]+0x28`** (= every record's `+0x30`). **`C+0x58/+0x60` = std::vector of
  6076 edge pointers**; edge = `+0x00` from record, `+0x08` to record, `+0x10` u32 index (= position, 6076/6076),
  `+0x14` i32 (signed arc/orbit value), `+0x18` u32 flags (0, 1, 1024, 1025). `C+0x20` = vector of 7633 0x18-byte
  entries (row, file, record) indexed by record `+0x10`.
- MEASURED: drawing every node–node edge between the dump rects onto a screenshot lands on the game's lines
  (straight chords for the curved ones). **Edges touching an icon-only node are not drawn** (two notables "linked"
  through a red mastery glow had no line between them), so drop them. Undirected adjacency = union of both
  directions; per-record vectors alone miss incoming edges.
- So the geometric fallback is not needed for neighbours; geometry is only for the direction words.

## Live readout (prototype, dump 203026 rects, screen y down)

- Armadura y evasión (small, **allocated**, 59779): DUELISTA start; abajo 219 px Velocidad de ataque
  (reachable); abajo 211 px Armadura y evasión (reachable); abajo-izquierda 324 px Atributo (reachable).
- Velocidad de ataque (small, reachable, 97): arriba 219 px Armadura y evasión (allocated); arriba 219 px Daño de
  proyectiles (reachable: linked to the start); abajo-derecha / abajo-izquierda 62 px Velocidad de ataque (locked).
  Two neighbours share "arriba" (a fork), so direction words need a tie-break.
- Regeneración de vida (small, locked, 14926, hovered): abajo 65 px Duro de matar; arriba 308 px Endurecido por la
  batalla (both locked).
- Points: **P child 8 → child 1 (`top_points_layout_gmp`) → child 0 → child 1 "Puntos de habilidad:", child 2
  "0"**; the attribute circles (DES 11, INT 7, FUE 11) are child 6 of the same layout. INFERRED: its 6 hidden
  siblings are the weapon-set and ascendancy counters.

## Reader sketch

1. Locate: P = `G+0x3C0`; canvas = P child 0; T = canvas `+0x3D8`; check T's `+0x160` and `+0x170` file names.
   Open = P visible. C = `[T+0x160]+0x28`.
2. Graph (static per session, cache it): walk canvas → group → elements; keep each element whose `+0x2C8`
   record has `+0x00` FK to PassiveSkills.dat; read Id, Name, GraphId, kind bytes. Drop hidden and icon-only
   (`+0x54`). Edges from `C+0x58`: add both directions, skip self-loops and icon-only endpoints, keep start
   records (name from row `+0x32`) as neighbours. Own start via Characters Id = player entity path.
3. Per hover change (`M+0x760` → element → record): allocated set = low u16 of each `T+0x1A8` entry
   (cross-check `+0x64C`); points from the label above; state per node as above.
4. Directions: angle from the hovered element's rect centre to each neighbour's (8 sectors, screen y down;
   start nodes: the portrait's position, OPEN); sort clockwise from up; same sector → order by angle and say
   "arriba, arriba" or use finer words. Announce "Velocidad de ataque, menor, alcanzable. Arriba: Armadura y
   evasión, asignado; …"; stats keep coming from the tooltip.
   MEASURED: the game builds a node's tooltip ~150 ms after the hover (log 2026-10-24 10:49:15.635 → .787).
   Name and state are said at once; stats, then links, follow with the tooltip. Links said before it made the
   late stat sound like the last neighbour's ("…, Projectile Damage. 10% increased Attack Damage", user
   report 2026-10-29).
5. Fault checks: every FK checked by table name; an allocated id with no element, or a `T+0x1A8` entry count
   that disagrees with the `+0x64C` flags, logs once and falls back to the tooltip's "Sin asignar".

OPEN: `+0x648` values and `T+0x1A8` entry layout with several or weapon-set allocations; the ascendancy view
(hidden nodes); start-node screen position; whether the stick can stop between nodes (`M+0x760` = 0).
