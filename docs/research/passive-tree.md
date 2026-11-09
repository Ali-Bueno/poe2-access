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

## Class overrides (2026-11-09)

- MEASURED (game files, 0.5.5f): **ClassPassiveSkillOverrides** (61 rows of 0x30: Characters key +0x00, the PassiveSkills
  row replaced +0x10, the row shown +0x20) swaps nodes for three classes only: Witch `Metadata/Characters/Int/IntFour`,
  Huntress `Dex/DexFourb`, Druid `StrInt/StrIntFourb`. The node record keeps the replaced row; the game shows the other
  one (its own name, stats and graph id). A Druid heard `spell_criticals7` "Daño crítico de hechizos" over the tooltip of
  `druid_class_start22` "Regeneración de vida", `criticals25_` "Destino cruel" over "Espíritu primitivo", `area_attacks4`
  "Área de ataques" over "Daño de hechizos y ataques" (2026-11-07 and 2026-11-09); the Mercenary has no overrides.
- The plugin reads the shown row for the player's class (catalog `passive-overrides.txt`, extractor `passive-overrides`;
  `UiSemantics.ShownRow`: the replaced row plus (shown − replaced) × the row size 0x1E7, trusted only when the Id there
  is the shown one). Name and kind come from it, the graph id, start and stick gate from the record's own row; a node
  counts as allocated under either graph id. **NOT MEASURED:** which of the two the allocated list keeps for an overridden
  node, and AscendancyPassiveSkillOverrides (13 rows, `AscendancyWitch3*` → `AscendancyAltWitch*`, its first key into a
  table not identified), not handled: the ascendancy view was never seen live.
- PassiveSkills row (0x1E7 bytes): Id +0x0, icon +0x8, stats key list +0x10, values from +0x20, graph id u16 +0x30, name
  +0x32 (unaligned: `dat-row-strings` steps by 4 and misses it).

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

## Stick movement (2026-10-31, kehgan, Spanish client)

- MEASURED: a push of the left stick (an octant) lands on the node scoring lowest within **45 degrees** of the push:
  **distance × (1 + 1.5 × tan(angle off the push))**, in screen pixels between element rect centres. Fitted to 127 moves
  recorded with the dev command `passivenear` (hovered node, the nodes around it with angle, distance and link, then
  the push and where it landed) at zoom x1,0, x1,9 and x0,6: 120 right (35/37, 30/30, 28/30, 27/30); the misses are
  near-ties or nodes 20-40 px apart at the farthest zoom. Links play no part: a node 800 px away is reached in one push
  when nothing lies nearer within the cone. Other forms tried and worse: distance × (1 + k × angle/45) (116),
  distance + c × angle (scale-dependent), links first (104). `PassiveStick` (pure, unit-tested).
- So a link is named by the push that reaches it (the nearest octant among those whose pick it is), or "not in one push"
  when none does; checked live on six links in a row (all landed as said).
- Zoom: LT/RT; the factor is the text "x1,4" beside the "Zoom" label (P child 7); the screen differ took its change for
  a counter tick until 2026-10-31.
- MEASURED (2026-11-08, yarkey, `stick-test`/`stick-walk` with the virtual pad, Y centring on armour_and_evasion2): the
  game lands on the same nodes at 1280x1024, 2560x1080 and 2560x1440 (eight pushes from one node, identical), so it picks
  in the tree's own coordinates. The model's input was one frame's screen pixels cached when the tree opened, and that
  frame gave other picks at 5:4 and 21:9 (2 of 8 right at 21:9). `PlacesOf` now takes laid-out places under the canvas
  (`UiSemantics.LayoutRect`: node and node holder are scale index 2 at local scale 0.42, the holder's RelativePosition is
  the tree place), the same at every window, zoom and pan: picks became identical at 5:4 and 21:9. Accuracy on random
  walks: 100/120 at 21:9 (seed 7, many pushes in the dense start area), 107/120 at 16:9 (seed 11). Refits on the 21:9 walk
  (angle penalty 0.75-1.0, linked nodes ×0.85-0.9) scored 108-110 there but 102-105 on the 16:9 walk, worse than the
  current rule's 108: kept as fitted. Misses: a linked node chosen over a nearer one straight ahead (down from
  armour_and_evasion2 lands on attack_speed2_, not attack_speed1), and pushes that do not move although a node lies
  ~800 px (21 node sizes) within the cone, while one 959 px at 15 degrees was reached.
