# Salvage bench (Mesa de reciclaje)

Live 2026-11-02 with yarkey (Mercenary 13) at the Clearfell Encampment bench, game in Spanish.

## What the game does
- No level requirement. The bench's own text: "Aquí puedes reciclar objetos de equipamiento con calidad o engarces
  para obtener objetos monetarios de calidad y esquirlas del artesano". A on an item without either: "El objeto debe
  tener calidad o tener engarces" (the plugin says it, as a message over the panel).
- Opened with A on the bench: the inventory opens with the bench beside it and the cursor turns into a hammer; A on an
  item salvages it (ClientStrings `GamepadHoldToSalvage` "RECICLAR" suggests holding on some setups). The legend
  shows only "Salir de la Mesa de reciclaje [B]" and "Comparar"/"Inspeccionar"; no "Reciclar" line was seen, because
  no item there could be salvaged (still unseen: the legend over a salvageable item).
- Y in the inventory is "Tirar" (drop): the "triangle does not salvage" report was Y. B closes the bench and the
  inventory together, back to the game (silence, as for other panels).

## Layout (dump `ui-tree-InGameState-20261101-002440.txt`)
- The bench panel is `panels_parent` group 1 child 25 (`[0,1,17,2,1,25]`), alone visible in its group; the inventory
  is in group 0. It has no framed title at `+0x2E8` (the passive tree's "Pasivas" has one): "Mesa de reciclaje" is a
  label three levels down (`[...,25,0,2,1]`, text at `+0x360`), followed by the description.
- So `UiSemantics.SidePanelTitle` found nothing and the salvage watch never started. Now a visible side panel without
  a framed title takes its first visible label within four levels (`UiSemantics.FirstLabel`); framed titles still win.

## What the plugin says
- Opening: "Mesa de reciclaje. Aquí puedes reciclar … del artesano. Vacío, columna 12, fila 5", then "Salir de la
  Mesa de reciclaje, B". Before, the inventory cell came first and the bench last, which sounded like the plain
  inventory (user: "se lía con el inventario"); `ScreenNarrator` now puts the side panel's lines first when a layer
  opens with the focus in the inventory (`SidePanel`).
- A salvage: `InventoryWatch.TickSalvage` compares the main inventory as a whole once it settles and says
  "Reciclado: X. Recibes: Y, 2" (`SalvageOutcome`, unit-tested: new stack, grown stack, a move says nothing).
  Detection checked live ("Salvage bench open: 8 items"); the sentence itself waits for an item with quality or
  sockets (none among yarkey's loose items; Renly sold one, a +7% crossbow for 843 gold).
- Quality is in every item's readout ("Calidad: +7%"); sockets have no text. A per-item "se puede reciclar" needs the
  item's sockets (no GameHelper component reads them; the entity's component list has to be measured first).
