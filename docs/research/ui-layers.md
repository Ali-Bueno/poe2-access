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
  and its "0%" sibling sits **just above it**; each widget's +0x120 tooltip explains it. At 1280x1024 in
  English (2026-11-07) the game **hides the captions** ("STR", "Life", the "Resistances" heading: flag 0x5026F1,
  text still there) and the "0%" no longer falls in the icon's rect: a tester's report reproduced as "1 Strength = +2 Life"
  with no number. So nothing is matched by place any more: `SheetStats` tells a widget by the English keys of its tooltip's
  markup (`[Cold]`, `[UncappedResist|…]`, `[Strength|…]` + `[SupportGem|…]`, the same in every language, ClientStrings
  `CharacterPanelFireResistDesc`…), and `PlayerStats` reads the number from the Stats component (`strength` 563,
  `fire_damage_resistance_%` 299…, keys in `catalog/game-ids.txt`; dev command `playerstats`). A stat missing from Stats
  is 0 (chaos resistance at 0% is absent). A number left bare by a hidden caption is named by the tooltip's title. In the stat
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
- MEASURED (2026-10-25, 0.5.5e): chat channel list = the chat layer's child 2, unnamed, parts `button`, `scrollbar`,
  `textField` like the gateway combo, button click sounds. Its combo fields sit 0xC8 past the options combos'
  (selected int64 +0x5E0, highlight +0x5F0, open byte +0x600, entries +0x750, entry qword 0 → std::wstring):
  Local, Global, Grupo, Susurros, Comercio, Hermandad. The field (child 3) holds the channel prefix: "" Local,
  `#` Global, `%` party, `@` whisper, `&` guild. Filter check boxes (X in typing mode; +0x60A checked) show or
  hide a channel; Global's tooltip says "Conectado al canal 1 Español del chat Global" and Y on it writes
  "/global 1" into the field. In typing mode A opens the system keyboard (Client.txt `OpenVirtualKeyboard`), Y
  sends from the panel. The panel's focus labels came from a hidden "Modo inspección" text in another layer
  until label searches stopped at the layer container.
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

## Character creation (0.5.5f, 2026-11-06)

- MEASURED: the screen opens with no class highlighted: A does nothing until the left stick has moved.
- MEASURED: the name step has two states. `name_textbox` focused takes keys; `name_frame` (after B or the stick,
  hint "Entrada [A]"), the league and Continue drop them with no sound or text. X on the frame swaps the variant
  and goes back to `name_textbox`; in `name_textbox` X, the stick and A do nothing.
- MEASURED: the field keeps letters (accents included) and `_`; digits, space, `-`, `'` and `.` are dropped in silence.
  26 letters typed left 23.
- MEASURED: Continue is grey under 3 characters ("", "a", "ab", "aé"; lit on "abc", "___", "aéé", "yarkey"). Grey, the
  stick skips it, Enter does nothing, B lands on `name_frame`. Lit, B lands on Continue. Its flags do not change
  (`0x8542EF1` in both): **button +0x3FA is 1 while grey** (also +0x177 0 and a pointer at +0x128 differ). Found by
  comparing the button at "ab" and "abc", held on a second, rebuilt button.
- MEASURED: `Core::OpenVirtualKeyboard called with mode '0', default text '' and title 'Nombre'` (Client.txt) when the
  field opens. Nothing showed on this desktop. OPEN: whether Big Picture or a Steam Deck opens Steam's keyboard there.