- READ FROM THE GAME'S CODE (2026-11-08, static disassembly of the exe, report and tools in
  D:\code\decompilers\projects\poe2-research\re\stick-rule.md, `validate.py -v`): the step is the function at RVA
  0x9B9BA0 on the tree canvas (`canvas+0x1368` = current node record = the hovered node's `+0x2C8`), given the stick's own
  direction (y flipped, not snapped to octants). (1) Linked nodes first: a link within 19.95 degrees (`dot >= 0.94`) is
  taken; the code computes `(1.5 - 8.333 * (dot - 0.94)) * distance` but stores the dot instead, so likely the first such
  link in the node's edge list wins; this stage is skipped when the node's row has a non-zero byte at `+0x66` (ascendancy?).
  (2) Else the lowest `(5 - 4 * dot) * distance` among nodes with `dot > 0.7071` (the constant at 0x9CA139 is 0x3F350481 =
  0.7071f, acos 45.0005 degrees, so a node at exactly 45 degrees is in the cone: 4 recorded pushes landed on one). (3) In
  both, only nodes whose centre is inside the game window, visible and selectable; no other distance limit (the pushes that
  did not move had their nodes off the window's top or bottom; the cursor sits at the panel's centre). (4) None: no move.
  Applied to the 240 recorded pushes: 240/240 (the fitted model 208; without the on-screen test 229, without the linked
  stage 234). Holding the stick past half deflection for 0.16 s pans the view and snaps on release to the on-screen node
  nearest the centre (0xA194C0, 0xA19920, 0xA1A410).
- IMPLEMENTED (2026-11-08): `PassiveStick.Pick(offsets, octant, linked, window)` is the game's rule, stages 1 and 2 with the
  game's float constants and comparisons (the linked stage keeps the cosine as its best score, as the code does, so the first
  link in edge order wins). Inputs from `UiSemantics.PassiveTree`: offsets are the laid-out places under the canvas;
  `linked` is the hovered record's own edge vectors (`+0x38` in-edges, other end at edge `+0`; `+0x50` out-edges, other end
  at `+8`; `GameLayout.PassiveTree.RecordInEdgesOffset/RecordOutEdgesOffset`), trusted only when every edge names the record
  and a node the file links it to, else the same order rebuilt from the graph file's edge list (in-edges in file order, then
  out-edges; `PassiveGraph.StickLinks`); the `Window` is the hovered node's screen centre (GameHelper's absolute rect),
  `Core.Process.WindowArea`'s client size and the scale = the node's screen width / its canvas width. The row's 8 bytes at
  `+0x66` (`RowNoLinkedStepOffset`, column unknown; the code also shifts such nodes by the sub-canvas, likely ascendancy)
  are read but not yet applied: the linked stage is skipped by the game when they are non-zero. The multiple-choice redirect
  (`row+0x8B`, `elem+0x648`) is not implemented. When the window or the rects are unreadable, the fitted model stands in
  (`PassiveStick.PickFitted`, 208/240). `Route` (finder and Build Planner ways) uses the same rule, the first step with the
  hovered record's live edge order and every other with the file order. Tests: `PassiveStickTests` replays the 240 pushes
  from `tests/Poe2Accessibility.Tests/Fixtures/stick-walks.txt` (cone candidates only, screen pixels, the cursor at the
  panel's centre: 1279.75, 585.9 at 2560x1080; 1279.75, 781.2 at 2560x1440) at 240/240; the recordings hold no edge order,
  so the three pushes with two links in the cone (down from armour_and_evasion2) feed the links by the game's linked score.

### Live confirmation protocol (run 2026-11-08, yarkey, Mercenary level 22, 2560x1440)

On the passive tree with the virtual pad, ResearchMode on so `passivenear` logs (`debug/command.txt`), screen reader on:

1. Y to centre on the allocated nodes; `passivenear` for the hovered node. The log must show `window WxH cursor X,Y px per
   unit S` with W, H the client size and X, Y about the panel's centre (not `none (fitted model)`), and `edge order record:
   … ; file: … ; no linked step False`. The record's order must list the same ids as the file's; note whether in the same
   order (if `unreadable`, the record vectors moved: the file order is in use).
2. Hover armour_and_evasion2 (Duelist/Mercenary start area, the node where the walks began) and `passivenear`: the record's
   edge order must have attack_speed2_ before armour_and_evasion1 (the game landed on attack_speed2_ with down, three
   times); if the opposite, the first-wins reading is wrong and 0x9B9F1A-0x9B9F26 must be re-read.
3. From that node push each of the 8 directions once (0.2 s), returning with the opposite push or Y: every landing must be the
   id the `picks` line named before the push, including the `-` ones (no move). Then at zoom x0.6 and x1.9 (LT/RT) the
   same 8 pushes: the picks line must change where nodes leave or enter the window, and the landings must match it.
4. Each hovered node's links line ("arriba: …, abajo izquierda: …") must name the push that lands on it: follow six links in
   a row by the push said; six of six must land on the node named.
5. Finder: pick a keystone with the D-pad and follow its "N pushes" way; the count must fall by one per push with no
   correction (a push landing elsewhere shows as a count that does not fall).
6. Any miss: `passivenear` on the node before the push and paste the `picks`, `window` and `edge order` lines with the
   landing into docs/research/passive-tree.md.

Results (0.5.5f, virtual Xbox pad): (1) after Y `window 2560x1440 cursor 1279,8,781,2 px per unit 0,3780`, `edge order record:
duelist597 strength34 attack_speed2_ armour_and_evasion1; file:` the same ids in the same order, `no linked step False`.
(2) attack_speed2_ before armour_and_evasion1: the first-wins reading holds. (3) The 8 pushes from armour_and_evasion2 landed
on the picks' ids 8/8 at x1.0 (`up -`: no move), 8/8 at x0.5 (up then picks spells18, which entered the window, and landed
there) and 8/8 at x1.9. (4) Six links followed by the push said (down, down-right, down-right, down, down, down-right): 6/6.
(5) Oasis from the end of that walk: one miss. On dexterity66 (Attribute) the way said "down-right, 3 pushes" and the push
landed on lightning11 (as the picks said), which said 7; reached again from lightning11 the same node said 8. Cause: the
readout comes a frame after the push while the view still pans: `passivenear` at once gave `hovered dexterity66 at 1309,695`,
`cursor 1307,3,699,5`, settled 1.5 s later `1280,781`. A window placed on the mid-pan node lets far nodes in, so the route and
even the links line ("right, not in one push, Lightning Damage" then, a frame later, "down-right, Lightning Damage") changed
with the side the node was reached from. The window now sits where the node rests, the centre of the canvas's frame (the
panel element `0,122 2560x1318`, `PassiveStick.SettledCursor`): dexterity66 says 8 from both sides and a walk from
armour_and_evasion2 counted 15, 14 … 1, "Arrived at Oasis" with no correction.

## Finder and what the pad does in the tree (0.5.5f, 2026-11-07, malgdhler)

- MEASURED, the virtual pad on a node (hint bar: "Editar filtro [L3]", "Modo inspección [R3]", "Zoom [RT]", "Centrar [Y]",
  "Seleccionar [A]"): L3 opens the filter, R3 inspect mode, Y centres on the allocated nodes, LB goes to the Character panel and
  RB back, X did nothing with no change pending. The **right stick moves the cursor too**, so it is not free. The stick rule's self-check judges only a left push that does not start where the right stick
  left the cursor: the user's evening of 2026-11-09 lost the rule twice ("7 hit, 3 missed") right after right-stick moves.
  MEASURED (2026-11-09 17:22, the user's Druid, zoom x0,4, trace `Tree right stick` of v2026.11.09-5): the right stick
  **pans the view**: the canvas slides (deflection 0.55: 5 px in the first 100 ms) while the cursor stays at the frame's
  centre (1280,781 in 2560x1440) and hovers whatever node passes under it ("no node" in between); **on release the view
  eases until the hovered node sits at the centre**, about 1 s (placeholder2 drawn at 1311 → 1280; spell_criticals7 1269 →
  1280). Once settled the cursor is back on the step rule's footing; a left push during that ease starts from an
  unsettled view, which is why the push after the right stick is not judged. **The D-pad does
  nothing in any direction**: it is the finder's (`Poe2AccessibilityCore.FindOnTree`, `ModCommands.OnTreeDirection`).
- MEASURED: "No cumples los requisitos" ends the tooltip of every node not linked yet, small ones and keystones alike, and
  not one reachable ("Atributo … alcanzable"). No node of the tree was seen with a level condition.
- MEASURED (dev command `passivemap`: links from each class start, drawn nodes only, other starts not walked through):

  | Start | Nearest keystones | Nearest notables | Keystones within 20 / 30 / 40 points |
  |---|---|---|---|
  | Ranger, Huntress | 11, 12, 12 | 5, 5, 7 | 13 / 28 / 33 |
  | Marauder, Warrior | 12, 12, 13 | 5, 5, 7 | 18 / 29 / 33 |
  | Duelist, Mercenary | 11, 12, 12 | 5, 5, 7 | 13 / 29 / 33 |
  | Druid | 9, 11, 13 | 5, 5, 7 | 19 / 31 / 33 |
  | Witch, Sorceress | 11, 13, 13 | 5, 5, 7 | 19 / 32 / 33 |
  | Shadow, Monk | 12, 13, 13 | 5, 5, 7 | 17 / 30 / 33 |

- 3910 drawn nodes: rings by thirds of the tree's half-size hold 336 / 1476 / 2098 (`PassiveMap.Zone`, centre of the bounding box).
- Cost is `PassiveMap.Steps`: breadth-first along the links from the allocated ids and the own start. The way is
  `PassiveStick.Route` (cursor pushes, not links: the cursor needs no link to look at a node), recomputed on every hover, so a
  push that lands elsewhere corrects itself. Followed live from the far side of the tree: 31 pushes, the count falling, to
  "Llegaste a Fe cicatrizada"; beyond 30 pushes it says "lejos" and the direction.
- A target chosen on a node must not change that node's tooltip: the narrator says a line replaced in place, which cut off the
  finder's own answer 10 ms after it began (`UiSemantics.FinderLine` keeps the line while the cursor stays).
- Row names may hold keyword markup ("Engarce de [Jewel|joya]"); `ReadPassiveRow` shows the display text.

## Build Planner (patch 0.5, 2026-10-31)

- MEASURED: **node element `+0x70C` byte = 1** while the node is in the active build (the same five elements read with a
  build chosen and with "None": nothing else of them changed). Weapon sets are drawn as ring colours (red set I, green
  set II); the author's note (`additional_text`) is drawn beside the hovered node **outside the UI tree** (absent from a
  full dump; its text lives in a text-layout object and in the parsed JSON, both found by a memory scan, neither on a
  path from the tree).
- MEASURED (2026-11-07, a test build): a node with a note owns **two tooltips** (vector +0x120, 0x18 bytes each): first the
  note, then the node's stats. Reading the first alone said the note and never what the node does (a tester: "it simply just
  tells you that they are in the build"). `TooltipEntries` reads them all: stats first, then the note, so a website build's notes
  are read too. The string at +0x620 ("Fuerza\nStrength\n\n+8 a la fuerza") is no fallback: all nodes had it in a Spanish session
  that had used the tree's search, none in an English one.
- The files: `Documents\My Games\Path of Exile 2\BuildPlanner\*.build`, JSON (GGG developer docs, "Build Planner" v1):
  `passives` = ids (PassiveSkills `Id`) or objects {id, level_interval, weapon_set 0-2, additional_text}; the GGG example
  writes attribute advice as notes ("<m>{<red>{Strength +5 is recommended}}"); Mobalytics exports carry no passive notes.
  The mod reads the file whose passives cover at least half of the marked nodes (`BuildPlanFolder`): step = place in the
  file, weapon set, note without markup, and for an attribute node (`strength17`, `attributes65`…) whose note shows the
  attribute only by colour, that attribute. A website-only build has marks but no file: no notes.
- UI: in the tree, L3 opens "Filters/Build Planner"; D-pad right twice reaches the planner button (`collapse_button`,
  A expands it), D-pad down the list (`BuildPlannerComboBox`, "Active Build"). The gem engraving window has the same
  list (`build_planner_combobox`) and a first category of the build's gems.
