# Tools

Research tooling that has to be re-run after every game patch. Nothing here ships with the plugin.

## Poe2UiExtract
.NET 10 console app that opens the Steam install's `Bundles2\_.index.bin` with `LibGGPK3.LibBundle3` and
extracts every `metadata/ui/**` script (`.ui`, `.xml`, ...) into an output folder. Read-only on the game.

```
dotnet build tools/Poe2UiExtract -c Release
tools\Poe2UiExtract\bin\Release\net10.0\Poe2UiExtract.exe "D:\games\steam\steamapps\common\Path of Exile 2\Bundles2\_.index.bin" external\poe2-ui
```
Needs `oo2core.dll` next to the exe (see below). Output lives in `external/poe2-ui/` (gitignored: game data).

### `item-names` command - currency names for inline item art
`dotnet run --project tools/Poe2UiExtract -c Release -- item-names "D:/games/steam/steamapps/common/Path of Exile 2"`
writes `Poe2Accessibility/catalog/item-names.json` ({ language: { idTail: name } }, BaseItemTypes Id +0x0 / Name +0x20);
it stops with an error when those columns move (find them again with `dat-strings`).

### `item-sounds` and `sounds` commands - game audio cues
Run in this order, with forward slashes (`dotnet` under bash eats backslashes):
```
dotnet build tools/Poe2UiExtract -c Release
dotnet tools/Poe2UiExtract/bin/Release/net10.0/Poe2UiExtract.dll item-sounds "D:/games/steam/steamapps/common/Path of Exile 2" Poe2Accessibility/catalog/item-sounds.json
dotnet tools/Poe2UiExtract/bin/Release/net10.0/Poe2UiExtract.dll sounds "D:/games/steam/steamapps/common/Path of Exile 2" Poe2Accessibility/sounds
```
`item-sounds` maps every item base id to the drop sample the game plays for it, from the BaseItemTypes, ItemVisualIdentity
and SoundEffects tables (`catalog/item-sounds.json`, committed like the other catalogs). `sounds` decodes a fixed cue list
(item, item_rare, currency, gold, quest_item, chest, shrine, exit, portal, waypoint, checkpoint, npc) plus every sample the
item table names (`items/<sample>.wav`) from `<game>\FMOD\Desktop\*.bank` (loose FMOD banks, not in the bundles) into
trimmed 16-bit WAVs plus `manifest.txt` (cue -> bank/sample). Uses the game's own `fmod.dll` read-only for Vorbis decoding
(no oo2core needed). The WAVs are gitignored game data. To change a cue, edit the `Cues` table in `SoundExtractor.cs`.
Details: `docs/research/game-audio.md`.

### `batch` command - several catalogs on one opened index
`Poe2UiExtract batch <gameDir> <command> <outFile> [<command> <outFile> ...]` runs `item-sounds`, `areas`,
`dat-columns`, `experience`, `hit-times`, `places` and `ui-catalog` (the `.ui` extraction plus `catalog`, through a temp
folder) side by side on one opened index (`Batch.cs`). Opening and parsing the index costs ~10 s and most of the ~2 GB a
run takes; LibBundle3 opens a bundle stream per file read and the commands only read the index, so they share it safely.
Each output is written as `<outFile>.part` and renamed when complete; exit code 1 if any command failed. The launcher
uses it after a game patch: 33 s for all seven instead of 85 s as separate runs (2026-10-28).

### `dat-strings` command - research aid
`Poe2UiExtract dat-strings <gameDir> <table> [language] [rows]` prints every string column of a table's first rows with its
byte offset (the same offset in a row in memory), English by default or a translated copy (`spanish`). It is how
`Characters` +0x08 was found to hold the localized class name ("Monje") read for the passive tree's class starts.

## ooz-shim — Oodle-compatible `oo2core.dll`
PoE2 links Oodle statically, so no `oo2core_9_win64.dll` is available. `oodle_shim.cpp` re-exports
`OodleLZ_Decompress` (the signature LibBundle3 P/Invokes) on top of the open-source decompressor
https://github.com/zao/ooz (`Ooz_Decompress` already has the same 14-parameter signature; codec is read from the
stream header so Kraken/Mermaid/Leviathan all work).

Build (VS 2022 Build Tools + CMake + Ninja):
```
git clone --recursive https://github.com/zao/ooz
copy tools\ooz-shim\oodle_shim.cpp ooz\
# CMakeLists.txt: add oodle_shim.cpp to libooz sources and set_target_properties(libooz PROPERTIES OUTPUT_NAME "oo2core")
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
cmake -S ooz -B ooz\build -G Ninja -DCMAKE_BUILD_TYPE=Release -DOOZ_BUILD_EXE=OFF -DOOZ_BUILD_BUN=OFF -DOOZ_BUILD_VALIDATE=OFF
cmake --build ooz\build --target libooz --config Release
```
The built binary is kept at `external/oodle/oo2core.dll` (gitignored). Decompression only; compression exports are stubs.

## Poe2AccessLauncher — one-click start for the player
.NET 10 WinExe (no console window, assembly `Poe2Access`) that replaces GameHelper's `Launcher.exe` for this project: no
update check, no key prompt, no visible window.

