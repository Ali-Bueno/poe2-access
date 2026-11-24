# Powder barrels of Journey's End (G4_2_2)

Asked by the owner on 2026-11-24 ("hay algo aquí que suena a enemigo, pero no me ataca"; "creo que es un barril… si puedes
añadirle un sonido"). Evidence: dumps `entities-20261124-*` (play copy) and the game's scripts read with `Poe2UiExtract cat`.

- **`Pirates/objects/GunpowderLines`** (trail): a "monster" of life 1 with Life, Actor and Stats only, no Monster and no
  Targetable component. `is_trap_object`, `base_cannot_be_damaged`, `set_cannot_die`: nothing to fight. It growled as an
  enemy until `EntityCatalog.IsProp`. It burns by its action (`SpellProjectileFire`), lit by the pirates.
- **`Pirates/objects/BarrelPlaced`, `BarrelPlacedSecondary`, `BarrelLandmark`**: no name, no Targetable, StateMachine with
  `prime` and `explode`. `on_state_prime_1` plays `open_windup` (sounds `ExplodingBarrels/Windup/Fuse` and `Ignite`),
  `on_state_explode_1` plays `open_01`, and BarrelPlaced stops blocking. Blast by the monster `Pirates/CampExplosion`
  (hidden, untargetable). PROVEN from the scripts; `prime=0, explode=0` seen live; a lit one not yet seen live.
- **`Pirates/objects/BarrelForPickup`**: a pirate's ammunition (`claimed`), no hazard of its own.
- The mod: `PowderBarrels` (pure), an Objects entry told by sound alone, `powder_barrel` standing and `powder_fuse` lit, two
  nearest of each. Open: the blast's radius and the fuse's length; the sounds are the owner's picks (A of both) on their
  [page](https://claude.ai/artifact/5FuHttSuuDXSen2JLYdS87).
