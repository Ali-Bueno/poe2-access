# Host and plugin cost (2026-11-08, 0.5.5f, headless host)

Goal: run on a Windows handheld (Ryzen Z1/Z2 class) with nothing audible changed. Measure: the host's `Frames:` line
(`Poe2AccessHost.log`, once a minute: ms per frame split into coroutines, reader, plugin, and the process' CPU share of one
core), the plugin's `costs` dev command (ms of CPU per second of play per part of the frame), and a sampling profile
(`dotnet-trace collect -p <pid> --duration 00:00:00:25 --format speedscope`, aggregated by thread; it also counts blocked
threads, so only relative figures mean anything).

## Town idle (Act 2 town, game in front, 60 Hz)

| Step | ms per frame (coroutines, reader, plugin) | Reads per frame | CPU of one core |
|---|---|---|---|
| GameHelper2 as host (before 0962190) | — | — | 70% |
| Headless host, per-frame UI/preload reads dropped (51369f3) | 3.2 (0.3, 1.1, 1.8) | ~1340 | 32–36% |
| Plugin: stat keys, one entity listing, layers trusted 100 ms, in-play last | 1.8 (0.2, 1.0, 0.6) | ~1340 | 30% |
| Host: belts at 10 Hz, no pool for the entity loop, 20 Hz in background | 1.7 (0.05, 1.0, 0.6) | 1341 | 25% |
| Host: resting components, small maps on one thread | 1.6 (0.05, 0.9, 0.6) | 1335 | 22–24% |
| Plugin + host: element text from the same read, rectangles cached per snapshot | 1.7 (0.05, 0.9, 0.7) | 1216 | 20% |
| Host: nil children skipped, Actor without per-skill reads | 1.5 (0.05, 0.7, 0.7) | 853 | 18.7% |

The plugin's parts before (ms per second): player news 57, sounds 34 (of which nearby 10, object sounds 6.4, enemy news
3.2, beacon 2.9, discoveries 2.2, enemy sounds 2.0, flasks 1.7, radar 1.1, map hints 1.0), screen 11. After: sounds 19
(nearby 5.6, beacon 2.7, object sounds 1.9), screen 17 (the screen watcher's own thread is not in these), player news 1.8.
The dev install runs with `ResearchMode` on, which adds a "research" part of 13–19 ms/s that users do not pay.

What the frame's reads are (final state, town): std::map nodes of the awake entities 430 (some 45 of them pass the
decoration filter and become entities), the screen watcher's element blocks and child lists 275, entity structs 57,
buffs 13. In a fight the monsters' components come on top (every frame: life, actor, render, buffs…); a fight was not
measured in this session (the character was 10 levels under the area).

## What cost, and what changed

- **`ChargeWatch.WatchRage`** resolved three stat keys by name every frame: `GameStats.ToString()` on a 20k-member enum was
  a third of the main thread's work (`Enum.GetEnumInfo` in the profile). The keys are now resolved once per catalog.
- **`EntityCatalog.Entries()`** rebuilt the listing from every awake entity on each call, and seven services called it on
  their own 250–1000 ms polls (about 15 rebuilds a second). One listing now serves all callers for 100 ms
  (`EntriesFreshMs`): a caller polling every 250 ms sees data at most 100 ms older than before.
- **The in-game layers** (chat, HUD, tutorial, map overlay, skill bar) were each found from the state object every frame:
  the container was read seven times, and each field's slot read and element checked (the chat's check walks its
  children). The container is read once a frame and a field found is trusted for 100 ms (`InGameFields.HoldsFreshMs`).
- **`EnemyCues.Collect`** read the in-play flag of every entity with a Render before asking whether it was an enemy or in
  view; it asks last now (same set, fewer reads).
- **The host runs at 20 Hz while the game is behind another window** (`FrameLoop.BackgroundFramesPerSecond`): the plugin
  plays and says nothing about the world then, and chat, area and screen changes have their own polls of 150 ms and more.
