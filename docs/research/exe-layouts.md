# What the game's exe says about its own layouts

Static study of `PathOfExileSteam.exe`, build of 2026-11-05 (0.5.5f), read as a data file: no process attached, nothing run or
changed. The question: can the exe tell the mod its struct offsets, so a patch needs no measuring?

**Answer:** yes, and further than byte signatures, but a single small patch is all the evidence that it lasts.
`tools/exe_reader/reader.py` recovers 21 of 21 checked offsets in about 7 seconds with no fixed address and no signature. Its rules were
written looking at the answers. The mod does not use it at run time: the shape searches ([compatibility](../compatibility.md)) already
cover these fields and are proven live. It stays a development tool until more patches are measured.

**First real measure.** Steam installed a new build at 02:42 on 2026-11-06, minutes after the study ended (buildid 26476662, exe linked
2026-11-06 03:24 UTC, 73 MB downloaded, `.text` 256 bytes larger). Nothing was changed for it. On the new exe the reader still gives 21 of 21,
and all 45 signatures still hit once with the same value, at addresses shifted by 0, 224, 1136 or 1376 bytes. No table key or animation
row changed. In play: "El juego se ha actualizado: el mod lo ha encontrado todo", 17 structs intact, 0 lost, the animation
found at +0x8B0, 100 enemy cues in a short fight. This is a hotfix that moved no field, so it says signatures survive small recompiles and
says nothing about a patch that reorders a struct.

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

They are the weakest route. Within this one build, Stats+0x160 has 1,918 sites with 44 encodings of the anchor and 283 three-instruction
skeletons; even 135 wrappers from one template come out in 5 shapes. Upstream's patterns changed registers twice (`4C 8B 06` to
`4D 8B 06`, `45 0F 57 C9` to `0F 57 FF`), and one jump between March and May rewrote all six. After that only one changed (0x140 to
0x148 in 0.5.5) while offsets moved in five patches, so code shape often survives; but those six are short and hand-picked, which bounds
nothing for field-reading code. `GameProcess.cs:344` treats every hit as RIP-relative, so reading a field would need a second list.

## How far it goes

| Approach | Changed registers | Changed inlining | Cost |
|---|---|---|---|
| Byte signature plus read | breaks (twice upstream) | breaks | least code, most upkeep |
| Virtual getter | immune | immune | few fields have one |
| Pool size | immune | immune | says a struct grew, not where |
| Name anchor plus operand rule | survives if rules name no register | partly | one rule per field, needs a decoder |
| Function matching across builds | mostly | mostly | offline tooling, better at development time |

A run-time reader would need a real decoder (Iced: a new dependency, so not without asking), `.pdata` plus decoding leaves up to `ret`, and
one pass collecting `E8` and RIP-relative `lea` targets. Its worst failure is a restructured function making a rule return a wrong value
in silence, so any result must be a candidate for `OffsetHealer`, never the truth. A renamed GEAL name just stops the rule, which is safe.
Assumptions in `reader.py`: vital Current at +0x30, Cooldowns = ActiveSkills + 0x18, LocalPlayer = head + 0x18, vitals by order; some rules
name `rcx` or `rax`.

## What would measure it

The hotfix above is one point. Steam app 2694490, depot 2694491. DepotDownloader with the owner's login can fetch only the exe of an old manifest
(`-app 2694490 -depot 2694491 -manifest <id> -filelist files.txt`; not tried, and old manifests may not be served); manifest ids come
from SteamDB, which refuses scripts. Ground truth for old builds is upstream's commits: `98ef3ad` (0.5.1), `4183681` (0.5.4), `3293e56`
(0.5.4c), `a99b8ba` (0.5.5). With none of that, each future patch adds a point: run the reader on the new exe and keep the output beside
[evidence/exe-reader-0.5.5f.txt](evidence/exe-reader-0.5.5f.txt), which is the 2026-11-06 build. That also answers, before anyone starts
the game, what moved.

## Mistakes worth not repeating

- `0xFD24B0` touched 12 "AreaInstance offsets" on one register and looked like its constructor. It is InGameState's. Large constructors
  touch nearly every slot, so offsets appearing together prove nothing.
- First-pass signatures had 3 fixed bytes and were unique by accident; hence the floor of 12.
- `0x5D8` is not a plain pointer but inline storage of a small vector (begin 0x5C0, end 0x5C8, capacity 0x5D0, room for two): reading it
  is right only while the vector stays inline.
