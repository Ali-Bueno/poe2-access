# POE2Radar — read-only structure study

> Source: https://github.com/NattKh/POE2Radar (clone 2026-09-14). Purpose: reuse its **read-only**
> knowledge (terrain, entities, UI flags). Everything in §7 is excluded from this project.

## 1. Architecture
- C#, `net10.0-windows`, x64 (`src/POE2Radar.Core/POE2Radar.Core.csproj:4-10`).
- Attach via `OpenProcess` (no driver, no injection): `src/POE2Radar.Core/ProcessHandle.cs:64-119`.
  Reads with `NtReadVirtualMemory` for speed (`Native/NativeMethods.cs:36-37`, `MemoryReader.cs:168-192`).
- Globals found by AOB scan of `.text` for RIP-relative refs (`Game/AobPatterns.cs`, `Game/AobScanner.cs`).
- `POE2Radar.Research/Program.cs:4-40`: CLI to validate/derive offsets on the live game
  (`--aob`, `--chain`, `--hp N`, `--find-entities/--find-terrain/--find-map`, `--dump`, `--entity`). Read-only.

## 2. Offsets (`src/POE2Radar.Core/Game/Poe2Offsets.cs`)
Annotated ✓ (live-validated) / (GH2) (from GameHelper2 dump) / (IDA). Provenance header at lines 1-12;
validated against patch dates (e.g. Life re-validated 2026-06-04, AOB for build 2026-05-05).
- `GameState`: CurrentStatePtr, States[12] (`:25-31`)
- `InGameState`: AreaInstanceData ✓0x290, **UiRoot ✓0x2F0**, Camera ✓0x368 (`:37-44`)
- `AreaInstance`: AreaInfoPtr ✓0xA0, LocalPlayer ✓0x5A0, AwakeEntities ✓0x6C0, SleepingEntities ✓0x6D0,
  TerrainMetadata ✓0x8A0, CurrentAreaLevel ✓0xC4, CurrentAreaHash ✓0x11C (`:58-68`)
- Entity / EntityDetails / ComponentLookUp (`:91-110`); components Life, Render (world pos +0x138),
  Player, Camera, MinimapIcon, ObjectMagicProperties (rarity), Chest, Positioned, AreaTransition,
  Npc, Targetable... (`:114-293`)
- Terrain + TileStructure (`:301-326`); UiElement (`:353-362`).

## 3. Live reading (`src/POE2Radar.Core/Game/Poe2Live.cs`)
- Chain: GameState → InGameState → AreaInstance → LocalPlayer; valid only when the player's metadata
  path starts with `Metadata/` (`:88-113`) — i.e. **returns false in menus/loading**.
- Entities: walks the `AwakeEntities` std::map (RB tree), skips id ≥ 0x40000000 (visual decorations) (`:250-273`).
- Player position: `Render.CurrentWorldPosition`; grid = world XY / (250/23) (`:700-713`).
- **No heading/facing field found anywhere.**
- Component resolution: EntityDetails → ComponentLookUp → name match → index into component vector (`:846-871`).

## 4. Terrain / walkability
- `Terrain` at `AreaInstance+0x8A0`: TotalTiles (0x18), TileDetailsPtr (0x28, 0x38-byte tiles),
  GridWalkableData (0xD0), GridLandscapeData (0xE8), GridLayer3/4 (0x100/0x118 — "PoE2 has FOUR grid
  layers"), BytesPerRow (0x130).
- Tile = 23×23 grid cells; 1 grid cell = 250/23 ≈ 10.87 world units.
- Walkable data: one nibble per cell (2 cells/byte); non-zero ⇒ walkable (`Poe2Live.cs:565-594`).
- Height: `TileStructure.TileHeight` (short, +0x30). Extra layers identified but not decoded.
- Screen projection: isometric, 38.7° tilt (`Pathfinding/MapProjection.cs:15-32`).

## 5. Landmarks / classification (`Poe2Live.cs:799-822`)
Substring rules on metadata path: `/Monsters/`→Monster (minus MonsterMods/Summoned/Daemon/Invisible),
`/Characters/`→Player, `/NPC/`→Npc, `/Chests`→Chest (minus breakable props), `Transition`→Transition,
`/Terrain/`→Object. League detection by substrings (`:765-784`). POI = has `MinimapIcon` component.
Static landmarks from tile `.tdt` paths + keyword list (`:412-475`).

## 6. UI
- `UiElement`: Self 0x08, Children StdVector 0x10, Flags 0x180 with visibility bit 0x0B; visibility is
  hierarchical (all ancestors must be visible) (`Poe2Offsets.cs:353-362`, `Poe2Live.cs:651-696`).
- No hovered-item, no login/char-select state handling.

## 7. EXCLUSION LIST (never port)
- `Core/Cheats/CheatManager.cs`, `CheatDefinition.cs`, `GameVisualTweaks.cs` (WriteProcessMemory / byte patches).
- `Core/Native/NativeMethods.cs:28,32` (`VirtualProtectEx`, `WriteProcessMemory`).
- `Overlay/Input/SendInputNative.cs`, `Overlay/Automation/AutoRule.cs`, `Overlay/RadarApp.cs` (auto-flask,
  SendInput), `Overlay/Web/ApiServer.cs` rule/pathing/cheat handlers (~211-289).
