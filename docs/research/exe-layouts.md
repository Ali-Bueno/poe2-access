# What the game's exe says about its own layouts

Static study of `PathOfExileSteam.exe`, build of 2026-11-05 (0.5.5f), read as a data file: no process attached, nothing run or
changed. The question: can the exe tell the mod its struct offsets, so a patch needs no measuring?

**Answer:** yes. `tools/exe_reader/reader.py` recovers 21 of 21 checked offsets in about 7 seconds with no fixed address and no signature,
and signatures with the operand wildcarded proved better than expected: measured on five builds, they read the very move that silenced
the mod.

## Measured on five builds

Steam's own log (`logs/content_log.txt`) keeps the manifest of every build installed, and the exe lives in the small depot (2694492, 117 MB),
not in the 154 GB one. Steam's console fetches an old one in four seconds with no password and no other tool:
`steam://open/console`, then `download_depot 2694490 2694492 <manifest>`; it lands in `steamapps/content/app_2694490/`, apart from the
install. Depot manifests of the builds found:

| Build | Manifest of 2694492 | Exe linked (UTC) | Exe size |
|---|---|---|---|
| 25244054, 25379396, 25568465 | 1823245192867123891, 7923567827680705977, 3364810464343845204 | 2026-09-10 03:17 | 78,325,080 (the same file in all three) |
| 25740136 | 246779027001700092 | 2026-10-05 01:52 | 78,335,832 |
| 26261440 (0.5.5e) | 4973272861086296374 | 2026-10-27 22:23 | 78,336,856 |
| 26473991 (0.5.5f) | 8359096664145304946 | 2026-11-05 10:04 | 78,439,768 |
| 26476662 (hotfix, still called 0.5.5f) | 3411296386151915616 | 2026-11-06 03:24 | 78,440,792 |

- **Signatures:** all 45 hit exactly once in all five distinct exes. Before 0.5.5f eight of them read other values: `Area.Level` 0xBC, `Hash`
  0x114, `Environments` 0x4C0, `ServerData` 0x5B0 and the rest of AreaInstance's old layout, exactly what upstream's constants said. So the
  signatures read, unchanged, the move of 0.5.5f that silenced enemies, radar and items. 0 false hits.
- **Reader:** 21 of 21 on the last two; on the older ones its three AreaInstance lines read the true old values (0x8D0, 0x5D0, 0x5B0),
  which is right. On 25740136 two rules fall through and those fields read None: the rules are less sturdy than the signatures.
- **The hotfix of 2026-11-06** landed at 02:42, minutes after the study ended, with nothing prepared for it (73 MB, `.text` 256 bytes
  larger). No field moved, no table key or animation row changed, the addresses of the hits shifted by 0 to 1376 bytes. In play: "El juego se ha
  actualizado: el mod lo ha encontrado todo", 17 structs intact, 0 lost.

These five span eight weeks of hotfixes and one real patch, on one compiler setup. They say nothing of a major version, and the build
of 0.5.5 itself and earlier are not in Steam's log here.

Proof levels below: **A** a string of the game names the function; **B** the class is proven by construction (a `GetComponent` return,
a vtable, a constructor); **C** weak. Everything was read at the addresses given; the meaning of several fields comes from our own tables,
not from a game name.

## The binary

- PE32+ x64, linker 14.40, `.text` 48,868,798 bytes of plain code (entropy 6.45): MSVC with link-time code generation, no PGO, no
  packer. Only 1 of 633,788 relocations falls in `.text`, so the file should match memory (inferred; no process was read).
- `.pdata` has 154,738 rows and **misses leaf functions**: a gap sweep found 38,840 more. An early "no getter exists" was wrong for this.
- No exports, no PDB. RTTI does not name the game's classes: 5,848 type names, mostly third party (`exprtk` alone 2,712).

## What the exe gives

- **Pools and sizes.** Initialisers do `lea rdx,"Life"; lea rcx,pool; call 0x1027EB0; lea rax,vtable`, so each component has a pool
  with its name. 220 of 233 show their size in vtable slot 1 as `cmp rdx,<size>`: Life 0x408, ClientActor 0xCD0, Render 0x620,
  Positioned 0x540, Buffs 0x180, Stats 0x1B8, Targetable 0x90, Player 0x400, Animated 0x370, Chest 0x190, Base 0xD0, Mods 0x270,
  Transitionable 0x128. A size that changes says a struct grew, not where. The "Actor" pool is 0x9B8 bytes: the real component is
  ClientActor, the only size with room for Deployed at 0xC28.
- **`GetComponent<T>`.** 135 wrappers of 116 bytes, 16,354 call sites (Animated 2,856, Actor 1,973 at `0x153470`, Stats 1,810, Life 861
  at `0x153B40`, Render 493, Buffs 289). Whatever uses the return value is that class, by construction.
- **GEAL, the game's script language.** A table of `{L"Name", factory}` rows at `0x444A8E0` (506 rows) leads from names to real code:
  `IsAlive` compares `[Life+0x1E0]`, `GetObjectId` reads `[+0x88]`, `IsWalkable` reaches one leaf holding the terrain layout. 223 more
  names appear in factory error strings (`GEAL::...::Name`). This is the closest thing to reflection the game has.
- **Other named fields:** `GetLocation` (Entity+0x98 caches Positioned, then 0x444), `GetOrientation` 0x44C, `GetObjectSize` 0x1F8,
  `GetAreaId` Area+0xA0, `CurrentAnimationIndex` AnimationController+0x190.
- **Dead ends:** exports, a PDB, debug command strings.

## Fields

| Field | Value | Proof | Site | Bytes |
|---|---|---|---|---|
| Entity.Details | 0x8 | B | 0x153B4D | `48 8B 49 08` |
| Entity.Components | 0x10 | B | 0x153B94 | `48 8B 47 10` |
| Entity.Id | 0x88 | A | 0x207CEBF | `8B 88 88 00 00 00` |
| Entity.IsValid | 0x8C | C | 0xF6FB44 | 17 test, 4 movzx sites |
| Actor.AnimationId | 0x8B0 | A | 0x2093E4C | `C7 80 B0 08 00 00 40 04 00 00` |
| Actor.PoiseThreshold | 0x4E4 | B | 0x1576F82 | `66 0F 6E 80 E4 04 00 00` |
| Actor.Poise | 0x4E8 | B | 0x28C430 | `66 0F 6E 90 E8 04 00 00` |
| Actor skill sub-object | 0x9C0 | A | 0x16DF240 | `48 8D 81 C0 09 00 00` |
| Actor.ActiveSkills | 0x9C0+0x148 | A | 0x206A166 | `48 8B 98 48 01 00 00` |
| Actor.Cooldowns | 0x9C0+0x160 | B | 0x1E973DA | `48 8D 8B 60 01 00 00` |
| Actor.Deployed | 0xC28 | B | 0x2156530 | `48 89 8F 28 0C 00 00` |
| Life Health.Current | 0x1E0 | A | 0x207C7FC | `83 B8 E0 01 00 00 00` |
| Life vital objects | 0x1A8, 0x200, 0x240, 0x2E0, 0x330 | B | 0x1D9308B on | `4C 8D BE A8 01 00 00` |
| Render position | 0x138 | B | 0x1A3C1B | `0F 10 B0 38 01 00 00` |
| Render bounds | 0x144 | C | 0x1806097 | `F3 0F 5C 88 44 01 00 00` |
| Render height | 0x1B0 | B | 0x157085E | `F3 0F 11 83 B0 01 00 00` |
| Buffs effects | 0x160 | A | 0x1DC1B75 | `48 8B 99 60 01 00 00` |
| Stats | 0x160 | B | 0x1292D3 (1,918 sites) | `48 8B 88 60 01 00 00` |
| Stats weapon index | 0x168 | B | 0x170D96 | `8B B6 68 01 00 00` |
| Stats | 0x1C8 | B | 0x18BDC3B | `48 8B 88 C8 01 00 00` |
| InGameState area | 0x290 | B | 0x238620 | `49 8B 9C 24 90 02 00 00` |
| InGameState ui root | 0x2F0 | C | 0xFD2553 | `48 89 86 F0 02 00 00` |
| InGameState pad root | 0x318 | C | 0xFFAE0A | `48 8B 81 18 03 00 00` |
| InGameState world data | 0x368 | B | 0x173A74 | `49 8B 8D 68 03 00 00` |
| Area.Level | 0xC4 | B | 0x206D24F | `0F B6 88 C4 00 00 00` |
| Area.Hash | 0x11C | B | 0x1683B9C | `8B 93 1C 01 00 00` |
| Area.Environments | 0x4C8 | B | 0x1689E4A | `48 8B AF C8 04 00 00` |
| Area.ServerData | 0x5B8 | A | 0x21429C9 | `48 8B 87 B8 05 00 00` |
| Area local players head | 0x5C0 | A | 0x2142762 | `48 2B 8F C0 05 00 00` |
| Area.LocalPlayer | 0x5D8 | B | 0x213DFD6 | `48 8D 86 D8 05 00 00` |
| Area awake list | 0x718 | B | 0x213E149 | `48 8D 9E 18 07 00 00` |
| Area sleeping list | 0x728 | B | 0x213E17A | `48 8D 9E 28 07 00 00` |
| Area.Terrain | 0x8F8 | A | 0xB2B1A0 | `48 8D 81 F8 08 00 00` |
| Terrain.TotalTiles | 0x18 | A | 0x1D1A434 | `6B 41 18 17` |
| Terrain.TileDetails | 0x28 | B | 0x1D1753D | `48 89 69 28` |
| Terrain.GridWalkable | 0xD0 | A | 0x1D1A454 | `4D 8B 84 C9 D0 00 00 00` |
| Terrain.BytesPerRow | 0x130 | A | 0x1D1A466 | `45 0F AF 91 30 01 00 00` |

