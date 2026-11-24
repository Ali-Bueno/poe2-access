# Strongbox (and Essence) currency: quick-use buttons, the gamepad hold, and what the mod must say

Research of 2026-11-23 (patch 0.5.5f). Sources, in order of weight: the game's own files as already
dumped in [league-mechanics.md](league-mechanics.md) §6 (install of 2026-11-14, ClientStrings MEASURED);
GGG's 0.2.0h patch summary and patch notes (May 2025, web, quoted below); the play log of 2026-11-18
(`accessibility.log`, the owner's session); one Reddit answer of 2025-01-19 for the console flow (web,
corroborated by the game string). Confidence marks follow league-mechanics.md: **MEASURED** (seen in a
file or the log), **LIKELY** (consistent, not seen running), **UNSURE**.

The gap (the owner, 2026-11-23): out in the world a sighted player right-clicks currency onto a
strongbox to change its rewards before opening it. On a controller there is no right-click. What does
the game offer instead, and which of its tooltips must the mod speak?

## 1. Two different systems, not one

"Quick-use currency" is **not** the controller substitute for right-clicking. There are two systems:

**(a) The gamepad hold — "Hold: Apply Currency"** (LIKELY since launch — the 2025-01-19 console report below already describes it; the controller path).
ClientStrings carries `GamepadInteractInfoStrongboxAltOnly` = "Hold: Apply Currency" /
"Mantener presionado: Aplicar objetos monetarios" (MEASURED, league-mechanics.md §6). Standing at a
strongbox on a gamepad: **tap** the interact button opens it (the ambush starts); **hold** the interact
button instead opens "a small inventory panel view which will let you use crafting currency you hold
or wisdom scrolls to identify" (Reddit, PS5, 2025-01-19, web — consistent with the string and with a
forum report of the same week: "I tried holding the A button, but only my inventory popup…"). What the
hold does in the current build was measured later the same day (§4c): it moves the game's focus into the
quick-use button row under the label — the "small inventory panel" of the launch-era reports is that row
plus its legend (Select A, Back B, **Inventory X**). The full manual range (chaos, vaal, …) is reached
through that Inventory entry — or by opening the backpack while the box is the interact target by any
road: every currency the box accepts then reads "Apply (Strongbox), X" on its cell and that X applies
it straight to the box (§4d, measured with a Vaal Orb). As on a mouse, corrupting a strongbox with a
Vaal Orb makes everything it drops Corrupted and locks it against any further currency (poe2wiki
"Strongbox").

