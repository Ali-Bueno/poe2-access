# What is nearby

`ProximityNarrator` (timing, speech) over `ProximityTracker` (pure) and `EntityCatalog.Entries()`.

- Every 250 ms in gameplay: checkpoints and waypoints, league devices (a Ritual altar), NPCs, objects (stash, bench, well), chests and shrines, items on
  the ground and area transitions (not enemies, players or the tiles' named places).
- An entry is named once when it comes within 200 world units (about half a second of walking) and again only after
  the player has been more than 300 units from it and, except for items on the ground, a minute has passed (a fight
  around a checkpoint repeated it every 8 s; an item walked back to said nothing, user 2026-10-26). Up to four names per utterance, utterances 900 ms apart, queued
  after what is being said (D2R ground-items rule).
- Paused, not reset, while a panel or dialogue is open: closing Renly's dialogue does not say "Renly" again. Reset on
  area change. Setting: `AnnounceNearby`.
- What A uses (the entity the game flags as targeted) is named with the button once it holds 200 ms, and again once the
  player has gone more than 300 units from it and come back within 200 (a lone item kept the game's target from afar, so
  walking back over it said nothing, 2026-10-26); it cuts in
  like a focus: queued, it came after other lines and the player pressed A on what the game had already moved to
  (2026-10-26). It goes out as the player's own move (`IAccessibilityOutputService.Moved`): it cuts another item's
  name, a message being kept ("Inventario lleno"), the quest page and news held only by its level (a state that began
  such as "Remolino", an arrival); it waits only behind a tutorial lesson, urgent news (life at its last mark, a boss's
  call, a discovery) for its 1.5 s, and a life or flask warning being heard (user 2026-11-11: of 1727 such names in
  two sessions' logs, 256 had waited for a kept line and 141 behind an urged or mentioned one). The game's tooltip for that item on the HUD is then not said again (only its quality), whatever name it
  shows: a magic or rare item's full name ("Bastón largo intenso de impacto") is not the base the scanner knows, and its
  whole tooltip used to be read as HUD news. Setting: `AnnounceInteractTarget`.
- A breakable's prompt is not said again when the last prompt said was a breakable of the same name less than 5 s before
  (a row of pots says "Tiesto, A" once; `BreakablePrompts`); another name or another kind of object is said at once.

- Where the player arrived and still stands (within 200 units of where the area's line went out) what A uses is no move of the player's: it
  waits for the area's line and the quest page, which it used to cut (review of 2026-11-12; heard live, "Transportador, A" after both).
- What the game writes an object is for is said once an area, after its name and button: "Cáliz de sangre, A. Derrota a todos los monstruos
  raros del área para llenar el cáliz" (a WorldDescription's `body_text`, `world-titles.json` "body:" entries, 34 objects; `ScanEntry.About`).
  An amount the game fills in is "algo" ("Restaura algo de honor"); a counter the game keeps is left out; nothing for what a clock runs against.
- What happened behind a kept line ("Recogido 5 de oro") follows the move that cuts the line instead of being dropped (`SpeechFloor.Move`).
