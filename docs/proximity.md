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
  (2026-10-26). The game's tooltip for that item on the HUD is then not said again (only its quality), whatever name it
  shows: a magic or rare item's full name ("Bastón largo intenso de impacto") is not the base the scanner knows, and its
  whole tooltip used to be read as HUD news. Setting: `AnnounceInteractTarget`.
