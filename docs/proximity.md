# What is nearby

`ProximityNarrator` (timing, speech) over `ProximityTracker` (pure) and `EntityCatalog.Entries()`.

- Every 250 ms in gameplay: checkpoints and waypoints, NPCs, objects (stash, bench, well), chests and shrines, items on
  the ground and area transitions (not enemies, players or the tiles' named places).
- An entry is named once when it comes within 200 world units (about half a second of walking) and again only after
  the player has been more than 300 units from it. Up to four names per utterance, utterances 900 ms apart, queued
  after what is being said (D2R ground-items rule).
- Paused, not reset, while a panel or dialogue is open: closing Renly's dialogue does not say "Renly" again. Reset on
  area change. Setting: `AnnounceNearby`.
