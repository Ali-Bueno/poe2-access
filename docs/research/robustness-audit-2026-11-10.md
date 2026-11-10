# Robustness audit (2026-11-10)

A read of the plugin for what makes it say something untrue, leak an internal name, or lose a feature to one bad read,
with the play logs of 2026-11-08 to 2026-11-10 beside it. What was fixed that day is listed first; the rest is open, most
player-visible first, each with where it lives.

## Fixed on 2026-11-10

- The passive tree's stick self-check reported a game update when it lost the rule (four times in two days, each "7 hit,
  3 missed"): no longer reported; it also skips pushes it cannot judge (a push it expects to move nothing, one whose pick
  changes ten degrees to either side, the first after the zoom or Y) (`UiSemantics.PassiveTree.JudgeStickLanding`).
- "lejos" for any way the rule could not count: now only when the way is longer than counted (`PassiveStick.Route`'s
  `ranOut`); a stuck or untrusted way is its side alone.
- A part found again before it was said missing was still said missing (`CompatibilityReport.Note`).
- One failed read of the allocated list read as "nothing allocated" (kept for two seconds now); a failed edge search
  silenced the tree for the session (three tries, ten seconds apart, now); the class's own start cached as none.
- "poise" missing after thirty bad ticks in total, however many areas (two areas must agree now); "walkable" on one
  window of thirty cells (two windows now) (`WorldLayoutWatch`).
- A held item said by its file path when its entity was already gone (`InventoryWatch.TickHand`): tried again for a
  second, then "unknown item".
- The scanner's "sin ruta" at a cut the player stood on (`EntityCatalog.Describe`): said as cut.
- A quest's name making a town's well the objective (`QuestTexts`, [ui layers](ui-layers.md)).

- Second pass, the same day (live on yarkey in G2_town, each heard in the log before and after):
  the build mark byte is distrusted when over a quarter of the nodes read marked (`BuildPlan.MarksPlausible`, reported as "passive build marks");
  L3 where the game uses it (a stash's or a merchant's "Edit search", the tree's filters) no longer says the new focus a second time
  (`L3Gestures.MovedFocus`, `ModCommands.OnGesture`); a merchant's tab changed with LB/RB from the inventory's side is said (`ScreenNarrator.TickTabKey`);
  a search kept across a tab change said "Search cleared" and never the new page's matches (`SearchHighlightWatch`: the page shown in place of a hidden one);
  a stash page with support gems said a bare "I", the tier drawn on the icon (`ScreenNarrator.IsTierNumeral`); gems under `Metadata/Items/Gem/` (Twister,
  Attrition, 593 more) were no gems to the move news and the loot filter (`BuildGems.IsGemId`). A merchant's search was tried and works.

## Open

| Where | What can go wrong | Smallest fix |
|---|---|---|
| `PassiveTreeFields.JudgeAllocated` | A class override node taken (Witch, Huntress, Druid) may fail the "only known ids" test and move the allocated offset onto another list | Know both rows' ids, accept 90% known, never move on a weak candidate |
| `UiSemantics.PassiveTree.PlacesOf` | Places are kept for the nodes visible at the first read: the ascendancy's 197 nodes get none | Rebuild when the count of visible node elements changes |
| `UiSemantics.PassiveTree` (`StickLinksOf`) | The row byte that makes the game skip the linked stage is read and ignored; the multiple-choice redirect is not modelled (the first push after an attribute popup lands one node off) | Empty links for such rows; skip them in the self-check |
| `BossWatch.WatchLife` | "Defeated" on one not-alive read; an allied unique is tracked as a boss | Two reads half a second apart; skip friendly entities |
| `AttackFieldCheck` | Verdicts last across a game restart within one host session | Reset beside `FlagBits.Reset` |
| `MonsterMods.Name` | A failed name read is cached empty for the process | Do not cache an unreadable row |
| `TradeOfferWatch` | Fixed child positions (grid, grid, title, title) | Find both grids by `TryReadGrid`, each title its nearest text sibling |
| `ComboBoxReader` | The first decoding that yields text is used for every dropdown | Latch after two dropdowns agree |
| `Beacon` | A route task that failed or ran out of budget is said like "no route yet" | Say it after two nulls; tell budget from unreachable |
| `FlagBits.EnabledTrusted` | Judged once, on the first screen | Judge again on later screens |
| `LayoutProbe` (entity names) | One bad batch of fifteen says "entity names" missing for the session | Judged only on success; missing after two failed batches |
| `EntityCatalog.Classify` (`ObjectNames.FromPath`) | An object no table names is said by its file's words, in English ("Chair") | Speak them only as a last resort, marked as unnamed outside English |
| `CullSkills.Load`, `HazardGround.Load`, `GameNews` | A catalog read with no IO guard: a locked file throws | The guard the other loaders have |

Not faults, by the logs: the vitals' thresholds re-arm with a margin (154 "Vida 50 por ciento" in two days of fights);
a targeted monster's name is said each time the target changes; the terms of use read their 248 paragraphs.
