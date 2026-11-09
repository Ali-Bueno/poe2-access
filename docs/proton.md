# Linux: Steam with Proton (Steam Deck)

Speech under Wine or Proton, as the sister mods do it (the D2R mod's
[issue #5](https://github.com/Ali-Bueno/d2r-access/issues/5) and
[PR #6](https://github.com/Ali-Bueno/d2r-access/pull/6), its releases 1.0.11 and 1.0.12: heard there under Proton
Experimental on Arch with Cthulhu). Built and checked on Windows on 2026-11-08 and 2026-11-09; **not yet run under
Proton or on a Steam Deck**. The rest of the mod is untested there too: it reads the
game's memory from its own process (`ReadProcessMemory` on the game's process, in the same Wine prefix), and whether
Wine allows that, and finds the game's window and modules as on Windows, is **unverified**.

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
`Poe2Accessibility/Services/PrismLibrary.cs`: `PrismConfig` field for field from the shipped `prism.h`, filled by the
library's own `prism_config_init` into a 256-byte slot (a later, larger config cannot overrun it), and only the calls
the mod makes. The plugin's speech and the executable's startup speech both use it.

With the bridges missing, PRISM is not silent but **mute**: `acquire_best` takes Wine's SAPI, Proton's `protontts`
over eSpeak-NG, which reports a voice while the Steam runtime carries no eSpeak-NG data, so nothing sounds and nothing
fails. At 0.18.3 SAPI, UIA and Speech Dispatcher share priority 97 (NVDA 103, Orca 100, OneCore 98, Spiel 96) and SAPI
comes first, so it wins whenever Orca is missing (the D2R mod's tester, 2026-11-09). Under Wine the binding therefore
never calls `acquire_best`: `PrismLibrary.StartBridged` tries only Orca, Speech Dispatcher and Spiel, by priority, each
checked with `prism_registry_get` and `PRISM_BACKEND_IS_SUPPORTED_AT_RUNTIME` before `prism_registry_acquire`
(`ALREADY_INITIALIZED` counts as started), and when none starts it says why for each. It also sends PRISM's own log to
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

## Starting the mod

From Steam's Play button, with the script the package carries beside `Poe2Access.exe`. Unzip the mod anywhere in your
home folder, then in Steam: **Path of Exile 2 > Properties > General > Launch options**:

```sh
sh "/home/<you>/Poe2Access/poe2access-proton.sh" %command%
```

(the script's real path; `sh` in front because a zip made on Windows keeps no execute bit). What it does
(`tools/package/poe2access-proton.sh`):

1. Takes from Steam's command (`%command%`) the Proton Steam picked (the argument ending in `/proton`), the game's
   `PathOfExileSteam.exe` and its arguments; keeps Steam's `STEAM_COMPAT_*` and `SteamAppId`, with the usual defaults
   when one is missing.
2. Puts `app/Plugins/Poe2Accessibility/wine` in `WINEDLLPATH`.
3. Runs `proton waitforexitandrun ./Poe2Access.exe "Z:\…\PathOfExileSteam.exe" <its arguments>`: the mod's launcher in
   the game's prefix, outside Steam's runtime container, as the D2R script does. The launcher starts the game from that
   path, as it does on Windows when it is the launch option (`GameInstall.SplitArguments`, `GameLauncher.Start`), so
   the game and the mod share one Wine session and the game keeps Steam's launch environment. Steam's own command,
   with its container, is not run.

Why not `protontricks-launch`: it does not pass `WINEDLLPATH` to the program (the D2R mod's tester, 2026-11-09: the
bridges did not load and PRISM took the mute SAPI). The mod's launcher must not be started on its own under Proton
either: without a game command it asks Steam for the game, and Steam would run the launch option again.

The host needs Orca (Cthulhu included) or Speech Dispatcher running, or Spiel; PRISM's priority picks among them. Orca is
the route heard in the D2R test. Speech Dispatcher's bridge needs the host's `libspeechd.so.2`, which Steam's runtime
container lacks; the script runs outside it, so the host's copy should be found (unverified).

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
