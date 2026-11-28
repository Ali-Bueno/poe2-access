# The game's item filter on ground items

The loot filter a player picks in Options > Game > Item Filter (NeverSink's, the game's own, or any `.filter` file) decides
for each item on the ground whether the game draws its label. Before 2026-11-28 the mod listed every ground item in the
scanner, played its drop sound and named it on approach, hidden or not. A tester of the fork oasis1701/poe2-access noticed a helmet with no pick-up prompt
that the mod still named. The game keeps the filter's verdict on the item's WorldItem component, and the mod now reads it
there (`GameItemFilter`). The mod has no setting for it, because the game's own option `hide_all_filtered_ground_items` is the switch.

## The verdict on the WorldItem component

The component the host calls WorldItem is the game's ClientWorldItem, 0xB8 bytes from pool 0x45C4980 (named
"ClientWorldItem" at 0x3D630). Exe build 26744644 of 2026-11-23, read as a file; the RVAs below are for that build.

| Offset | What it holds | Proof | Helmet hidden | Spear shown |
|---|---|---|---|---|
| +0x28 | the item entity (the host's `ItemEntityPtr`) | the debug dump 0x1E11010 prints its name | | |
| +0x54 | set for items the filter never hides (quest items, a base-type flag) | the setter 0x1E10829, the test 0x17E2E7A | 0 | 0 |
| +0x70 | the cached verdict; bit 0 is Hide | copied from the evaluator 0x21C2C80 at 0x17E2F14; the Hide handler sets the bit at 0x21BD217 | 1 | 0 |
| +0x71 | bit 0 is Minimal (a reduced label, still shown) | the Minimal handler 0x21BD30E | 0 | 0 |
| +0xB0 | non-zero once the verdict is computed for the current filter | tested at 0x17E2EC2, set at 0x17E2FAF, cleared by a filter reload at 0xE4CF1F | 1 | 1 |
| item +0x2E | keeps the item shown; meaning not found | read at 0x17E2E89 only | 0 | 0 |

The game's own test is slot 0xC8 of the vtable 0x33CE098 (0x17E2E60). It calls an item shown when the option
`hide_all_filtered_ground_items` is off (getter 0x19467D0, the `[UI]` key of `poe2_production_Config.ini`), when +0x54 or
the item's +0x2E is set, or when bit 0 of the verdict is clear. The verdict comes from the getter 0x17E2EB0, which
evaluates the filter only when +0xB0 is clear. The item's setter runs the test as the item is created, so a fresh drop
has its verdict whether or not a label is ever drawn. The label code (0xE580C4) and the controller's targeting
(0x1A5204) read the same bit, which is why a hidden item offers no pick-up.

## What the mod does

`EntityCatalog.Classify` leaves out a ground item when all of these hold, and the item is then neither listed, heard nor
named. Its earlier entry is dropped from the remembered list on the next listing, so a filter changed while items lie
there takes effect too.

- the game's config file reads `hide_all_filtered_ground_items=true` under `[UI]` (`GameConfigOption`, read again when
  the file's write time changes, at most every 2 s; a missing or unreadable key hides nothing and is logged once; a value
  read before stands 10 s while the file cannot be read, not for good);
- the same section's `item_filter` is not `None`, the game's word for no filter chosen (`GameItemFilter.Wanted`): with
  none there is nothing to hide, and the verdict is not read at all;
- the code catalog placed all four fields of the verdict on this build (`EntityCatalog.ItemFilterPlaced`, set when the
  code fields are read; `GameItemFilter.Fields`);
- the component's +0xB0 is 1, +0x70 has bit 0 set and +0x54 is zero (`GameItemFilter.VerdictHides`, one read of
  the component from +0x54 to +0xB0);
- the item entity's +0x2E is zero (read only for an item the verdict hides).

The four offsets are plugin offsets (`GameLayout.WorldItemComponent`) that the code catalog places on each
build (`layout:WorldItemFilterResult`, `layout:WorldItemFilterReady`, `layout:WorldItemNeverHidden`,
`layout:WorldItemItemNeverHidden`; [evidence](evidence/code-fields-item-filter-2026-11-28.json)). Every way a read can fail leaves the item listed. The drop
detection of `InventoryWatch` (`GroundItemIds`, `GroundItemName`) still sees every ground item. Each hidden item is logged
once per area (`Ground item hidden by the game's item filter: <id> <base>`).

## Live check, 2026-11-28

The tester played (Manor Ramparts, area level 14, controller, `always_highlight.gamepad=true`) while the agent sent dev
commands (`entities`, `dump`, `hex`) to the running mod. A Hunting Spear and a Rusted Greathelm, both normal, lay on the
ground. NeverSink's semi-strict filter shows nearly all white gear in act 1 (rule 584 for pure armour bases), so both were
shown. A one-line filter `Hide Class == "Helmets"` then hid the helmet.

| State, in order | Helmet's verdict (+0x70, +0xB0) | Spear's verdict | Helmet's label |
|---|---|---|---|
| NeverSink semi-strict | not read yet | not read yet | drawn, also when off screen |
| helmets hidden (first switch) | not read | not read | drawn in the dump right after the switch, invisible in the next reading; the spear's stayed visible, paused and with the inventory open too |
| NeverSink again | 0, 1 | 0, 1 | visible again |
| helmets hidden (second switch) | 1, 1 | 0, 1 | not read |

The helmet stayed in the area's entity list throughout, which is why the mod kept naming it.

The built mod was then deployed to the development host with the game open (`evidence/item-filter-2026-11-28/deployed-check.txt`).
At attach it read `Code fields: 88 of 88 … moved: none` and `Game option hide_all_filtered_ground_items=true`.