**(b) Quick-use currency buttons** (0.2.0h, May 2025; all input methods, a shortcut).
GGG, patch summary: *"We've added Quick-Use buttons that appear below the labels of Strongboxes and
Essences. For Strongboxes, the currency displayed is dynamic based on the state of the Strongbox and
is only displayed if that currency is in your inventory. As an example, if the Strongbox is
unidentified it will only display a Wisdom Scroll. If it is Normal Rarity it will display an Orb of
Transmutation, if it has one magic modifier it will display an Orb of Augmentation. It will also be
able to have buttons for Orbs of Alchemy, Exalted Orbs, and Regal Orbs. We have intentionally not
included currency like Divine Orbs as that would be a mistake to use. Essences have also received the
same change but with Vaal Orbs for corrupting them into the Corrupted Essences."* Patch notes:
*"Added quick-use Currency buttons to Strongboxes and monsters imprisoned in Essences. These buttons
will only be visible if you have the currency items in your inventory."*
The buttons are currency icons in a row under the world label (preview image in the summary: "LARGE
STRONGBOX / Guarded by 10 packs of Monsters" with two orb icons under it — seen 2026-11-23). On a
mouse each icon is a click target; on a gamepad the hold of (a) focuses the row and A applies
(measured, §4c). A game option toggles the row: the options panel's UI section has a **"Currency
Quick UI"** checkbox (MEASURED: UI dump of 2026-09-15, between "Hide Remove-only Tabs" and "Support
Gem Utility Popup").

Do not confuse either with **"Bulk Quick Use [hold Y]"**, the stash/inventory hint the mod already
reads (play log, layer 61): that moves whole currency stacks between inventories (0.2.0h "CTRL +
Right Click", gamepad hold Triangle/Y) and has nothing to do with strongboxes.

## 2. What a sighted player sees that the mod does not say

The strongbox "tooltip" is its world label itself: the box's name, coloured by rarity, with its
modifier lines under it ("Guarded by 10 packs of Monsters"), the gamepad hold hint beside it, and the
quick-use icons under it. An essence's label likewise names the held monster and its essences, with a
Vaal Orb icon when one is carried and the essence is not yet corrupted.

The labels are in the UI tree but in a layer speech never read: they live under the
`world_overlay`/`world_ui_layer_parent` prefixes at `[0,1,11]`, which the screen watcher skips (the
layer and its metadata-path naming were already documented in [ui-layers.md](ui-layers.md) and
[minimap.md](minimap.md); §4c settles the label subtree itself). That, not world-drawing, is why the
2026-11-18 play log shows HUD (layer 12) tooltip texts for ground items but never a strongbox text —
no modifier line of any box appears anywhere in the log or in the UI dumps the watcher took (MEASURED
by absence: `grep -i "guarded by"` over the log and `docs/research/evidence/` finds nothing). The exit
label's "Hold A (Show Options)" never reached speech for its part either (ProximityNarrator,
2026-10-24, written before the layer was read).

What the mod says today, from the same log (five boxes met: Researcher's ×3, Armourer's, Large,
Blacksmith's):

- on taking the interact target: **"Researcher's Strongbox, A"** — name and button only
  (`ProximityNarrator.Label` over `EntityCatalog.InteractTarget`). No rarity, no modifiers, no
  unidentified/corrupted state, no hold hint. In every case the next line is the ambush: the box was
  opened as found, because nothing said it could be anything else.
- "…: ambush" / "…: can be opened" afterwards (`LeagueWatch` on the `started` state) — already said.
- the scanner lists boxes under Chests by name (`EntityCatalog.Classify`, Chest branch: `Chest`
  component's `IsStrongbox`, `IsOpened`); the name is the Render name ("Researcher's Strongbox").
- Essences are better served: the crystal is named with the held monster, the essence count, corrupted
  or not, and the essences by name (`Announcements.Essence`, `EssenceContents`). Still missing: that a
  Vaal Orb can be applied to it before freeing the monster, and by what input.

## 3. Where the missing facts live (no new offsets)

| Fact | Source the host already reads | Status |
|---|---|---|
| "hold ⟨button⟩ to apply currency" | the game's own string above; spoken the way the exit's hold hint is (`Announcements.HoldForOptions`, once per area) | done, this change (§4) |
| strongbox rarity + modifiers | candidate: `ObjectMagicProperties` (Rarity, Mods/ModNames) or `Mods` on the box entity. The host reads both components elsewhere, and `Entity.TryCalculateEntitySubType` reads a chest's `ObjectMagicProperties.Rarity` — but only on the non-strongbox branch | rarity **spoken** from the follow-up slice and confirmed live (§4d: "Strongbox, rare, A" after the owner's Alchemy; Normal read on a box offering only transmutation/alchemy buttons). The modifiers and the corruption stat are still only logged (`LogStrongbox`, on every change of the read) until a session's log settles speaking them (§5) |
| modifier lines as text | rendered from the mod ids through the same machinery that writes an item's lines (`catalog/mod-texts.json`, `mod-stats`; the strongbox mods are ordinary Mods rows, e.g. `ChestSummonNormals` "Guarded by (4-8) packs of Monsters", poe2wiki's strongbox modifier tables) | superseded as the first road: the label subtree (§4c) carries the lines in the game's own words — read them there (§5 item 1) |
| which quick-use buttons would show | derived, as the game derives them: box state (unidentified / rarity / modifier count) × currency in the player's inventory (the host reads inventories; `InventoryReader` names and counts stacks) | not needed: the row itself is read (§4c); deriving it would only duplicate the game |
| the hold panel's contents | a UI panel like any other once open: `ScreenWatcher`/`FocusTracker` read panels and inventory grids (a currency cell already reads "…, X to use, then A …") | done: captured by the dumps of §4c and read by `DescribeQuickCurrency` (§4c–§4d) |
| essence vaal-able | the crystal's states already read (`is_corrupted`, `num_essences`, … in `LeagueWatch`'s "League piece seen"); a Vaal Orb in the inventory as above | design in §5 |

