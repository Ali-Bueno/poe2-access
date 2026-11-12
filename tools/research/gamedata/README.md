# Game data research scripts

Scratch tools of the boss research of 2026-11-12 (docs/research/boss-mechanics.md §13), kept so the next boss costs
minutes instead of a new reverse-engineering pass. They are not part of the build and nothing ships from here.

- `dat.py` reads any `.datc64` table with the community column layouts (poe-tool-dev `dat-schema`, `schema.min.json`
  from https://github.com/poe-tool-dev/dat-schema/releases/latest). A layout is a hint: `Dat(name).ok()` says whether
  its row size matches this patch's (54 of the 56 tables used did on 0.5.5f, the other two were empty).
- `skill.py <GrantedEffects id>…` prints a skill's card: its kind, animation, stats with values, and the row of the
  table its `SkillID` points into (GeometryAttack, GeometryTrigger, SpawnObject, MoveDaemon, ExecuteGEAL…).
- `gealmap.py` writes `geal-map.tsv`: every GeometryAttack row that has an ExecuteGEAL, with the objects it acts on and
  the commands it runs on them and on its caster. `geal-map.tsv` here is that table for patch 0.5.5f.

They expect, beside this folder's parent as they were run: `dat/data_balance_<table>.datc64` (the tables under
`data/balance/` of the game's bundles, dumped with a scratch bulk dumper that was not kept) and
`schema_dl/schema.min.json`. Set `S` in `dat.py` to the folder that holds both.

## The census of every boss (2026-11-12)

- `cards.py` (with `scripts.py`, `armidx.py`, `summary.py`) builds, in about 20 s from the local dumps, one card per
  fight (`census/cards/*.txt`: skills, ExecuteGEAL rows, adds, objects with their components and states, own states and
  handlers, lines, linked buffs, the arena room) and `census/mechanics.tsv`: one row per (fight, mechanic) with its class,
  what the player must do, the evidence, the run-time signal and the catalog line. `census/content.txt` lists the
  fights by content group. The classes and their data signatures: docs/research/boss-mechanics.md §14.
- `dumper/` is the bulk dumper the dumps were made with (`dumper <gameDir> <outDir> <path prefix>…`, extensions in the
  `EXTS` environment variable, `BIN=1` for binary files such as the `.datc64` tables). The scripts expect, in the folder
  `S` of `dat.py`: `dat/` (tables), `ot/` (scripts), `armall/` (rooms), `all.txt` (every bundled path) and
  `schema_dl/schema.min.json`. Regenerate the census after a patch that adds bosses.

## The Trial's "find the exit" rooms (2026-11-12)

- `trial-rooms/armlinks.py` reads a room's `.arm`: its objects in order, each one's variables, and the links between them
  (`<from> <to> "<label>"`); `rules2.py` checks that a door's levers are told by the pillar its indicators draw; `table.py`
  writes `find_the_exit_rooms.tsv`. `wiring_by_room.txt` is every lever, indicator and door of the 28 rooms for 0.5.5f.
  They expect the Trial's terrain files dumped beside them (`dumper <gameDir> <out> metadata/terrain/gallows/leagues/sanctum/`,
  `EXTS=.arm`). What they showed: docs/research/trials-and-campaign.md §1.6.
