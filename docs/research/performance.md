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
  monsters and players keep every frame (life, animations and positions feed the timing cues).
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
