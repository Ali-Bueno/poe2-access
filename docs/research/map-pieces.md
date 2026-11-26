# Ground apart in one area: tombs, Trial rooms, Chaos rooms (2026-11-26)

Asked by the owner on 2026-11-26 in El cruce de Khari (P2_1): inside a tomb the radar keeps citing ways outside it, and
the same happens in the Trial of the Sekhemas and the Trial of Chaos. Investigation only, no code yet.

## What the game does

- **A tomb is the same area.** The 14:21 log: the player takes "Escaleras", no area load follows ("Area:" not logged), the
  tomb's way out reads "El cruce de Khari, 3 pasos", and the map part jumps from "Esquina superior derecha, 21%" to
  "Borde superior, 43%": the tomb is ground apart, placed elsewhere on the same terrain grid, joined to the outside only
  by the transition pair. Inside it the mod said "Antes de salir: 47 caminos pendientes. Camino pendiente en Pueblo, a
  107 pasos, el camino se corta antes" (outside ground) and "Pueblo, ya estuviste aquí…".
- **A Sekhemas floor** is one area whose rooms are grounds apart ([trials](trials-and-campaign.md) §1).
- **A Trial of Chaos stretch** is one instance whose rooms are joined by lifts ([chaos](trial-of-chaos.md), LIKELY);
  whether a lift carries the player to ground apart or along walkable ground is not known.

## What the mod already has

- `WalkableRegions` (`Services/Scanner/WalkableRegions.cs`): 8-neighbour labelling of the walkable grid with `Apart(a,b)`,
  built off-thread by `EntityCatalog.Terrain.cs` (`OnGroundApart`); used only for town NPCs and portals.
- `ExplorationMap.Reachable` (`ExplorationMap.Frontier.cs`): a flood from the player every survey; the frontier, unexplored
  stretches, left-behind, `WayOn` route and the counted blocks of the map parts already follow it.
- `CarriedWatch` (`Services/Trials/CarriedWatch.cs`): a jump of more than a look onto ground apart; only used in Trials.
- `RoomsApart()` = in a Trial: turns off the map parts, left-behind and the quest-exit kind there.

## What still uses the whole map

- Exits, places, waypoints and NPCs listed (`EntityCatalog.Listing.cs`, ground filter only in town).
- Forks and dead ends (`MapTopology`, built once per area over all ground), `LandmarkAhead` (straight distance), room
  names (`RoomMatcher`/`PlaceNarrator.RoomAt`), the "Antes de salir" pending count, `LostGoal`.
- The map parts' frame (`MapFrame`, all walkable ground's box): inside a tomb the thirds are the whole area's.
- The local transition is an ordinary exit: `ExitEntities` has no local/other-area kind and no "where it leads".

## Built (2026-11-26, unit tests; not heard yet)

- `GroundPieces` (pure): the first ground stood on is the main one; a carry (`CarriedWatch.CarriedFrom`, now looked at in
  every area) onto another `WalkableRegions` piece sets that piece apart and keeps the pair of cells as the way between.
  Ground a closed gate cuts off is never apart (no carry reached it), so the map frame's worry does not apply.
- `EntityCatalog.OffPlayersPiece`: on a piece apart everything off it is hidden; on the main ground only the pieces apart.
  Applied to the listing (exits, places, NPCs, loot, remembered landmarks, trails, quest targets: so the beacon, the
  lost hint and the forks' named goals too), the pending ways (count, nearest, a fork "left" by a carry is let go
  unsaid) and the landmarks ahead.
- A quest goal on other ground becomes "Misión. Antes, pasa por <exit>": the exit by the cell a carry joined the two
  grounds at, else on a piece apart its nearest exit.
- Map parts and their glance are off on a piece apart (as on a Sekhemas floor); "Zona aparte: solo se dice lo que hay
  aquí" / "De vuelta al mapa principal" on the carry (setting `AnnounceMapParts`; log `Ground: EnteredApart at cell …`).

- Before any carry: an exit named after the area itself (a tomb's way back) sets its piece apart as soon as the game
  wakes it (`GroundPieces.MarkApart`); a quest goal there with no way known is kept as it was, never dropped.
- Open ground (owner, same day: "sigue saliendo un montón de caminos sin explorar"; the log: "Dejas 41 caminos
  pendientes", "Zona con rampa: sigue a la derecha, abajo a la derecha, arriba…" in the desert): a place with more than 8
  ways toward 6 or more of the 8 sides is named only, its ways neither told nor kept pending (`ForkNarrator`); a fork
  left says the number of sides, not of skeleton ways.

Open: the `AreaTransition` component's layout (local kind, target), from the exe's code reader, to name a way in before
it is used; the Trial of Chaos lifts, if they move the player along walkable ground, set nothing apart.
