# The mod's executable

`Poe2Access.exe` is the whole mod in one process: the launcher steps (single instance, update check, starting the game,
regenerating its data, cleaning leftovers) and then the host, which reads Path of Exile 2's memory (read-only,
`PROCESS_VM_READ`) and runs the accessibility plugin (`Poe2Accessibility/`) sixty times a second. It has no window, no
overlay and asks for no administrator rights. `tools/Poe2UiExtract` is a library inside it (`Extractor.Run`) and still
builds as its own exe for development.

| Project | What it is |
|---|---|
| `Poe2Access/` | The executable. Root: `Program.cs` (commands), `Instance.cs` (mutex, stop event, stray copies), `HostRun.cs` + `FrameLoop.cs` (the loop), `PluginHost.cs`, `HostLog.cs`, `AppFolders.cs`, `AppCleanup.cs`, `CommandLine.cs`. `Launcher/`: updater, game start, game data, legacy cleanup. |
| `GameReader/` | The game's memory model (states, area, entities, components), the run-time layout searches and the exe's code reader. |
| `GameOffsets/` | The structs that model it. |
`GameReader/` (namespace `GameReader`, called `GameHelper` until 2026-11-08) and `GameOffsets/` started as a copy of [GameHelper2](https://github.com/Gordin/GameHelper2) v2.8.0 (commit
`46cc3bb`, 2026-09-14, recorded as GPLv3 in `docs/research/ecosystem-and-policy.md`; the repository itself carries no
licence file), with the mod's changes that used to live in `tools/package/gamehelper.patch`. Its overlay, ImGui windows,
plugin manager, launcher, input sending and connection killer were left out, and with them ClickableTransparentOverlay
and SixLabors.ImageSharp (NU1902/NU1903 advisories).

**One instance:** one per user (mutex `Local\Poe2Access`); setting the event `Local\Poe2Access.Stop` (what `Poe2Access.exe stop`
does) makes it save the plugin's settings, disable it and exit; it exits by itself when the game closes. The plugin is
loaded from `Plugins\Poe2Accessibility\Poe2Accessibility.dll`: beside the executable in a development build, in `app\`
in the package (`AppFolders.Resolve`). The log is `Poe2Access.log` in that same folder (launcher steps and host lines).

**Build:** `pwsh tools/build.ps1` builds it with the plugin, which deploys itself into the executable's build folder
(`Poe2Access/bin/Release/net10.0-windows/win-x64/Plugins/Poe2Accessibility`), and runs the tests; `dotnet build` at the
repository's root builds the same without them ([tools](../tools/README.md)). The three projects share `Directory.Build.props`
(x64, nullable, latest C#).

**Cost:** once a minute the log says what the frames cost (`Frames: … ms each (coroutines, reader, plugin) … GC … reads per frame … CPU`).
