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

**(a) The gamepad hold — "Hold: Apply Currency"** (since launch; the controller path).
ClientStrings carries `GamepadInteractInfoStrongboxAltOnly` = "Hold: Apply Currency" /
"Mantener presionado: Aplicar objetos monetarios" (MEASURED, league-mechanics.md §6). Standing at a
strongbox on a gamepad: **tap** the interact button opens it (the ambush starts); **hold** the interact
button instead opens "a small inventory panel view which will let you use crafting currency you hold
or wisdom scrolls to identify" (Reddit, PS5, 2025-01-19, web — consistent with the string and with a
forum report of the same week: "I tried holding the A button, but only my inventory popup…"). In that
panel the chosen currency is applied to the box; the full manual range is available (identify,
transmute, augment, regal, exalt, alchemy, chaos, vaal…), as right-clicking is on a mouse (the wiki's
currency pages all read "Right click this item then left click an item…"; a strongbox is such a
target: corrupting one with a Vaal Orb makes everything it drops Corrupted, poe2wiki "Strongbox").

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
mouse each icon is a click target. Whether/how a gamepad presses an icon is **UNSURE**: no source
found; the gamepad has the hold of (a), so the icons may be display-only there (LIKELY). A game option
toggles the row: the options panel's UI section has a **"Currency Quick UI"** checkbox (MEASURED: UI
dump of 2026-09-15, between "Hide Remove-only Tabs" and "Support Gem Utility Popup").

Do not confuse either with **"Bulk Quick Use [hold Y]"**, the stash/inventory hint the mod already
reads (play log, layer 61): that moves whole currency stacks between inventories (0.2.0h "CTRL +
Right Click", gamepad hold Triangle/Y) and has nothing to do with strongboxes.

## 2. What a sighted player sees that the mod does not say

The strongbox "tooltip" is its world label itself: the box's name, coloured by rarity, with its
modifier lines under it ("Guarded by 10 packs of Monsters"), the gamepad hold hint beside it, and the
quick-use icons under it. An essence's label likewise names the held monster and its essences, with a
Vaal Orb icon when one is carried and the essence is not yet corrupted.

The labels are drawn in the world, not in the UI tree the mod reads: in the 2026-11-18 play log, every
ground item that took the target produced HUD (layer 12) tooltip texts, but no strongbox ever did —
no modifier line of any box appears anywhere in the log or in the UI dumps (MEASURED by absence:
`grep -i "guarded by"` over the log and `docs/research/evidence/` finds nothing). The exit label's
"Hold A (Show Options)" is the same kind of text and is likewise "drawn outside the UI tree"
(ProximityNarrator, 2026-10-24).

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
| strongbox rarity + modifiers | candidate: `ObjectMagicProperties` (Rarity, Mods/ModNames) or `Mods` on the box entity. The host reads both components elsewhere, and `Entity.TryCalculateEntitySubType` reads a chest's `ObjectMagicProperties.Rarity` — but only on the non-strongbox branch, so a strongbox carrying either is UNVERIFIED | logged once per box from this change; spoken only once a log confirms which component carries the label's lines (§5) |
| modifier lines as text | rendered from the mod ids through the same machinery that writes an item's lines (`catalog/mod-texts.json`, `mod-stats`; the strongbox mods are ordinary Mods rows, e.g. `ChestSummonNormals` "Guarded by (4-8) packs of Monsters", poe2wiki's strongbox modifier tables) | after the component is confirmed |
| which quick-use buttons would show | derived, as the game derives them: box state (unidentified / rarity / modifier count) × currency in the player's inventory (the host reads inventories; `InventoryReader` names and counts stacks) | design in §5; speak only what the game would show |
| the hold panel's contents | a UI panel like any other once open: `ScreenWatcher`/`FocusTracker` read panels and inventory grids (a currency cell already reads "…, X to use, then A …"). No dump of this panel exists yet | needs one UI dump with the panel open (§5) |
| essence vaal-able | the crystal's states already read (`is_corrupted`, `num_essences`, … in `LeagueWatch`'s "League piece seen"); a Vaal Orb in the inventory as above | design in §5 |

