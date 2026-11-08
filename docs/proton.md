# Linux: Steam with Proton (Steam Deck)

Speech under Wine or Proton, as the sister mods do it (the D2R mod's
[issue #5](https://github.com/Ali-Bueno/d2r-access/issues/5), its release 1.0.11). Built and checked on Windows on
2026-11-08; **not yet run under Proton or on a Steam Deck**. The rest of the mod is untested there too: it reads the
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

Steam's launch options (`"<path>\Poe2Access.exe" %command%`) do not apply under Proton: there `%command%` is a Linux
command line. Start `Poe2Access.exe` in the game's prefix with the bridge folder in `WINEDLLPATH`, from the mod's folder
(the one with `Poe2Access.exe`):

```sh
WINEDLLPATH="$PWD/app/Plugins/Poe2Accessibility/wine${WINEDLLPATH:+:$WINEDLLPATH}" \
  protontricks-launch --appid 2694490 ./Poe2Access.exe
```

This is the D2R reporter's chain (verified there with Grimdark, the Grim Dawn mod), with Path of Exile 2's app id.
`Poe2Access.exe` asks Steam for the game when it is not running (`steam://rungameid/2694490`); whether that reaches
Steam from inside the prefix is unverified, so starting the game from Steam first, then this command, is the safer
order: the mod finds the running game's window.

The host needs Speech Dispatcher (works with any reader), Orca or Spiel running; PRISM's own priority picks among them.

## Checking

- `app/Plugins/Poe2Accessibility/logs/accessibility.log` says `PRISM 0.18.3 initialised, backend: <name> (under Wine
  <version>)` when it works.
- Under Wine without a backend it says `PRISM 0.18.3: no speech backend under Wine: start the mod with
  WINEDLLPATH="<path>" ...`, with the bridge folder's Unix path from `wine_get_unix_file_name` (`PrismLibrary.WineHint`),
  and what `WINEDLLPATH` the mod sees, if any.
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
