# Enemy sounds

After the D2R mod's design (`d2r-access/docs/entity-cues.md`). Code: `EnemyCues` (selection), `EnemyCueScheduler`
(timing, pure), `CueSynth` (sounds), `AudioCueService` (winmm mixer, part of Windows: no new DLL).

- Every living hostile monster on screen, in play (not still buried or dormant: Targetable `+0x69`) and in line of
  sight on the terrain's projectile layer (`TerrainMetadata.GridLandscapeData`, read once per area; more than two
  blocking cells in a row hide it); uniques are heard through walls. The walkable grid was tried first and hid
  almost everything: it marks every log and rock. It stays the fallback (runs over 8 cells) if the layer is unreadable.
- One shared sound: a 90 ms low growl (175 → 125 Hz, fluttering at 36 Hz). Magic and rare monsters add a bright 750 Hz
  buzz chattering at 62 Hz; uniques add a struck-metal ring. So pitch only ever means up or down.
- Stereo = side of the screen (±0.9); below the player the pitch drops to 0.8 at the bottom edge.
- Tempo = nearness: 600 ms between pulses at the screen's corner down to 150 ms next to the player.
- NPCs and other players (in play, on screen, in sight): a light wooden tap (84 ms, 700 Hz knock) for the nearest
  one, on its own stream beside the enemy streams, same pan and tempo rules. Setting `NpcSounds`.
- At most four enemy streams: the nearest unique, the nearest elite, then the nearest others. Starts keep 100 ms apart; due
  streams take turns, most overdue first; a stream keeps its cadence when its target changes (no burst).
- Silent whenever a panel, menu or dialogue has the focus, the game window is in the back, or the player is dead.
  Settings: `EnemySounds`, `EnemyVolume`, `SoundVolume`. The log gets a summary every 5 s, never a line per pulse.

Live 2026-10-23: "44 pulses, up to 6 enemies in the last 5 s" in a wolf pack (~9 a second; tune by ear). A first
build never refreshed its targets (`now - long.MinValue` overflowed); timestamps start at `long.MinValue / 2`. Open: reach ping, boss life narration (D2R/LeAccess have them).