- **The awake-entity std::map** was read node by node with both children of every node, so each leaf cost two reads of
  the nil sentinel: a child that points at the map's head node is known to be nil without a read (`SafeMemoryHandle.
  ReadStdMap`). Maps under 128 nodes are walked on the calling thread: waking the pool for a town's entities cost more
  than reading them.
- **Components of entities that are neither monsters nor players** are re-read every 100 ms (`Entity.RestingComponentsMs`);
  monsters and players keep every frame (life, animations and positions feed the timing cues). Entities the host cannot
  type rest the same way (`EntityRefresh`): only what they are is asked once a second, not their components.
- **`Actor`** read every active skill's details, the cooldowns and the deployed minions each frame (61 reads a frame for
  the player in town, more per monster in a fight) for data the mod reads from the component's memory itself; it reads
  the component struct only now.
- **Inventories** (belts, the bag, the cursor) were re-read at 50 Hz on a `Parallel.ForEach`; the mod polls them every
  100–500 ms, so they refresh at 10 Hz on one thread (`Inventory.UpdateSeconds`).
- **The screen watcher** read every element twice (base block, then the text block from the same address) and placed
  texts and controls by re-reading their whole parent chain: one read serves both now (`UiElementReader.TryReadElement`
  keeps the block) and a `PluginUiElementReflection.RectCache` places each ancestor once per snapshot.
- **The host's `Frames:` line** now counts the reads per frame by type: the measure to watch after a change.
- **ImGui and GameHelper's overlay-only code** were removed (no per-frame cost, but less to load and keep compiled).

## Reader pass (2026-11-10)

Starting point (read-only analysis, 0.5.5f): a frame cost a median 1.74 ms (reader 0.69, plugin 0.94), 678 reads a frame,
up to 905 in a boss fight, and in fights about 30 frames a minute over 16.7 ms (slowest 60–93 ms) with no known cause.

- **Awake-entity map** (`StdMapWalk`): ids from `EntityFilter.FirstVisualId` (0x40000000) up are client-side visuals the
  reader drops, and the map is ordered by id, so the walk leaves unread the right subtree of a node at or above it. It
  checks every node it reads against its ancestors' ids; on a node out of place it reads what it left (that walk misses
  nothing), stops pruning for the area and logs `[AreaInstance] The awake entities' map is out of id order` once. The
  sleeping-entity scans (dev, map hints) keep the full walk.
- **A walk cut short** (a child unreadable, nil or of no colour, or more nodes than the size) used to leave every entity it
  missed invalid, so a monster near the player was removed and came back the next frame as a new `Entity`. Now the walk
  says whether it was complete; the entities an incomplete walk missed keep their state, for at most
  `IncompleteWalks.MaxKept` (3) incomplete walks in a row, then are dropped as before.
- **`StateMachine`** names belong to the machine's type: read again only when the type pointer, the values vector, the count
  or the list layout changes, once a second, or after a name that failed to read. Values stay per frame.
- **`Base`** names are read for a new component address or new rows (or while one is still empty), as `ObjectMagicProperties`
  does its mods.
- **Mouse-over entity**: the narrator only compares its address, so only the address is read each frame
  (`InGameState.MouseOverEntityAddress`); the offset helper reads the entity when it asks (`ReadMouseOverEntity`).
