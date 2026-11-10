# Patch day

What to do when Path of Exile 2 updates. Most of it happens by itself ([surviving game updates](compatibility.md)); this page
is for when something is said lost, and for checking a new build before anyone plays it. Where every field lives and what
protects it: [field protection audit](research/field-protection-audit.md).

## 1. What happens by itself
| When | What | Where it shows |
|---|---|---|
| `Poe2Access.exe` starts after the install changed | the catalogs made from the game's files and the extracted sounds are rebuilt in the same process (about 53 s on the first run, 18 s after a patch; a step that fails keeps the old file, see below; columns that moved are found by their content) | `Poe2Access.log`; `catalog/game-data-failed.txt`; spoken "Actualizando los datos del juego tras el parche…", then "Datos del juego listos", or "parte de los datos del juego…" for a failed step |
| The host attaches to a new game process | the game's code on disk is matched against `catalog/code-fields.txt` | plugin log `Code fields: N of M read in … ms; moved: …` |
| The first three areas of a game process | the struct sweep and the shape searches judge every host struct; the plugin's probes judge the UI and entity fields | `Layout: <part> where measured / moved from … to … / NOT FOUND`, `Struct sweep: … intact, … moved, … lost` |
| Anything lost | said once ("Aviso de compatibilidad: no encuentro …"); a new build with nothing lost says "El juego se ha actualizado: el mod lo ha encontrado todo" | speech log, `config/game-build.txt` |
| New game content | new monsters, skills, buffs, areas, map icons and screens are listed, each with what the mod will make of it from its catalogs (dodge data, buff rule or hazard, area name, icon kind, place in the UI catalog: `GameNewsNotes`) | `config/game-news.txt`, a short summary spoken once |

### When a step fails
A column that moved is found by its type, its shape and a known row's value (a name column by the stable name of one row,
`NameAnchor`); one that is not found, a table whose length is not whole rows, or a catalog that comes out under a quarter of
today's entries (`GameDataCounts`), under half of the previous file or without a section it had fails its step: the previous
file stays, `catalog/game-data-failed.txt` lists the step and the mod says "parte de los datos del juego". The reason, with
the table and column, is on the step's line of `Poe2Access.log` (`Game data: <step> failed in … s: <reason>`). A known-row
check that could not run because the row is gone is logged too (`its known row is not in the table`).

| What the log says | Meaning |
|---|---|
| `bundle index` in the failed list; `Game data: bundle index failed: …` | the index would not open or does not look like the game's (no files, no `stats.datc64`): every step failed at once; fix the reader (`SharedBundles`, `BundleIndexCheck`) |
| `Table.Column near +0x…: not found` | the column moved beyond the search or its known row changed: `dat-strings` on the table, then the column's measured offset or known value in its `*Columns.cs` / `NameAnchors.cs` |
| `not a table this reader understands` | a `.datc64` whose length is not whole rows or has no marker: the file format changed |
| `… entries, under the N a catalog of this kind has` | the new file is under a quarter of today's count (`GameDataCounts.Today`): a column probably moved; raise the table's numbers only after reading the real output |
| `MISSING n of m cues` (sounds) | a bank or sample the cue list names is gone; the sounds folder is left as it was |

An optional step that fails is not stamped: it is retried on the next starts, three per game build and step version
(`GameDataStamp.MaxAttempts`, stamp lines `tried`/`gaveup`), then left alone and still listed as failed until a mod update
raises its version or the next patch. A step that is not optional is retried on every start while its catalog is missing.

