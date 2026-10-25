# HUD skill bar: button and skill name per slot (PoE2 0.5.5e, 2026-10-23)

Read live, read-only (`tools/computer_use/peek.py` and scratch scripts built on its `Process` class; no input
sent), Spanish client, controller mode, crossbow character, PID 30604, InGameState `0x46678E53F10`.
UI dump `debug/ui-tree-InGameState-20261023-192234.txt`, entity dump `debug/entities-20261023-193250.txt`.
Confidence: **MEASURED** = read live and checked against game output; **INFERRED** = consistent, not proven
on every case; **OPEN** = not known.

## Elements (G = `InGameState+0x318`)

- MEASURED: **`G+0x610` = `skill_bar`** (control name at +0x98; also dump line "state+0x318->+0x610"). Its
  child 0 is the background art, **children 1..12 are the slot frames** in screen order, then XP bar, front
  art, poise, weapon-set labels ("Set de habilidades principal/secundario").
- MEASURED: each slot frame has **2 children of one class (same vtable)**: child 0 = primary skill set,
  child 1 = secondary set (used while the swap action is held). Call them *slot elements*.
- MEASURED (local co-op, 2026-10-25): each player has a `skill_bar` (`G+0x610` player 1, `G+0x7B0` player 2) whose
  child 0 holds the twelve frames in two rows of six; each frame is a background, then the two slot elements
  ([local co-op](local-coop.md)).
- MEASURED: **slot element `+0x370` (int32) = binding index**: 0..11 primary, 12..23 = same button, secondary
  set (child 1 index = child 0 index + 12, all 12 slots). Other fields of the slot element: `+0x2E8` icon
  child (= child 2), `+0x2F0` number backing (child 0), `+0x300` 85x85 child 1, `+0x120` tooltip vector.
- MEASURED: **`G+0x788` = `bind_management_display`** (the "Set Skill Bind" screen, kept hidden in game). It
  holds two bars ("Set de habilidades principal" / "secundario", 13 children: title + 12 slots). Bar slot k:
  `[k,0,0]` is an element **of the slot-element class with the same `+0x370` index**, `[k,2,0,0]` is the
  button glyph image (texture at **+0x238**, `…/Common/Gamepad/XboxOne/<File>.dds`). The glyphs are present
  while the screen is hidden; the bind screen's own skill icons are empty then.

## Slot → button

- MEASURED (bind screen, both bars identical): index → glyph
  `0 RB, 1 RT, 2 LB, 3 LT, 4 X (Button1), 5 Y (Button2), 6 B (Button3), 7 A (Button4), 8 D-left, 9 D-up,
  10 D-right, 11 D-down`; HUD/bind order left to right = LT, LB, D-up, D-down, D-left, D-right, A, X, Y, B,
  RB, RT (HUD indexes 3,2,9,11,8,10,7,4,5,6,0,1). The pattern (shoulders, face clockwise from X, D-pad
  clockwise from left) suggests a fixed enumeration, but read it from `G+0x788` (glyph family follows the pad).
- Cross-checks with the game's own text: tutorial "Presiona <<xbox_button_rb;0>> para cargar Saetas de
  fragmentación" (dump 193459 line 1587) ↔ index 0 = RB holds Load Fragmentation Rounds; "Presiona B para
  evitar el ataque" ↔ index 6 = B holds Dodge Roll (and the bind screen earlier read "B, Dodge Roll/Sprint");
  "Presiona cruceta izquierda para usar un frasco de vida" ↔ index 8 = D-left holds the life flask; "Press A
  to talk" ↔ index 7 = A holds Interact (plugin log, 2026-10-23).

## Slot → localized name

Two kinds of binding:

