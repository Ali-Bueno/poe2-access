# ExileCore2 — evaluation as host (2026-09-14)

> Source: https://github.com/exCore2/ExileCore2. **Binary-only** distribution (no C# source), IL-obfuscated
> core, `[FieldOffset]` values scrambled, patterns shipped as an opaque `sig.bin`, access gated through
> Discord (release `freepreview05`, 2026-05-19, "free preview (GONE)"). Findings below come from
> decompiling `ExileCore2.dll` / `GameOffsets2.dll` signatures only.

## Verdict
**Rejected as host.** Closed, obfuscated and Discord-gated: impossible to fix offsets ourselves after a
patch, and incompatible with an open accessibility project. GameHelper2 stays the host.

## What it teaches us about the game (targets for our own runtime discovery)
- The game's UI element (`ElementOffsets`) carries: `SelfPointer`, children vector (`ChildStart/ChildEnd`),
  `Position`, `Scale`, `Type` (ushort), `Parent`, `Flags`, **`Tooltip`** (pointer to another element), `Size`,
  label colours, `isHighlighted`, `ShinyHighlightState`, **`Text` (UTF-16)**, `TextureNamePtr`,
  **`TextNoTags` (UTF-16, already tag-stripped)**. → Our string probe in the dumper must find two
  wstrings per labelled element (raw with markup + clean).
- `IngameState` exposes `UIRoot`, `UIHover`, `UIHoverElement`, `FocusedInputElement`, `IngameUi`.
  → The hover/focus pointers live in the InGameState object; GameHelper2 already reads a nearby chain
  (`MouseOverHostPtr` @0x300 → +0x3F0 → +0xA8 entity). Candidate area for a "hovered UI element" pointer.
- `EscapeState.UIRoot` exists (escape menu is its own state with its own root).
- Only `AreaLoadingState`, `EscapeState`, `IngameState` are wrapped; **login / character select / create
  character have no UI root there either** → the same gap as GameHelper2. Our state-object scan
  (`UiTreeReader.FindUiRootCandidates`) is the plan to close it.
- `IngameUIElements` wraps: InventoryPanel, StashElement, NpcDialog, PurchaseWindow, SellWindow,
  TradeWindow, SkillBar, TreePanel, QuestTracker, OpenLeftPanel/OpenRightPanel, PopUpWindow, Cursor,
  ItemsOnGroundLabels, Map, WorldMap, ChatPanel, PartyElement, ResurrectPanel, CurrencyExchangePanel,
  Gemcutting, CraftBench, ControllerGeneralMenu... Hovered item: `HoverItemIcon.Item` (Entity).
- Element `Type` is a ushort with only 4 named values (MiscGroundLabel, GroundItemLabel, PassivePoint,
  InventoryItem): element kind is mostly not encoded; classification must use StringId/structure.
- `GameStateTypes` identical to GameHelper2's list.

## Excluded there anyway
`ExileCore2.Input` (Click/KeyDown/MouseMove), `IInputManager`, plugins PickIt/ReAgent/NpcInvWithLinq.