One trap for whoever builds §5: `ObjectMagicProperties.UpdateData` and `Mods.UpdateData`
(`host/GameReader/RemoteObjects/Components/ObjectMagicProperties.cs`, `Mods.cs`) rebuild their mod
lists only when the component address changes (`hasAddressChanged`); rarity is re-read every update,
before that guard. Applying currency changes a box's mods in place, so after an application the
cached list may be stale — the component must be re-read (or its change detected via rarity) before
the new lines are spoken, or the mod will repeat the old box.

## 4. What this change does (2026-11-23)

The smallest slice that game data alone supports, no unverified field spoken:

- `ScanEntry.Strongbox` (set in `EntityCatalog.Classify` where `Chest.IsStrongbox` already picks the
  strongbox sound; `Kind` untouched, its consumers are many).
- The first strongbox that takes the interact target in an area is named with the game's own hint:
  **"Researcher's Strongbox, A. hold A to apply currency"** (`ProximityNarrator.Label`, beside the
  exit's "hold A for options", once per area the same way; `Announcements.HoldToApplyCurrency`, phrase
  in all ten languages, `phrases.py check` clean).
- `EntityCatalog.LogStrongbox`: each time a box's read changes, log what `ObjectMagicProperties` and
  `Mods` read on it, with the candidate corruption mark in its three possible homes — the
  `GameStats.dropped_items_are_corrupted` stat in `ObjectMagicProperties.ModStats` and in the entity's
  own `Stats` (`StatsChangedByItems`/`StatsChangedByBuffAndActions`) — since a corrupted box's mark is
  not spoken yet ("Strongbox mods: Metadata/Chests/StrongBoxes/…: ObjectMagicProperties Rare,
  mods […], corrupt stat 0; Mods none; Stats corrupt 0/0"). The shrine-row/essence-contents pattern:
  how the field is confirmed from a session's log before anything is spoken from it (AGENTS 26: the
  log confirms a finding, it is not the way to it — the design in §5 stands on the files either way;
  the log only picks the component).

Build and the full unit suite pass (`tools/linux-tests.sh`); not heard live at this slice — heard
later the same day (§4b–§4d).

## 4b. The same day, live (play log 2026-11-23, the build before §4)

The owner walked around two landmark strongboxes in Holten trying exactly this. What the log proves:

- The first box (`LandmarkStrongbox/P1_4LandmarkStrongbox1`) read `opened=1, started=0` from first sight, its
  loot (an uncut gem, gloves) already on the ground, and never took the interact target in 40 minutes of
  walking around it — while the scanner named it, sounded it and guided to it as a live chest, because
  `EntityCatalog.Classify` trusts the Chest component's `IsOpened`, which read unopened. Ordinary spent boxes
  agree in both (a looted `CasterStrongboxLow` was left unlisted on 2026-11-18); the landmark variant does
  not. Fixed the same day: a strongbox whose script state `opened` is set is spent
  (`LeaguePieces.IsStrongboxSpent`, unit-tested; the state's meaning is MEASURED in
  [league-mechanics.md](league-mechanics.md) §6 — Strongbox.ot handlers: `opened` 1 = looted, the
  script every strongbox inherits from `Metadata/Chests/StrongBoxes/Strongbox`) and is no longer
  listed or named. A box whose states do not read stays listed.
