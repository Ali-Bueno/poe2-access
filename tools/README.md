# Tools

The build, package and release scripts, and research tooling that has to be re-run after every game patch. Nothing here
ships with the plugin.

## build.ps1 — build and check everything
```
pwsh tools/build.ps1                                     # Release: build, unit tests, phrases check
pwsh tools/build.ps1 -SkipTests                          # build (and deploy the plugin) only
pwsh tools/build.ps1 -Configuration Debug -ShowWarnings  # Debug, each warning listed
```
It builds `Poe2Access.slnx` (the host's projects, the plugin, the extractor, the tests; a plain `dotnet build` at the root
builds the same), so the plugin deploys itself to `host/Poe2Access/bin/<Configuration>/net10.0-windows/win-x64/Plugins/Poe2Accessibility/`
(`DeployToHost`: the dll, PRISM, every file of `Poe2Accessibility/catalog/` but its README, `assets/`, the extracted `sounds/`),
then runs `tests/Poe2Accessibility.Tests` and `py -B tools/localization/phrases.py check`. Plain text, one line a step
(`Build Release: 0 errors, 4 warnings.`, `Tests: 447 passed, 0 failed.`, `Phrases: 878 phrases, 8 languages, 0 incomplete.`);
the first failure prints its errors and `FAILED at step <name>: ...`, exit code 1. Warnings are counted for what was
recompiled. It refuses while the mod runs from that build folder. The steps live in `BuildSteps.ps1`, shared with the
package scripts.

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
Each output is written as `<outFile>.part` and renamed when complete; exit code 1 if any command failed. The mod
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

## Poe2Access — the mod's one executable
.NET 10 WinExe (`host/Poe2Access`, no console window, headless) that starts the game, updates the game data and runs the
host loop in the same process: no separate host or extractor process.

```
pwsh tools/build.ps1                                   # the executable (Poe2UiExtract is built in as a library), the plugin beside it
Poe2Access.exe [start|restart|stop|update|extract ...]  # start (default): open PoE2 via Steam if needed, update the data, run until the game closes
"<path>\Poe2Access.exe" %command%                       # in Steam's launch options: Steam's Play button starts the game and the mod
```
- `start`: single instance (mutex `Local\Poe2Access`; a second `start` only says it runs, or, from Steam, starts the game and waits
  for it), update check, game started if needed and its window awaited, game data regenerated, leftovers deleted, then the host
  loop on the main thread until the game exits or the stop event `Local\Poe2Access.Stop` is set.