1. **Skill** (MEASURED, 3 slots). The icon child (slot element child 2, a SkillIcon; texture +0x240 =
   `Art/2DArt/SkillIcons/StandardBolts.dds`, `BurstShotPhysical.dds`) holds **`+0x2D8` → ActiveSkillDetails**,
   the very object listed in the player's `Actor+0xB08` vector (entries 26, 30, 32; `+0x2E0` = same − 0x10).
   Chain, each foreign key a (row ptr, file ptr) pair whose file object has its table name as a std::wstring
   at +8 (use it to validate every hop):
   - `sk+0x58` → GrantedEffectsPerLevel row (`sk+0x60` file = "Data/Balance/GrantedEffectsPerLevel.dat").
     `sk+0x48/+0x50` is an **ActionTypes** FK ("CrossbowAmmo"), `sk+0x40` = GameHelper's
     UnknownIdAndEquipmentInfo (0x80401040). **GameHelper's `ActiveSkillDetails.GrantedEffectsPerLevelDatRow`
     at +0x48 is stale in 0.5.5e** (it reads the ActionTypes row).
   - `gepl+0x0` → GrantedEffects row (`gepl+0x8` file = "Data/Balance/GrantedEffects.dat"); `ge+0x0` → Id
     ("UnloadAmmoPlayer").
   - `ge+0x4F` → ActiveSkills row (unaligned; `ge+0x57` file = "Data/Balance/ActiveSkills.dat" — GameHelper's
     `ActiveSkillDatPtr` at +0x57 is that file pointer).
   - ActiveSkills row: `+0x0` Id ("unload_ammo"), **`+0x8` DisplayedName = localized name**, `+0x10`
     description, `+0x18` ActionTypes FK, `+0x28` icon path (equals the HUD icon's texture key: checked on all
     three). Strings are null-terminated UTF-16.
2. **Action** (MEASURED): dodge, flasks, interact, map, swap, contexts, quest cycle have no skill pointer
   (no dat reference within 3 hops); the slot element itself carries the icon at +0x240
   (`…/SkillIcons/GamepadActions/DodgeRoll.dds`; swap has none) and a **+0x120 tooltip whose first text is
   the localized title**. INFERRED: action tooltips are built without hover (all 9, including secondary-set
   ones, had text and the roots were rebuilt with text during the session), whereas skill tooltips held only
   their background (`SkillHoverBgGreen`) until hovered — so skills must use the dat chain.
   Dodge Roll's ActiveSkills DisplayedName is empty: the action title is the only name.
3. **Empty**: no tooltip text, no +0x240 icon, no `+0x2D8` skill → "vacío".

## Live readout (prototype, all 24 bindings)

LT Activar set de habilidades secundario · LB vacío · D-up vacío · D-down Alternar mapa · D-left Usar frasco de
vida · D-right Usar frasco de maná · A Interactuar · X **Saeta básica** (skill) · Y vacío · B Esquivar
rodando/Esprintar · RB **Cargar saetas de fragmentación** (skill) · RT **Saetas de fragmentación** (skill).
Secondary set: LT swap, D-up/D-down/D-left Contexto 1/2/3, D-right Rotar misión, rest empty.

- MEASURED: the HUD shows the skill the button fires *now*. At 17:41 the bind screen read "RT, Power Shot"
  (icon `PowerShot.dds` = `player_melee_crossbow`, "Disparo con ballesta"); with Fragmentation Rounds loaded RT
  now shows "Saetas de fragmentación". The reader reflects the current state; that is the right answer.
- OPEN: confirm on screen that the X slot's tooltip title reads "Saeta básica" (hover it in HUD inspect mode
  R3 or on the bind screen); confirm DualSense glyph family in the bind bar; confirm the secondary-set
  phrasing ("LT + X") against the game.

## Reader sketch

1. G → `skill_bar` = G+0x610 and `bind_management_display` = G+0x788; check both control names (+0x98).
2. Slot class = vtable of skill_bar child 1's child 0 (runtime value, never hard-coded). Button table: walk
   the bind display (bounded); for each element of that class, index = +0x370, glyph = first texture under
   `/Gamepad/` in its grandparent's subtree (`UiElementReader.ReadTextureKey`, +0x238 alt) →
   `ControllerGlyphs.FromTextureKey`. Cache per area/state; rebuild if a lookup misses.
3. For each skill_bar child 1..12 → child 0 (and child 1 on request), index = +0x370:
   skill name via the dat chain above with the three table-name checks; else tooltip title
   (`UiSemantics.TooltipLines(...)[0]`); else icon file name (`IconName`); else `Announcements.Empty`.
   Speak in screen order: "LT: …. LB: vacío. …". Index ≥ 12: prefix with the swap action's button.
4. Same chain upgrades `UiSemantics.Slots.DescribeButtonSlot` on the bind screen: its skill icons can read
   `+0x2D8` instead of waiting for the hover tooltip ("Unarmed Default Skill" fallback).
