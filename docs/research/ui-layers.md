# UI layers, roots and the UI manager (PoE2 0.5.5e, 2026-10-23)

Validated live with the virtual-pad loop (`tools/computer_use`), dumps in the plugin's `debug/` folder.
Confidence: **MEASURED** = read live and checked against the screen; **INFERRED** = consistent but not
proven on every screen; **OPEN** = unknown.

## UI manager (`M`)

- MEASURED: one manager class, **one instance per state that owns UI** (login/pre-game, in game, game
  menu = EscapeState). Every UI element points at its manager at **`element+0x90`**; in game
  `InGameState+0x310` holds it directly (`G+0x90` gives the same, G = `InGameState+0x318`).
- MEASURED: `M+0x688` = the state's root element (parentless), **`M+0x748` = element under the controller
  cursor** (0 when nothing has focus), `M+0x750` = previous focus (kept hidden after a panel closes;
  `M+0x978` is a copy). Pre-game M sits inside a larger object at +0x240, hence the older numbers
  +0x8C8 / +0x988 / +0x990 / +0xBB8 (that object is `LoginState+0x2B8`).
- MEASURED (Opus run, 2026-10-23): focus followed inventory slots (boots → belt → gloves → flask →
  grid → scroll), the skills panel rows and the game-menu buttons (resume_game, social_panel_button,
  challenges_panel_button, trade_market_button). No per-element "focused" flag exists.
- `UiManagerLocator`: InGameState+0x310, else the first element pointer in the state object → +0x90.

## Pre-game root (`root_control`)

- MEASURED: the root's children are layers in drawing order. Opening a popup **moves it up** to just
  below the controller hint bar: the terms window was child 12, the account window then became child 12
  and the terms window dropped to child 10 (hidden). A full-screen dimming layer sits under a modal.
- MEASURED: the hint bar is the layer whose entries carry a label named `entry_label`
  (`[bar,1,k,1,3]`); its button icon is an image `entry_icon` (`[bar,1,k,1,0,0]`) with no text.
  OPEN: which field identifies the glyph (research in progress).
- MEASURED: flag bit **0x0D = enabled**. The terms window's Continue went `0x8540EF1 → 0x8542EF1` when
  I Agree was ticked; the account window's Create stays without it while the name is empty.
- MEASURED: text fields keep their placeholder at **+0x798** ("Type your character name here...",
  "Type desired account name here..."), the only label some fields have.
- MEASURED: character creation keeps all its UI in one layer; picking a class changes the text of the
  same label elements (class name, quote), so it reads as "texts changed in the owning layer".
- MEASURED: the class-skill preview (video + text, D-pad "Cycle Class Info") is **not** in the UI tree
  (no element covers it, no string matches). OPEN: separate renderer.

## In-game root

- MEASURED: `InGameState+0x318` (GameHelper `GamepadUiRootStructPtr`) points to an element whose 65
  children are the in-game layers; `+0x2F0` (mouse variant) is null in controller mode. Its parent is
  an element named `root` (2 children).
- MEASURED: layers visible on arrival: `map_overlay`, `world_overlay`, `world_ui_layer_parent`,
  `HUD` (24 children, includes `tutorial_popup`), an unnamed 408×63 box (tutorial "L to Move / R to Aim")
  and the chat panel (unnamed, holds the chat filter regex). The full tree exceeds 20 000 nodes.
- INFERRED: GameHelper's `ImportantUiElements` (ChatParent, LeftPanel, RightPanel, LargeMap, MiniMap,
  Atlas, gem-cutting panels, skill-tree nodes) give typed addresses for classifying these layers.

## In-game panels (G = `InGameState+0x318`)

- MEASURED: `G+0x378` panels layer (visible while any panel is open), `G+0x398` inventory panel,
  `G+0x3A0` skills panel, `G+0x558` HUD (hidden while a panel is open), `G+0x680` item-action hint bar
  ("Pick Up | Unstack | Drop | Inspect"), `G+0xB88` map overlay. GameHelper's LeftPanel/RightPanel
  (0x6D0/0x6D8) do not toggle in controller mode. Child indexes move when panels open: never use paths.
- MEASURED: View opens the inventory; LB/RB switch the top tabs; Circle closes. In-game controls do
  **not** carry flag bit 0x1B, so "highest layer with controls" cannot find panels: in game the panel
  that owns input is the layer holding `M+0x748`.