No name anchor exists for `Positioned.Reaction`, `IsValid`, poise or the Render fields. Which Life sub-object is Mana, shield, Ward or
Divinity rests on their order only. The Life constructor builds seven objects (the five above plus 0x298 and 0x380), which matches
what `StructShapes.PlaceVitals` found live.

Values derived from the exe before reading our source, as a check: Transitionable 0x120, Chest 0x160 and 0x168, Base 0x10 and 0x18, Player
0x1B0 and 0x204, Animated model 0x358, and Targetable 0x69 and 0x6B (the plugin's live values, upstream's are stale) all agree.
`Animated.AnimatedEntityPtr` does not: 0x280 has no code using it, 0x350 has 2,564 sites. The plugin reads only `ModelPath` from that
component, through 0x358, so nothing depends on it.

## The animation record, from both sides

`SetActorType` writes `[Actor+0x8B0]` right after `GetComponent<Actor>`: the same immediate at D and D+0x18, then qword, word and byte
stores at D+8, +0x10, +0x12. All 12 matches of that shape, with 6 different base registers, are this field (0x738 at 3 sites: a
sub-object at +0x178). The same record sits at +0x380 and +0x398, which is the pair `AnimationFieldSearch` found live with no knowledge
of the code. `CurrentAnimationTypeMatches` reads +0x380. The immediate is 0x440, which is 1088, the row count of the Animation table: a
"none" past its last row, so it moves with the table.

Live, the record is layered. For a run or a monk's blow, +0x380 and +0x8B0 hold the same id and +0x398 holds 1088. While a crossbow shoots,
+0x380 stays on `IdleLayerBaseStatic` (443) and +0x398 and +0x8B0 go `PowerShotPiercing` (362), `ChangeAmmoAfterFirePiercing` (638),
`PowerShotPiercing`. So +0x8B0 is the animation that shows, and it is the right field for the dodge and shield cues. The study's guess that it
was a pending record was wrong.

## Signatures

45 are unique over the whole image with at least 12 fixed bytes: [evidence/exe-signatures-0.5.5f.json](evidence/exe-signatures-0.5.5f.json),
each with every displacement, immediate and branch operand wildcarded. Rated by how they are built, not by measured survival: 9 low
fragility, 24 medium, 12 high. Entity 0x8 and 0x10, the 0x5C0 read and the leaf getters have no unique form.

```
AnimationId  C7 80 ^ ?? ?? ?? ?? ?? ?? ?? ?? 45 33 C9 4C 89 88 ?? ?? ?? ?? 66 44 89 88 ?? ?? 00 00
Entity.Id    8B 88 ^ ?? ?? ?? ?? 89 4C 24 ?? C6 44 24 ?? ?? 48 8D 4C 24 ??
Render pos   F3 0F 10 80 ^ ?? ?? ?? ?? F3 0F 10 88 ?? ?? ?? ?? F3 0F 11 44 24 ??
Walkable     41 0F B6 C0 48 8D 0C 40 4D 8B 84 C9 ^ ?? ?? ?? ??
```

Before they were measured they looked like the weakest route, and that reasoning still marks where they will break: within one build,
Stats+0x160 has 1,918 sites with 44 encodings of the anchor and 283 three-instruction skeletons; even 135 wrappers from one template come
out in 5 shapes. Upstream's patterns changed registers twice (`4C 8B 06` to
`4D 8B 06`, `45 0F 57 C9` to `0F 57 FF`), and one jump between March and May rewrote all six. After that only one changed (0x140 to
0x148 in 0.5.5) while offsets moved in five patches, so code shape often survives; but those six are short and hand-picked, which bounds
nothing for field-reading code. `GameProcess.cs:344` treats every hit as RIP-relative, so reading a field would need a second list.

### In use since 2026-11-06

