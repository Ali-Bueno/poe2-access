# Plugin catalogs

Every file in this folder except this README ships with the plugin: the build copies the whole folder into
`Plugins/Poe2Accessibility/catalog/` (`DeployToHost` in `Poe2Accessibility.csproj`), and the package takes that same copy.
A new catalog needs no change to the project file or the package script, only a line here.

**Made by** names a `tools/Poe2UiExtract` command:
`dotnet run --project tools/Poe2UiExtract -c Release -- batch "<game folder>" <command> Poe2Accessibility/catalog/<file>`
(`item-names`, `monster-names` and `world-titles` are standalone commands: `-- <command> "<game folder>"`). The ones marked
**start** are also rebuilt from the player's own install by `Poe2Access.exe` on the first start and after each game patch
(`host/Poe2Access/Launcher/GameDataSteps.cs`); the shipped copy stands in until then. **Read by** is the plugin class
(`Poe2Accessibility/Services/`) that loads it.

| File | What it holds | Made by | Read by |
|---|---|---|---|
| `action-types.txt` | the game's action names | `action-types`, start | `AttackData` |
| `area-names.json` | area names per language | `areas`, start | `LandmarkTable`, `GameNewsNotes` |
| `attack-reach.txt` | how far each monster clip's blow reaches and lands | `attack-reach`, start | `AttackData` |
| `boss-mechanics.txt` | boss fights' hides, phases, adds, unhurtable buffs, movement rules, mechanic lines | `boss-mechanics`, start | `BossMechanicsCatalog` |
| `buff-names.json` | harmful buffs' names per language | `buff-names`, start | `HarmfulBuffs` |
| `class-mechanics.json` | class resources, limits, buff names, skill types | `class-mechanics`, start | `ClassMechanicCatalog` |
| `code-fields.txt` | what the game's code reads, to catch a field a patch moved | by hand ([exe layouts](../../docs/research/exe-layouts.md)) | `CodeFieldCatalog`, tests |
| `cull-skills.txt` | granted effects that deal Culling Strike | `cull-skills`, start | `CullSkills` |
| `currency-art.json` | the item each currency icon shows, for prices | `currency-art`, start | `CurrencyIcons` |
| `dat-columns.json` | the dat columns the plugin reads (all of them, or the step fails and the old file stays) and each table's row size, by which a compiled offset is judged | `dat-columns`, start | `DatColumns` |
| `dodge-timings.txt` | dodge timings measured in development | by hand, from `BossWatch` "Dodge timing" log lines | `DodgeTimings` (seed) |
| `endgame.json` | map objectives, contents, the Atlas's graphs | `endgame`, start | `EndgameCatalog` |
| `experience.json` | the total experience each level starts at | `experience`, start | `ExperienceTable` |
| `game-ids.txt` | the game's numbers for animations and stats | `game-ids`, start | `GameIds` |
| `game-keys.txt` | the Ids of the tables a player meets, to tell what a patch added | `game-keys`, start | `GameNews` |
| `game-names.txt` | the names the plugin's rules match (buff and animation Ids, trap and door scripts and states, boss tails), each found in the game's files; a name gone fails the step, which names it | `game-names`, start | none (the check is the step; `GameNames` lists them) |
| `gems.json` | skill and support gems by base item Id: kind, effects, skills, tags, attribute shares; names, tag names, support texts and skill names per language | `gems`, start | `GemCatalog` (skill names for `PlayerSkills`, the gems for `GemIndex`, which names the Build Planner's gems through `GemNames` and a moved gem without a tooltip) |
| `hazards.txt` | buffs from standing in harmful ground | `hazards`, start | `HazardGround` |
| `hit-times.txt` | when monster attacks land | `hit-times`, start | `DodgeTimings` |
| `important_tgt_files.txt` | named tiles (exits) | vendored (formerly GameHelper2's Radar table) | `LandmarkTable` |
| `inline-icons.txt` | the markup images texts show as pictures | `inline-icons`, start | `InlineIcons` |
| `item-bases.json` | every base a character wears or wields by base item Id: item class, level, base armour, evasion and energy shield; names per language | `item-bases`, start | `ItemBaseCatalog` (`IItemBases`: the base a build plan names) |
| `item-names.json` | currency item names for inline item art | `item-names` | `ItemNames`, `LandmarkTable` |
| `item-sounds.json` | item base id to the drop sample the game plays | `item-sounds`, start | `ItemDropSounds` |
| `landmarks.json` | areas `important_tgt_files.txt` lacks, and corrections to it | by hand | `LandmarkTable` |
| `map-hints.json` | GGG's map cues by area | by hand, from [map hints by act](../../docs/research/map-hints-by-act.md) | `MapHintTable` |
| `mod-stats.json` | every modifier by its Mods Id: its stats in the order its values follow, each with the least and most it rolls; the modifiers whose values are not told apart; the stats the description file says have no line | `mod-stats`, start | `ModStatCatalog` (`ItemModLines`: an item's modifier lines without its tooltip, the texts taken from `mod-texts.json`) |
| `mod-texts.json` | the game's stat description blocks (`stat_descriptions.csd`), a block a line: its stat Ids and, per language, its lines as the file holds them (conditions, text, handlers) | `mod-texts`, start | `ModTextCatalog` (`IModTexts`: a modifier line told in one language and said in another) |
| `monster-names.json` | the radar's boss names as the game names them | `monster-names` | `LandmarkTable` |
| `passive-tree.json` | the main passive tree by graph id: PassiveSkills Id and kind, links, class starts; node and class names per language | `passive-tree`, start | `PassiveTreeCatalog` |
| `places.json` | named map pieces and props from the terrain files | `places`, start | `PlaceCatalog` |
| `semantic-fields.txt` | the code reader's semantic rules, read with `code-fields.txt` | by hand ([exe layouts](../../docs/research/exe-layouts.md)) | code-field check, tests |
| `telegraph-effects.txt` | the effect files monster skills play, by `.ao` path | `telegraph-effects`, start | none yet |
| `telegraph-skills.txt` | what the game draws before a monster skill lands | `telegraph-skills`, start | `AttackData` |
| `trials.json` | the trials' icons and HUD formats per language | `trial-data`, start | `TrialCatalog` |
| `ui-catalog.json` | the menus, from the game's `.ui` scripts | `ui-catalog`, start | `UiCatalog` |
| `world-titles.json` | names objects show only in a WorldDescription | `world-titles` | `LandmarkTable` |
| `lang/<code>.json` | the mod's phrases in the game's other languages | `py tools/localization/phrases.py merge` | `Localization` |

The game's own sounds (`../sounds/`, gitignored) are extracted by the `sounds` command, deployed to the development host
only and never shipped; `../assets/` holds the mod's own recorded sounds, which ship.
