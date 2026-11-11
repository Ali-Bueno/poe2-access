# Linux: Steam with Proton (Steam Deck)

The mod runs under Proton the way it runs on Windows: `Poe2Access.exe` in the game's own Wine prefix, beside the game,
reading its memory with `ReadProcessMemory`. Speech goes to the Linux desktop through PRISM's Wine bridges (Speech
Dispatcher, Orca or Spiel), and the start-up questions and errors through zenity or kdialog, which Orca reads.

**Checked on Linux (Linux Mint 22.3, Proton Experimental 11.0, Orca 46.1, Speech Dispatcher; 2026-11-11), without the
game:** the solution builds on Linux; all unit tests pass under Proton's Wine; PRISM speaks through Speech Dispatcher
under Wine; a Wine process reads another Wine process's memory (`OpenProcess(PROCESS_VM_READ)`, `ReadProcessMemory`,
about 11 µs a read, with `kernel.yama.ptrace_scope` 1), finds its main window, main module and image path; zenity opens
from the mod and its answer comes back. And the whole start, with Wine's Notepad renamed `PathOfExileSteam.exe` standing
in for the game: `poe2access-proton.sh` with a Steam-like command ran `Poe2Access.exe` under `proton`, which started
the stand-in from that command, saw its window, loaded the plugin (speech: "Path of Exile 2 accessibility ready"),
opened the process and searched its memory; `poe2access-stop.sh` stopped it in a third of a second; closing the
stand-in ended the mod and the script. **Not yet checked: the game itself** (it was still downloading): Steam's real
command, a real PoE2 process and the game data step (FMOD, Oodle) under Wine wait for a first run.

