# Unseen panels: inventory, widget coverage and plan (0.5.5f, build 26476662, 2026-11-08)

> Every game screen the mod has not been heard reading, found from the game's own files, with the widgets each one
> holds, whether the shared archetype readers cover them, what was added for them and how to check them live. Endgame
> mechanics (atlas, map device, waystones) belong to [endgame-ui](endgame-ui.md); ascendancy trials to the trial notes.
> This note keeps to widget reading. Confidence: PROVEN (seen in game or in the game's files), LIKELY (inferred from
> the game's strings and panels of the same build seen live), UNSURE (a guess to check).

## Where the panels come from

- PROVEN: the UI layout scripts cover few panels. The bundles hold 144 `metadata/ui` files (`Poe2UiExtract paths <game>
  metadata/ui/`): 76 help articles, 20 login/creation variants, the HUD, the game menu, the death screen, the character
  summary, five confirmation-type windows (`confirmationwindow`, `firmwarningconfirmationwindow`, `buymarkettabwindow`,
  `waitingfortradewindow`, `createsteamaccountwindow`), the help panel frame and `uisettings*.xml`. The catalog
  (`catalog/ui-catalog.json`, 126 parsed sources) keeps only controls with an `id`, so templated children (`Label based
  on CONTENT`) are absent. Inventory, stash, vendors, benches, exchange, ritual, expedition, guild stash, ladder,
  instance manager, hideout editing and every league window are built in code (no layout file, no element names: the
  dumps show `name=""` under `panels_parent`).
- PROVEN: the exe keeps no RTTI for UI classes (170 class names in all, none a panel). Its UTF-16 ids name a few
  native windows: `brequel_exchange_window`, `brequel_skill_tree_window`, `loreweave_exchange_window`,
  `verisium_smithing_window`, `display_case_window`, `mystery_box_window`, `mtx_combine_window`, `reclaim_mtx_window`,
  `rename_window`, `unstack_window`, `instance_chooser_window`, `async_trade_price_item_dialog`, `challenges_panel`.
- PROVEN, the inventory used here: ClientStrings ids ending in `WindowTitle`/`PanelTitle`/`ScreenTitle`/`PanelName`
  (102 rows) plus the titles of the benches and windows that carry another id (`CurrencyExchangeTitle`,
  `RecipeBenchTitle` "Reforging Bench", `SalvageBenchTitle`, `UnveilingUITitle` "The Well of Souls",
  `SentinelRecombinatorTitle`, `InstanceChooserTitle`, `HideoutEditControl*`), each with the strings the window shows
  (buttons, legends, column headings). Dump: `Poe2UiExtract dat-row-strings <game> ClientStrings "" english`.
- PROVEN: a full in-game dump stops at 20,000 nodes inside `panels_parent` (`ui-tree-InGameState-20261108-063118`: 19
  panels, 3 dumped), so dumps cannot list the panels; use `dump 0x<panel>` for one panel.

## Inventory (110 titles)

Counts are panels sorted by hand from the 102 title rows and the 8 other titles (one panel can hold several titles,
the options tabs and social pages are counted as panels of their own).

| Group | Count | Panels |
|---|---|---|
| Seen and read | 27 | login, creation, character select, terms, league migrations, game menu, options (Graphics, Sound, UI), inventory, stash, vendor buy/sell, disenchant, NPC dialogue + reward, quests/world map, waypoint and checkpoint maps, passive tree, skills + bind screen, gemcutting, character, social (friends, guild page), challenges, cosmetics, shop, trade market + search, salvage bench, death screen, radial menu, chat |
| Unseen, the current character can reach | 14 | options Input / Game / Notifications / Controller tabs, item filter list, Instance Manager, help panel, ladder, events, social party tab and invites, player trade, report window, value editor / split stack, shop windows (purchase, reclaim, combine, salvage box, equip cosmetic) |
| Unseen, later in the game | 19 | Ritual favours, Reforging Bench, Currency Exchange (+ currency selection, orders), Runeforging (Verisium anvil, Act 3), Well of Souls, Recombinator, guild stash (+ tab permissions), hideout selection and editing, guild hideout, Expedition merchants (haggle, gamble, deal, exchange), Expedition locker, async-trade offer menu and tab time, Temple Console; endgame and trial windows are listed in their own notes |
| Not in PoE2's game (other platforms, PoE1 leftovers) | 50 | Labyrinth (map, altar, font, darkshrine), Cadiro, talisman, Heist, Harvest, Incursion 1, Sentinel, Lake of Kalandra, Royale, Archnemesis, Blood Crucible, Kirac's vault, Bestiary, Tencent/PS4/mobile/console-only windows, the hideout stash, card trade |

## Unseen panels, one by one

Widgets come from the window's strings (PROVEN as strings) and the web (poe2wiki, poe2db; LIKELY as layout).
"Generic" says what the shared readers do with them today.

| Panel (how it opens) | Widgets | Generic reader | Confidence | What a blind player needs |
|---|---|---|---|---|
| **Ritual favours** (`RitualRewardWindowTitle`; A on a ritual altar once the area's rituals are done, or the HUD's favours button) | item grid with a cost in Tribute per item, tribute counter "{0} Tribute", "{0} Rituals Remaining", Defer Item, Reroll (once per area, cost popup), Apply Item Filter | grid cells read as a vendor's (item, column, row, tooltip); the cost's picture is the game's `RitualTribute` (`killpointicon`): now said as "250 tributo, no alcanza" after the name (`InlineIcons.PriceUnit`); counters are panel notes | LIKELY | price and whether it is affordable, the tribute left, which favours are deferred (`ritual_suspended` picture: "reservado") |
| **Reforging Bench** (`RecipeBenchTitle`; in Act 3 towns after "Treasures of Utzaal") | three input slots, "Reforge" button, "Destroys three similar items…" | side panel found by its first label (as the salvage bench); each slot reads "vacío, columna 1, fila 1" if it is a 1×1 grid | LIKELY, slot wording UNSURE | which slot is which (1 of 3), what went in, what came out (`SalvageOutcome`-style news) |
| **Currency Exchange** (`CurrencyExchangeTitle`; Ange, Act 4 / hideout) | "I Want" / "I Have" pickers ("Click to Select" until chosen), Currency Selection window (grid of currencies, search), amounts, market ratio "<<market_ratio>> Market Ratio", gold cost "<<village_gold>> {0}", ratio/stock list, Place Order, orders list (collect, close order: gold forfeited) | pickers: label from the row ("Quiero"), value now the currency once chosen (`ArtCaptions` from currency-art.json); gold cost "500 oro" (`InlineIcons`); ratio/stock rows "Tasa: 1:5, Existencias: 200" (`TableHeadings`) | LIKELY | the two currencies and amounts, the ratio against the market, the gold cost, each order's state |
| **Well of Souls** (`UnveilingUITitle`; Abyss league hub) | one item slot, Reveal, three modifier choices, Confirm | slot as an item slot; choices as a list of buttons with captions | LIKELY | the three mods in full, which one is chosen |
| **Runeforging** (`VerisiumSmithingWindowTitle`; Verisium anvil after Farrow's quest, Act 3) | item slot, Forge, info text | as the salvage bench | LIKELY | cost in Verisium (a currency item: named by `CurrencyIcons`) |
| **Recombinator** (`SentinelRecombinatorTitle`, `RecombinatorUI*`) | two item slots, modifier list with select/unselect legends, success chance, toggle "Unpredictable Recombination" | mod rows read as rows; selected state not known | UNSURE | which mods are selected, the chance |
| **Expedition merchants** (Tujen haggle, Gwennen gamble, Rog deal, Dannig exchange) | vendor grids priced in artifacts; haggle: an offer amount and Confirm, "New Offer"/"Same Offer" notices; gamble: "<<bag_icon>>{0}/{1}" stock; deal: Upgrade / Take Item | grids and prices as vendors (artifacts are currency items: named); the bag picture is not named (no word for it yet) | LIKELY, haggle control UNSURE | price, the offer and its answer, stock left |
| **Guild stash** (`GuildStashPanelTitle`) | stash tabs and grids, "Your guild does not yet have a stash", tab permissions (member type × add/view/remove check boxes) | as the stash; permissions as check boxes in rows | LIKELY | the same as the stash; permission rows' column (`TableHeadings` if headed) |
| **Instance Manager** (`InstanceChooserTitle`; hold A on an area exit: the "hold for options" prompt) | rows: players, time left, created, realm, with headings and subheadings; Enter / New; "You are about to enter {0}" | rows now "Jugadores: 3, Tiempo restante: …" (`TableHeadings`, subheadings dropped) | LIKELY | which instance, how full, how long it stays |
| **Ladder / Events** (`LadderPanelTitle`, `EventsPanelTitle`; PC keys, maybe no pad route) | ladder table (Rank, Name, Class, Level, Points…), filters; events list, PvP tab | rows with headings (`TableHeadings`), up to 10 cells when headed (6 otherwise) | UNSURE (reach), LIKELY (rows) | rank and class per row |
| **Help panel** (`TutorialPanelName`; keyboard H) | topic list (left), article (right): TITLE / CONTENT / SUBTITLE labels and captioned pictures, videos (`tutorialhelper.ui`) | topic buttons read; the article's labels arrive with the focus move and are read after the topic, all at once | LIKELY | the article paragraph by paragraph (L3 steps lines only in tooltips today) |
| **Options: Input, Game, Notifications, Controller** | rows of label + key buttons (Input), combos and check boxes, the item filter list (`OptionsItemFilterFileList`) + Reload | options rows (label, value, position) as on the seen tabs | LIKELY | key rows: the action, then each bound key |
| **Item filter selection** (Options > Game) | combo of filter files, Reload button, "Item Filter loaded successfully." | combo; the load result as a message | LIKELY | the chosen filter, the load result |
| **Social: party tab, invites, recent players** | party invite box, search bar, party list rows, accept/decline | text fields with placeholders, rows | LIKELY | who is in the party, the invite's buttons |
| **Player trade** (`TradeWindowTitle`) | two grids (yours, theirs), accept, the waiting window (`waitingfortradewindow.ui`: message + cancel) | the other side's offer said as it changes (`TradeOfferWatch`, as disenchanting) | LIKELY | each item the other player puts in or takes out |
| **Report, value editor, split stack, rename** | text field / number field + OK/Cancel | text fields with labels | LIKELY | the number and its bounds |
| **Shop windows** (purchase, reclaim, combine, salvage box, equip cosmetic) | confirmation windows, cosmetic grids | confirmations (seen archetype), cards | LIKELY | price and what is bought |
| **Hideout editing** (`HideoutEditControl*`: Decorations, Import, Export, Reclaim All, Music) | decoration grid with counts, then placing an object in the world with the cursor | the lists read; placing is spatial | LIKELY (lists), deferred (placing) | out of scope: placing needs a spatial helper |
| **Death screen variants** (`deathscreen.ui`) | title, respawn, spectate, respawn at checkpoint, revive at checkpoint (map revives) | buttons | PROVEN layout, LIKELY reading | the revives left (party `<<party_revives_icon>>` now "resurrecciones") |

## What the shared pipeline lacked, and what was added (2026-11-08)

1. **Inline pictures in texts** (PROVEN gap): the game's stripped copy drops `<<token>>` images, so "500" was said for
   "<<village_gold>> 500" (Currency Exchange order cost, `CurrencyExchangeGoldCost`; ascendancy "not enough gold",
   `AscendancySelectorWindowInsufficientGoldPopup`), "30" for the spirit tab's "30 <<spirit_resource_icon>>", and a
   ticked splinter ("<<TickIcon>> Name", `SplinterInfoComplete`) sounded like an unticked one. The game's own table is
   `uisettings.xml` `<Props id="MarkupImages">` (187 tokens: 21 pad glyphs as `Controller/Button4`, the rest
   `Art/2DArt/UIImages/...`); `Poe2UiExtract inline-icons` writes it to `catalog/inline-icons.txt`. `InlineIcons` names a
   token by its art's file stem: gold and tribute (prices), spirit, map portals, revives (units, said after their number),
   tick, check box on/off, deferred favour (states, said after the line), weapon set I/II/III (in place); a word the line
   already says is not repeated. Pad glyph tokens the mod's own list lacks are named from the same table, so a glyph
   token a patch adds is read. `ControllerGlyphs.Plain`/`Render`, `UiElementReader.DecodeText`. Unit-tested on the
   game's strings (`PanelWidgetTests`).
2. **Price pictures that are not items** (PROVEN table, LIKELY use): a tooltip's cost drawn with the tribute or market
   gold picture is a price unit like a currency item (`UiSemantics.Tooltips` → `InlineIcons.PriceUnit`).
3. **Icon-only controls** (LIKELY gap): a control with no caption, no id and no tooltip that shows a currency's art is
   named by it (`ArtCaptions` over `catalog/currency-art.json`; `UiSemantics.Panels.ArtCaption`): the exchange's pickers
   once a currency is chosen. Only when nothing else named the control.
4. **Table rows under column headings** (PROVEN that the tables exist: `InstanceChooser*ColumnHeading`,
   `LadderColumn*`, `CurrencyExchangeRatioText`/`StockText`; LIKELY layout): a row of a list whose block before the list
   is a shown row of plain labels says each cell with its heading. Columns are cut halfway between neighbouring
   headings, subheadings and titles over several columns are dropped, the headings must be one row and within one of
   the row's cell count. Guards against seen panels: the character panel's stat lists keep a hidden English "Name |
   Value" header row before the rows (dump 2026-11-08: header frame `vis=-`, children visible), so hidden blocks are
   skipped; a block holding anything navigable (the friends list's check boxes) is no heading row. `TableHeadings`
   (pure, tested), `UiSemantics.Panels.HeadedCells`, hooked in `UiSemantics.RowText`.

## Still open (plan)

- 1×1 bench slots: a single-cell grid says "columna 1, fila 1"; among sibling slot grids it should say "1 de 3". Needs
  the grid's size (cell count from the server inventory, `InventoryReader`, Opus) — UNSURE until a bench is dumped.
- Help articles: read the article paragraph by paragraph with the L3 line gesture, as tooltips are (the article is a
  stack of labels, `tutorialhelper.ui` templates) — UNSURE how long articles arrive; check in game first.
- The gamble stock's bag picture (`bag_icon`) has no word; the Expedition haggle offer control is unknown.
- Launcher step: `inline-icons` is in the extractor's `batch`; the launcher's `GameDataSetup` (moving to
  `host/Poe2Access`) needs one step `inline-icons → catalog/inline-icons.txt` so the table is regenerated after a patch.

## Live protocol

Speech log = `Plugins/Poe2Accessibility/logs/accessibility.log`; take `dump 0x<panel>` (dev command) of each new panel.

| Panel | Steps | The log must show |
|---|---|---|
| Inline icons (any) | start the plugin | "Inline icons: 187 markup images, 12 named" |
| Skills > spirit tab | open Skills, switch to the spirit management tab | "30 espíritu" (not a bare "30"), no "espíritu espíritu" |
| Instance Manager | in town, hold A on an area exit | "Jugadores: N, Tiempo restante: …" per row; Enter / New buttons |
| Friends (regression) | Social > Friends, move over a friend | "yarkidius#7485, Campamento…" with no heading words |
| Character stats (regression) | Character, R3, move in a stat list | "Vida máxima, 513" unchanged, no "Name:"/"Value:" |
| Options Input / Game / Notifications / Controller | game menu > Options, RB to each tab | tab name, then each row "label, value, role, k de n"; key rows say the key |
| Item filter | Options > Game > Item Filter, pick one, Reload | the combo value, then "Filtro de objetos cargado satisfactoriamente." |
| Help panel | keyboard H (if no pad route) | the topic, then the article's paragraphs |
| Party tab | Social > Party | invite field with its placeholder, rows of members |
| Ritual favours | an area with a ritual altar after its rituals | "Favores", items "…, 250 tributo" after the name, "no alcanza" when short |
| Reforging Bench / Runeforging / Well of Souls | Act 3 town / hub | the bench's title before the first slot, the slots, the button |
| Currency Exchange | Ange (Act 4 or hideout) | "Quiero, Haz clic para seleccionar", after choosing "Quiero, Orbe exaltado", "500 oro", "Tasa: …, Existencias: …" |

The current character (level 22 Mercenary, Act 2) can check now: the inline-icon load line, the spirit tab, the
Instance Manager, the two regressions, every options tab, the item filter, the help panel (keyboard), the party tab,
the shop windows and a player trade with a second account. Ritual favours need an altar in a ritual area (Act 1
Freythorn again, or a later ritual); the benches, the exchange and the guild stash need Act 3, Act 4 or a guild.
