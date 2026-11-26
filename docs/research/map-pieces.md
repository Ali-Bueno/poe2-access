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

## The plan proposed

1. **The player's piece:** the `WalkableRegions` region the player stands on, but treated as apart only when the player
   was *carried* there (a generalised `CarriedWatch`), never by a split the grid shows otherwise: a closed gate must not
   hide what lies behind it (the reason the map frame uses all ground, [radar](../radar.md#where-the-map-starts)).
2. **Scope every source to the piece** while in a piece apart: listings, forks, dead ends, landmarks ahead, room names,
   pending ways, map parts with the piece's own frame ("Borde superior" of the tomb). The outside's memory stays and comes
   back on return.
3. **Outside goals through the way out:** a quest target or exit on other ground is said through the transition that
   leads back ("Fuera de aquí: …, sal por …"), not by a straight line through the wall.
4. **Say the change:** "Entras en …" / "Sales a …" on the jump, named by the transition taken or the piece's room label.
5. **Pair the transitions:** the one beside the player before the jump and the one beside the player after are the two ends;
   kept per area, so the entrance can be said as "Entrada a tumba" (or the room label) once used, and before use if the
   `AreaTransition` component's kind (normal vs local, by the exe) can be read.
6. Trials: Sekhemas falls out of 1-2 with `RoomsApart` kept; Chaos by the same jump if the lift teleports, else by the
   room bounds of the `.arm` (rooms done drop out).

Open: the `AreaTransition` component's layout (local kind, target), from the exe's code reader, not from play.