- The second box (`…/P1_4LandmarkStrongbox2`, `opened=0`) took the target once the owner stood close enough
  ("Strongbox, A", 14:31:39 — beacon "arrived" and scanner "here" are both outside interact reach; the
  target line is the only sign A will work). Holding A then **did open the apply panel** — and the mod said
  only its hint line: `Focus -> … role=Panel text="Select, Back, Inventory"`, repeated verbatim as the
  D-pad moved ("A direction moved nothing: the focus again" ×4). No currency, no box state, no selection
  is read from it (MEASURED). What its elements are waits on a UI dump of the panel open (`ResearchMode`
  now on in the play copy: one auto-dump per new panel, 2.5 s after it settles).
- Trying the other road — X to use a Vaal Orb in the ordinary inventory (13:55) — puts the game in
  item-apply mode ("Change Item [Y] | Cancel Apply [B] | Quick Navigate (Applicable Items Only)"), whose
  targets are items only; the box cannot be chosen there. Nothing was applied (the Vaal Orb count never
  changed), and five silent openings of an empty-reading layer 66 punctuate the attempt.

## 4c. The hold panel captured (UI dumps 2026-11-23 14:34, live-verified after)

Two research dumps 18 s apart, before and after the hold, plus live `describe`/`hex` probes, settle what the
panel is. (The dumps are the play copy's `ResearchMode` auto-dumps of 2026-11-23 14:34 —
`ui-tree-*-20261123-1434*.txt` in its `Plugins/Poe2Accessibility/debug/`, per `UiTreeDumper`'s naming;
they are runtime evidence on the play machine, not committed here. The 15:12 rare-label dump of §4d
is the same road. A patch recheck (AGENTS 18) re-takes them the same way: `ResearchMode` on, target a
box, hold, dump.) World labels live in their own layer (`[0,1,11]`, before the HUD; the screen watcher skips the
`world_overlay`/`world_ui_layer_parent` prefixes, which is why no label text ever reached speech;
layer provenance §2):

- `[0,1,11,0,0,*]` — every nearby object's plain label, hidden until targeted; each element's name is the
  object's file ("Metadata/Chests/Manor/Urn1").
- `[0,1,11,0,1,0]` — the targeted object's label, named by its file, its first child the title ("Strongbox")
  and a `layout` of modifier lines ("Guarded by 10 packs of Monsters", the advanced "(5-10)" variant and
  tier hidden). The generic reading of this root already says it whole: `Button "Strongbox, Guarded by 10
  packs of Monsters"` (measured by `describe`).