- MEASURED: inventory grid keeps the focus on the grid element; **`grid+0x358` = (col, row) int32**,
  **`grid+0x350` = item element under the cursor** (0 on an empty cell), `grid+0x33C` = cell size
  (80.0). The D-pad snaps to items (cell (1,0) → (11,4) where the scroll stack lies).
- MEASURED: item element **+0x4E0 → item entity** (GameHelper `ItemStruct`; path `Metadata/Items/...` at
  details+0x08). GameHelper `Base`/`Mods`/`Stack` give base name, rarity, stack size (not display mod text).
- MEASURED: grid **+0x6F8 → server inventory object** (InventoryStruct, TotalBoxes at +0x150): main (12,5),
  flasks (5,2), helmet/gloves/boots (2,2), belt (2,1), rings/amulet (1,1). Its address equals a value of
  GameHelper's internal `ServerData.PlayerInventories` (InventoryName → address), which names the slot.
  Every equipment slot is its own grid element.
- MEASURED: panel title at **element+0x2E8** (only titles there: Inventory, Skills, Passives, Stash,
  Merchant, Cosmetics, Character).
- MEASURED: chat layer = **G+0xA60** (GameHelper zeroes ChatParent in controller mode).
- MEASURED: **inspect mode** (R3 on an item) opens a tooltip layer whose texts are ordinary labels (some
  with link-highlight children): name, "Flask: Item Level 1", stats, help text with keyword links
  `[Flask|Flasks]`, "Equipped", legend "Unequip" (X). The focus moves to the keyword link boxes (empty
  children of the text; k-th box = k-th link); A expands a definition into the same layer, B goes back.
- MEASURED: in-game legends use glyph markup with a variant suffix (`<<xbox_button_lt;0>>`), sometimes
  combined (`<<rt;0>>+<<lstick;0>>`), in their own text element next to the label.

## Area name

- MEASURED: **AreaLoadingState+0xEA0 → current WorldArea.dat row** (+0x00 id "G1_1", +0x08 name
  "The Riverbank"; GameHelper `WorldAreaDatOffsets`). GameHelper reads +0xF40, which is null in 0.5.5e,
  so its `CurrentAreaName` stays empty; the plugin tracks the row pointer itself.

## In-game menus and tooltips

- MEASURED: in-game list items carry no interactive flag; the items of one list are visible siblings of
  the same class (same vtable at +0x00): skill context menu "Manage Sockets / Advanced Info / Set Skill
  Bind / Back" (title "Punch" above them). Position and "other items" come from that.
- MEASURED: skills panel rows read "LVL: 1, Punch"; R3 inspects the skill gem (same inspect layer as
  items); A opens the skill's context menu; Manage Sockets moves the focus over `skillgem socket` and
  `new_support_gem_entry` elements (control ids only).
- MEASURED: hover tooltips beside the panels ("Support Gem Slot …") are **not** among their owner's
  children. The owner keeps them in a `std::vector` at **element+0x120** of 0x18-byte entries; the first
  qword of an entry is the tooltip root, whose parent pointer is the owner (`new_support_gem_entry`,
  skill gem rows, 2026-10-23). The tooltip's texts are ordinary labels: a one-label tooltip carries the
  layout's wrapping as `