## 2. Before playing: check the new exe
1. Keep a copy of every build: `D:\code\decompilers\projects\poe2-research\builds\<build id>\PathOfExileSteam.exe` (the build id
   and the depot manifest are in Steam's `logs/content_log.txt`; an older build: `steam://open/console`, then
   `download_depot 2694490 2694492 <manifest>`, [how](research/exe-layouts.md#measured-on-five-builds)).
2. Run `powershell -File tools/Check-GameExe.ps1 <exe>` (2 s, the game not running): the static patterns (each must match
   once and point into writable data), `catalog/code-fields.txt` and the 80 rules of `catalog/semantic-fields.txt`. It exits 0
   when everything reads where it was measured. `MOVED` is a field the mod follows by itself at the start; `UNREAD`, `WRONG`
   with nothing read, or a pattern not right is what to look at before the game is started. On the five archived builds the
   patterns are right on all and the rules name the area fields that moved between them. `py -I tools/exe_reader/reader.py <exe>`
   reads 21 fields with rules of its own, as a second opinion.

## 3. After the first start: read the log
In `Plugins/Poe2Accessibility/logs/accessibility.log` look for `NOT FOUND`, `moved from`, `lost`, `does not match the
catalog`, `Aviso de compatibilidad`, and in `Poe2Access.log` for `threw`. Then run the dev commands (write the word to
`Plugins/Poe2Accessibility/debug/command.txt`): `structs` (the host sweep), `layout` (every plugin search started 0x18 off
target: each must come back; run it at login, character creation, in town near a quest NPC, in the options, the inventory,
the passive tree and with the left trigger held), `sounds` (what played), `costs` (what the frame costs).

## 4. Fixing what was lost
| What was lost | Where it lives | How to find the new value | How to confirm |
|---|---|---|---|
| A host struct field (area, entity, component, terrain) | `host/GameOffsets/Objects/**` (`[FieldOffset]`) | the search that judges it prints where it looked; `tools/computer_use/peek.py` reads memory (`hex`, `ptr`, `find`); the exe's code: [exe layouts](research/exe-layouts.md) | `structs` says intact; the feature heard live; `dotnet run --project tests/Poe2Accessibility.Tests` |
| A field the code catalog reads | also `Poe2Accessibility/catalog/code-fields.txt` (value at the current build) | update the value with the constant; a sequence that no longer hits: make a new one from a function that reads the field (`D:\code\decompilers\projects\poe2-research\re\`) | `Code fields: … moved: none` |
| A plugin offset (UI element, UI manager, in-game layer, control kind) | `Poe2Accessibility/GameLayout.cs` | `layout` says what it found instead; a UI dump (`dump`) shows the tree | `layout` all ok |
| A dat column (names, flags, keys) | `catalog/dat-columns.json`, made by `tools/Poe2UiExtract dat-columns` | rerun the extractor on the new install | log `Dat columns: … where measured` |
| An animation or stat id | `catalog/game-ids.txt`, made by `tools/Poe2UiExtract game-ids` | rerun it | the dodge warning and Rage read right |
| The game state pointer, its stack and list, the in-game and loading states | `host/GameOffsets/StaticOffsetsPatterns.cs`, `GameStateOffsets.cs`; `GameStatesSearch` finds the pointer without the pattern (host log `Game states: found by search …`: the mod works, the pattern needs remaking), `GameStateLayout` and `GameStateRoles` judge the rest on every attach, and the states are counted (`the game has N states`) | `Layout: game state stack / list / in-game state …`; `Check-GameExe.ps1` | the state changes are logged at login |

### A fix without a new mod version
A host struct field or a plugin offset that the code catalog names can be set by hand for one game build in
`Plugins/Poe2Accessibility/config/layout-overrides.txt` (the updater keeps `config/`), then `Poe2Access.exe restart`:

```
build 78440792                               # the game exe's size in bytes: lines count only for that build
AreaInstanceOffsets:PlayerInfo 0x5B8 0x5C0   # Struct:Field (dotted for nested structs), where the mod has it, where this build has it
layout:MapOverlayOffset 0xB90 0xB98          # a GameLayout offset by its code-catalog name
```

Each line is placed as the code catalog would place it: it replaces the code's reading of that field, every later field of
the struct moves with it up to the next field placed, a line whose first value is not the mod's is refused (the struct is
left as it is), and a field a live search already placed stays there. The plugin log says `Layout overrides: N for this
build …` and lists the move with the code fields (`moved: override …`). A new build ignores the lines, so put the fix into
the source afterwards.

Change one thing at a time, rebuild (`pwsh tools/build.ps1 -SkipTests`, with the host stopped: `Poe2Access.exe stop`), restart (`Poe2Access.exe restart`), and read the same log lines again. Record
what moved and why it was found or not in [surviving game updates](compatibility.md), and the new build's results beside the
earlier ones in `research/evidence/`.
