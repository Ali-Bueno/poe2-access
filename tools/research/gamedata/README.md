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