```
dotnet build tools/Poe2AccessLauncher -c Release     # also copies the exe + dlls beside GameHelper.exe
Poe2Access.exe [start|restart|stop] [--host <dir>]   # start (default): open PoE2 via Steam if needed, then restart the host
```
`restart` stops every process whose image is in the host folder (not `Launcher.exe`), deletes the previous random-named
copies, makes a new copy of `GameHelper.exe` with rewritten version-info strings and starts it with
`__COMPAT_LAYER=RunAsInvoker`. Single instance; steps go to `Poe2Access.log` next to the exe; a failure shows a message box
and exits 1. The host folder defaults to the exe's own folder when it holds `GameHelper.exe`, else the repository build output.
The GameHelper-style copy/rename logic is a short independent reimplementation (GameHelper is GPLv3).
In a test package `start` also keeps the game data current (`GameDataSetup.cs`): when `Poe2UiExtract.exe` sits beside it, it
compares a stamp of the install (size and time of `Bundles2/_.index.bin` and of the game exe, `fmod.dll` version) and of
each step's version with `app/GameData/game-version.txt` (`GameDataStamp.cs`). It reruns the steps whose stamp differs:
all of them after a game patch or on the first run, only the ones whose version a mod update raised otherwise. The
catalog steps (`item-sounds`, `areas`, `dat-columns`, `experience`, `hit-times`, `places`, `ui-catalog`) go to one
silent `batch` run of the extractor, then `sounds` (rerun with `item-sounds`) on its own, with 3 minutes per step,
logging to `Poe2Access.log`. Outputs are written to a temp file or folder and replace the old ones only on success; each
step is stamped as it succeeds (a failed one is retried on the next start, an optional one is stamped anyway). About
33 s for all of them (85 s as separate runs), about 15 s for one catalog, 2 s for the sounds alone. Everything stays in the mod's folder: the catalogs in `app/GameData/catalog`,
copied over the plugin's shipped copies on every start (an update or an unzip over the mod puts those back), the sounds
in the plugin's `sounds/`; no package carries either, so a mod update keeps them. **Raise a step's version in
`GameDataSetup` when its output changes**, so that step alone reruns once on every machine. The first start after the old
layout (one `format=` number in `Plugins/Poe2Accessibility/catalog/game-version.txt`) carries over what that stamp
vouched for on the same install. The extractor opens the bundles sharing them with the running game (`SharedBundles`): the game locks some
once in game, and the launcher starts it before regenerating.

## package — test build for other players
```
pwsh tools/package/New-TestPackage.ps1      # with GameHelper stopped; writes dist/Poe2Access-test-<date>.zip (+ .7z with 7-Zip)
pwsh tools/package/Update-GameHelper.ps1    # after a game patch: pull GameHelper2 upstream, rebuild host + plugin, run the tests
```
`Update-GameHelper.ps1` lists the new upstream commits (`-CheckOnly` stops there), refuses to pull over local changes in
`external/GameHelper2` or while the host runs from its build folder, and with `-Package` also builds the test package.
The top folder holds only `Poe2Access.exe` (the launcher as one self-contained, trimmed file, ~14 MB, left uncompressed so the archive compresses it better), `README.txt`,
the controls and the stop shortcut; GameHelper and Poe2UiExtract are published self-contained into `app/` (they share the
.NET 10 runtime files, nothing to install; the launcher finds `app/GameHelper.exe` and logs to `app/Poe2Access.log`), with only
the accessibility plugin (dll, PRISM, catalogs, the Radar plugin's
`important_tgt_files.txt`, research mode off), GameHelper's `core_settings.json` with only this plugin enabled,
`README.txt` (English instructions), the controls doc (`Controls.md`) and a stop shortcut (`Stop the mod.bat`). No game sounds are shipped: the launcher
regenerates them, and the catalogs, from the tester's install (`oo2core.dll` ships for bundle reading). GameHelper's bundled fonts (its "Universal font" option, off), the native PDB reader and `createdump.exe` are left out. About 45 MB zipped; the .7z (LZMA2, ~29 MB) fits a 30 MB chat upload, and Windows 11 opens it (Windows 10 needs 7-Zip or WinRAR). `dist/` is gitignored; publishing a release stays a
manual decision.

## localization — the mod's phrases in the game's other languages
Spoken strings are `T(english, spanish)` calls in `Poe2Accessibility/Announcements*.cs`; pt, fr, de, ru, th, ja, ko and
zh-TW look the English text up in `Poe2Accessibility/catalog/lang/<code>.json` (an interpolated phrase by its composite
format, "Life {0} percent", built at run time by `Phrase`). A missing entry is spoken in English.
```
py tools/localization/phrases.py check            # missing, stale and placeholder-mismatched entries per language
py tools/localization/phrases.py missing fr       # {english: spanish} still to translate
py tools/localization/phrases.py merge fr new.json  # add translations, drop stale entries
py tools/localization/phrases.py leaks            # holes that carry literal text (an English word would leak)
```
Run `check` after changing any phrase. A plural is two phrases ("1 step" / "{0} steps"), never a literal inside a hole.
