# The mod's host

`Poe2AccessHost.exe` reads Path of Exile 2's memory (read-only, `PROCESS_VM_READ`) and runs the accessibility plugin
(`Poe2Accessibility/`) sixty times a second. It has no window, no overlay and asks for no administrator rights; the launcher
(`tools/Poe2AccessLauncher`) starts and stops it.

| Project | What it is |
|---|---|
| `Poe2AccessHost/` | The executable: frame loop, plugin loading, log (`Poe2AccessHost.log`), single instance, graceful stop. |
| `GameHelper/` | The game's memory model (states, area, entities, components) and the run-time layout searches. |
| `GameOffsets/` | The structs that model it. |

`GameHelper/` and `GameOffsets/` started as a copy of [GameHelper2](https://github.com/Gordin/GameHelper2) v2.8.0 (commit
`46cc3bb`, 2026-09-14, recorded as GPLv3 in `docs/research/ecosystem-and-policy.md`; the repository itself carries no
licence file), with the mod's changes that used to live in `tools/package/gamehelper.patch`. Its overlay, ImGui windows,
plugin manager, launcher, input sending and connection killer were left out, and with them ClickableTransparentOverlay
and SixLabors.ImageSharp (NU1902/NU1903 advisories).

**Contract with the launcher:** one instance per user (mutex `Local\Poe2AccessHost`); setting the event
`Local\Poe2AccessHost.Stop` makes it save the plugin's settings, disable it and exit; it exits by itself when the game closes.
The plugin is loaded from `Plugins\Poe2Accessibility\Poe2Accessibility.dll` beside the executable.

**Cost:** once a minute the log says what the frames cost (`Frames: … ms each (coroutines, reader, plugin, after) … CPU`).
