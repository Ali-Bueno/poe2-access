# StashUtility — UI explorer patterns (GameHelper2 plugin)

> Source: https://github.com/AlexanderHel/StashUtility (clone 2026-09-14). Host: **GameHelper2**
> (`README.md:3`, `StashUtility.csproj:24-29` ProjectReference to `..\..\GameHelper\GameHelper.csproj`,
> `StashUtilityCore.cs:22` `sealed class ... : PCore<StashUtilitySettings>`). TFM `net10.0-windows`.
> No memory writes, no input simulation, no hotkeys (all ImGui buttons).

## UI Path Explorer (`StashUtilityCore.cs:334-527`, helpers `:1163-1183`, `:2887-2962`, `:2037-2089`)
- Root: `Core.States.InGameStateObject.GameUi.LeftPanel.Address` or a typed hex address.
- Children: read raw `UiElementBaseOffset` via reflection-wrapped host `ReadMemory<T>`, then
  `ReadStdVector<IntPtr>(off.ChildrensPtr)` (`:439-440`, `:2916-2917`).
- Path = comma-separated child indices from root (`StashUtilitySettings.cs:17`, e.g. `"2,0,0,0,1,1,45,0,1"`).
- Per node: address, **`StringId` = `ReadStdWString(off.StringIdPtr)`** (`:464`, `:501`) — so the 0x128
  StdWString IS readable; it is an element *name/id*, not the displayed label —, visibility via
  `UiElementBaseFuncs.IsVisibleChecker(off.Flags)`, child count, `UnscaledSize`.
- Screen rect for a node: `PluginUiElementReflection.CreateUiElement(addr, parentsObj)` +
  `UiElementPositionProperty`/`UiElementSizeProperty` (`:1593-1607`).
- "Dump UI Tree to File" (`:370-398`, `:3085-3144`) → `config/ui_tree_dump.txt` (address, visible, child
  count, size, indented by path) + brute-force scan at each node for pointers to entities whose
  `+0x08 → +0x08` StdWString starts with `Metadata/` (item-in-slot discovery, `:2813-2863`).

## Hovered item (`:530-633`, `:2186-2214`)
Not a host API: compares `ImGui.GetMousePos()` with each slot's rect. Item object built via
`Activator.CreateInstance(typeof(Item), NonPublic, …, addr)` then `TryGetComponent<Base/Mods/ObjectMagicProperties>()`
(`BaseItemName`, `Rarity`, implicit/explicit/enchant mods `:547-592`).

## Memory access pattern (`:2964-3053`)
Reflects into `Core.Process.Handle` for `ReadMemory`/`TryReadMemory`/`ReadStdVector`/`ReadStdWString` —
reuses GameHelper2's reader instead of P/Invoking. Same trick lets a plugin read any offset struct.

## Takeaways for the accessibility plugin
- Tree walking + StringId + visibility + rect are all achievable from a plugin today.
- Display text is still missing: needs a new offset (label StdWString on text elements) — see ExileCore2 study.
