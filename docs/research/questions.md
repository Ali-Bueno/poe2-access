# Research questions (brief §22) — status

Legend: `[x]` answered (source in `docs/research/*.md`), `[~]` partial, `[ ]` open.

## Host / state
- [x] Plugin contract — `gamehelper2-plugin-api.md` (PCore<T>, sealed, one per assembly, DrawUI per frame).
- [x] How is the current game state known? `Core.States.GameCurrentState` polled (event is internal).
- [x] How is AreaInstance obtained? `Core.States.InGameStateObject.CurrentAreaInstance`; area name via
      `Core.States.AreaLoading.CurrentAreaName`; `RemoteEvents.AreaChanged` is public.
- [x] State object addresses for pre-game states? `Core.States.AllStates` (address → GameStateTypes).

## UI
- [x] UI root pointer/object (in game)? `InGameStateOffset.UiRootStructPtr` (0x2F0) → `UiRootStruct.UiRootPtr` (0x340);
      controller mode uses `GamepadUiRootStructPtr` (0x318). GameHelper2 wraps child 1 as `GameUi`.
- [~] UI root for LoginState / SelectCharacterState / CreateCharacterState? **LoginState solved live (2026-09-14):**
      `LoginState+0x10` → object with element pointers (+0x2B8…+0x438) → climb ParentPtr → root `root_control`.
      Same technique to be confirmed on SelectCharacterState / CreateCharacterState.
- [~] UI element text representation? Element has `StringId` (StdWString @0x128, internal name). Display
      text: per ExileCore2 the element also holds `Text` and `TextNoTags` UTF-16 strings at unknown
      offsets → dumper string probe on the main menu will reveal them.
- [ ] Focus / hover / selected state? GameHelper2: none. ExileCore2: `IngameState.UIHover`,
      `UIHoverElement`, `FocusedInputElement` (offsets hidden). Flags bit 0x0B = visible, 0x0A = modify-pos,
      chat-active bit 0x12 on ChatParent. Plan: diff dumps with/without hover, and scan InGameState near 0x300.
- [ ] Does keyboard/controller navigation keep a focused element the game itself tracks? Hands-on test.
- [ ] Inventory slot geometry? StashUtility derives per-slot rects from the tree (`TryGetAbsoluteRect`).
- [ ] Hovered item source? StashUtility: rect hit-test + pointer scan in the slot element for an entity
      whose `+0x08 → +0x08` StdWString starts with `Metadata/Items`.

## Player / world (later phases)
- [x] Which object is the local player? `AreaInstance+0x5A0 LocalPlayer` (POE2Radar ✓), GameHelper2 `AreaInstance.Player`.
- [x] How are entities enumerated? `AwakeEntities` std::map at `AreaInstance+0x6C0` (GameHelper2 `AwakeEntities`).
- [x] Grid/world position API? `Render.CurrentWorldPosition`; grid = world / (250/23).
- [ ] Player heading/facing? Not found by anyone. Candidates: movement delta, `Actor` animation state.
- [x] Terrain grid source / cell size / walkability encoding? `AreaInstance+0x8A0` terrain struct, 23 cells per
      tile, nibble per cell (non-zero = walkable), four layers (`poe2radar-study.md` §4).
- [~] Height/elevation? `TileStructure.TileHeight` (short); extra grid layers undecoded.
- [x] Area transition detection? `RemoteEvents.AreaChanged` + `CurrentAreaName` diff.
- [x] NPC / waypoint / door / chest classification? Metadata-path rules (`poe2radar-study.md` §5) +
      GameHelper2 components (`Npc`, `AreaTransition`, `Chest`, `MinimapIcon`).
- [x] Monster hostility / rarity / life? Components `Positioned` (team), `ObjectMagicProperties` (rarity), `Life`.
- [ ] Current target representation? Only `InGameState.MouseOverEntity` known.