| Filter, in order | The scanner's Items | Log |
|---|---|---|
| NeverSink semi-strict | Rusted Greathelm (758), Hunting Spear, Gold | |
| helmets hidden | Hunting Spear, Gold | `Ground item hidden by the game's item filter: 758 Rusted Greathelm` |
| NeverSink again | Rusted Greathelm (919, at the same cell 826,421), Hunting Spear, Gold | |

While the helmet was hidden the tester pressed A on it on purpose. The game targeted it (`A pressed on: 758`) and answered
"Inventory Full". About a second later 758 was gone and a new entity, 919, held the same item, with no model (bounds 0,0,0)
at the map's origin, still hidden. After a portal to town and back it lay at its own place again with its model. So a
hidden item can lose its place in the client, one more reason a hidden item is not listed at all: a mod setting that listed
them anyway (`FollowGameItemFilter`, in the first version of this change) did not list 919, whose origin was ground never seen
(`Sighted`). The setting was taken out the same day (the tester said the game's option is the switch and the project has too many
mod settings).

Not seen yet are a fresh drop the filter hides (each case above switched the filter on items already lying there), the
option off, and an item with +0x54 or the item's +0x2E set. A later session's log confirms the first through its
`Ground item hidden` lines.

## Made safe to merge (the owner's review, 2026-11-28)

This reading hides loot from a player who cannot see it, so a wrong "hidden" loses items with nothing said, while a wrong
"shown" is only how the mod was before. A second reader went through every way to the first, and each now lists the item:

| Way to a wrong "hidden" | Now |
|---|---|
| A patch changes the code around a field: its line does not settle, the compiled offset stood "right or wrong", and bytes that are another field pass the test often (any non-zero "computed", a set bit 0, two zero bytes) | Nothing is hidden unless the catalog placed all four fields on this build (`ItemFilterPlaced`; null until the code is read, false when it is not). When the player has the option on and a filter chosen it is said once ("qué objetos del suelo oculta el filtro de objetos del juego, así que esos también se listan") and logged |
| The "computed" byte read as any non-zero value | It must be the game's 1 (0x17E2FAF sets it, 0x17E2EC2 tests it) |
| The item's keep byte (+0x2E) was a bare constant: read as zero somewhere else, it lets a hide through | A catalog line of its own, `WorldItem.ItemNeverHidden`: two signatures over the one site that reads it (0x17E2E89) |
| The option on with no filter chosen: what the verdict holds then was not measured (the owner's own file: `hide_all_filtered_ground_items=true`, `item_filter=None`) | `item_filter=None` hides nothing; any other value, or a key not read, leaves the verdict to decide |
| The first `hide_all_filtered_ground_items` line of the file was taken, whatever its section | Its `[UI]` section only |
| A file that stays unreadable kept the last value for good | For 10 s (`GameConfigOption.StaleMs`), then it is not known |

The four lines read alike on the seven builds kept (25244054 to the installed 26744644): `signatures.py check`, "0 lines
the mod would not believe". The second signature of the new line first hit twice on two of them (the same test in a
sibling class) and was lengthened to the one `WorldItem.NeverHidden` already proves unique.

Each area's log line says how the filter read: `Item filter: 12 ground items asked, 12 with a verdict, 3 hidden`. Many
asked and none with a verdict, with a filter chosen, is the "computed" byte gone from where it was; that is seen in the
log only, as a filter being reloaded reads the same for a moment.

Live on kehgan, 2026-11-28 (0.5.5h, the owner's settings): `Game option hide_all_filtered_ground_items=true`, `Game
option item_filter=None`, `Code fields: 89 of 89 … moved: none`; a whetstone dropped in Villafrey said "Tirado: Piedra de
afilar" and was listed (`Scan Items id=352 kind=currency "Piedra de afilar"`), then picked up. With a filter that hides
was not tried here: the owner plays without one.

## The label layer (not used)

The labels were the first road, and they work less well.

- `world_ui_layer_parent` is at +0xB98 of the in-game layer container (`map_overlay` is at +0xB90 on this build), and
  its first child, the label owner (0x678 bytes, vtable 0x315E010), is at +0xBA0 (UI builder 0xF823A0, 0xF84452).
- The label owner holds a std::list at +0x3F0 of records {label element at +0x10, entity ids}, and a map from an entity id to
  its label at +0x428 (MSVC unordered_map: list head +0x430, size +0x438, buckets +0x440, mask +0x458). The hidden
  helmet stayed in both. A vector of label groups at +0x2F0 holds only the labels drawn.
- A label element names no entity; it is named "Metadata/MiscellaneousObjects/WorldItem" and carries the item's text.
- A hidden item's label clears the element's visible bit (flags +0x168, bit 11), and three bytes past the plain label's
  0x700 changed with it (+0x6FD 0 to 1, +0x6FE 1 to 0, +0x738 0 to 1).

The visible bit is not the filter's alone. A player without always-highlight, or one who turns labels off with the
highlight toggle (Z), would have every label invisible, and the mod would have hidden all their loot. Pressing Z with the
controller in use changed nothing in memory. A far item may also have no label yet. The component's verdict has none of
these limits.

## Evidence

`evidence/item-filter-2026-11-28/`: `worlditem-components.txt` (both components and items in both states, from the
mod's log), `ground-labels.txt` (the labels and the label owner element through each state), `world-label-layer.txt` (the layer in
three dumps), `ground-items-20261128-005032.txt` (the three items in the entity list), `static-notes.txt` and
`static-disasm.txt` (the exe study: every site above, disassembled).