- Each further child of the root is one **quick-use button**: a 61×51 wrapper over a click-sound button whose
  icon child carries the currency art as its texture. The buttons stand under the label as soon as the box is
  targeted (the 0.2.0h row, sighted players click these); **holding the interact button moves the game's
  focus onto the first** — that focus change is the whole "hold panel". One legend row joins the wrapper
  while it holds the focus ("Select", "Back", "Inventory" texts with Steam Deck glyphs — what the old build
  read as the panel's name). One button was present: `Art/2DItems/Currency/CurrencyUpgradeToRare.dds`, the
  Orb of Alchemy (read from the texture object's buffer by hex probe; the owner carried 2, and no
  transmutation).
- The icon art does not resolve through the price catalog: several items share one art file in `BaseItemTypes`,
  so `CurrencyArtExtractor` leaves it unnamed. A button whose art stem is the item's own id names that item
  (`CurrencyIcons.QuickUseName`: catalog first, stem else; the Alchemy stem measured above). The tiered orbs: §4f.
- The icon child is flagged invisible in every dump (its parent draws it), so texture hunting must walk
  invisible children too.

Built on it the same day, deployed and verified live at the same box: `UiSemantics.DescribeQuickCurrency`
makes the focused button read as itself — `Focus -> … role=Button text="Orb of Alchemy"
context="Strongbox, Guarded by 10 packs of Monsters"` — the label as the spoken context, "x of n" when a
label offers several buttons (at this slice UNVERIFIED, one-button sample; settled in §4d).
`ResearchMode` was switched off again after the capture (an auto-dump costs a ~5 s tick).

## 4d. The box made rare, and the Vaal question settled (live, 2026-11-23)

The owner applied the Alchemy (Select in the panel): the panel closed, and the next target line read
"Strongbox, rare, A" — the rarity speech of §4's follow-up slice, live on its first box. The rare label
(dump 15:12) is the Normal one's shape grown: title two lines ("Horror Bond\nStrongbox"), the implicit,
then an `explicit-stats` layout of five visible lines for four modifiers — "63% more Quantity of Contained
Items" (two suffixes summed: 30(15-30) with 17% increased Effectiveness, 33(25-50)), "20% more Rarity of
Contained Items / Freezes you when activated" (the Arctic prefix), "Guarded by a pack of Rare Monsters";
the hidden reminder branches carry each mod's range, tier (T1) and Prefix/Suffix. Two prefixes and two
suffixes: LIKELY room for one more of each (the 3+3 cap is item-mod logic by analogy; no
strongbox-specific source found).

The Vaal road was also tried to its end — and it **works**, correcting what this section first said. The
inventory opened while the box is the game's interact target (the panel's X legend, or the backpack key
beside the targeted box) is no ordinary backpack: every currency the box accepts reads on its cell
"**Apply (Strongbox), X**" instead of "Use, X", and that X applies it straight to the box — no target
selection (measured 15:13–15:14: Chaos and Vaal cells carried the suffix; the same inventory opened
without the box targeted, at 13:55 and 15:09, read plain "Use, X" and put the game in the item-only
apply mode). The mod already speaks that suffix from the cell's own hint, so the manual road was
accessible before quick-use was; only nobody could know to stand at the box first. One Vaal Orb spent
this way at 15:14: the stack went 2 → 1, the suffix vanished (a corrupted box accepts no more), the
label gained a visible "Corrupted" line, and rarity and modifiers stand unchanged — which also locks
the box against the Exalted Orbs that were next in line: **exalt first, vaal last**.

The same backpack split its currencies in two (15:46, the next box): the manual-only ones (Vaal, Chaos)
read "Apply (<box name>), X", while a quick-listed one the box's state calls for (an Exalted Orb at the
now-Rare box) read plain "Use, X" and its X entered the item-apply mode — the quick row is that orb's
road, the backpack the others'. Measured in two sessions at three box states; the mechanism (a suffix
only where the row cannot serve) is LIKELY.

That box also stood two buttons in its row at once (Transmutation and Alchemy art in one read): the row
is a single focused element, directions move a selection inside it, and the first reader named whichever
icon it found first — the owner heard "Orb of Transmutation" four times while the selection stood
elsewhere. Applying the Transmutation updated the row in place (the same focus read "Orb of Alchemy"
under the new "Arctic …" label a minute later). A later two-button capture disproved the first guess
that the selected button stands grown: both press buttons read 60×60. (The sizes in this file are of
different elements: the root-child wrapper 61×51 in §4c, the press buttons inside it 60×60 here, and
the single-button row's button 61×61 focused / 40×40 not, from the first capture — size therefore
only a fallback.) The UI manager instead alternates the two press-button addresses in its hovered
slot as D-pad left/right move the selection. That slot is no new offset: it is the pre-existing
`GameLayout.UiManager.HoveredOffset` (0x760, layout-probe adjusted) the focus tracker already read
for hover (`FocusTracker.Reading.cs`; audited in
[field-protection-audit.md](field-protection-audit.md)). The reader now names the slot whose address
matches that hovered element, with its place in the row. Verified live at the same two-button box
after deployment: D-pad right logged `selected 2 of 2` and said "Orb of Alchemy, button, 2 of 2"
(2026-11-23 17:00).