- `stop` sets the event, gives the running instance 5 s to exit (it holds the mutex), then kills any other `Poe2Access.exe`, old
  `Poe2AccessHost.exe` or GameHelper copy whose image is in the install (the exe's folder or `app`), never the caller.
- `restart` stops the running instance, then this process becomes the host (the game is not started). `update` looks for a newer release.
- `extract <Poe2UiExtract arguments>` runs an extractor command in this process (`Poe2Access.exe extract batch <gameDir> areas out.json`).
- Started by Steam it gets the game's command (`PathOfExileSteam.exe --nopatch`) and runs that instead of asking Steam. A hand-opened
  instance with these options set asks Steam for the game; the one Steam then starts finds the first still running, starts the game and waits.
- Folders (`AppFolders.cs`): Plugins beside the exe when that folder exists (development build), else `app\Plugins` (package); the
  data (`Poe2Access.log` + `.old`, `version.txt`, `GameData`) lives next to Plugins. One log with the launcher's and the host's
  lines (4 MB cap, the previous one kept as `.old`); the per-minute `Frames:` line stays.
- Each `start`/`restart` deletes GameHelper's leftovers (`LegacyHost.cs`) and, in the package, every file at the top of `app\`
  the layout does not ship (`AppCleanup.cs`: only `*.log`, `*.log.old`, `version.txt`, `oo2core.dll` stay; folders `Plugins`, `GameData`),
  and the plugin folder's files no version ships any more (`AppCleanup.UnshippedPluginFiles`: `Prismatoid.dll`).
- The update writes each file beside its target (`.new`) and moves it over, so a library a process still maps (a Wine
  bridge's `.dll.so` under Proton) is never truncated in place; a file Windows refuses to replace rolls the update back.
- Elevation: with the Korean client, a game whose memory Windows denies the launcher (Steam run as administrator), or an instance left running as administrator, it runs itself again as administrator (`--parent <pid>` keeps the child from killing it).
- Tests may set `POE2ACCESS_INSTANCE=<suffix>` to run apart from the player's own instance (separate mutex and event).

`start` keeps the game data current (`GameDataSetup.cs`): it compares a stamp of the install (size and time of `Bundles2/_.index.bin`
and of the game exe, `fmod.dll` version) and of each step's version with `app/GameData/game-version.txt` (`GameDataStamp.cs`). It
reruns the steps whose stamp differs: all of them after a game patch or on the first run, only the ones whose version a mod update
raised otherwise. The catalog steps (`item-sounds`, `areas`, `dat-columns`, `experience`, `hit-times`, `hazards`, `attack-reach`, `action-types`, `telegraph-skills`, `telegraph-effects`, `buff-names`, `passive-overrides`, `class-mechanics`, `currency-art`, `inline-icons`, `cull-skills`, `game-ids`, `game-keys`, `places`, `endgame`, `ui-catalog`, `trial-data`) go to one `Extractor.Run(["batch", ...])` call, then `sounds` (rerun with
`item-sounds`) on its own, each on a worker task with 3 minutes per step (a step that overruns is logged and abandoned, the host
goes on); the extractor's output is redirected (`Say`) and only its `batch` result lines reach the log, and the memory is collected
afterwards. `oo2core.dll` is found beside the exe or in `app\` (`OodleLibrary.cs`, a `NativeLibrary` resolver for LibBundle3). Outputs
are written to a temp file or folder and replace the old ones only on success; each step is stamped as it succeeds (a failed one is
retried on the next start, an optional one is stamped anyway). The first regeneration takes about 53 s on the development machine
(it was 152 s): the batch opens the index once (about 7 s) and runs the catalogs 3 at a time (`Batch.MaxParallel`; 2 to 21 made no
difference), the tables are found through one lower-case path index per bundle index (`BundleFiles`: the old linear scan per table
open cost 55 s in `trial-data` and 75 s in `endgame`), then `sounds` (about 22 s). Peak memory about 2 GB (the parsed index), collected
before the host starts. While it runs the mod speaks through the plugin folder's `prism.dll` (`StartupSpeech`, `GameDataNarrator`): an
opening line, a progress line every 30 s ("Game data: 8 of 22") and a closing one, in the plugin's language (settings.txt, else the
game's, else the system's; Spanish and English in code, the others from `catalog/lang/<code>.json` by the English text; the three
phrases are in `StartupPhrases.cs`, listed by `phrases.py missing <lang>`); PRISM's backend and context are released before the plugin starts them again (`prism.dll` stays loaded: the plugin loads the same file). Everything stays in the mod's folder: the catalogs in `app/GameData/catalog`, copied over the plugin's shipped copies
on every start (an update or an unzip over the mod puts those back), the sounds in the plugin's `sounds/`; no package carries either,
so a mod update keeps them. **Raise a step's version in `GameDataSetup` when its output changes**, so that step alone reruns once on
every machine. The first start after the old layout (one `format=` number in `Plugins/Poe2Accessibility/catalog/game-version.txt`)
carries over what that stamp vouched for on the same install. The extractor opens the bundles sharing them with the running game
(`SharedBundles`): the game locks some once in game, and the mod starts it before regenerating. Two extractor processes at once can
lock each other's index out (a sharing violation); the next start retries.

## package — test build for other players, and releases
```
pwsh tools/package/New-TestPackage.ps1            # with the mod stopped; writes dist/package/Poe2Access-test-<date>.zip (+ .7z with 7-Zip)
pwsh tools/package/Publish-Release.ps1 -DryRun    # all a release does but the tag and the upload; prints tag, assets and notes
pwsh tools/package/Publish-Release.ps1            # the latest GitHub release, which every tester's install updates to
```
Both first run the build and checks of `build.ps1` (`New-TestPackage.ps1 -SkipTests` skips the tests and phrases), then
build the package (`BuildSteps.ps1`, `New-ModPackage`): the executable published under `<output>/build`, the plugin through
its own deploy step pointed into the package without the game's sounds (`-p:PluginDeployDir=... -p:DeployGameSounds=false`),
so a new catalog ships with nothing to list, then PRISM's licenses, research mode off, and a check of the layout the
updater and `AppCleanup.cs` rely on before anything is archived. It needs `external/oodle/oo2core.dll`.
The top folder holds `Poe2Access.exe` (one self-contained, single-file, not trimmed, uncompressed, ReadyToRun build, ~94 MB: the plugin
is loaded at run time and uses framework APIs the exe does not, so trimming is off), `README.txt`, the controls and the stop shortcut.
`app/` holds only `oo2core.dll` (bundle reading), `version.txt` and `Plugins/Poe2Accessibility` (dll, catalogs including
`important_tgt_files.txt`, research mode off, and PRISM v0.18.3 from `third_party/prism`: `prism.dll`, the Wine bridges'
placeholders `prism_{orca,speech_dispatcher,spiel}_bridge.dll` beside it, their Unix halves in `wine/*.dll.so`, its
licenses in `licenses/prism`; [Proton](../docs/proton.md)): nothing of the .NET runtime. All of it sits under `Plugins`,
which the start-up cleanup of `app/` never touches; the cleanup deletes the plugin folder's old `Prismatoid.dll`. No game sounds are shipped: the mod regenerates them, and
the catalogs, from the tester's install. The .7z (LZMA2, about 33 MB; the zip about 47 MB) no longer fits a 30 MB chat upload, and
Windows 11 opens it (Windows 10 needs 7-Zip or WinRAR). `dist/` is gitignored; publishing a release stays a manual decision.
A release refuses (a dry run lists it and goes on) a checkout not on `main`, uncommitted files (it names them), a branch
without an upstream or not in step with it, `gh` not signed in, a tag not newer than the latest release, or no commits
since it; it needs 7-Zip. The tag is `vYYYY.MM.DD`, with `-2`, `-3`... for later releases the same day (`ReleaseTag.cs`
parses it); it goes into `app/version.txt`. The assets are always `Poe2Access.zip`, `Poe2Access.7z` and `release.json`
(tag, the zip's SHA-256, notes: what the installed mod reads to update); after publishing it checks all three are on the release.
Updating an install made by the previous layout (`Poe2Access.exe` plus ~250 runtime files, `Poe2AccessHost.exe` and `Poe2UiExtract.exe`
in `app/`): the update copies the new package over it and the next start deletes what the new layout never ships from the top of `app/`.
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
