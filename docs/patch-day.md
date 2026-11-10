# Patch day

What to do when Path of Exile 2 updates. Most of it happens by itself ([surviving game updates](compatibility.md)); this page
is for when something is said lost, and for checking a new build before anyone plays it. Where every field lives and what
protects it: [field protection audit](research/field-protection-audit.md).

## 1. What happens by itself
| When | What | Where it shows |
|---|---|---|
| `Poe2Access.exe` starts after the install changed | the catalogs made from the game's files and the extracted sounds are rebuilt in the same process (about 33 s on the first run, measured 2026-11-10: 57 s before the index was opened once for the batch and the object sounds and the terrain files were read bundle by bundle; a step that fails keeps the old file, see below; columns that moved are found by their content) | `Poe2Access.log`; `catalog/game-data-failed.txt`; spoken "Actualizando los datos del juego tras el parche…", then "Datos del juego listos", or "parte de los datos del juego…" for a failed step |
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

Every step gets a second opinion when it ends ([self-describing tables](compatibility.md#self-describing-tables)): each
column it read is judged from the bytes alone (text, key, list, flag, number) and each text file by how it starts. Run
`Poe2UiExtract dat-kinds "<game folder>" <table> [language] [Id or #row]` to see a table laid out column by column, with
one row's values: it is how a moved column is found again by hand.

| What the log says | Meaning |
|---|---|
| `bundle index` in the failed list; `Game data: bundle index failed: …` | the index would not open or does not look like the game's (no files, no `stats.datc64`): every step failed at once; fix the reader (`SharedBundles`, `BundleIndexCheck`) |
| `Table.Column near +0x…: not found` | the column moved beyond the search or its known row changed: `dat-strings` on the table, then the column's measured offset or known value in its `*Columns.cs` / `NameAnchors.cs` |
| `not a table this reader understands` | a `.datc64` whose length is not whole rows or has no marker: the file format changed |
| `… entries, under the N a catalog of this kind has` | the new file is under a quarter of today's count (`GameDataCounts.Today`): a column probably moved; raise the table's numbers only after reading the real output |
| `MISSING n of m cues` (sounds) | a bank or sample the cue list names is gone; the sounds folder is left as it was |
| `column changed kind: <table> +0x… is read as text, but its bytes hold a key` | the extractor still reads the column where it was, and something else sits there now: `dat-kinds` on the table, then the measured offset in the step's `*Columns.cs` |
| `… +0x0 is read as the Id, but its bytes hold display text` | a column was put before the Id: every reader of that table starts at the wrong place (`DatColumnSearch.IdColumn`, `RowOf`) |
| `<skill> no longer reads with …` (telegraphs), `do not give the known items their samples` (item sounds) | a known row leads somewhere else through a list or key column: the column is another of the same kind; update the offset, or the known row if the game really changed it (`DatAnchors`, `TelegraphExtractor.KnownLines`, `ItemSoundExtractor.Known`) |
| `the .ao format changed: n of m files read start as this reader knows (a version line up to 3)` (also `.ot`, `.act`, `.amd`, `.arm`, `.tmd`) | most files of that format open with a newer version line: read one with `Poe2UiExtract cat`, fix the patterns that read it, then raise its version in `GameFormats.Known` |
| `bundle index failed: … not the container this reader knows` | `_.index.bin` no longer starts with the bundle header LibBundle3 reads (sizes, block count, 256 KiB blocks): the library needs an update before anything is read (`BundleIndexCheck.HeaderProblem`) |
| `… .bank is not the FMOD bank this reader knows` | the bank is not RIFF "FEV " with an FSB5 archive of header version 0 or 1 whose sizes fit: `Fsb5.Problem`, `SoundExtractor` |
| `version N not read as a graph` (`.psg`) | the skill graph's layout changed, not only its version byte: `SkillGraphFile.Parse` against the new bytes |
| plugin log `Dat columns: X read at its compiled offset +0x…, which nothing judged` and the spoken "parte de los datos del juego, así que alguna lectura puede ser errónea" | `dat-columns.json` is missing, or lacks that column and says its table has another row size: rerun `dat-columns`; the offset is `GameLayout`'s |

An optional step that fails is not stamped: it is retried on the next starts, three per game build and step version
(`GameDataStamp.MaxAttempts`, stamp lines `tried`/`gaveup`), then left alone and still listed as failed until a mod update
raises its version or the next patch. A step that is not optional is retried on every start while its catalog is missing.

### Still judged by less than a known row
What a patch could move without the mod telling (2026-11-10), with the reason each is left:

- Which Id a table starts with: +0 is judged as text and as identifiers (no blanks), not as *the* Id; a second
  identifier column put first would pass. Only Characters, GrantedEffects and WorldAreas have their Id searched.
- A stat's key in memory is its row plus one: no file holds the key (a Stats row is an Id, flags, a type and a hash), so
  only memory can check it (key 1 must read the character's level; the plugin does not check it yet).
- Numbers a balance patch may change are used as known rows: the telegraph cone and rectangle sizes, the trigger delay
  1800, experience 525 and 4250334444. A rebalance fails `telegraph-*` loudly; `experience` reads on and says so.
- The telegraph tables' own Id (an int at +0 of GeometryAttack, GeometryTrigger, SpawnObject, EffectDrivenSkill,
  MonsterMortar) is read in place: a number has no kind to contradict. Every geometry skill must find its row through
  it, and the known lines must come out, so a move is heard as a failure.
- GeometryTrigger's markers and its animation: one known skill covers either, not each.
- The plugin's compiled row offsets (`GameLayout`, used without `dat-columns.json`) are judged by the table's row size
  only: two columns swapped inside the same row size are not seen. With the file they are not used.
- The data bundles' own headers, and the `.tdt`, `.xml` and `.ui` files: `.ui` is parsed, not pattern-matched, and its
  catalog has a count minimum; the others are read for names only.
- Gem requirements by level: GrantedEffectsPerLevel has a level (+0x10) and a float that rises with it (+0x6C); what the
  float means was not verified against the game, so `gems.json` does not carry it.

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
| A dat column (names, flags, keys) | `catalog/dat-columns.json`, made by `tools/Poe2UiExtract dat-columns` | rerun the extractor on the new install; `dat-kinds <table>` lays the table out | log `Dat columns: … where measured` |
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