- **Overlay leftovers**: the Delirium/Breach caches read the area's environments every frame (one read) only to remove
  entities the stale-entity cleanup (120 invalid frames) removes anyway, and recreated still-valid ones as new objects;
  the render events had no subscriber. The camera matrix stays: it comes with the same `WorldDataOffset` read as the
  area-details pointer, so reading it on demand would add reads (the offset healer's camera judge projects per candidate).
- **CPU per entity**: the monster-folder test (`StartsWith` without a comparison: culture-aware, Spanish culture) and the
  entity-backed buff search (case-folded `Contains`) ran per entity per frame; they run once per path read now.
- **Identity**: a new id at an entity's address, like a new address, resets path, type and state with the component map.
  Before, a new address kept the old type, and a Useless non-monster returned before its component map was re-read.
- **Buff names** are kept per definition only when they read as a buff id (`StructShapes.IsBuffId`); a garbage or failed
  read serves its frame only instead of the rest of the area. Nothing reads `StatusEffects` off the frame thread (the
  screen watcher, beacon, map hints and place builds touch no awake entity), so the set is still refilled in place.

**Slow frames, named.** The host's `Frames:` line adds `GC a/b/c (gen 0/1/2), N ms paused` for the minute. The plugin
logs `Slow tick: <part> took N ms` for a part's run over 25 ms (once in 10 s per part; parts nest: "sounds" holds
"radar", "nearby"…), and `costs` gives each part's `slowest` run and its runs `over 8 ms`, plus
`Screen snapshots: n, avg ms average, slowest ms` (the watcher's own thread). A minute's over-budget frames that match
gen 1/2 collections and the pause point at the GC; `Slow tick` lines name a plugin part; neither leaves the reader
(parallel walk, pool wake-ups) or the system.

### Measured live (yarkey standing in G2_town, 2026-11-10, a full minute each)

| | Before | After the pass | After the grid change below |
|---|---|---|---|
| Reads a frame | about 645 | 370 | 338 |
| ``StdMapNode`2`` reads a frame | 170 to 273 | 59 | 56 |
| Frame cost | 1.08 ms | 0.90 ms | 0.82 ms |
| CPU of one core | 18% | 11.6% | 9.3% |
| Frames over 16.7 ms | 0 to 4 | 1 | 1 |
| Collections (gen 0/1/2) | not counted | 105/12/5, 74.5 ms paused | 101/11/4, 72.7 ms paused |
| Runs over 8 ms in about 155 s: sounds, nearby, beacon | not counted | 73, 32, 25 | 27, 12, 6 |

- **The hitches are the collector.** The runs over 8 ms came once every 5 s in "nearby" (the entity listing, the part that
  allocates most), which is the rate of the generation 1 collections. `EntityCatalog.WalkableGrid` read the whole live
  grid into a new array on every call and returned a copy more, hundreds of kilobytes each, so on the large object heap,
  and the guide asks on every ping. It now reads the grid twice a second (`SampleMs`), keeps one array for samples that did
  not change, combines them again only when a sample or the barrier memory changed (`BarrierMemory.Changes`), and hands
  every caller the same array, which none writes to. What is left is the reader's own arrays (109 `Byte[]` reads a frame):
  a read into a caller's buffer would be the next step.
- **One walk out of id order in town**, 80 s into the area and never again: the game moving nodes as they were read. The
  order is given up for an area only after three such walks in a row (`OrderFaults`); none was logged in the 10 minutes after.
- **Not measured:** a fight (the script does not fight). The `Frames:` line and `costs` now say what a fight costs, with the
  collections, the slowest run of each part and `Slow tick` lines: read them after the user's next boss.
- **Screen watcher:** 2.4 ms a snapshot on average (slowest 31 ms), so it is not where the rest of the CPU goes.

**To confirm live** (`Frames:` reads by type, town then a fight): ``StdMapNode`2`` close to the awake entities under the
visual ids plus ~10 (Act 2 town: some 55 instead of ~430, and the total about 375 lower); no `EnvironmentStruct[]`;
`StdString` near zero outside a state machine's first second; no `out of id order` line; and while hovering an entity
with the mouse, no `EntityOffsets` and component reads for it.

## Allocation pass (2026-11-10, second pass; measured live in town, not in a fight)

The collector, not the reads, made the hitches, so this pass removes what was allocated per read and per frame. No timing a
player hears changed, and no read was dropped except the two named under "Reads".

**What allocated, and what it does now**

- **The screen watcher's element blocks**: every element of every snapshot was read into a new array of about 1.4 KB, some
  6 400 a second in town (the 109 `Byte[]` reads a frame): by far the most garbage. `UiElementReader.TryReadElement(address,
  BlockPool, out info)` reads into a block of the watcher's `BlockPool`, and the walk gives it back when it is done with the
  element (`ScreenWatcher.CaptureLayer`: at the next pop). Invariants: a block is exact-size (`info.Block.Length` is still
  "how much was read", and the decoder's bounds check holds); it goes back only after its text was decoded into strings;
  nothing a snapshot keeps (addresses, names, texts) points into one; a block never given back (the budget ran out) is lost
  to the collector, never handed out twice. `TryReadElement(address, out info)` is unchanged and still allocates: some 200
  callers keep its `ElementInfo` in queues and lists. The child list of each walked element is read into one array
  (`walkChildren`), and the walk's six working lists serve every layer.
- **Strings** (`SafeMemoryHandle.ReadStdString`, `ReadStdWString`, `ReadString`, `ReadUnicodeString`) are read on the stack and
  decoded by `NativeText`, pinned in `ReaderBufferTests` against the old decoding (inline text half by half, the three-zero
  terminator). `UiElementReader.ReadWStringStructAt` (an entity's name, per listing) reads into one array per thread.
- **A struct a patch moved** (`TryReadMoved`) is read on the stack or into the shared pool; the 8 KB `AreaInstance` block
  into the shared pool (the layout search, once per area, gets a copy of its own).
- **Vectors a component reads and is done with** (`RentStdVector`: stat lists, the buff list, a state machine's values) come
  from the shared pool as a `PooledArray<T>`, a ref struct: it cannot be stored, so nothing reads it after it is returned.
- **The live walkable grid** is read into one buffer per grid size (`EntityCatalog.ReadLiveGrid`), under the samples' lock; a
  sample is copied out of it only when it differs from the one before. Standing still: no array at all, where each sample
  was hundreds of kilobytes on the large object heap. The samples, the barrier memory and the keep-or-combine decision are
  `GridSamples`, pure and tested (`GridSamplesTests`). Its rules: a sample kept owns its array; the combined grid is kept
  only while every sample is the one array it was made from and the barrier memory did not change. The morning's version
  kept a combination of differing samples under the newest one, so a door that opened stayed closed for routes (f470410).
- **`Buffs.StatusEffects`** is a plain `Dictionary`: the concurrent one allocated a table per `Clear`, a node per buff per
  frame and a list per `Keys`, and `EntityCatalog.IsHidden` asks for the keys of every monster for every sound. Invariant:
  the dictionary is refilled on the thread that updates its entity and read on the frame thread only (the plugin's tasks
  are the screen watcher, the beacon's route search, the map hints' sleeping-entity scan and the place build; none touches
  an awake entity's buffs). Its order is now the game's list order; before it was a hash order that changed every run.
- **A monster's `is_dead`**: `Entity.TryGetStatValue` built a whole `Stats` component (two dictionaries of every stat, five
  reads) per monster per frame whenever nothing kept one, which is the usual case. It reads the same lists and looks for the
  one stat (`StatPairs.TryFind`: the last pair of a key, as the dictionary held it). In a fight this was probably the
  largest source of garbage after the screen blocks.
- **`StateMachine.States`** keeps its list while names and values are the same (the list is replaced, never changed);
  **`Render`** packs its grid position into one word instead of an object per update; **`WorldData`** writes a camera
  snapshot only when the matrix's bits changed.
- **The entity listing** (`EntityCatalog.Entries`): `QuestMatcher.Targets` folded every name against every quest text ten
  times a second; a `QuestMatcher.Memo` keeps each answer until the texts or the area change (an answer depends on those and
  the name alone). `TileLandmarks` walked every tile of the area per landmark, building a string per tile; the places of a
  named tile are kept per tile table (`TilePlaces`, `EntityCatalog.TileCentres`). The listing's working collections are
  reused, the awake entities are walked by their pairs (`Values` copied them all under every lock), and the list returned is
  still built new. `ListingTests` pins the matcher word for word against its old code, with and without the memo.
- **The frame itself**: the fault delegate, the timer's wait array, the closure over the frame's time and the walk's closure
  are made once.

**Reads** (the only two dropped): a minimap icon's name is read once per MinimapIcons row and area (`Core.GgpkStringCache`,
as a mod's name), the row pointer still on every update: two reads and 512 bytes less per update of each entity with an
icon. A monster's dead stat never costs more reads than before, and fewer when the first list has it.

**To confirm live** (same place and minute as the table above, then a fight):

| `Frames:` line | Before (measured) | Expected (estimate) |
|---|---|---|
| `GC a/b/c (gen 0/1/2)` | 101/11/4 | gen 0 under a third (15 to 30), gen 1 at 1 to 3, gen 2 at 0 or 1 |
| `ms paused` | 72.7 | under 20 |
| `reads per frame` | 338 | about 330 (this pass is not about reads) |
| read types | `Byte[]` 109 | `Byte[]` a little lower, with `Char[]` new beside it (texts over seven characters, read as characters now) |
| `over 16.7 ms` | 1 | 0 or 1 |

`costs`: the runs `over 8 ms` of sounds, nearby and beacon (27, 12, 6 in about 155 s) should fall with the generation 1
collections; `Screen snapshots:` average should not rise (2.4 ms). In a fight, compare the same line with the old 905 reads
and 30 frames a minute over 16.7 ms: the reads stay, the collections should not grow with the monsters as they did.
What a wrong pooled block would look like: a text of one element said for another; none is expected, and
`Screen snapshot failed` in the log would name an exception.

**Measured live** (yarkey standing in G2_town the same afternoon, a full minute, after five more fixes the live run
found): `GC 26/2/0`, 17 ms paused (from 101/11/4 and 73 ms), 363 to 381 reads a frame, no text of one element said for
another. The reads rose a little with the gem and Build Planner readings added the same day. A first start that
regenerates every catalog took 33.8 s with every step succeeding. **No fight was measured**: the comparison with the old
905 reads and 30 slow frames a minute is still to make at the next boss (`Frames:` in `Poe2Access.log`, then `costs`).

**Measured in fights** (the owner on malgdhler, Druid 8, with another player, 2026-11-10 14:22 to 14:30: the Devourer and
three ritual bosses; research dumps off): 396 to 530 reads a frame, 1,1 to 1,5 ms a frame (reader 0,5 to 0,75, plugin 0,5 to
0,65), 0 to 2 frames a minute over 16,7 ms with the slowest at 17,8 ms, `GC` 33 to 47 / 3 to 6 / 0 or 1 with 30 to 46 ms
paused a minute, 11 to 14 % of one core. The plugin log had no warning or error and one `Slow tick` (the pad). Against the
old fight figures (905 reads, 30 slow frames a minute) the reads are about half and the slow frames all but gone. It is a
low-level fight with few monsters: a late-game pack is still to measure.

**Looked at and left** (each would need a live proof first):

- *The local player as two entities* (`AreaInstance.Player` and its entry in `AwakeEntities`): nothing compares entities by
  reference (`Seats.IsFollowed` compares addresses), but one object in both places would be invalidated by a walk that
  missed it, updated twice a frame unless the walk skipped it, and left under a stale key if the player's pointer changed.
- *Slower components for monsters*: `Targetable` (the plugin reads its own flags at the component's address and never the
  host's `IsTargetable`), `NPC`, `DiesAfterTime` and `ObjectMagicProperties` (rarity) cost a read each per monster per
  frame for values no timing cue needs. Their re-read is also what notices a component that moved
  (`IsParentValid`), and the plugin reads flags at those addresses, so a slower one could serve a stale address for 100 ms.
- *The entity struct of resting entities* (57 `EntityOffsets` reads a frame in town, of which some 45 for entities whose
  components rest): skipping it between component reads would delay only the struct's own valid flag by up to 100 ms; the
  walk still sees an entity leave at once. Not proven harmless, so not done; it is the largest read left outside the walk.
- *`ActorOffset` and `LifeOffset` trimmed*: a read's cost is the system call; the struct lands on the stack (no garbage),
  `AnimationId` sits at 0x8B0 of 0xC40, so a trimmed read still copies 2.2 KB, and a healed struct is rebuilt from
  `ReadSize` bytes by segments that assume the whole struct. Nothing to gain for the risk.
- *Not this pass's files*: `Poe2AccessibilityCore.Isolated(service, () => ...)` makes a closure per part per frame;
  `EnemyStateWatch.Primed` reads two 4-byte arrays per monster (`TryRead<int>` would not allocate); `StdMapWalk`'s own lists.

## Not done, and why

- The screen watcher's 150 ms snapshot: its interval is the speech latency after a screen change (two snapshots confirm
  one), so it stays; the walk reads one block and one child list per visible element (~275 reads a frame in town). Caching
  child lists by the vector's bounds was rejected: a list rebuilt with the same count keeps its bounds and would serve
  stale children.
- Refreshing far entities less often: the awake map only holds what is within the network bubble (150 cells, about a
  screen's half width), so almost nothing qualifies as far.
- Cutting the pool's spin (`System.Threading.ThreadPool.UnfairSemaphoreSpinLimit`): not measured; the remaining pool
  work is the screen watcher's task and the entity walk of large maps.
- Enemy cue scheduling, the radar's 50 ms poll, dodge/parry warnings and pad gestures stay every frame: their gaps are
  50–150 ms and a 10–20 Hz tick would jitter audibly ([enemy cues](../radar.md), [dodge timing](dodge-timing.md)).