## 4e. Exalted: GGG's list against the gamepad's reality (live, 2026-11-23)

At the same now-Rare box, carrying 6 Exalted Orbs, **no Exalted button exists on a controller**:

- In the backpack opened on the targeted box, the Exalted cell read plain "Use, X" in every session
  (15:13, 15:46, 15:54) while Chaos and Vaal cells in the same openings read "Apply (<box>), X".
- Holding the interact button at the Rare box (15:57, pad log: A held ~0.9 s) never focused a row
  button — the backpack opened directly. At Normal and Magic boxes the same hold focused the row.
  The row at this Rare box is empty (its branch stands hidden and childless in the dumps), and an
  empty row's hold falls through to the backpack.

So GGG's 0.2.0h sentence ("buttons for Orbs of Alchemy, Exalted Orbs, and Regal Orbs") does not hold
on a gamepad in 0.5.5 for Exalted — whether the row differs by input, by campaign/endgame, by an Atlas
passive, or the button quietly went away is UNSURE; no source found distinguishes them. What is
measured: **on a controller, an Exalted Orb cannot be applied to a strongbox by any known path**
(mouse right-click remains, per the wiki's currency pages). The suffix split of §4d also reads
differently in this light: "Apply (<box>), X" marks exactly the currencies with no row button at this
box, quick-listed or not — Exalted's plain "Use, X" then means the game expects it in a row that, on
this input, it never builds (LIKELY mechanism for what looks like a gamepad-side bug).


## 4f. Which tier a button spends (game tables, 2026-11-23)

`BaseItemTypes` → `ItemVisualIdentity.DDSFile` (the `currency-art` command's own columns, ids printed): the base,
Greater and Perfect tiers of the Transmutation, Augmentation, Regal, Exalted and Chaos orbs (ids `X`, `X2`, `X3`) all
point to the **same file**, the base's. No numbered art exists, so an icon never tells the tier. Alchemy's file is
shared only with a "[DNT] Not Shown To Players" item.

GGG's notes (§1b): a button shows only for a currency in the inventory. With a single tier in the backpack the button
can therefore spend no other, and it is said by its full name; with several, or with the backpack unread, the base
name is said with "tier unknown" (`Announcements.TierUnknown`). The backpack is the host's `MainInventory1`
(`UiSemantics.CarriesCurrency`). Unit-tested; not heard live. Which tier the game picks with several carried: UNKNOWN.

## 5. The rest, in order

1. **Say the label on taking the target, without the hold.** The label subtree (§4c) stands in the tree
   whenever the box is targeted, title and modifier lines in the game's own words — a better source than
   reconstructing them from a mods component. What is missing is only the wiring from the entity the
   catalog targets to its label element (the root is named by the entity's file; several same-file
   labels can stand in `[0,1,11,0,0]`, the targeted one under `[0,1,11,0,1]`). Rarity needs no label:
   it is already spoken from `ObjectMagicProperties` (§4d). Unidentified must withhold the lines, as
   the label does. The "Strongbox mods:" log (§4) meanwhile keeps watching for what no label text
   carries — above all where a corrupted box's mark reads — before anything more is spoken from it.
2. **Several tiers carried**: which one the button spends is unknown (§4f). Several buttons on one label are settled
   (§4d: hovered-slot selection, live 17:00). Still to do: re-say the label when its lines change after a currency is
   applied — the label elements rebuild (addresses differed between the two dumps), so diff by text, not address.
3. **Essences**: the same reader covers a crystal's label by construction (its root is the held
   monster's file), but no essence button has been seen: whether holding the interact button at a
   crystal focuses its Vaal button the same way is UNVERIFIED until a dump shows one.

Out of scope, by the rules: applying anything for the player. Every step above is speech about what
the game already shows; the hands stay the player's (AGENTS 4, 14).