- MEASURED: a name in use goes Loading and back to the screen with the `message` label ("Ya existe un personaje con
  este nombre."), which stays after the name changes. The league combo is disabled from then on (`0x8554EF1`, bit 0x0D off).
- MEASURED: leaving and entering the screen rebuilds its controls under the same root, so highlight markers kept by root
  went stale: `FocusTracker` forgets them on a state change.

## Other pre-game findings (0.5.5f, 2026-11-06)

- MEASURED: the login's `motd_frame` banner is a picture; its only text is the tooltip "Se abre en un navegador web".
- MEASURED: LB at login (`patch_notes_button`) opens the patch notes in the browser as a child of the game: Steam keeps
  the app "Running" and refuses a new launch (AppError_16) until that browser closes.
- MEASURED: the selection's filter opens with L3, a glyph beside it and not in the hint bar.
- MEASURED: the notice corner's `> ` lines hold news as well as connection progress: "> No hay ninguna liga disponible
  desde la cual migrar el progreso." (hold Y), "> Se ha producido una desconexión inesperada.".
- MEASURED: a short stick push moves the terms up to 8 paragraphs over 2-3 polls of 150 ms.

## Area level on a new character (0.5.5f, 2026-11-06)

- MEASURED: live on the user's new character in the Riverbank: `+0xC4` holds `01 01 00 00`, the level byte and a flag beside it,
  with the empty vectors at `+0xD0`, `+0xE8`, a full one at `+0x100` and the hash at `+0x11C`. Read as 32 bits it was 257, over
  the limit of 100, so the search refused the level and kept the stale offsets: "Area level 0", instance "47F" (the top of a
  pointer) and the compatibility warning. `AreaLayoutSearch.IsHeader` now reads one byte, as `Compose` always did.
- OPEN: what the flag means.

## The caravan's desert map ("Viajar", 0.5.5f, MEASURED live 2026-11-12, yarkey in G2_town)

- Opened at the "Mapa del desierto" beside Sekhema Asala. The act's canvas holds 39 pin buttons; the D-pad moves between them.
- A pin can draw a button of its own: `canvas > pins > pin [10] > container > button` (72 x 72 under the pin; the pin keeps its
  address at pin+0x300). The button has no text, art, tooltip or pin key (+0x2D0 is 0; +0x2D8 and +0x2F8 are its click sounds):
  it read "Vacío, botón, 21 de 39" (21 times in the user's logs). It is named by the pin above it, within two parents:
  "Keth, botón, 21 de 39" (`UiSemantics.PinNameAbove`, the pin's MapPins row through `DatKey.Row`).
- The hovered pin of this map is a stop of the caravan: MapPins Ids `G2_town_marker_keth`, `…_gates`, `…_badlands`, with a
  name, no words, and the town's own area (WorldAreas `IsTown`). So "Pueblo" followed every destination (46 times in a
  session). A pin of a town's area is the town only when it carries the area's name (`GameLayout.WorldAreaRow.NameOffset`,
  dat column `WorldAreas.Name`); a stop has no kind. Heard after: "Páramo de los mastodontes. Nivel de monstruos: 22. …" and no more.
- Not known: what the pin's own button does that A on the pin does not ("Seleccionar, A"); whether the other four pins
  that have one read the same (only pin 10's was reached with the D-pad).

## HUD during an area load (0.5.5f, 2026-11-06)

- MEASURED: on an area change or a respawn, `AreaLoadingState` can still show the in-game layers: a `TopLayerChanged` with the
  whole HUD and the chat (1875 characters on a death). 7 such lines in the logs, 3 of them on 2026-11-06. The state's own lines
  are `loading_screen_tip_label` and `loading_screen_area_name`.

## Account flow seen (new Steam account)

Log In → Terms of Use window (I Agree on R3, Continue on A, Back on B) → Account window (Create
Account: name field on Y + Create on LB; Link Account on RB) → character creation (class on the left
stick, class info on the D-pad, Select on A) → name field + league combo + Continue (Enter submits) →
Loading → Waiting → AreaLoading → InGameState (beach, Act 1).

## A first start of the game (0.5.5g, 2026-11-14)

Asked by the owner after a friend's first start: he could not accept the terms of use with the controller (he used the
mouse) and could not link his Steam account (he used OCR). Replayed with `poe2_production_Config.ini` moved aside, the
owner's Steam account, the virtual Xbox pad plugged in before the game:

- MEASURED: the game starts in English whatever language Steam has, with the login screen drawn for the keyboard and
  mouse: no hint bar, "Log In. Sao Paulo (BR) (Auto-selected). PoE2 0.5.5g". Nothing on screen names the controller.
  The mod now says, 2.5 s after the screen and once a session, "The game is waiting for the keyboard and mouse. Press a
  button on the controller to play with it." (`ControllerPrompt`; heard live).
- MEASURED: A on the controller switches the input ("Input method set to Controller") and logs in with the same press.
- MEASURED: **an account that agreed before goes straight to character selection**: no terms window, no account
  window. Both are the server's answer to a Steam account with no Path of Exile account, not a first start's; no file
  of the settings folder brings them back.
- MEASURED: "Terms of Use, RB" on the login screen opens the same text with "Back, B" alone: no "I Agree", no Continue.
- MEASURED: the language is changed blind from the login screen: D-pad up from Options to "Language, English, combo
  box", A, "Choosing: English, 1 of 10", the D-pad through the ten names (each said in its own script), A, "Debes
  reiniciar Path of Exile… Ok, button". The game restarts in that language and the mod follows it.
- MEASURED: character creation reads as before on the fresh settings ("Choose your class: move the left stick, then
  press A"); the fresh file differs from a played one in graphics, volume, `disable_tutorials=false` and the tutorial
  flags, nothing the mod reads.
- NOT REPLAYED: the terms window with "I Agree" (R3) and Continue (A), and the account window (Create Account: the
  name field on Y, Create on LB; Link Account on RB, its e-mail and password fields). Last seen on 2026-10-23, before the
  long text reader, the hint rules and the creation guide existed. What the code would do today, unproven: Continue
  turning usable is news (`ScreenDiffer`, a control no longer off), the password field is said by its length alone
  (`UiCatalog.IsPassword`, `password_textbox` of `createsteamaccountwindow.ui`). It needs a Steam account that never
  opened the game, logged in to Steam on the test PC, or the friend's `logs/accessibility.log` and `.old` of that day.

## A popup list with no focus: the options' popup notifications (0.5.5f, 2026-11-08)

Options > Interfaz > Chat > "Notificaciones emergentes, editar ajustes" opens "Ventanas emergentes de notificaciones": 31 rows of a
label and a check box. The stick moves a highlight ("L Navegar", "R Navegación rápida" jumps to the end) while the UI
manager's focus (+0x748) and hover (+0x760) stay null and no row element changes. Raw bytes of the popup subtree (0xC00 per
element, `snap.py`-style scratch diff) changed in one place per step: an int at +0x398 of the popup's panel (two levels above
the rows' holder), 0 → 1 → 2 down, 4 → 5 → 6 → 5 down, down, up (`GameLayout.HighlightList`). `NoFocusList` watches the lists
under the last focus's layer once the focus is gone and takes the one whose index moves between rows; its row's check box is
then read as a focus: "Se ha invitado a un compañero al grupo, marcado, casilla de verificación, 8 de 31", A reads "desmarcado".

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

## Items in a grid: search highlight, unmet requirements (0.5.5f, 2026-11-10)

Measured on yarkey's stash and inventory in G2_town (UI dumps `ui-tree-InGameState-20261110-07*.txt`, read-only memory
snapshots of the item elements under three searches).

- **A merchant's search** (Shambrin, 2026-11-10) works as the stash's: "4 resaltados: Sandalias de red, columna 1, fila 3. …".
  A tab changed with a search typed keeps the search: the watch drops the grid no longer drawn and takes the one drawn in
  its place (it said "Búsqueda borrada" and nothing more before). A merchant's page changed from the inventory's side is
  said by its tab ("pestaña 2"; Risu's are named: "Armaduras").
- **Search highlight.** A stash's field "Resaltar objetos" (L3 "Editar búsqueda", hold L3 "Borrar búsqueda") lights the
  items that match and dims the rest; the player's own inventory beside it is not touched. Each item element (the grid's
  children, entity at `+0x4E0`) holds the state in the byte at **`+0x624`**: 0 with no search, 1 dimmed, 2 lit; the byte at
  `+0x627` is 1 only while dimmed, and its drawn alpha (`+0x16F`) falls from FF to 8C. Validation: all 21 items of the tab
  read 0 with the field empty, 1 with "cetro" (no match), and with "guante" the three gloves read 2 and the other 18 read 1,
  as the screen showed. `InventoryReader.HighlightOf` trusts only the pairs (2, 0) and (1, 1); anything else reads as no
  search. The offset rides on the inventory controls' measured shift (`ControlGroup.Inventory`), like the entity pointer.
  An item's cell is its `RelativePosition` over the grid's cell size (`+0x33C`, 80 units).
- **An item the character cannot use.** The game paints its cell red, but nothing in the item element tells (four red
  items against nineteen others: no byte in 0x1000 separates them). The tooltip does: the requirement line colours only
  the unmet value inline, `Requiere: <valuedefault>{Nivel <valuedefault>{19}, <augmented>{10} Fue, <unmet>{21} Int}`
  (`+0x360`, the text with markup). `UnmetRequirements.Of` names each part holding an `<unmet>` tag ("21 Int"). An
  unidentified item is red too and says so on a line of its own ("Sin identificar").
- **Equipped line.** `Equipado: [WeaponSets|Set de armas]<<weapon_set_2>>`: the picture's name repeats the words before
  it, so the picture adds only its numeral (`InlineIcons.WithoutEcho`).
- **A grid on arrival and on leaving.** A grid's hovered item (`+0x350`) is filled up to 170 ms after the cursor arrives,
  and emptied a moment before the focus moves to another grid: an empty cell is said on its second reading only. While
  a currency is being applied (X on an orb, then the right stick's quick navigation) an equipment slot's grid names no
  hovered item at all: the item lying over the cursor's cell is then read from the grid's children
  (`InventoryReader.ItemAt`). A on the slot applied nothing in two tries with an Orb of Augmentation on a one-modifier
  magic spear, and the legend offered no apply button there: how the pad applies a currency to a worn item is not known.

## Quest tracker rows (0.5.5f, 2026-11-10)

Each `quest_display` of the HUD holds a `title_layout` with one `title_label` (the quest's name) and a
`quest_info_layout` of `quest_info_entry` rows, each a `bullet_point` and a `quest_info` (an objective). Only objectives
name what to look for: the title "El pozo de las almas" made every town's well the quest's target (`QuestTexts` leaves
`title_label` texts out).

## Build Planner gems (0.5.5f, 2026-11-10)

Measured on yarkey (level 23 Mercenary, "Campaign Progression - [0.5.5] Twister G" active) in G2_town: screenshots of the
tree's planner, the Skills panel and the inventory, a UI dump with the Skills panel open, read-only memory scans.

- **Where the game shows the build's gems.** Nowhere on the Skills panel: its rows carry no build mark (screenshot and
  dump). The tree's planner is one list ("Active Build"). The game shows the build's gems only in the engraving window
  (a first category of the build's gems) and the build's equipment as a small planner icon on each slot the file names,
  whose note is read on a held R3 ("Mostrar notas del Planificador de build": the file's `additional_text`, read by the
  screen narration as it appears). So the mod sets the file's gems against the character's own.
- **The file.** `skills` = `[{id, level_interval [from, to], support_skills: [{id, level_interval}]}]`, ids are
  BaseItemTypes ids under `Metadata/Items/Gem/` **or** `Metadata/Items/Gems/` (595 and 641 rows): a gem is told by its
  id's last part. `inventory_slots` = `{inventory_id, additional_text, level_interval, slot_x, slot_y}`.
- **The active build without the tree.** The game's settings (`Documents\My Games\Path of Exile 2\poe2_production_Config.ini`)
  hold `active_builds=[{"character":"yarkey","path":"file:C:/…/x.build"}]`. `BuildPlanFolder.Active` reads the file
  it names for the character; what the tree's marks matched after the settings were last written wins (the game may write
  them late), and marks that read "none" then mean none. A build followed on the website has no `file:` path: no gems read.
- **Gem names.** A gem not owned has no item to read its name from: `GemNames` takes it from the game files' `gems.json`
  (`GemIndex.ByBaseItem`, by the last part of the id, case ignored), made again at every start, since 2026-11-10; the
  hand-made `gem-names.json` is gone. Over the 101 gems yarkey's seven build files name, both gave the same name in all ten
  languages. A gem newer than the catalog, or one the files mark unused (`[DNT-UNUSED] …`), is named by its id's words; a
  name the game fills in play ("Espectro: {0}") is said without the hole. A Skills row without a gem is matched by the
  names of the skills the gem grants (`GemNames.RowNames`), not only by the gem's.
- **The character's gems.** Every server inventory of the player (`ServerData.PlayerInventories`, read on demand with
  the host's `InventoryStruct`/`InventoryItemStruct`: `ServerInventory`) is looked through for gem items: those of the
  main inventory are "in your inventory", those elsewhere are skills. Seen with the dev command `inventories`: inventory
  47 holds the skill gems (x = the skill's slot: Attrition 1, Whirling Slash 3, Twister 5, Herald of Thunder 8; the
  panel's row is x + 5), inventory 75 the weapon's own skills (Spear Stab, Spear Throw, Punch); the host's enum names for
  those ids are PoE1's and mean nothing here.
- **Socketed supports: the skill gem item's `Sockets` component** (the host's component lookup names it). MEASURED: a
  scan of the game's memory for the entity of the Martial Tempo socketed in Whirling Slash found it inside one component
  of the skill's own item, at **+0x30**; the component is {vtable, owner entity +0x08, …, entity pointers from +0x30,
  a std::vector at +0x60 two bytes long (the panel shows two sockets), …, 0x90 bytes}. Twister's and Herald of Thunder's,
  with no support, hold zeros from +0x30 to +0x60. Only one support per skill was there to see, so the second socket's
  place (+0x38 by the layout) is not measured: every pointer from +0x30 up to the vector is read and believed only when
  it is an item entity whose id is a support gem's (`GameLayout.GemSockets`). The support socket elements
  (`new_support_gem_entry`) hold no entity: a socketed gem's tooltip names it, and that name tells which socketed gem it is.
- **Skills panel elements** (dump): each row = `skillgem_socket_frame` → `skillgem_inventory_view` (an inventory view
  whose child item element holds the skill gem at `+0x4E0`), `skill_title_layout` (NIVEL:, number, name), `dps_frame`,
  `support_gem_container` → one `new_support_gem_entry` per socket. An empty row has no item element.
- **What is said** (`UiSemantics.BuildGems`, pure `BuildGems`): on entering the panel "Build: 2 de 9 habilidades, 0 de 20
  asistencias. Siguiente habilidad: Colmillos de escarcha. Siguiente asistencia: Furia II para Corte giratorio"; on a
  skill's row "en la build. Asistencias de la build: Furia II, falta; Ataques rápidos II, tienes Ataques rápidos I; Empuje,
  falta" or "fuera de la build"; on an empty row the build's skills still to get ("…; Frenesí de combate, desde el nivel
  26"); on a socketed support "en la build" / "la build pide Ataques rápidos II" / "fuera de la build"; on an empty socket
  "La build pone aquí: …"; on a cut gem anywhere (inventory, stash, vendor) "en la build, para Torbellino" or "fuera de la
  build"; on an engraving choice the same, told by its name. A gem the build uses only below the character's level is
  left out; one it takes later says from which level.
- **`InventoryReader.IsGem` took only `/Gems/`:** Twister, Attrition and 593 other gems under `/Gem/` were no gems to the
  move news and the loot filter. Both now ask `BuildGems.IsGemId`.
- **A skill a weapon grants** (Parry from a buckler, Spear Stab, Spear Throw) has a row but no gem view: its
  `skillgem_socket_frame` holds a bare element, and Parry has no item in any server inventory (dev command `inventories`
  with a Ridged Buckler equipped). Such a row is told by the name in its title (`skill_title_layout`), and the names of
  all the rows are kept so that Parry counts as owned ("Build: 3 de 9 habilidades" once the buckler was on). An empty
  row's title keeps a bare "NIVEL:": the row is empty when nothing names it.
- **Stash tabs are server inventories too** once seen (145 and 146 here): only the main inventory and the skill
  inventories are looked at (`GameLayout.SkillGemInventories`: 47 and 75 measured, plus whatever a gem view of the panel
  shows through `InventoryReader.InventoryOf`).
- Not read: which gems are Spirit gems before they are owned (BaseItemTypes only tells skill from support; an owned one's
  tooltip says "Reserva: 30 Espíritu").

## Build Planner gear (0.5.5f, 2026-11-10)

Code and unit tests only (the game was in use): what a live session must check is listed last.

- **The file.** `inventory_slots` holds one entry per piece: `{inventory_id, slot_x, slot_y, level_interval [from, to],
  additional_text}` where the text's first line is the base in the author's English and the next are the wanted
  modifiers, numbered, most important first (0 to 8 in yarkey's ten files); or `{inventory_id, slot_x, slot_y,
  unique_name}` with no levels, the name sometimes followed by a note in brackets ("Darkness Enthroned (body armour)").
  `inventory_id` is the game's inventory name: `Weapon1/2`, `Offhand1/2`, `Helm1`, `BodyArmour1`, `Gloves1`, `Boots1`,
  `Amulet1`, `Belt1`, `Ring1/2`, `Flask1` (`slot_x` 0 life, 1 mana), `Charm1` (`slot_x` 0..2). Pure `BuildGear.Parse`,
  `EntryFor` (the latest piece that applies at the level, else the first to come).
- **Which slot a control is.** An equipment slot is a one-cell inventory grid (`role=GridCell`, play log 2026-11-10:
  `text="Casco, Halo de cólera, raro" context="Inventario"`); `InventoryReader.InventoryOf` names its server inventory,
  and the host's `InventoryName` uses the file's very ids (`Helm1`…), so the slot is `InventoryName.ToString()` and, for
  `Flask1`, the cell's column. A stash tab the host names after a slot is told apart: the slot's own cell holds the
  entity its server inventory holds. `Charm1` is not in the host's enum: charms are not compared.
- **Where an item goes.** By its id's folder, read live (`InventoryReader.ItemBaseId`; ids seen in the play logs
  2026-11-08..10): `/Armours/Helmets/`, `/BodyArmours/`, `/Gloves/`, `/Boots/`, `/Armours/Shields/` (bucklers too:
  `FourShieldDex`) → both off-hands, `/Weapons/` → both main hands, `/Rings/` → both rings, `/Amulets/`, `/Belts/`,
  `/Flasks/FourFlaskLife…` and `…FourFlaskMana…`. Charms (`/Flasks/FourCharm…`), quivers and foci (folders not seen) go
  nowhere.
- **What is compared, and from what.** The worn item per slot and its base id come from the server inventories without
  any tooltip (`ServerInventory`); its base name from the item (`ItemBaseName`), its rarity from `ItemKind`. Its modifier
  lines exist only in its tooltip, which the game builds when the cursor rests on it: the lines of each worn item are
  kept from the last time its slot was focused (dropped when the item leaves the slot). So the summary counts bases for
  every slot and modifiers only on the pieces read. Reading them without the cursor needs a measurement: whether a slot's
  item element keeps a tooltip root (`GameLayout.Element.TooltipsOffset`) before it was ever hovered, or where the item's
  `Mods` component keeps its explicit modifiers and their values.
- **Names and texts.** `GearCatalogs.cs` is the contract with the game files' catalogs: `IItemBases` (a base by name in
  any language or by id, with its armour, evasion and energy shield) and `IModTexts` (a modifier line in any language →
  template and numbers, and back). `BuildGear.Bases` / `BuildGear.Texts` are the seam, empty until the plugin sets them.
  A wanted line and an item's line are the same modifier when their templates are, else when they are the same words
  around other numbers (the author's line, or the game's own text of it); they compare by the first number. A wanted
  line the texts do not know is "not compared", never missing, and is left out of the counts; a base the files do not
  name is said in the author's words and judged only by an equal name; a unique only by its own name.
- **Speech.** `DescribeInventory` puts the short line before the item's lines and the modifiers after them (the tooltip's
  own lines: read on focus, tap and hold); nothing is said of an item until its tooltip is read, so no modifier is
  called missing from a tooltip still being built. The summary rides the panel notes of the player's own grids, kept off
  when the focus comes from another grid (stash, merchant). A worn piece whose slot was not focused yet is compared by
  the lines made from its modifiers in memory ([item mods](item-mods.md)); the item under the cursor always by its tooltip.
- **To check live.** (1) Inventory open, cursor on the helmet: the log's `Speech` line ends with "Build: …" and one line
  per wanted modifier; with the catalogs loaded the base and the lines are Spanish. (2) Cursor on a helmet in the bag or
  a merchant's stock: "Build, Casco: n de m modificadores, …" right after the name. (3) Open the inventory: `Focus ->`
  shows `notes="Build: … piezas con su base…"`. (4) Flasks: that the grid's column and the server item's X are 0 for
  life and 1 for mana. (5) A two-handed weapon and the off-hand entry; a ring against both ring slots.