One trap for whoever builds §5: `ObjectMagicProperties.UpdateData` and `Mods.UpdateData` rebuild their
mod lists only when the component address changes (`hasAddressChanged`); rarity is re-read every
update. Applying currency changes a box's mods in place, so after an application the cached list may
be stale — the component must be re-read (or its change detected via rarity) before the new lines are
spoken, or the mod will repeat the old box.

## 4. What this change does (2026-11-23)

The smallest slice that game data alone supports, no unverified field spoken:

- `ScanEntry.Strongbox` (set in `EntityCatalog.Classify` where `Chest.IsStrongbox` already picks the
  strongbox sound; `Kind` untouched, its consumers are many).
- The first strongbox that takes the interact target in an area is named with the game's own hint:
  **"Researcher's Strongbox, A. hold A to apply currency"** (`ProximityNarrator.Label`, beside the
  exit's "hold A for options", once per area the same way; `Announcements.HoldToApplyCurrency`, phrase
  in all ten languages, `phrases.py check` clean).
- `EntityCatalog.LogStrongbox`: once per box, log what `ObjectMagicProperties` and `Mods` read on it
  ("Strongbox mods: Metadata/Chests/StrongBoxes/…: ObjectMagicProperties Rare, mods […]; Mods none") —
  the shrine-row/essence-contents pattern: how the field is confirmed from a session's log before
  anything is spoken from it (AGENTS 26: the log confirms a finding, it is not the way to it — the
  design in §5 stands on the files either way; the log only picks the component).

Build and the full unit suite pass (`tools/linux-tests.sh`); not heard live yet.

## 4b. The same day, live (play log 2026-11-23, the build before §4)

The owner walked around two landmark strongboxes in Holten trying exactly this. What the log proves:

- The first box (`LandmarkStrongbox/P1_4LandmarkStrongbox1`) read `opened=1, started=0` from first sight, its
  loot (an uncut gem, gloves) already on the ground, and never took the interact target in 40 minutes of
  walking around it — while the scanner named it, sounded it and guided to it as a live chest, because
  `EntityCatalog.Classify` trusts the Chest component's `IsOpened`, which read unopened. Ordinary spent boxes
  agree in both (a looted `CasterStrongboxLow` was left unlisted on 2026-11-18); the landmark variant does
  not. Fixed the same day: a strongbox whose script state `opened` is set is spent
  (`LeaguePieces.IsStrongboxSpent`, unit-tested) and is no longer listed or named. A box whose states do not
  read stays listed.
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


## 5. The rest, in order

1. **Pick the component** from the first log carrying "Strongbox mods:" lines against a box whose
   label a sighted check (or the box's own ambush: packs summoned, Daemon hazards) corroborates.
   Then, on taking the target (and in the scanner entry's description), say rarity + the modifier
   lines — the sighted label, word for word — and re-say them when they change after a currency is
   applied (diff the snapshot; mind the stale-list trap in §3). Unidentified must say
   "unidentified" and withhold the lines, as the label does.
2. **Name the quick-use buttons** the game would show, with the target line, only for currency in the
   inventory: e.g. "Orb of Transmutation, Regal Orb available" — or, if the hold panel (3) reads
   well, leave the choice to it and keep the target line to the state + hold hint. Decide by speech
   length: the target line is heard while walking, often mid-fight.
3. **Read the hold panel**: one UI dump (`ResearchMode`) with the panel open at a box; check whether
   its grid is the inventory grid the mod already reads, what its title/hints say, and what applying
   answers. Until then, holding A at a box opens a panel whose reading is UNVERIFIED — the hint now
   spoken relies on the generic panel reading, and B closes it like any panel (LIKELY).
4. **Essences**: when a crystal takes the target, is not corrupted, and a Vaal Orb is carried, say so
   once ("… a Vaal Orb can corrupt it" — wording from the game's strings when found; the
   `GamepadInteractInfo*` family has an essence member only UNSURE). How a gamepad vaals an essence —
   the same hold, or only the quick-use icon — is UNSURE until the strings or a dump say.

Out of scope, by the rules: applying anything for the player. Every step above is speech about what
the game already shows; the hands stay the player's (AGENTS 4, 14).
