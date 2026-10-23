# GameHelper2 — UI tree representation (as of clone 2026-09-14)

> Source: https://github.com/Gordin/GameHelper2. Repo-relative paths. Conclusion first: **GameHelper2's UI
> support is in-game only, untyped, has no text reading, no hover/focus/tooltip, and no pre-game UI root.**

## 1. Wrapper: `GameHelper/RemoteObjects/UiElement/UiElementBase.cs:19`
| Property | Line | Status |
|---|---|---|
| `Position`, `Size`, `Scale` | 61-94 | implemented (walks parent chain, applies scale) |
| `IsVisible` | 99-115 | implemented (own flag bit AND all ancestors) |
| `TotalChildrens`, indexer `this[int]` | 120, 141-162 | implemented, lazily cached |
| `TryGetParent` | 122-132 | implemented (`UiElementParents` cache) |
| `Address` | inherited | the only id |
| **Text** | — | **NOT implemented.** `StringIdPtr` (StdWString @0x128) declared in the offset struct but never read (`UpdateData` 228-258) |
| enabled / type / hover / focus / tooltip | — | not implemented (one-off: `ChatParentUiElement.IsChatActive` = flag bit 0x12) |
Elements are identified by raw `flags` fingerprints (e.g. `AtlasMapNodeFp = 0x542EF3`) or hardcoded child-index paths.

## 2. Offsets: `GameOffsets/Objects/UiElement/UiElementBaseOffset.cs:8-37`
`0x000 Vtable`, `0x008 Self`, `0x010 ChildrensPtr` (StdVector), `0x0B8 ParentPtr`, `0x100 RelativePosition`,
`0x108 PositionModifier`, `0x118 LocalScaleMultiplier`, `0x128 StringIdPtr` (StdWString, TODO comment),
`0x168 Flags` (uint; visible bit 0x0B, shouldModify bit 0x0A), `0x172 ScaleIndex`, `0x270 UnscaledSize`,
`0x28C BackgroundColor`. `BaseResolution = (2560,1600)` from GGPK `Metadata/UI/UISettings.xml`.
Subclasses: `MapUiElementOffset` (Shift 0x350, DefaultShift 0x358, Zoom 0x390), `SkillTreeNodeUiElementOffset` (SkillInfo 0x2A0).

## 3. UI root
- `InGameState.UpdateData` (`RemoteObjects/States/InGameState.cs:77-91`) reads `InGameStateOffset.UiRootStructPtr`
  (or `GamepadUiRootStructPtr` in controller mode) → `UiRootStruct.UiRootPtr` → `uiRootAddress` (internal).
- Usable root = `InGameState.GameUi` (`ImportantUiElements`, child index 1 of UiRoot) — curated hardcoded paths.
- Valid only in `InGameState` / `EscapeState`; `UiElementParents` caches cleared on other states
  (`Cache/UiElementParents.cs:197-237`).
- **No UI root for pre-game states.** `GameStateTypes` (`RemoteEnums/GameStateTypes.cs:10-82`): AreaLoadingState,
  ChangePasswordState, CreditsState, EscapeState, PreGameState, LoginState, WaitingState, CreateCharacterState,
  SelectCharacterState, DeleteCharacterState, LoadingState, UnknownState, GameNotLoaded. Only the state
  address + enum tag are exposed (`GameStates.cs:40,55-69`, event `RemoteEvents.StateChanged`).

## 4. Wrapped panels (`InGameStateObjects/ImportantUiElements.cs`)
LargeMap, MiniMap, WorldMapPanel (+Act tabs, Atlas), AtlasSkillsPanel, TempleConsole, Atlas nodes,
`LeftPanel`/`RightPanel` (untyped containers), CurrencyExchangePanel, Gemcutting panels, SekhemasTrialMapPanel,
ChatParent, SkillTreeNodesUiElements. `IsAnyLargePanelOpen`, `IsPassiveSkillTreeOpen`.
Inventory (`InGameStateObjects/Inventory.cs`) is server-data items, not a UI wrapper. Stash/NPC dialog/vendor/tooltip: not wrapped.

## 5. Hover
- `InGameState.MouseOverEntity` (`InGameState.cs:57,93-112`) = world entity under cursor, not UI.
- `Ui/ElementFinder.cs:88-151`: manual hotkey hit-test of mouse against child rects (debug window, not an API).

## 6. Text
Pattern to adapt: `reader.ReadUnicodeString(...)` on a StdWString (`ImportantUiElements.cs:730,999`).
Applying it to `StringIdPtr` is untested; and a "string id" is likely an element name, not display text.

## 7. Debug tools
`Ui/GameUiExplorer.cs` (recursive child tree, address/pos/size/visible), `Ui/ElementFinder.cs`, `Ui/DataVisualization.cs`.

## 8. Walking children from a plugin
Pattern `ElementFinder.cs:112-151`. Cross-assembly plugins cannot `new UiElementBase` (internal ctor) —
`Plugin/PluginUiElementReflection.cs:17-45` reflects `CreateParents()`/`CreateUiElement()` but only exposes
Position/Size (no IsVisible/children) — a plugin must extend the reflection or the host must be patched.
