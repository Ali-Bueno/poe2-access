# PoE2 UI layout scripts (`metadata/ui/**.ui`) — offline menu knowledge

> Extracted 2026-09-14 from `Bundles2` with `tools/Poe2UiExtract` (+ `ooz` shim). 144 files, UTF-16LE text:
> `pregamestate` (23), `ingamestate` (100, ~76 of them tutorial articles), `loadingstate` (3), plus `uisettings.xml`.
> Kept in `external/poe2-ui/` (game data, gitignored).

## Format
- One control per `begin <Type> [based on <Template|Id>] ... end`; nesting by indentation = children.
- `template <Type> id = NAME; ... end` declares reusable bases (e.g. `loginscreencommon.ui`).
- `id = name;` → **exactly the control name we read at element +0x98**. Not unique (three sibling `TextBox`
  in `changepasswordscreen.ui`); many decorative blocks have no id.
- Text: `text = { LocKey };` (string table) or `text = "literal";`. Tooltip: `tip_text = { Key }, mouse;`.
- Focus/gamepad are explicit: `can_focus_with_controller = true|false` (46 uses), `tab_index = n` (21),
  `gamepad_highlight_border`, `keyboard_enabled`, `tag = "GamepadOnlyButton" | "ExcludeOnGamepad" | "ExcludeOnConsoles"`.
- Layout: `size`, `location`, `y`, `float = centre`, `margin`, `padding`, `auto_size`; constants `const $x = ...;`
  referencing `Fonts.*`, `Colours.*`, `Sizes["Button"]` from `uisettings.xml`.
- **No sound references** (click sounds we see in memory come from a runtime per-widget style table).

## Widget classes seen (count)
Label 4436 (mostly credits), Frame 328, StackLayout 275, Picture 235, FlowLayout 180, Button 100,
TutorialArticle 76, VideoDisplay 53, IconLabelButton 51, Login_IconButton 23, TextBox 17, FourComboBox 13,
ImageButton 12, MOTDDisplay 9, Login_Button 7, ComboBox 7, CheckBox 4, TouchButton 4, Login_TextButton 2,
Login_TextBox 2, GreenButton 2, BasicButton 1, ScrollableFrame 1, HUD-specific (Orb, FlaskBar, SkillsBar,
ExperienceBar, BuffDisplay, SkillIcon...). **No Slider / ScrollBar / Tab class** in the scripts (the options
menu sliders are native).

## Pre-game screens
- `steamloginscreen.ui` (+`loginscreencommon.ui`): `login_button` (Login_Button, text LoginScreenLogInButtonText),
  `options_button`/`patch_notes_button`(not focusable)/`exit_button` (Login_IconButton), `gateway_combobox`
  (tab 3) / `language_combobox` (tab 2) (FourComboBox), `motd_frame` (MOTDDisplay). `loginscreen.ui` adds
  `remember_login_checkbox`.
- `createcharacterscreen.ui`: `options_button`, `exit_button`, `pvp_build_type_combobox`, `exit_frame` →
  `title`/`label`, `create_character_frame` → `name_frame` → `name_textbox` (TextBox), `league_combobox`
  (FourComboBox), `create_button` (Login_Button); `message`, `class_unavailable_text` labels.
- `deletecharacterscreen.ui`: `DCMainFrame` → `warning`, `cancelButton`, `deleteButton`, `message`.
- `changepasswordscreen.ui`: 3 frames each Label + `TextBox`; `cancelButton`, `changeButton`, `message`.
- `loginqueue.ui`: `login_queue_frame` → `message_label`, `cancel_login_button`. `dailydeal.ui`: `daily_deal`
  → `deal_image`, `deal_label`, `new_price_label`, `balance_label`, `buy_now_label`, `time_left_label`, `shop_button`.
- **Character select is NOT a .ui file** (only `escapemenu.ui: exit_to_character_selection` refers to it) → native code;
  same for inventory, stash, vendor, NPC dialog, passive tree, chat → keep the runtime research path for those.

## In-game files
`hud/hud.ui` (+console/tencent variants; orbs, flasks, skills, menu icon row: `inventoryButton`,
`skill_panel_button`, `passiveButton`, `characterButton`, `worldButton`, `mapButton`, `socialButton`,
`market_button`...), `hud/skillsbar_console.ui`, `characterpanel/characterpanel.ui` (attribute labels with
`tip_text`), `escapemenu.ui` (`resume_game`, `social_panel_button`, `options`, `exit_to_login`,
`exit_to_character_selection`, `exit_game`...), windows (`confirmationwindow.ui`, `buymarkettabwindow.ui`,
`createsteamaccountwindow.ui`, `waitingfortradewindow.ui`...), `deathscreen/deathscreen.ui`, tutorial articles.

## Name → class conventions (type must come from the declared class, names are inconsistent)
`*_button`/camelCase `*Button` → Button family; `*_combobox` → ComboBox/FourComboBox; `*_checkbox` → CheckBox;
`*_textbox` / `TextBox` → TextBox; `*_label`, `message`, `title`, `warning` → Label; `*_frame` → Frame;
`*_layout` → Stack/FlowLayout. `label_(button)` (seen in memory) is engine-generated, not authored.
