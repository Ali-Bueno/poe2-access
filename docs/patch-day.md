# Patch day

What to do when Path of Exile 2 updates. Most of it happens by itself ([surviving game updates](compatibility.md)); this page
is for when something is said lost, and for checking a new build before anyone plays it. Where every field lives and what
protects it: [field protection audit](research/field-protection-audit.md).

## 1. What happens by itself
| When | What | Where it shows |
|---|---|---|
| The launcher starts after the install changed | the catalogs made from the game's files and the extracted sounds are rebuilt (about 35 s; a step that fails keeps the old file) | `Poe2Access.log`; `catalog/game-data-failed.txt`; spoken "parte de los datos del juego…" |
| The host attaches to a new game process | the game's code on disk is matched against `catalog/code-fields.txt` | plugin log `Code fields: N of M read in … ms; moved: …` |
| The first three areas of a game process | the struct sweep and the shape searches judge every host struct; the plugin's probes judge the UI and entity fields | `Layout: <part> where measured / moved from … to … / NOT FOUND`, `Struct sweep: … intact, … moved, … lost` |
| Anything lost | said once ("Aviso de compatibilidad: no encuentro …"); a new build with nothing lost says "El juego se ha actualizado: el mod lo ha encontrado todo" | speech log, `config/game-build.txt` |
| New game content | new buffs, areas, screens are listed | `config/game-news.txt`, spoken once |

## 2. Before playing: check the new exe
1. Keep a copy of every build: `D:\code\decompilers\projects\poe2-research\builds\<build id>\PathOfExileSteam.exe` (the build id
   and the depot manifest are in Steam's `logs/content_log.txt`; an older build: `steam://open/console`, then
   `download_depot 2694490 2694492 <manifest>`, [how](research/exe-layouts.md#measured-on-five-builds)).
2. Run the code-field catalog over it: `py -I D:\code\decompilers\projects\poe2-research\re\cat_check.py` (edit the exe path at
   its top) or `py -I tools/exe_reader/reader.py <exe>`. Every line that moved or missed is a field to look at before the game
   is started.

## 3. After the first start: read the log
In `Plugins/Poe2Accessibility/logs/accessibility.log` look for `NOT FOUND`, `moved from`, `lost`, `does not match the
catalog`, `Aviso de compatibilidad`, and in `Poe2AccessHost.log` for `threw`. Then run the dev commands (write the word to
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
| The game state pointer, its stack and list, the in-game and loading states | `host/GameOffsets/StaticOffsetsPatterns.cs`, `GameStateOffsets.cs`; `GameStatesSearch` finds the pointer without the pattern, `GameStateLayout` and `GameStateRoles` judge the rest on every attach | `Layout: game state stack / list / in-game state …` | the state changes are logged at login |

Change one thing at a time, rebuild (`dotnet build host/Poe2AccessHost -c Release`, `dotnet build Poe2Accessibility -c Release`,
with the host stopped: `Poe2Access.exe stop`), restart (`Poe2Access.exe restart`), and read the same log lines again. Record
what moved and why it was found or not in [surviving game updates](compatibility.md), and the new build's results beside the
earlier ones in `research/evidence/`.