Why not natively, as [Awakened PoE Trade](https://github.com/SnosMe/awakened-poe-trade) runs on Linux: that tool never
reads the game's memory (it reads the log file `Client.txt`, the clipboard and the X11 window), so a native Electron
build is enough. This mod reads the memory, decompresses the game's bundles with an Oodle-compatible `oo2core.dll`,
renders the game's sounds with the game's own `fmod.dll` and plays its cues through `winmm`: all Windows libraries.
Inside the prefix they work unchanged, and a Wine process may read the game where a native one would need ptrace rights
over it (Wine lets its own server trace its processes, so the default Yama level 1 is enough).

## Why it was silent

Inside a Proton prefix there is no NVDA or JAWS, OneCore is not implemented and SAPI has no voices, so PRISM's
`acquire_best` finds no backend. PRISM reaches the Linux host's speech through its Wine bridges (Orca, Speech
Dispatcher, Spiel), and its Windows builds before 0.18.3 left them out of the registry (ethindp/prism#126). The mod
used the Prismatoid 0.4.0 NuGet binding, which carries PRISM 0.17.3: no bridges. An NVDA stand-in in the prefix (Wine
Access) does not help either: since PRISM 0.16.2 the NVDA backend also calls `RpcMgmtInqIfIds`, a stub that always fails
in Wine (found by the d4-assist session, 2026-11-08).

No Prismatoid release carries 0.18.3, and Prismatoid 0.4.0 with the 0.18.3 `prism.dll` is not safe: 0.18.3 raised
`PRISM_CONFIG_VERSION` to 4 (`PrismConfig` gained `availability_baseline_callback`) and its `prism_config_init` writes 56
bytes where Prismatoid 0.4.0 allocates 48. The mod now has its own binding of the C API,
`Poe2Accessibility/Services/Speech/PrismLibrary.cs`: `PrismConfig` field for field from the shipped `prism.h`, filled by the
library's own `prism_config_init` into a 256-byte slot (a later, larger config cannot overrun it), and only the calls
the mod makes. The plugin's speech and the executable's startup speech both use it.

With the bridges missing, PRISM is not silent but **mute**: `acquire_best` takes Wine's SAPI, Proton's `protontts`
over eSpeak-NG, which reports a voice while the Steam runtime carries no eSpeak-NG data, so nothing sounds and nothing
fails. At 0.18.3 SAPI, UIA and Speech Dispatcher share priority 97 (NVDA 103, Orca 100, OneCore 98, Spiel 96) and SAPI
comes first, so it wins whenever Orca is missing (the D2R mod's tester, 2026-11-09). Under Wine the binding therefore
never calls `acquire_best`: `PrismLibrary.StartBridged` tries only Orca, Speech Dispatcher and Spiel, by priority, each
taken with `prism_registry_acquire` and kept only when `PRISM_BACKEND_IS_SUPPORTED_AT_RUNTIME` is set and it initialises
(until 2026-11-11 it asked `prism_registry_get` first, which at v0.18.3 only returns an instance acquired before: null
for every bridge, so nothing spoke under Wine) (`ALREADY_INITIALIZED` counts as started), and when none starts it says why for each. It also sends PRISM's own log to
the mod's log at debug level while it starts (`prism_set_log_handler`), the only place PRISM says why a bridge did not
load, then lowers it to warnings. On Windows none of this runs: `acquire_best`, NVDA first, no PRISM log.

## What ships

In `app/Plugins/Poe2Accessibility/`, from `third_party/prism/` (the plugin build copies `runtime/` beside the plugin):

| File | From the PRISM v0.18.3 release |
|---|---|
| `prism.dll` | `prism-windows-x64.zip`, `dynamic/release/bin/` |
| `prism_orca_bridge.dll`, `prism_speech_dispatcher_bridge.dll`, `prism_spiel_bridge.dll` (2 KB placeholders) | `prism-linux-x64.zip`, `dynamic/release/wine/placeholders/` |
| `wine/*.dll.so` (the Linux x86-64 halves) | `prism-linux-x64.zip`, `dynamic/release/wine/` |
| `licenses/prism/` (`NOTICE`, `LICENSES/`: PRISM is MPL-2.0, source at https://github.com/ethindp/prism/tree/v0.18.3) | `prism-windows-x64.zip` |

The zips' SHA-256: `prism-windows-x64.zip` 13d3c9d1d524b0737cde261c6468845753e36b6c5c2637e7d26bfb8bf4d8783f,
`prism-linux-x64.zip` 2b3050c89eaa4fb6c452377ec155ec7b71f444b424b547e04da238575d98a31c. The files are the same as the
D2R mod's (`third_party/prism/bin/`). `third_party/prism/include/` holds the header the binding matches. PRISM's
[Wine bridges](https://github.com/ethindp/prism/blob/v0.18.3/doc/src/wine-bridges.md) manual requires both halves from
the same release and never renamed.

## How PRISM finds them

- Only under Wine: each bridged backend first checks `ntdll`'s `wine_get_version`. On Windows the placeholders are
  never loaded (checked: NVDA chosen, no bridge module in the process).
- A placeholder is looked for on the DLL search path, then in `prism.dll`'s own folder (`source/optional_library.cpp`
  at v0.18.3), so beside `prism.dll` is enough.
- Wine then loads the matching `.dll.so` by name from `WINEDLLPATH`, read when the process starts: the mod cannot set
  it from inside.

## Installing

Unzip the mod anywhere in your home folder, open a terminal there and run `sh install-linux.sh`
(`tools/package/install-linux.sh`). It finds the game in the Steam libraries, says whether speech (Speech Dispatcher or
Orca), zenity or kdialog and the kernel's ptrace setting are as the mod needs, and sets the game's launch option in
Steam's `userdata/<account>/config/localconfig.vdf` (a backup `.poe2access-backup` beside it), keeping any other option
the player had (`PROTON_LOG=1 %command% --x` becomes `PROTON_LOG=1 sh "<mod>/poe2access-proton.sh" %command% --x`; a
moved mod folder replaces the old path). Steam must be closed, since it rewrites that file when it exits; otherwise the
script prints the steps to do it by hand. `sh install-linux.sh --uninstall` takes the launch option out again.

By hand, in Steam: **Path of Exile 2 > Properties > General > Launch options**:

```sh
sh "/home/<you>/Poe2Access/poe2access-proton.sh" %command%
```

(the script's real path; `sh` in front because a zip made on Windows keeps no execute bit).

## Starting the mod

From Steam's Play button. What `poe2access-proton.sh` does (`tools/package/poe2access-proton.sh`):

1. Takes from Steam's command (`%command%`) the Proton Steam picked (the argument ending in `/proton`), the game's
   `PathOfExileSteam.exe` and its arguments; keeps Steam's `STEAM_COMPAT_*` and `SteamAppId`, with the usual defaults
   when one is missing.
2. Puts `app/Plugins/Poe2Accessibility/wine` in `WINEDLLPATH`, and sets `DOTNET_SYSTEM_GLOBALIZATION_USENLS=1`: .NET
   takes ICU for culture data and Wine's `icu.dll` lacks it, so `Poe2Access.exe` stopped at once ("Cannot get symbol
   u_charsToUChars from libicuuc", seen 2026-11-11); Windows' NLS calls, which Wine implements, serve instead. Windows
   is not touched: only this script sets it.
3. Warns, aloud through `spd-say` and in its log, when `kernel.yama.ptrace_scope` is 2 or 3 (then even Wine's server
   may not read the game), and records the session (`$XDG_RUNTIME_DIR/poe2access-proton.env`) for the stop script.
4. Runs `proton waitforexitandrun ./Poe2Access.exe "Z:\…\PathOfExileSteam.exe" <its arguments>`: the mod's launcher in
   the game's prefix, outside Steam's runtime container, as the D2R script does. The launcher starts the game from that
   path, as it does on Windows when it is the launch option (`GameInstall.SplitArguments`, `GameLauncher.Start`), so
   the game and the mod share one Wine session and the game keeps Steam's launch environment. Steam's own command,
   with its container, is not run. Outside the container the bridges find the host's `libspeechd.so.2` (Steam Linux
   Runtime 4 has none) and the mod reaches the host's zenity.

Proton runs with fsync, under which the first zero-timeout wait of a process fails on a free mutex: the mod's
single-instance check (`Instance.TryOwn(0)`) took itself for a second copy, only started the game and stayed silent
(2026-11-11). `TryOwn` now looks a second time with a 100 ms wait, which answers right under fsync and costs Windows
that wait only when another copy really holds the mutex.

Steam shows nothing a launch option prints: the script logs to `app/proton-launch.log` and says its errors aloud. Run
by hand without Steam's command (a terminal, a shortcut), it asks Steam to start the game when the launch option is
set, which runs it again the proper way, and otherwise says to run `install-linux.sh`.

Why not `protontricks-launch`: it does not pass `WINEDLLPATH` to the program (the D2R mod's tester, 2026-11-09: the
bridges did not load and PRISM took the mute SAPI). The mod's launcher must not be started on its own under Proton
either: without a game command it asks Steam for the game, and Steam would run the launch option again.

The host needs Speech Dispatcher, Orca (Cthulhu included) or Spiel; PRISM's priority picks among them. PRISM's Orca
bridge talks to Orca's D-Bus service, which Orca 46 does not have: with it (Linux Mint 22.3, Ubuntu 24.04) the bridge
loads and Speech Dispatcher speaks instead, with Orca's own voice settings.

## Stopping

The mod stops by itself when the game closes. `sh poe2access-stop.sh` (beside `Poe2Access.exe`) stops it while the game
runs, as `Stop the mod.bat` does on Windows: it runs `proton runinprefix ./Poe2Access.exe stop` in the session the
launch script recorded (`proton run` goes through Proton's `steam.exe`, which waits for the game to close).

## Questions and errors (dialogs)

A Windows message box under Wine is a bare X11 window: Orca cannot read it, so the update question at start would
have stopped a blind player before the game. Under Wine, `Dialogs.Show` asks the Linux desktop instead
(`HostDialog`): it writes a small sh script and the message under `/tmp` (Wine's `Z:` drive), runs it with
`start.exe /unix /bin/sh …`, and the script opens zenity (`--question`, `--error`, `--info`, `--no-markup`) or kdialog.
`start.exe /wait` does not wait for a Unix program and its exit code is lost, so the script creates an "open" file when
the dialog opens and moves the exit code into an "answer" file when it closes; the mod polls them (15 s for the dialog
to open, an hour for the answer). With neither program installed the script answers "none" and the message box is
shown as before. Checked 2026-11-11: the text came through whole, with line breaks and quotes.

## Developing on Linux

`sh tools/linux-tests.sh` builds the solution (`-p:EnableWindowsTargeting=true`) and runs the unit tests under
Proton's Wine in a prefix of its own (`~/.cache/poe2access-tests-prefix`); `sh tools/linux-tests.sh prism` also says one
line through the built `prism.dll` and its bridges. Run natively, eight tests fail: they build Windows paths. The
release package itself is made by `tools/package/*.ps1` under PowerShell and needs `external/oodle/oo2core.dll`.

## Checking

- `app/Plugins/Poe2Accessibility/logs/accessibility.log` says `PRISM 0.18.3 initialised, backend: <name> (under Wine
  <version>)` when it works.
- Under Wine without a backend it says `PRISM 0.18.3: no speech backend under Wine: start the game from Steam with the
  launch option sh "<mod folder>/poe2access-proton.sh" %command% ...`, with the bridge folder's Unix path from
  `wine_get_unix_file_name` (`PrismLibrary.WineHint`), what `WINEDLLPATH` the mod sees, and each bridged backend's
  reason (`Orca not supported here; Speech Dispatcher unavailable; ...`).
- Under Wine the lines `PRISM log <level>: <source>: <message>` above it are PRISM's own: a bridge that did not load
  says why there.
- `app/Poe2Access.log` has the same for the startup speech, when the game data is regenerated.

## Updates

The updater writes each new file beside its target and moves it over (`Updater.Put`), so a library still mapped by a
process (a `.dll.so` that Wine loads without a Windows handle) is never truncated under it. The update runs before this
process loads PRISM, after the other copies are stopped; a file Windows refuses to replace fails the update and the
install is put back as it was.

## Upgrading PRISM

1. Download `prism-windows-x64.zip` and `prism-linux-x64.zip` of the **same** release into a new empty folder.
2. Replace `third_party/prism/`: `runtime/prism.dll` (Windows zip, `dynamic/release/bin/`), the placeholders
   (`runtime/`) and `runtime/wine/*.dll.so` (Linux zip, `dynamic/release/wine/`), `include/prism.h` and
   `prism_version.h`, `NOTICE` and `LICENSES/`.
3. Compare `PrismLibrary` with the new `include/prism.h`: `PrismConfig`, `PRISM_CONFIG_VERSION`, the signatures of the
   calls it makes and the `PrismError` values it names. `PrismTests` fails when the header's config changes.
4. `dotnet run --project tests/Poe2Accessibility.Tests -c Release`, then
   `dotnet run --project tests/Poe2Accessibility.Tests -c Release -- prism <plugin folder>` (loads that folder's
   `prism.dll`, lists the backends, says one line).
5. Update the hashes and the version above.
