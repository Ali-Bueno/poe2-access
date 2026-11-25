# PoE1Access's exploration against this mod's (2026-11-25)

Asked by the owner on 2026-11-25: what that mod does for exploring in real time, with no key, that this one lacks, and
whether PoE2 allows it showing only what a sighted player sees. Read from its source at `6c39bf4`
(`D:\code\c#\PoE1Access\PoE1Access`); it follows this mod's design, so most is shared. **Built the same day: A, B and C of the
verdict** ([radar](../radar.md), "Ways to known places" and "Exploring guide"); D is the exit named as the quest's
([quest goals](quest-goals.md#a-step-done-in-another-area-2026-11-25)), its spoken route not built.

## What it does

| Feature | Where | With no key | Here |
|---|---|---|---|
| Unexplored ground in four sizes, in the scanner | `Navigation.cs:1557` | no | same, plus unopened doors |
| ", leads to a new exit" / ", leads to something for your quest" on an entry | `Navigation.cs:1604-1700` | only in explore mode | missing |
| Radar pips | `Navigation.cs:253` | yes | same, one pip judged by the map's shape |
| Explore mode: the guide goes on by itself to the next unexplored ground, "Next: ...", "Area explored" | `Mapping.cs:83` | after the first press | missing: the guide ends on arrival |
| Quest route on entering an area: "next area X, then Y. Last waypoint on the way: Z" (after the campaign, untested) | `QuestRoute.cs` | yes | data present (`quest-goals.json`), not said |
| ", your quest leads there" on an exit | `Navigation.cs:1287` | no | the exit is in Quest; "New exit" does not say it |
| Area description: size, openness, stretch, bosses, areas it leads to | `AreaDescription.cs` | yes | the corner and whether the map changes |

Here and not there, with no key: forks, dead ends, pending ways, rooms, map parts entered and finished, a nook left
behind, circling, the lost hint, map hints and trails ([radar](../radar.md), [map hints](map-hints.md)).

## How its hint works

It is no more than the game's own entities: an exit or quest object the client has been sent, standing on ground not seen
(a disc of 60 cells), tags the whole unseen region holding it. It reaches as far as the client's bubble. Two limits: every
edge of that region gets the tag, so in open ground all of them do; and it says nothing of exits beyond the bubble.

PoE2's client is the same (bubble about 150 cells, [minimap](minimap.md)), and here that very exit is already named with
its route the moment the client has it ("New exit: ..."), which is more than the hint. Tags for farther exits would come
from the tile table, known whole on arrival: a map hack, which only the lost hint uses, for a side, after minutes lost.
On 2026-11-05 the owner found "Way to ..." from that table too easy and had it taken out.

## What a sighted player has in PoE2

- Seen: the fog cleared around the player, drawn and kept on the map; icons near; the minimap's edge arrow to boss rooms
  and quest marks within their pointer reach; the world's cues (river, road, camps, signs); the world map's pin on the
  quest's area ([minimap](minimap.md), [quest goals](quest-goals.md)).
- Not found in any source: a path or arrow to an objective across unexplored ground beyond that reach.
- **Light radius** widens the fog cleared and the distance at which doors and points of interest appear on the minimap,
  and Blind cuts it (poe2wiki "Light radius" and Mobalytics, search excerpts only; the wiki refuses automated reads). The
  mod's 60 cells are fixed. Measured since: 80 ([minimap](minimap.md), "The fog").
- UNSURE: whether an icon is drawn over fog inside the bubble. If it is not, "New exit" comes early.
- UNSURE: whether the world map draws areas not visited and the lines between them.

## Verdict

| Idea | Sighted player has it | Verdict |
|---|---|---|
| A. Continuous exploring guide: on arrival it goes on to the next unexplored ground, pending ways included | yes: it reads only seen ground | build |
| B. Unexplored ground (scanner, forks, explore guide) says where it leads, for places already known: listed exits, the edge arrow's boss rooms and quest marks, trails, signs, camps, places kept from earlier visits; by `ExplorationMap.WayOn`, so one edge is tagged, not all | yes: the place is on their map | build |
| C. "New exit: X, your quest leads there" | yes: tracker text and exit name | build |
| D. Quest route on entering an area | the quest's pin, yes; areas not visited, UNSURE | after looking at the world map live; only names it shows |
| E. "Leads to an exit" for exits not known yet | no | not built |
| F. Area size, shape and openness on arrival | no: taken from the grid under the fog | not built; the map parts say it as it is seen |

B is not the arrow loop removed on 2026-11-18 (a straight line through walls): it names the edge of the seen ground the
walk goes through. An exit the client has not sent stays unsaid, as decided on 2026-11-05.
