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

`object-sounds <gameDir> <soundsDir>` gives every chest-like object no material word names the sound the game gives it:
Chests row, its `.ao` files, the `open` animation's first sound in `SoundEvents`, that FMOD event played offline by the
game's own `fmodstudio.dll` (`EventRenderer`, 15 s for all) into `breakables/<name>.wav`, plus `breakables.txt` (object,
sound). In 0.5.5f: 617 of 682 objects, 140 sounds. The rest have none in the game (`Chests/Gold/invisible_chest.ao`, terrain
doodads) or are strongboxes, whose event sits outside the chest banks; they keep the common break. `object-links <gameDir>`
prints every object and its sound. It runs as its own process after `sounds`: it calls native code `sounds` does not,
and failing costs only these sounds.

Columns an extractor reads should be found by what they hold (`DatContentColumns.Nearest` with `IsStringListColumn`,
`IsKeyListColumn`, `IsKeyColumn`), or guarded by the table's row size (`HitTimeExtractor.Sized`): a wrong column must fail, not
write a wrong catalog.

To audition a bank before choosing a cue, `bank-samples <gameDir> <outDir> <bank,bank...> [filter]` writes every sample
whose name holds the filter as `bank__sample.wav` (first 4 s) and prints each one's length and peak; keep `outDir` in the
session scratch folder.

### `batch` command - several catalogs on one opened index
`Poe2UiExtract batch <gameDir> <command> <outFile> [<command> <outFile> ...]` runs `item-sounds`, `areas`,
`dat-columns`, `experience`, `hit-times`, `hazards`, `attack-reach`, `action-types`, `telegraph-skills`, `telegraph-effects`, `cull-skills`, `game-ids`, `game-keys`, `places` and `ui-catalog` (the `.ui` extraction plus `catalog`, through a temp
folder) side by side on one opened index (`Batch.cs`). Opening and parsing the index costs ~10 s and most of the ~2 GB a
run takes; LibBundle3 opens a bundle stream per file read and the commands only read the index, so they share it safely.
Each output is written as `<outFile>.part` and renamed when complete; exit code 1 if any command failed. The launcher
uses it after a game patch: 33 s for all seven instead of 85 s as separate runs (2026-10-28).

### `dat-strings` command - research aid
`Poe2UiExtract dat-strings <gameDir> <table> [language] [rows]` prints every string column of a table's first rows with its
byte offset (the same offset in a row in memory), English by default or a translated copy (`spanish`). It is how
`Characters` +0x08 was found to hold the localized class name ("Monje") read for the passive tree's class starts.

## exe_reader — struct offsets from the game's exe
`py -B -I tools/exe_reader/reader.py [exe]` reads offsets and component sizes out of `PathOfExileSteam.exe` as a data file, in about 7
seconds, from the names the game keeps for its own script language and the shape of the code around them. Run it after a patch, before
starting the game: a WRONG line is a field that moved or a rule the patch broke, and only a live check tells which. Keep the output
in `docs/research/evidence/`. Development only, needs `capstone` and `numpy`; the mod does not use it ([study](../docs/research/exe-layouts.md)).

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
.NET 10 WinExe (no console window, assembly `Poe2Access`) that starts the game and the mod's host (`host/Poe2AccessHost`,
headless) with no visible window.

```
dotnet build tools/Poe2AccessLauncher -c Release     # also copies the exe + dlls beside Poe2AccessHost.exe (host build output)
Poe2Access.exe [start|restart|stop] [--host <dir>]   # start (default): open PoE2 via Steam if needed, then restart the host
"<path>\Poe2Access.exe" %command%                    # in Steam's launch options: Steam's Play button starts the game and the mod
```
Started by Steam, the launcher gets the game's command (`PathOfExileSteam.exe --nopatch`, from Steam's app info), so it runs
that instead of asking Steam, and it stays until the game closes: Steam counts the game running while that launcher lives.
A launcher opened by hand with these options set asks Steam for the game; the one Steam then starts finds the first still
running (the single-instance mutex), starts the game and waits, and the first goes on to the host once the window is up.
`stop` sets the host's named event `Local\Poe2AccessHost.Stop`, gives it 5 s to exit, then kills it; every other process whose
image is in the host folder (an old GameHelper copy) is killed outright. `restart` stops, deletes what packages before the own
host left (`GameHelper.exe`, its random-named copies listed in `Poe2Access.copies.txt` / `tempFileLocations.dat`, the overlay's libraries such as ImGui and ImageSharp, and its `configs`, `fonts` and `Localization` folders; `LegacyHost.cs`)
and starts `Poe2AccessHost.exe` plainly (no window; its manifest asks for no administrator rights). Single instance; steps go
to `Poe2Access.log` next to the exe; a failure shows a message box and exits 1. The host folder defaults to the exe's own
folder when it holds `Poe2AccessHost.exe`, else `app` beside it, else the repository build output
(`host/Poe2AccessHost/bin/Release/net10.0-windows/win-x64`).
In a test package `start` also keeps the game data current (`GameDataSetup.cs`): when `Poe2UiExtract.exe` sits beside it, it
compares a stamp of the install (size and time of `Bundles2/_.index.bin` and of the game exe, `fmod.dll` version) and of
each step's version with `app/GameData/game-version.txt` (`GameDataStamp.cs`). It reruns the steps whose stamp differs:
all of them after a game patch or on the first run, only the ones whose version a mod update raised otherwise. The
catalog steps (`item-sounds`, `areas`, `dat-columns`, `experience`, `hit-times`, `hazards`, `attack-reach`, `action-types`, `telegraph-skills`, `telegraph-effects`, `cull-skills`, `game-ids`, `game-keys`, `places`, `ui-catalog`) go to one
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
pwsh tools/package/New-TestPackage.ps1      # with the host stopped; writes dist/package/Poe2Access-test-<date>.zip (+ .7z with 7-Zip)
```
The top folder holds only `Poe2Access.exe` (the launcher as one self-contained, trimmed file, ~14 MB, left uncompressed so the archive compresses it better), `README.txt`,
the controls and the stop shortcut; the host (`host/Poe2AccessHost`) and Poe2UiExtract are published self-contained into `app/`
(they share the .NET 10 runtime files, nothing to install; the launcher finds `app/Poe2AccessHost.exe` and logs to
`app/Poe2Access.log`), with only the accessibility plugin (dll, PRISM, catalogs including `important_tgt_files.txt`, research
mode off), `README.txt` (English instructions), the controls doc (`Controls.md`) and a stop shortcut (`Stop the mod.bat`). No
game sounds are shipped: the launcher regenerates them, and the catalogs, from the tester's install (`oo2core.dll` ships for
bundle reading). The runtime's native PDB reader and `createdump.exe` are left out. The .7z (LZMA2, about 35 MB since the recorded item sounds; the zip about 51 MB) no longer fits a 30 MB chat
upload, and Windows 11 opens it (Windows 10 needs 7-Zip or WinRAR). `dist/` is gitignored; publishing a release stays a
manual decision. Updating an install made before the own host: the launcher keeps `app/Plugins/Poe2Accessibility/config`
and the extracted sounds, deletes the loose files in `app/` the new package lacks (GameHelper and its libraries, keeping
`*.log`) and its old folders (`Updater.Prune`). An update made by a launcher from before the own host copies without
deleting, so every start also deletes GameHelper's exe, copies, libraries and folders by name (`LegacyHost.cs`).

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