Two more studies made 169 signatures for 66 fields ([evidence/code-fields-0.5.5f.json](evidence/code-fields-0.5.5f.json)); 50 lines of
them went into `catalog/code-fields.txt` ([how they are used](../compatibility.md#read-from-the-games-code)). Its new anchors: 29
per-component debug dumps labelled in the exe (`=== Player ===`, `Experience: `, `Chest open: `, `Team: `) and the `.ot` key parsers
(`charges_per_use`, `max_stack_size`, `large`). Each pattern hits once in `.text` in all five builds, reads the right value, and wildcards
only operands. 6 of the 45 earlier signatures keep operand bytes fixed, because capstone misreports a displacement after a 66 prefix;
these decode ModRM.

Control, on signatures built from 0.5.5f alone and not picked to pass:

| Sample | Sites | Still one hit | Gone | Many hits |
|---|---|---|---|---|
| Random field reads, two months | 813 | about 99% | 1.1% | 0.1% |
| Functions that changed, two months | about 1,300 | 81 to 83% | 16 to 18% | 0.2 to 0.4% |
| Functions that changed, hotfix | 328 | 96.6% | 3.4% | 0 |

1.6 to 2.4% of 108,000 functions changed shape between builds, 0.9% on the hotfix. So expect one signature in five to miss in a changed
function, which is safe. The risk is a wrong value: about 5.5% of single hits in changed functions read another operand, some certainly
real moves, not all provable. That is why two from different functions must agree and a live judgment wins. All five builds are one
two-month line, and upstream's jump from March to May rewrote all six of its patterns, so none of this speaks for a major patch.

The last 11 fields (the stun record, `NodeInBuild`, `ButtonMenu`, `PlayerStride`, the terrain) each have two or three signatures from
different functions. Their functions did not change across the five builds, so they say nothing yet about surviving a recompile.
`GridLandscapeData` is proved only as the second of four grids of one format: "landscape" is upstream's name.

The study also corrected the structs: `IsStrongbox` reads +0x50 where the code reads the key at +0x48 (the other half of it, so it works),
`IsLabelVisible` is the `.ot` key `large`, and Transitionable is a byte with a separate flag at +0x121, which the C# `short` mixes.
`UnscaledSize` 0x270, `BackgroundColor` 0x28C and `StringIdPtr` 0x128 are stale upstream fields; the mod reads none of them.

## The semantic source

Since 2026-11-08 the host reads a third set of offsets from the exe on disk, beside the live searches and the signatures:
`host/GameReader/Utils/CodeReader/`, rules in `catalog/semantic-fields.txt`, ported from `re/semantic.py` and `re/semantic_ext.py`
(poe2-research). A rule names no byte and no register beyond the calling convention (`this` in rcx, a result in rax). It starts from
a name the game carries and takes what the code does next to it, so a recompile that moves registers or inlining does not stop it
the way it stops a signature.

| Kind | Starts from | Takes |
|---|---|---|
| `label` | a dump's wide label (`L"Life: "`) | the k-th field loaded after the call that prints it, up to the next label |
| `field` | the same | the k-th distinct field of the dump's `this` in a linear sweep; a virtual slot that is `lea rax,[rcx+X]; ret` in every vtable holding the dump adds X |
| `called` | the same | in the n-th function called after the label, the value it returns or the k-th field of argument i |
| `geal` | a GEAL row, then the node vtable's slot 2, then the evaluator (through a thunk) | the k-th field read through `GetComponent<T>`'s result |
| `world` | the same, after `GEAL::GetGameWorld()` (the one function loading that string) | an AreaInstance field; with `deref D`, the first pointer loaded from the world followed D levels down and the lowest field read there |
| `callee` | the same | the lowest `[rcx+D]` read by the callee that follows `GetComponent<T>` |
| `getter` | `GetComponent<T>`: the 0xA0-byte or smaller function loading T's name that has the most callers | details, lookup, map, components |
| `object` | a GEAL row, as `geal` | the k-th field read through the game object the argument helper returns: the function loading `Requires a Game Object (or an Animated Object attached to one)` |
| `deref` | the same | from argument i of the evaluator, the first pointer loaded at each level, k levels down; the lowest field read through the last one |
| `slot` | the same, after `GEAL::GetGameWorld()` | the world's virtual slot called next (in the evaluator, or in the first callee the world is passed to), read in every vtable holding a dump that prints the label: `lea rax,[rcx+X]; ret` gives X, anything else its lowest this-field, and all must agree. `then` adds the lowest field the next callee reads from the result; `inline` takes the address the constructors (the functions loading those vtables) store into the field |
| `this` | the one function loading an ASCII string (and a second one) | the k-th distinct field it reads through `this` |
| `called … deref` | a label, as `called … arg` | from the callee's argument i, the first pointer loaded at each level, k levels down; the lowest field read there |
| `stride` | a label's dump, or a GEAL evaluator | the first vector read after the site (a pointer load compared with the next pointer within 6 instructions), then the immediate the loop adds to its iterator right before comparing it with the end again |
| `entry` | the same | the k-th distinct field that loop reads through the pointer each entry holds (one level down from the iterator) |
| `leaf` | a GEAL row, after `GEAL::GetGameWorld()` and the world's virtual slot | the k-th distinct field (indexed reads included) the callee taking the slot's result reads through it; the sweep to that callee goes past jumps, because the evaluator short-circuits before it |
| `scale` | a label's dump | the factor an index is scaled by (`shl r,n`, `lea r,[x+x*s]`, three-operand `imul`) before it meets a pointer the code loaded (`add r,[mem]`, or both in one address): a vector's element size when the code walks it by index |

**Fields.** There were 64 rules for 50 fields, and all 64 read the 0.5.5f value (80 rules since [the gap fields](#the-gap-fields)):

- **Life:** Health, Mana and ES, Current and Total. Health.Current also comes from GEAL `IsAlive`.
- **Player:** Xp. Level, from the `Level: ` printed in the function that also prints `Experience: `. Name, through an address-of.
- **Chest:** IsOpened.
- **Targetable:** the plugin's IsTargetable.
- **Transitionable:** the state, from the label and from `GetTransitionState`.
- **Stats:** the weapon index, and 0x160 from `GetStat` and `IfStat`.
- **ObjectMagicProperties:** Rarity and ImplicitMods. ExplicitMods comes from the field rule: `add rdi,0xC8` and then +0xA0.
- **Buffs:** the effect vector, from `HasBuff`, `Buffs: ` and `GetBuffTotalCount`.
- **StatusEffect:** the definition at +0x8 and the charges at +0x40. Both come from the printer `Buffs: ` calls, which takes the entry as its second argument.
- **Positioned:** Reaction. The `Team: ` getter returns `word [+0x1E0] & 0x7FFF`.
- **Actor:** AnimationId, from `SetActorType`. This is the weakest naming, because 0x440 is the Animation table's row count, not a name. ActiveSkills comes from virtual slot 0x170 (`lea rax,[rcx+0x9C0]`) plus 0x148.
- **StateMachine:** 0x158 and 0x160, from two dumps.
- **Area:** the level, from `IsLevel` and `IfLevel`. The hash comes from `Tile hash: `, which is the area's own tile hash in slot 0x1E0 of both world vtables, not the terrain's.
- **Entity:** 0x8 and 0x10, the details' lookup at 0x28 and its map at 0x28, from the Life and Render getters.

- **Entity:** the id, from `GetObjectId`. The path, from `TypeContains`.
- **Area:** the terrain, the entity maps and the local player, from the world's virtual slots that `IsWalkable`, `GetGameObjectById` and `GetLocalPlayer` call.
- **InGameState:** the area, from the one function that loads `league` and `class`.

- **AnimationController** (the dodge clock, 2026-11-08; `dodge-timing.md`, Protections): the clip index from GEAL
  `CurrentAnimationIndex` and `GetCurrentAnimationLength`; the clip speed from `GetCurrentAnimationSpeed` and the
  `Current Animation Speed: ` dump; the global speed from `GetGlobalAnimationSpeed` (its first field) and
  `Global Animation Speed Multiplier: `; the global-off flag as that evaluator's second field; the position from
  `Current Animation Position: `.
- **Actor** (the dodge aim): the current action at 0x238 from `GetTarget`, `GetTargetLocation` and `GetCurrentActionStat`;
  the action flags at 0x2A0 from `IsCurrentAction`. These 12 rules read the same value on all five builds.

### The world fields

Added 2026-11-08 for the seven fields of the audit that had one protection each (`field-protection-audit.md`, A1, A10, A14, A15, E1, E4b,
C1). Each rule hits once per build and reads one value; the three older builds read the layout before 0.5.5f (terrain and entities 0x28
lower, the player 8 lower), which is what upstream's constants said and the live searches measured. The id, the path and the state's
area did not move in these five builds, and the signatures of the earlier study read the same.

| Field | Rule | What it anchors on | 25244054, 25740136, 26261440 | 26473991, 26476662 |
|---|---|---|---|---|
| `EntityOffsets.Id` | `object GetObjectId 0` | the evaluator calls the helper whose error reads `Requires a Game Object (or an Animated Object attached to one)` and then `mov ecx,[rax+0x88]` | 0x88 | 0x88 |
| `EntityDetails.name` (the path) | `deref TypeContains arg 3 2` | the GEAL value in r8: `[value]` is the object, `[object+0x8]` its details, and the path is the wide string at details+0x8 (its capacity +0x20 and size +0x18 are read beside it, the lowest wins) | 0x8 | 0x8 |
| `AreaInstanceOffsets.TerrainMetadata` | `slot IsWalkable "Tile hash: "` | after `GetGameWorld()` the evaluator calls slot 0x1A8; in the two world vtables holding the `Tile hash: ` dump it is `lea rax,[rcx+0x8F8]; ret` (a third vtable has a stub there that reads no field, so it does not count) | 0x8D0 | 0x8F8 |
| `AreaInstanceOffsets.Entities` | `slot GetGameObjectById "Tile hash: " then` | slot 0x1B8 is `lea rax,[rcx+0x708]`, the object registry, in all three vtables; the lookup it is passed to reads `[rcx+0x10]` first, the awake map's head | 0x6F0 | 0x718 |
| `AreaInstanceOffsets.PlayerInfo.LocalPlayerPtr` | `slot GetLocalPlayer "Tile hash: " inline` | `GetLocalPlayer` is a factory of another GEAL table, found by its own arity error string; its evaluator passes the world to a helper that calls slot 0x148; two vtables read `[this+0x5C8]` and `[this+0x5C0]`, the players' end and begin (the third reads nothing). The base constructor, the function loading the base vtable, does `lea rax,[rsi+0x5D8]; mov [rsi+0x5C0],rax`: the inline storage of the small vector is the field | 0x5D0 | 0x5D8 |
| `InGameStateOffset.AreaInstanceData` | `this "league" 0 with "class"` | one function loads both `league` and `class` (and `level`, `area`): a status builder on the in-game state. The first field it reads through `this` is the area: `cmp [rbx+0x290],0`, later `mov rax,[rbx+0x290]; mov rcx,[rax+0xA0]` (the area id) | 0x290 | 0x290 |
| `RenderOffsets.CurrentWorldPosition` | two signatures, `Render.WorldPosition` in `code-fields.txt` | no name reaches it: the Positioned dump's `Position: (` prints Positioned's own floats at 0x490, and no first field dominates the 493 `GetComponent<Render>` sites (0x1D0 47 times, 0x39A 41, 0x138 16). Of the 14 sites reading it through the getter, two in different functions (0x269C50 and 0x266B50 in the hotfix) are unique with the operand wildcarded, and both read 0x138 on all five builds | 0x138 | 0x138 |

Tried and dropped: `IsType` and `IfType` compare component names (`player`, `monster`, `chest`) through the lookup, not the path;
`IsSameType` compares the two details pointers; `GEAL::GetGameWorld()` does not read the state but asks the GEAL context (its
slot 0x28); no leaf `mov rax,[rcx+0x290]; ret` exists, so the state has no getter to find through a vtable.

### The entry fields

Added 2026-11-08 for the fields of the audit that had no protection or one (`field-protection-audit.md`, C17b, C11, C14, T1, T3,
T4). Each rule reads one value on all five builds; none of these fields moved in them, and the signatures of `code-fields.txt` read
the same where they have one. The skill entry size became a plugin layout field (`layout:SkillEntrySize`) so the catalog can move it.

| Field | Rule | What it anchors on | Five builds |
|---|---|---|---|
| `BuffDefinitionsOffset.Name` | `called "Buffs: " 1 arg 2 deref 1` | the printer the Buffs dump calls per effect does `mov rax,[rdx+8]; mov rdx,[rax]`: the definition is the entry's first pointer and its name the first field of that | 0x0 |
| `layout:SkillEntrySize` | `stride label "Active Skills: "`, `stride geal ConsumeSkillUse` | the Actor dump loads `[sub+0x148]`, compares it with `[sub+0x150]` and walks it with `add rbx,0x10`; `ConsumeSkillUse` walks the same vector the same way | 0x10 |
| `layout:SkillLevelKey` | `entry label "Active Skills: " 0`, `entry geal ConsumeSkillUse 1` | each entry's first pointer is the ActiveSkillDetails; the dump reads only `[details+0x58]` (the GrantedEffectsPerLevel key it prints), `ConsumeSkillUse` reads `word [details+0x44]` and then `[details+0x58]` | 0x58 |
| `TerrainStruct.TotalTiles` | `leaf IsWalkable 0` | after the world's slot 0x1A8 (the terrain) the evaluator calls one leaf with it: `imul eax,[rcx+0x18],0x17; … [rcx+0x20]` | 0x18 |
| `TerrainStruct.GridWalkableData` | `leaf IsWalkable 2` | the same leaf picks a grid by kind: `mov r8,[r9+rcx*8+0xD0]; cmp r8,[r9+rcx*8+0xD8]` (the five grids are 0x18 apart, so the landscape grid is never named) | 0xD0 |
| `TerrainStruct.BytesPerRow` | `leaf IsWalkable 4` | the same leaf: `imul r10d,[r9+0x130]`, then half the x (two cells to a byte) | 0x130 |

Tried and dropped the same day:

- `StatsStructInternal.Stats` 0xF8: `GetStat` and `IfStat` read `[Stats+0x160]` and call slot 0 of that object's vtable, and no name
  reaches that class (the Stats pool's vtable only allocates; `GetMapStat` looks a stat up in another class at world+0x130).
- `StateMachine` entries: the dump after `StateMachine state ` reads `[StatesPtr+0x10]` and a 0xC0 stride (`lea rax,[rcx+rcx*2];
  shl rax,6`), but the host's `StateMachine.cs` hard-codes both, so no target can take the value yet.
- `ModArrayStruct.ModsPtr` and its stride: read raw by `ReadStdVector` and `MonsterMods.cs`, so they stay `report`.
- Chest 0x160: `AddChestOnOpen` passes the component to a function that reads only its owner at +0x8; the Chest dump prints the
  bytes at 0x168, 0x169 and 0x16D directly.
- `ActiveSkillDetails.SkillId` 0x40: `ConsumeSkillUse` compares `word [details+0x44]`, `HasGrenadeSkill` reads no field of the details.
- Actor cooldowns and the cooldown entry: `IsSkillOnCooldown`, `ForceSkillIntoCooldown` and the like exist as wide strings built into
  static string objects, not as GEAL factories, and `ForceSkillIntoCooldown` (a real GEAL row) only walks the skills.
- Render bounds X 0x144: one site reads it through the getter (one unique signature, `F3 0F 5C 88 ^ … F3 0F 58 C7` at 0x1806097),
  so no second can agree; the Y and Z lines of `code-fields.txt` move the same `CharactorModelBounds` field with a bias, X included.
- `Shrine.IsUsed`: no dump, no GEAL function; the `Shrine` wide string belongs to a UI function. Its signatures now place the field
  (`ShrineOffsets:IsUsed`), since the host reads the struct through the remap.

### The window cull

`GameCullSize` is a static the game subtracts from x coordinates (`sub eax,[rip+X]`) in some two hundred functions; the host's pattern
reads one of them. A second pattern, `GameCullSize (alt)` in `StaticOffsetsPatterns`, reads another (`2B 05 ^ ?? ?? ?? ?? 0F 57 F6 0F 57 FF
48 8B CF F3 48 0F 2A F8`, 16 fixed bytes) and `Core.UpdateCullData` takes it when the first is gone. Both hit once and resolve to the same
static on the five builds (0x458B100, 0x458D210, 0x458D250, 0x45A4BC0, 0x45A4C00). Seven more candidate windows did the same and are
not shipped.

### The gap fields

Added 2026-11-08 for the fields of the audit left with no protection or one (`field-protection-audit.md`, section 2: C26, C28,
C34b, C39b, C40, D2, D3, T2). Two forms are new: `world … deref` and `scale` (table above; the decoder now tells `shl` and `imul`
apart, checked against capstone by the fixture). Each rule hits on all five builds and reads one value; the area row's pointer they
go through moved in 0.5.5f (`world GetAreaId 0` reads 0x98 on the three older builds, 0xA0 after) and the rules follow it.

| Field (audit) | Rule | What it anchors on | Five builds |
|---|---|---|---|
| `WorldAreaDatOffsets.IdPtr` (D3) | `world GetAreaId deref 1` | `mov rdx,[world+0xA0]; mov rdx,[rdx]`, then a `wcslen` loop: the area's WorldAreas row and its Id | 0x0 |
| `WorldAreaDatOffsets.NamePtr` (D2) | `world GetAreaName deref 1` | the same row, `mov rdx,[rdx+8]` | 0x8 |
| `WorldAreaDatOffsets.IsTown` (D2) | `world IfTownArea deref 1`, `world IfTownOrHideoutArea deref 1` | `cmp byte [row+0x14],0` (the second also tests +0x12A, the hideout flag the mod does not read) | 0x14 |
| `layout:MinimapIconComponentRow` (C40) | `geal SetMinimapIcon MinimapIcon 0` | after `GetComponent<MinimapIcon>` the evaluator stores the new row at +0x20 | 0x20 |
| `TerrainStruct.TileDetailsPtr` (T2) | `leaf IsInSector 2`, `leaf IfInSector 2` | the terrain leaf: `imul eax,[t+0x18],0x17` and `[t+0x20]` bound the tile, then `imul rbp,rcx,0x38; add rbp,[t+0x28]` | 0x28 |
| `layout:IsTargeted` (C28) | `geal DisableHighlighting Targetable 0` | `mov byte [Targetable+0x6B],0`, then `+0x6A` (the byte `EnableHighlighting` sets) | 0x6B |
| `layout:ModRowOffset` (C26) | `label "Implicit Mods: " 1`, `label "Explicit Mods: " 1` | the ObjectMagicProperties dump: `mov rax,[this+0x150]; mov rdx,[rax+0x28]; mov rdx,[rdx]`, the first entry's Mods row and its Id | 0x28 |
| `layout:ModEntrySize` (C26) | `scale label "Implicit Mods: "`, `scale label "Explicit Mods: "` | the rest of the entries: `shl rdx,6; mov rdx,[rdx+rcx+0x28]` | 0x40 |
| `layout:StateListOffset` (C34b) | `label "StateMachine state " 2` | `mov rdx,[sm+0x158]; mov ecx,[index]; …; add rax,[rdx+0x10]` | 0x10 |
| `layout:StateStride` (C34b) | `scale label "StateMachine state "` | `lea rax,[rcx+rcx*2]; shl rax,6` | 0xC0 |
| `AnimatedOffsets.AnimatedObjectPtr` (C39b) | `field "Animated Object: " 0`, `geal IfHasAnimation Animated 0` | the Animated dump prints `[[this+0x350]+8]+8`, the `.ao` path; the GEAL function reads +0x350 right after the getter | 0x350 |

`code-fields.txt` gained `Animated.Object` (three signatures, after `GetComponent<Animated>` in `IfHasAnimation`, `SwitchAOTypeName`
and the animated-object helper the mesh and effect-pack functions share: each hits once and reads 0x350 on the five builds, no
operand byte fixed), and its two `ModArrayStruct` lines now place `layout:ModRowOffset` and `layout:ModEntrySize` instead of only
telling. With them, 80 of 80 rules read their value on the two 0.5.5f builds and 74 of 80 on the three older ones (the six area
fields that moved in 0.5.5f, as before).

Swept and not found: a scratch run of 191,188 candidate rules (every form above over every dump label ending in `: `, every GEAL
function of both tables and 48 components) kept only rules whose value was a field the audit wanted on all five builds. Nothing
anchored on their own component came out for Shrine (its getter has one caller, which reads none of it), TriggerableBlockage
(`Block: ` is a DelveNode dump), Charges, Stack, Base, Mods.Rarity, the chest data, the actor's cooldowns or Stats 0x1C8; the
Characters and GrantedEffects Ids (`IsMalePlayer`, `GetAscendancyName` read other columns); the UI flag bits; or the in-game
state's world data and hover chain (`this "league" … with "class"` reads only the area at k 0).

No name reaches these:

- Render: it has no dump and no GEAL getter call; its position has the two signatures above.
- Actor cooldowns and deployed.
- Stats 0x1C8 and the stat vector.
- Base.
- Mods.Rarity.
- Targetable.IsTargeted.
- Animated 0x358.
- Chest 0x160.
- StatusEffect timers and source.

**Simulated move.** In a copy of the exe, the displacement at the `Level: ` site was changed to 0x20C and the two `IsLevel`/`IfLevel` sites to 0xBC. The rules read 0x20C and 0xBC, and the other 35 rules were unchanged.

**Decoder.** It is our own, so no dependency was added. It handles:

- the 66, F2, F3, segment and lock prefixes, and REX;
- the one-byte, 0F, 0F38 and 0F3A maps, and VEX;
- per instruction: the length, the memory operand, the branch target, the immediate, and the general registers it writes.

An encoding it does not know decodes to nothing, so a rule never guesses. This covers x87, system opcodes, EVEX, the 67 prefix, moffs, SHA and AES.

`re/decoder_truth.py` dumps capstone 5.0.7 on every instruction of N random `.pdata` fragments and every instruction the prototype rules walk (70 forms, 2,069 instructions). `dotnet run --project tests/Poe2Accessibility.Tests -- x64-truth <tsv>` compares, for each instruction:

- the length;
- base, index, scale and displacement;
- the branch target;
- the first operand's kind;
- the kind (mov, lea, call, jmp, jcc, ret, cmp, test, add, ...);
- the registers a mov or lea names, and its size;
- add's immediate;
- that its written registers include capstone's.

| Sample | Instructions | Decoded | Disagree |
|---|---|---|---|
| 2,000 fragments | 151,946 | 151,928 (99.99%) | 0 |
| 12,040 fragments | 880,112 | 879,970 (99.98%) | 0 |

- **Writes:** exactly capstone's on 879,868 instructions. The rest are a safe superset, such as `cdq` and `test eax,imm`, which capstone counts as writing rax.
- **Unknown:** data read as code (`cli`, `in`, `out`, `retf`), x87, and VEX `ldmxcsr`.
- **Fixture:** three rows per encoding shape (2,598 rows) are committed as `tests/Poe2Accessibility.Tests/Fixtures/x64-capstone.tsv` and checked by the test suite.
- **Whole fragments:** 154,714 of 154,738 decode (99.98%).

**Switch tables.** MSVC puts switch tables inside the fragment, after the code. Decoding therefore stops at a table the code indexes ahead of itself (`[base+index*4+table]`). At an unknown encoding the code ends after the last ret, jmp or int3 and the rest counts as data; with neither, the fragment does not decode. Every rule takes the first k of something, so a cut can remove a candidate but never change one. The two rules that need a whole stretch check that they have it: the returned load needs its ret, and the field sweep must reach the label.

**Wiring.** At attach, `StartCodeFields` reads the signatures and the rules on a worker, from one read of the file. Each frame, `TakeCodeFields` (in `SayCompatibility`) places what came back:

- `CodeFieldCatalog.Settle` keeps one value per target. A target that the signatures and the rules read differently stays where it is and is logged.
- The value then goes through `PlaceFromCode` or `LayoutFields` as before. A live segment wins, and a value more than 0x400 from the catalog is not believed.
- The log line reads `Semantic fields: 80 of 80 read in … ms; moved: none` (64 before the gap fields). On the warm 0.5.5f exe, the index takes 0.32 s (every E8 call and RIP `lea`, 154,738 fragments) and the rules take 0.15 s.
- `dotnet run --project tests/Poe2Accessibility.Tests -- semantic <exe> <catalog>` prints each rule's candidates for any build.

**Limits.**

- Rerun on the five builds of the table above (archived under `D:\code\decompilers\projects\poe2-research\builds\<build>`,
  2026-11-08): every rule read the true value of each build. On the three builds before 0.5.5f the area level read 0xBC and
  the hash 0x114, the old layout upstream's constants had, so it reads the very move that silenced the mod; 0.5.5f and its
  hotfix read 0xC4 and 0x11C. With the world, entry and dodge fields of 2026-11-08, 58 of 64 (74 of 80 with the gap fields) "right" against the current constants
  on the old builds means exactly the six that moved in 0.5.5f (level, hash, terrain, entities, local player).
  Still one compiler setup over two months: a major version is untested.
- `label` relies on the dump printing in order, and `field`'s sweep ignores control flow.
- A removed dump, a renamed GEAL function or an inlined getter only stops a rule.
- The worst case is unchanged: a restructured function that reads another field of the same shape. That is why its values are candidates that must agree with the signatures, and why a live judgment still wins.
- Placement now comes about a second after attach instead of before the first frame. A live judgment made in that second
  wins, for host structs (segments) and for plugin `layout:` offsets alike (a moved constant is kept, logged "kept … where a
  live search put it").

## How far it goes

| Approach | Changed registers | Changed inlining | Cost |
|---|---|---|---|
| Byte signature plus read | breaks (twice upstream) | breaks | least code, most upkeep |
| Virtual getter | immune | immune | few fields have one |
| Pool size | immune | immune | says a struct grew, not where |
| Name anchor plus operand rule | survives if rules name no register | partly | one rule per field; in use, [above](#the-semantic-source) |
| Function matching across builds | mostly | mostly | offline tooling, better at development time |

A run-time reader would need a real decoder (Iced: a new dependency, so not without asking; the host now has its own, [above](#the-semantic-source)), `.pdata` plus decoding leaves up to `ret`, and
one pass collecting `E8` and RIP-relative `lea` targets. Its worst failure is a restructured function making a rule return a wrong value
in silence, so any result must be a candidate for `OffsetHealer`, never the truth. A renamed GEAL name just stops the rule, which is safe.
Assumptions in `reader.py`: vital Current at +0x30, Cooldowns = ActiveSkills + 0x18, LocalPlayer = head + 0x18, vitals by order; some rules
name `rcx` or `rax`.

## What would measure it

Each future patch adds a point: Steam's log gains the manifest, the reader and the signatures run on the new exe, and the output goes beside
[evidence/exe-reader-0.5.5f.txt](evidence/exe-reader-0.5.5f.txt), which is the 2026-11-06 build. That also answers, before anyone starts
the game, what moved. Ground truth for old builds is upstream's commits: `98ef3ad` (0.5.1), `4183681` (0.5.4), `3293e56` (0.5.4c), `a99b8ba`
(0.5.5). SteamDB would give older manifests, but it refuses scripts.

## Mistakes worth not repeating

- `0xFD24B0` touched 12 "AreaInstance offsets" on one register and looked like its constructor. It is InGameState's. Large constructors
  touch nearly every slot, so offsets appearing together prove nothing.
- First-pass signatures had 3 fixed bytes and were unique by accident; hence the floor of 12.
- `0x5D8` is not a plain pointer but inline storage of a small vector (begin 0x5C0, end 0x5C8, capacity 0x5D0, room for two): reading it
  is right only while the vector stays inline.

## League components (2026-11-14, build 0.5.5f, hotfix of 2026-11-06)

Asked of the exe for the league mechanics ([league-mechanics.md](league-mechanics.md) §14), with the method above; scripts in the session's
scratch folder only (`p2lib.py`, `s1_pools.py` … `s15_world.py`: pools, vtables, GEAL names, parser keys, cross references). Nothing was read
in a running game by this study; what the plugin reads of it is judged at run time.

| Field | Value | Proof | Site | Bytes |
|---|---|---|---|---|
| Transitionable (pool 0x128) type data | +0x118 | B | 0x1E0D334 (constructor 0x1E0D2D0) | `49 89 B8 18 01 00 00` |
| Transitionable state, **one byte, counted from 1** | +0x120 | A | 0x2073F11, GEAL `GetTransitionState` | `0F B6 80 20 01 00 00` |
| the same, set to 1 at construction | +0x120 | B | 0x1E0D33B | `41 C6 80 20 01 00 00 01` |
| the same, stored by `Transition` | +0x120 | A | 0x209D150 | `88 82 20 01 00 00` |
| Transitionable type data: `num_states` (2 to 127) | +0xB0 | A | 0x209D13D | `3A 81 B0 00 00 00` |
| Transitionable "has transitioned" (set, never cleared) | +0x122 | B | 0x209D156 | `C6 82 22 01 00 00 01` |
| Transitionable lock (`LockTransition` / `UnlockTransition`) | +0x123 | A | 0x2097CBC | `C6 80 23 01 00 00 01` |
| Shrine (pool 0x68) IsUsed | +0x24 | B | 0x1DF9795 (network reader 0x1DF9720) | `88 46 24` |
| Shrine **row of Shrines.dat** | +0x28 | B | 0x1DF981C; read again by the popup at 0xE74412 | `48 89 56 28`; `48 8B 51 28` |
| Shrine table handle (what upstream took for the row) | +0x30 | B | 0x1DF9820 | `48 8D 56 30` |
| ControlZone (pool 0xF0) radius, a word | +0x3E | A | 0x2093008, GEAL `SetControlZoneRadius` (component + 0x18, then +0x26) | `66 89 47 26` |
| ControlZone centre offset, a qword | +0x40 | A | 0x2092EB8, `SetControlZoneCentreOffset` | `48 89 47 28` |
| ControlZone objects inside, a vector of 8-byte entries | +0xD8 | B | 0x1DC6029 ("In Zone Count: ") | `48 8B 96 C8 00 00 00` |
| StateMachine values, one 8-byte slot per declared state | +0x160 | B | constructor 0x1E06C30 | - |

- **Transitionable.** Upstream's `short` at +0x120 is a byte followed by three flag bytes (+0x121 a copy of the type's
  `transition_on_damage_taken`). There is no previous or target state. The `.ot` keys: `num_states`, `transition_on_damage_taken`,
  `link_to_instance_group_flag`, `transition_on_damage_from_any_team`, `enable_logging`, events `on_transition`, `on_untransition`. The debug
  dump prints `"Transitionable state: "`, the byte, then the count (0x1E0DDBD). Falsified by a live untransitioned object reading 0, or
  any object reading above its count: `Transitions.Judge` refuses both.
  Read in a running game on 2026-11-14 (Clearfell Encampment, kehgan, dev command "entities all"): the six objects with the component read a state of 1 or 2 and a count of 2, the default of an empty block (Una 1, Renly and the crafting bench 2); no object with a count of its own was in town.
- **Shrine.** The server sends one byte: 0xFF is "used", else the top bit is IsUsed and the low seven bits the row index, multiplied by
  0x122 (the row size of `Data/Balance/Shrines.dat`; a string "Unknown shrine sent by server" stands in the same function). The component
  has no `.ot` keys. The row's display-name column was not reached in code: its Id (the text at the row's +0) is what a reader can trust.
- **ControlZone.** A trigger-area shape copied from the type data sits at +0x20 (shared with ProximityTrigger, whose radius
  `SetProximityTriggerRadius` writes at the same slot of its shape). The `.ot` key `radius` sets the default (0x1DB881D). Flags: byte +0x39
  (`require_line_of_sight` 4, `use_manhattan_distance` 8, `use_distance_to_edge` 0x10, `use_perpendicular_distance` 0x20,
  `trigger_on_hidden` 0x40), byte +0x3A (`match_any_condition` 1, `match_non_collidable` 2, `orientation_rotates_trigger_area` 4). **No
  progress field**: a Breach's ring growing can only show as a script writing the radius.
- **StateMachine.** `define_state`, `define_shared_state` and the two checkpoint kinds all append to one vector of 0xC0-byte definitions in
  the type data (+0x10: name, initial value +0x24, shared flag +0x2C; 0x1E03EC0), and the component holds one value per definition whatever
  its flags. The network reader skips the entries that are not shared (0x1E07CE3), so a client state (`in_placing_range`, `in_range`,
  `blocked`) is **in the same array a memory reader walks**, holding what the client computed. One "StateMachine" registration, no client
  class of its own. Not traced: that the `client { }` block of a file feeds the same object type (it does for every other component).
- **GEAL names of the leagues.** `ClearControlZone`, `ForEachInControlZone`, `SetControlZoneRadius`, `SetControlZoneCentreOffset`,
  `TickControlZone`, `SetProximityTriggerRadius`, `IfExpeditionArea` (the area's WorldAreas row), `AreaGroupRitualCompleted`,
  `AreaGroupRitualNumCompleted`, `AreaGroupRitualTotal`, `AreaHasRitualGroup` (not examined). `DisableFog` and `ResetFog` are a shared stub
  on the client: the Delirium fog has no field this way. No GEAL name mentions Delirium, Affliction, Breach, Abyss, Essence, Ultimatum or
  Shrine.

## Rulesets (2026-11-14, third pass: the league mechanics are not in components)

Asked: where the Breach's ring, the Expedition's charges and the Trial of Chaos' round are kept, since no component held them. Of the 233
component pools none is a Breach, an Expedition explosive, an Ultimatum encounter or an Affliction (`BreachObject` is only a name in the
component enum; `BrequelFruit`, `ExpeditionSaga`, `RitualEncounter`, `UltimatumKey`, `IncursionTemple` are item components). **The client keeps
one ruleset object per row of `Rulesets.dat`, filled from server packets**, and the mechanics live there. Scripts in the session's scratch
folder only (`exe3/`: `a1_allpools`, `a5_rulesets`, `a7_vt`, `a8_datrow` …). The pool registrar of the file on disk is at 0x1028270, not
0x1027EB0 as written above.

**Reaching a ruleset.** The session of the instance server (constructor 0x212D160, which logs "Connecting to instance server") stores the
AreaInstance at +0x20F0 (`0x212D805: 48 89 91 F0 20 00 00`) and an array of ruleset pointers by row at +0x2400 (`0x212F7FB: 4A 89 9C F6 00
24 00 00`), which the getter reads back for a row below 0x66 (`0x284BBB: 48 8B 80 A8 01 00 00`, `0x284BC5: 48 8B 84 C8 00 24 00 00`). The
session is stored at +0x340 of an object taken for InGameState, not proven (`0x1631DAC: 49 89 85 40 03 00 00`). Every ruleset (base
constructor 0x1729A80): +0x30 its Rulesets row, +0x38 the row's table handle, +0x48 the session. Rows by the constructor's argument: 92
ExpandingBrequelEncounterRules, 93 Established…, 51 ExpeditionRules, 49 UltimatumRules, 40 AfflictionEncounterRules.

| Field | Value | Proof | Site | Bytes |
|---|---|---|---|---|
| Breach rules (0x1B0 bytes, vtable 0x33BD200): vector of 0x48-byte encounters | +0x190 | B | packet reader 0x172A8F0, update 0x172A3E0 | - |
| encounter: size it grows from / to, floats 0..100 | +0x0 / +0x4 | B | 0x172AB62 / 0x172AB8A | `F3 0F 11 03` / `F3 0F 11 43 04` |
| encounter: duration / elapsed (reset on each packet) | +0x8 / +0xC | B | 0x172AB94 / 0x172ABAF; 0x172A4E7 adds dt×(1 + int +0x18 / 100) | `F3 0F 11 4B 08` / `89 7B 0C` |
| encounter: a countdown, −dt per frame | +0x10 | C | 0x172A4AD | `F3 0F 11 40 10` |
| encounter: centre, int32 grid x and y | +0x24 | B | 0x172AA63 | `48 89 48 24` |
| encounter: state (2 plays BreachEnrageStinger; 3 caps the size at 100) | +0x38 | B | 0x172AAD5; 0x172ABCA | `89 4B 38`; `83 7B 38 02` |
| Expedition rules (0x278 bytes, vtable 0x33BD938): vector of 0x70-byte records, one per detonator | +0x1E0 | B | - | - |
| record: charges placed, a vector of int32 x,y pairs | +0x40 | A | HUD 0x4F4323 (its length is the placed count) | - |
| record: grid place (the nearest to the player is used) | +0x60 | B | 0x172BD28 | `2B 54 38 60` |
| record: **how many charges**, a byte | +0x68 | A | 0x4F4280, then 0x4F442D formats SideEncounterExpeditionPlacedCount | `0F B6 74 01 68` |
| Ultimatum rules (0x1F8 bytes, vtable 0x33D2D48): round, from 0 | +0x1A8 | A | 0x181F9A2 (server), 0x181FE3E (+1 on UltimatumRoomCompleted); HUD 0x5721A4 adds 1 | `41 88 87 A8 01 00 00` |
| Ultimatum rules: rounds in all (always 10 in this build) | +0x1A9 | A | 0x173EEA1 the only writer; HUD 0x5721E5, then 0x57220B formats UltimatumWindowRound | `66 C7 83 A8 01 00 00 00 0A` |
| Affliction rules (0x140 bytes): the map stat `map_delirium_fog_never_dissipates` | +0x131 | A | 0x1802AD2 | `88 87 31 01 00 00` |
| ClientMonolith (pool 0x108): Essences rows, a vector | +0xC8 | B | network reader 0x16F75E0 | - |
| ClientMonolith: MonsterVarieties row of the monster held | +0xE0 | B | 0x16F76D6 | `49 89 95 E0 00 00 00` |
| Counter (pool 0x20): its value, int32 | +0x18 | A | 0x1DC8CBD ("Counter value: ") | `8B 57 18` |

- **The Breach's ring.** Its size is lerp(from, to, elapsed / duration), 0 to 100, and it is drawn with a radius of
  **300 × min(size / 100, 1) ^ 0.6 grid cells** (`0x172A627: F3 41 0F 59 C3`), passed with the centre to 0x16914D0, which turns grid into
  world with ×10.8696. ControlZone has no network reader (its vtable slot 19 is a stub): the ring was never there.
- **Expedition's blast**, computed on the client by 0x172BFC0 and clamped to 0..255:
  `isqrt((100 + AoE%) × 100) × (100 + Radius%) / 100 / 100 × (B + 8 × (n + byte[ruleset + 0x272]))`, with the area's map stats
  `map_expedition_explosion_area_of_effect_+%` and `…explosion_radius_+%`, B = 30, or 37 in special areas, and n the bonus objects within
  reach of the cursor. In units of ten world units: 30 is 27.6 grid cells and 37 is 34.0, which is what the community measured (28 and 34,
  [league-mechanics.md](league-mechanics.md) §15). 0x172C0D0 adds each object's size and **sets the markers' client states `in_range` and
  `in_placing_range`**: they are the game's own answer, map stats included. The placement indicator is record + 0x50 of a per-player map at
  ruleset + 0x190; `blocked` is the byte at request + 0x18 being zero. The maximum placement distance is not in the client.
- **Not found:** the Trial's encounter type, bar, timer and ring (`UltimatumOrigin` and `UltimatumTimeStopProgress` are shader parameters
  whose writers were not traced); the Delirium fog's edge or timer (the rules read the player's grid place and a float at +0x84; +0x128
  holds 8000 at construction; `AfflictionFogInterpolator`, with a radius in world units, serves only the Act 4 encounter); the crystal's
  interactions left; anything of the Ritual (no client ruleset for row 48; `RitualSacrificePercent` and `RitualFogData` are shader
  parameters).
- **Debug dumps:** ControlZone "In Zone Count: ", Counter "Counter value: ", StateMachine "StateMachine state … = " and "StateMachine
  timer … = "; the rulesets, Monolith, Timer and GroundEffect have none.

**In a running game:** kehgan in the Act 1 town, 2026-11-14 night, three unattended passes: the session is not at +0x340 of the host's game state but at +0x5B8 of the area (dev command "rulesets", which looks one and two pointers deep for an object holding the area near +0x20F0 with rulesets pointing back to it); it holds the area at +0x20F0 exactly as the exe says, and the town's one ruleset, at place 79 of its array, points back to it: row 79 of Rulesets.dat is AncestralOmenRules, so a place in the array is a row of the table. The log then reads "Rulesets: session 0x…, 1 of this area: AncestralOmenRules" and "Rulesets read: no breach; charges none; trial round none"; no error line, inventory read as before. A town has no Breach, Expedition or Trial: the fields inside those three rulesets stand on the exe and the range checks until a session's log shows them.

## The Delirium fog, the Trial's room, the Ritual's sacrifice (2026-11-14, fourth pass)

The loose ends of the Rulesets pass, followed to the end; scripts in the session's scratch folder (`exe4/`, `b1_strgrep` … `b17_bytes`).

**The Delirium fog is not a circle.** Every place of the area has a *depth*: its path distance in grid cells from the mirror, along the
walkable ground. The fog is the band of depths between a *rear* and a *front*, both computed on the client from two clocks the server
sets and the client advances. The whole computation, written out for a port (every width, rounding and clamp, with what is unsure
marked), is in [evidence/delirium-fog-port.md](evidence/delirium-fog-port.md); `FogMath` and `FogReader` are that port.

| Field (R = AfflictionEncounterRules, row 40, 0x140 bytes) | Value | Proof | Site | Bytes |
|---|---|---|---|---|
| row of AfflictionBalancePerLevel for the area's level | R+0x58 | B | chosen once by 0x1F66C40 | - |
| the area's factor on the rear's acceleration; its extra delay | R+0x70; R+0x74 | B | 0x1F66E5E; 0x1F66E97 (three map stats) | - |
| the front's clock; the rear's clock, seconds (−1.8 at construction) | R+0x78; R+0x7C | B | packets 0x1803E05, 0x1803E2B; tick 0x1803A7D | `89 4E 78`; `89 4E 7C` |
| the end, on the rear's clock; +inf while running | R+0x84 | B | 0x1803E51 | `89 8E 84 00 00 00` |
| pause seconds left; pause hold | R+0x88; R+0x130 | B | 0x1803E7A | `89 8E 88 00 00 00` |
| active; finished; never dissipates | R+0x8C; +0x8D; +0x8E | B | - | - |
| the depth map, embedded: area +0x08, header +0x10, regions +0x18, cells +0x30, distance grid +0x48 | R+0xA0 | B | constructor 0x1F65A50, lookup 0x1F66820 | - |
| the deepest depth of the area (8000 until the first packet) | R+0x128 | B | 0x1803F63 | - |
| balance row: front speed after the ramp, rear speed, rear delay, rear acceleration, ramp seconds, first speed | +0x04, +0x08, +0x0C, +0x10, +0x34, +0x38 | B | Front 0x1F66EF0, Rear 0x1F66F60 | `F3 0F 10 58 34` … |

- Front = v0·t + ½(v1 − v0)/T·t² until t = T, then + v1 a second. Rear: starts after the row's delay plus the area's, at the row's
  speed with the row's acceleration times the area's factor; after the end it accelerates two thousand times faster; it never passes the
  front. Row 0 (area level 1): 150 slowing to 30 cells a second over 100 s; the rear 10 s in, at 1 cell a second, gaining 0.38.
- The player's depth: the cell of a map of half tiles gives a region; the region's own depth plus the distance inside it, read between
  four nodes of a grid of half tiles; rounded up, between 1 and the most; a region the fog cannot reach gives 8000.
- The game's progress bar (0x3B74E0, the art `DeliriumProgressBar*`) shows rear / most, front / most, the player's depth / most and
  "paused": exactly these numbers.
- **Being in the fog is the server's word**: the client only reads the player stat key 10259 `is_in_affliction_fog` (0x12FD33:
  `BA 13 28 00 00`), which confirms `catalog/game-ids.txt`. The four seconds a guide gives outside the fog are not in the client either
  (no 4.0 or 4000 compare in the rules' code).
- **Maps:** AfflictionMapRules (row 42) has no clocks, no front and no rear: its bar shows `map_endgame_fog_depth` / 4000 beside the
  player's depth, and the fog is drawn over the whole map. The bar prefers row 40 whenever it exists. Which of the two an endgame map
  creates the client's code does not say: the plugin logs it.

**The Trial of Chaos.** UltimatumRules +0x1B0 is the room being played, a row of UltimatumRooms (packet type 5, 0x181FBF8, which loads the
table by name); its +0x10 is the row of UltimatumEncounterTypes, whose +8 is the text the HUD's label shows (0x572860: `48 8B 92 B0 01 00
00`, then +0x10, then +8). The modifiers taken are 16-byte {row, table} pairs at +0x1C0 / +0x1C8, those offered at +0x1D8. The bar
"Challenge Progress" reads a float of the *session*, +0x23E0 (0x5787D2: `F3 0F 10 B8 E0 23 00 00`), 0 to 1. "Return to the Ring" is a
UI event (9) carrying a countdown in seconds; nothing on the client holds the ring's centre or radius, and the shader parameters
`UltimatumOrigin` and `UltimatumTimeStopProgress` are never filled. The timer's source was not found.

**The Ritual.** The altar's Render component +0x46C is the float the shader parameter `RitualSacrificePercent` is fed from (0x170D90F:
`F3 0F 10 88 6C 04 00 00`), eased toward the entity's variable `ritual_sacrifice_percent` (the Render tick fetches it by the game's own
hash 0x460E6134, reproduced). Its scale is not known. `RitualFogData` has no provider: the circle's centre and radius are not on the
client.

**Expedition's placement.** The limit is a path length of 90 grid cells (108 in a logbook: area +0x1B4 being 0x1C or 0x27) times
(100 + `map_expedition_maximum_placement_distance_+%`) / 100, checked on the client (0x1F6A363: `6B C8 5A`; 0x1F6A35D: `6B D1 6C`) from
record +0x60; another charge within 20 cells also blocks a place (0x172C783: `81 F9 90 01 00 00`), as does ground tagged
`expedition_no_placement`. The same 90 and 108 the community measured.

## The Targetable component's flags (2026-11-16, build 0.5.5f)

Read for issue #7 (Sekhema Asala, `Metadata/Monsters/NPC/MarakethMatriarchDreadnought`, not listed after the Jamanra fight).
The parser (0x17E86B0) stores the file's keys in the shared type data: `attackable` +0x17, `targetable` +0x18,
`force_targetable` +0x19, `queens_sacrifice` +0x26, `always_targetable_in_couch_coop` +0x30. The constructor (0x17E8380, size
0x90) keeps the type at +0x48 and copies +0x17 to **+0x68 (attackable)** and +0x18 to **+0x69 (in play)**; +0x6A is "not
`no_highlight`"; +0x6E (quest state), +0x6F (item requirements) and +0x72 start at 1. The debug dump (0x17EB540) names +0x69
"Targetable:", +0x73 "Hidden from Player:", +0x6F "Meets Item Requirements:". `EnableTargetable`/`DisableTargetable` write +0x69.

The game's test, inline at about eight places (0x1A7720 also needs +0x68: attacking; 0x1A7AE0: interacting):
`type+0x26 || (type+0x30 && couch co-op) || (!(entity+0x8D & 1) && +0x69 && !+0x73 && +0x72 && +0x6E && +0x6F && !+0x74)`.
No buff is read. `force_targetable` only lets an entity be interacted with while its InteractionAction is unavailable; it does
not stand in for +0x69. The plugin reads +0x69, +0x6E and, since this pass, +0x68 (`TargetableComponent.AttackableOffset`):
1 on all 313 living targetable monsters and players of 47 entity dumps, 0 on all 186 NPCs and chests.

What changed for #7: a creature with the NPC component that cannot be attacked is a character whatever side its reaction byte
reads (town NPCs read "hostile" in every dump), and the `hidden_monster` buff no longer takes a character out of play. Not seen
in play: the log line `Character not listed: <path> (<reason>)`, once per file, says why any character is left out.
Later the same day: the Dreadnought's Asala has no NPC component at all (`MarakethMatriarchDreadnought_.ot`: InteractionAction,
Targetable, MinimapIcon, Actor, Stats), so "character" now also means a monster with the InteractionAction component.
