# Ecosystem, policy and speech output — web research (2026-09-14)

## Hosts / repos
- **GameHelper2** (https://github.com/Gordin/GameHelper2): GPLv3, actively released — v2.7.5 [PoE2 0.5.5b]
  (2026-09-11), v2.7.4 (09-07), v2.7.0 (09-05). `net10.0-windows`, build `dotnet build GameOverlay.sln -c Release`
  (verified locally: 0 errors), output `GameHelper\bin\Release\net10.0-windows\win-x64\`, run `Launcher.exe`.
  Targets `PathOfExile.exe` and `PathOfExileSteam.exe`.
- **CoreExile2** (`coussiraty/CoreExile2`) is gone (404). Replaced by **`coussiraty/ExileBridge`**: read-only
  PoE2 memory API + HTTP + MCP server (loot/monsters/chests/offsets/GGPK data), last update ~2026-06-16.
  No `PLUGIN_GUIDE.md`/`SDK_DESIGN.md`, no plugin SDK, no UI tree → **not a host candidate for menus**.
- **ExileCore2**: evaluated separately (see `exilecore2-study.md`).

## Game client
- Steam exe `PathOfExileSteam.exe`, proprietary GGG engine, DirectX 12. Steam app id 2694490.
- **No kernel anti-cheat**; detection is server-side (game runs under Proton).
- Current version 0.5.5b ("Forbidden Rites", ~2026-09-04). Install ≈100–138 GB.

## Policy (GGG)
- ToU forbids bots/automation (7c), client modification (7b), unauthorized clients (7e), reverse engineering (7i).
- Forum thread 3518296 (staff): anything doing "more than one action per keystroke/click" is bannable —
  it targets input automation, not passive reading. No official carve-out for memory-reading overlays;
  tolerance of GameHelper2/ExileCore2 is inferred, not guaranteed. No confirmed PoE2 bans found for these.
- **Project stance stays: external, read-only, no input automation.** Ask GGG in writing before public release.
- No native blind-accessibility settings in PoE2 (forum thread 3652275 asks for it, no staff reply).
  No existing blind-accessibility mod found.

### In-process vs external track record (web research 2026-10-24)
Asked whether injecting the mod as a DLL has precedent. No injected PoE tool with a clean record exists;
the only client-side detection reports concern in-process code. Several sources blocked fetching (403).
- **Map hacks** (client memory patching, PoE1): GGG warned 3,921 players and threatened permanent bans
  (gamestar.de report, undated, anecdotal: https://www.gamestar.de/artikel/path-of-exile-letzte-warnung-an-cheater,3082194.html).
- **Optiscaler in PoE2** (a DLL graphics tool, not a cheat; June 2025): one user got a "map hack" kick
  warning, another did not; no staff reply (https://www.pathofexile.com/forum/view-thread/3786781).
  Read as: a foreign DLL in the client can trip detection.
- **Exilebuddy** (PoE1 bot, Bossland): shut down ~2017–18 amid a "warning flood" (OwnedCore, anecdotal).
- **External readers** (PoEHUD, ExileApi, GameHelper): no documented ban wave found; described as treated
  "notoriously gently" (EyeAuras, Dec 2024, anecdotal).
- No GGG statement separates memory reading from injection, or mentions accessibility tools. The viral
  "disabled player banned for macros" story was false — GGG told TheGamer nobody was banned
  (https://www.thegamer.com/disabled-path-of-exile-player-banned-macros/).
- **Conclusion:** the record supports the existing stance (external, read-only); injection adds account risk.

## Native UI navigation
- Full first-party controller support (menus navigable, reported clunky). KB+M ↔ controller cannot be
  swapped in-game (only at main menu / character select).
- Keyboard-only (arrows/Tab) menu focus: **unverified** — test hands-on once the game is installed.
  Key design question: does the game keep a "focused element" we can read (controller mode) or only hover?

## Speech output — PRISM / Prismatoid
- PRISM latest **v0.18.2** (2026-08-26): `prism-windows-x64.zip` present; no `tolk.dll` asset anymore.
- **Prismatoid 0.4.0** on NuGet (2026-08-12): `lib/net8.0`, `lib/net10.0`, and **bundled natives**
  `runtimes/win-x64/native/prism.dll` (+ arm64, linux, osx). Verified by unpacking the .nupkg.
- API: `new PrismContext()` → `context.AcquireBestBackend()` → `backend.Speak(text, interrupt)`,
  `Braille`, `Output`, `Voices`, `Rate/Volume` (feature-gated), `AvailableBackends` (name/priority),
  availability-changed callback, `PrismLog.SetHandler`. Exceptions: `PrismException`.
- GameHelper2 is `net10.0-windows` → **consume Prismatoid via NuGet** (no hand P/Invoke).
  Caveat to verify: plugins load in their own `AssemblyLoadContext`; native `prism.dll` probing from
  `runtimes/win-x64/native` may fail there → set `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` so the
  native lands next to the plugin dll, and/or add a `NativeLibrary.SetDllImportResolver` in the plugin.