` ("Support Gem Slot
Socket a Support Gem of any
colour to apply …"), so the
  first line is the title and the rest one paragraph. Rich tooltips (skill gem) are several labels: tags,
  level, cost, damage, description. `UiSemantics.Tooltips`.
- OPEN: the class-skill video popup at character creation (not checked against +0x120).

## In-game panels (tabs, cursor modes)

- MEASURED: LB/RB cycle the panel tabs Inventory → Quests → Cosmetics → Character → Passives → Skills
  (wraps). All panels share one layer under G; the tab's name is the `label_tab_title` text.
- MEASURED: **M+0x760 = element under the free cursor** while M+0x748 (focus) is null: the hovered
  passive-tree node (tracks the left stick; searched M for the hovered node's address). The game builds
  the node's +0x120 tooltip a frame later and **keeps it** after the cursor leaves (vector stays
  non-empty, root keeps its visible flag), so "has a tooltip" does not mean "hovered". Node tooltip:
  title, stat lines, "Unallocated" (the last text starts with a line break).
- MEASURED: passive tree container: ~1372 group children, nodes are 39×39 elements without text; the
  skill-points icon has its own tooltip (the passive-point rules).
- MEASURED: Quests tab = act world map: no focus and no hover; the tooltip of the area under the cursor
  ("The Riverbank / Monster level: 1 / … / The Bloated Miller") is ordinary texts in the panel layer.
  X opens the quest list (empty at the start of Act 1). The map legend is one label with `\r\n` wraps.
- MEASURED: Cosmetics has no focus until the d-pad is pressed; then ordinary buttons ("Weapon Effect").
- MEASURED: Character tab, inspect mode (R3) moves the focus over the stat widgets: the attribute circles
  hold only "STR" while the "7" is a **sibling drawn inside the circle**; a resistance icon has no text
  and its "0%" sibling sits **just above it**; each widget's +0x120 tooltip explains it. In the stat
  list the cursor lands on keyword links only; the link's own +0x120 tooltip is the definition, and its
  row is text → cell → row (label cell + value cell: "Evasion Rating" | "7").
- MEASURED: Skills panel rows: "LVL: 1, Punch, DPS: 6,1"; empty skill rows have no text (their gem
  socket holds a 1-cell `skillgem_inventory_view`).
- MEASURED: Set Skill Bind / skill-bar management: each slot has no text; the button glyph is an image
  of a smaller class whose texture pointer is at **+0x238** (not +0x240): TriggerLeft.dds … TriggerRight.dds
  in bar order. The bound skill's icon (`…/SkillIcons/…`) carries the skill tooltip, built only after
  the first hover ("Dodge Roll/Sprint" yes, Punch on RB not yet → icon name `UnarmedDefaultSkill`).
- MEASURED: inventory: the hovered **item element** (grid+0x350) owns the full item tooltip at +0x120
  (name, "Stack Size: 3/40", description, usage line with glyph markup `<<…>>` in the tagged text,
  hidden "Account-bound"/"Amount in inventory" lines). Built when the cursor lands, so read it fresh.
- MEASURED: in-game options: control → cell → label/control pair (2 same-class siblings, one captioned)
  → row (rows of one section are same-class siblings: "Device / Channel Count / Audio Mix") → section
  ("Hardware / Volume / Customise"). Rows own +0x120 tooltips with the setting's description.
- MEASURED: when the game window loses the foreground, M+0x748 is cleared (focus comes back on input).

## Gameplay layers (children of G)

- MEASURED (G field → layer, 2026-10-23): +0x558 `HUD` (child 12: quest tracker "Reaching Clearfell /
  Search for the Miller …", flask charges, ground-item tooltip "Withered Wand / Wand / Grants Skill …",
  "Game Paused"), +0xA60 chat (child 13), +0xDD8 objective / tutorial box (child 14: "Talk to the
  wounded man", "Press <<xbox_button_a>> to talk to non-player characters."), +0xB90
  `world_ui_layer_parent` (child 11: NPC and item labels; an NPC label's control id is its metadata
  path), +0x798 NPC dialogue box (child 57 while open).
- MEASURED: the HUD's texts reappear whenever a panel closes (the quest tracker hides under panels).
- MEASURED: NPC dialogue: the focus is the dialogue box (its row text is the line plus "Continue"); the
  layer holds speaker name, line, Continue [A], Skip Dialogue [Y]. Voiced lines auto-advance; each new
  line replaces the speaker and line texts (the focus keeps its address, only its text changes).
- MEASURED: chat message list = the chat layer's descendant with most children (~200 lines); each line
  is one text "Name: message". Channels: `#` global, `$` trade (other players); NPC shouts
  ("Wounded Man: Reach... Clearfell..."), the character's own remarks ("Yarkidius: The dead rise
  again?…") and system notices ("AFK mode is now ON…") have no prefix.
- MEASURED: pickup = A on the ground item; equip = X on the item in the inventory (tutorial prompts).

## Controller input as the game sees it

- MEASURED (2026-10-23, virtual DualShock 4 through Steam Input, game reports "Xbox Controller #1"): the
  UI manager holds the pad state: **M+0x768** `uint32 buttons; float lx, ly, rx, ry, lt, rt` (0x1C bytes;
  stick up = +y, right = +x, triggers 0..1), **M+0x784** the same layout (previous frame). Copies also live
  in other objects (0x...1110, 0x...E460) not yet tied to a stable owner.
- MEASURED button bits: d-pad up 0x1, down 0x2, left 0x4, right 0x8, Options/Start 0x10, Create/View 0x20,
  L3 0x40, R3 0x80, LB 0x100, RB 0x200, A/Cross 0x400, B/Circle 0x800, X/Square 0x1000, Y/Triangle 0x2000,
  LT 0x4000, RT 0x8000. The touchpad click never reaches the game (0).
- MEASURED: XInput polled from another process sees no pad at all while Steam Input is on (Steam's virtual
  pad exists only inside the game), so reading the game's own state is the controller-agnostic path.
- MEASURED in gameplay: R3 opens a HUD inspect mode (focus on the XP bar: "Level 1 (0%), Current Exp: 0 /
  Next Level: 525"); L3 did nothing visible; the right stick aims ("R to Aim"). Options → Input lists
  device, glyph art, deadzone, item snapping, navigation speed, vibrations — no button remapping.

## Menus and lists (pre-game and game menu)

- MEASURED: game menu (EscapeState) has its own manager; its buttons are siblings (10), so "x of n" comes
  from counting sibling controls. Its hint bar (`entry_label`) lives inside the same layer as the buttons.
- MEASURED: popups built inside the focused layer (exit confirmation) appear as texts of that layer right
  after the focus moves to their button.
- MEASURED: character selection list keeps the focus on the list widget; **list+0x388 = selected index**
  among the entry holder's children (0 Create Character, 1 the character). Entries hold `name_label`,
  `desc_label` ("Level 1 Witch"), `location_label` ("Act 1: The Riverbank"), hidden `league_label`.
- MEASURED: options tabs are buttons with the FourTabButton click sound; **tab+0x391 (byte) = selected**
  (moved UI → Sound on R1). In game, panel tabs show their name in `label_tab_title`.
- MEASURED: glyph variant `;1` = second controller ("Press <<xbox_button_a;1>> to join", "P2"). With the
  real DualSense connected, a virtual pad reconnected later is player 2: it navigates but A on the
  character list did not start the game (Enter did).

## Account flow seen (new Steam account)

Log In → Terms of Use window (I Agree on R3, Continue on A, Back on B) → Account window (Create
Account: name field on Y + Create on LB; Link Account on RB) → character creation (class on the left
stick, class info on the D-pad, Select on A) → name field + league combo + Continue (Enter submits) →
Loading → Waiting → AreaLoading → InGameState (beach, Act 1).

## Vendor, stash and waypoint map (2026-10-23, 0.5.5e, Clearfell Encampment)

- MEASURED: vendor window `NPCBuyWindow` (Renly, "Buy or Sell items" on X): stock in a `TabContainer` with
  `buttons_container` (tabs "-1-" "-2-" "-3-", FourTabButton) and `pages_container`; each page has its own
  12×11 grid element. The selected tab is drawn after `fade_bar` and has **tab+0x391 = 1** like options tabs.
- MEASURED: stash panel (title "Stash" at +0x2E8): `top_level_tabs` holds a vertical tab list ("1".."4",
  plus "Buy Stash" with the same click sound but +0x393 set instead), a title label with the tab name, and
  `pages_container` with a 12×12 grid. +0x391 marks the selected tab. GameHelper's PlayerInventories map
  named stash tab 1's inventory `BereksTamingExchangeOutput`: unknown inventories must not be read as slots.
- MEASURED: an item tooltip's price is a child named `cost`: an image with key
  `Art/2DItems/Currency/Ruthless/CoinPileTier1.dds` then a label "128". The inventory's gold counter is a
  label "0" whose child is the same coin image. Sell value appears in the same `cost` block on own items.
- MEASURED: **label+0x6D0 = packed ARGB text colour**: unaffordable price FFE40000, "Buy Item" FFBDBD9A,
  stat lines FFA0A0A0 (four labels of one tooltip). Unmet requirements should use the same red (not seen yet).
- MEASURED: the waypoint "Teleport" map keeps the quest list panel visible-flagged but off screen
  (x = 2596 on a 2560-wide root): snapshots drop texts whose absolute rect misses the root rect. The D-pad
  moves the map cursor between areas; the hint bar's A action ("Instance Manager" on the town) appears only
  where A does something. Clearfell (no waypoint taken) showed no A action.
