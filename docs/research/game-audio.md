# PoE2 game audio: where it lives and how to extract it

Status: working (12 cues + 113 item drop samples extracted, verified non-silent). Tool: `tools/Poe2UiExtract` command `sounds`.

## Format
- Audio is NOT in `Bundles2/`. It is 2935 loose FMOD Studio banks in `<game>\FMOD\Desktop\*.bank` (15 GB), plus
  `Master Bank.bank` / `Master Bank.strings.bank`. The UI `Audio/Sound Effects/...` paths are FMOD event names, not files.
- Each `.bank` is RIFF `FEV `/`FMT ` with a `LIST` (event metadata) and a `SND ` chunk holding one **FSB5** archive
  (magic `FSB5` found by scanning; a few bytes of padding precede it).
- All 190,749 samples in all banks use FSB5 codec 15 = **Vorbis**, mostly 44.1 kHz mono/stereo. No PCM/FADPCM, no `.ogg`/`.wav`.
- Sample names are present in the FSB5 name table (e.g. `GoldDrop_01`, `Item_Drop_Rare_01`), so no event->sample
  resolution through the Studio metadata was needed.
- FMOD Vorbis stores only a CRC of the setup header; the codebooks live in the FMOD library. A standalone
  FSB5+NVorbis decoder is therefore not realistic (python-fsb5 was not available via pip either).

## Method that works
Load the game's own `fmod.dll` (2.03.11; header version `0x00020311`) via P/Invoke in NOSOUND output mode, write the FSB5
bytes to a temp file, `System_CreateSound(CREATESAMPLE)`, pick the sub-sound by name, `Sound_Lock` the decoded float PCM,
trim leading silence (< -50 dBFS), cap at 1.5 s with an 80 ms fade, write 16-bit WAV. Read-only on the install.
The header version is derived from `fmod.dll`'s product version (2.3.11 -> `0x00020311`), so an FMOD update needs no code change.

## Cue map (bank in `FMOD/Desktop`, sample name)
| cue | bank | sample |
|---|---|---|
| item | ItemDrops | Item_Drop_Magic_01 |
| item_rare | ItemDrops | Item_Drop_Rare_01 |
| item_unique | ItemDrops | Items_UniqueDrop_01 (4 s swell, peak at 0.5 s; a unique used `item_rare` until 2026-11-04, the user could not tell them apart) |
| currency | ItemDrops | OrbDrop_Chaos_01 |
| gold | ItemDrops | GoldDrop_01 (0.34 s) |
| quest_item | ItemDrops | Item_Drop_Quest_01 |
| chest | Chest_Rarity | Chests_Rarity_Magic_Open_01 |
| shrine | ChallengeShrines | Gameplay_Shrines_ChallengeShrine_Activate_Stinger_01 |
| exit | UI_General | WorldAreaTransition_02 |
| portal | PortalScrollPortal | PortalScroll_Effects_Po_Activate_01 (the portal scroll's portal; idle loop `_Po_IdleActive_01`) |
| waypoint | Waypoint4.0 | WaypointActivate_01 |
| checkpoint | BossCheckpoints | BossCheckpoints_Activate_01 |
| npc | General_Gameplay | Quest_UI_Offer_01 (placeholder; no generic NPC greeting found) |
| switch | Foley | PlayerCharacter_Foley_HandGrabLever_01 (0.4 s, about 18 dB under `breakable`) |

## Idle loops (`loops/`, 2026-10-27)
Kept whole (up to 20 s), the seam crossfaded over 50 ms; the plugin brings each to one RMS (peaks capped at half
scale) and plays it as a bed.
No FSB5 sample carries a loop chunk: the loop regions live in the Studio events, so loops are chosen by name and
checked for a steady level (RMS of 100 ms windows) and distinct spectra.

| loop | bank | sample | character |
|---|---|---|---|
| portal | PortalScrollPortal | PortalScroll_Effects_Po_IdleActive_01 | 4 s, mid (centroid ~2.2 kHz) |
| waypoint | Waypoint4.0 | WaypointActive_01 | 19 s, low hum, very steady |
| checkpoint | BossCheckpoints | BossCheckpoints_Loop_01 | 19 s, bright (~3.9 kHz), steady |
| ~~exit~~ | UI_General | WorldAreaTransition_02, frozen | 6 s of 120 ms grains from its 0.05–0.5 s (granular freeze); dropped 2026-11-02 |

Area transitions have no idle sound of their own, and the crossing sounds (`WorldAreaTransition_02`, `Travel_1..3`)
are one-shots that die out within 2 s, so the exit loop holds the crossing's loud start still (user's choice
2026-10-27). Spectra did not predict what sounds alike: the Delirium encounter-object idle loop (a 400–3200 Hz
shimmer, far from the ritual stinger's spectrum) was heard as the ritual sound, and a torch
(`Act3_Braziers/FireTorch_loop_01`) did not suit an exit (user 2026-10-27). Rejected for exits: wind loops (the radar's open floor is a
noise breath), `Doodads/SubTransition_01` (a sub-bass throb once a second that dies out, like the weak-point
heartbeat), `PortalScroll_Effects_Id_IdleInactive_*` (about -39 dB and uneven), the tormented-spirit wisp loop
(same spectrum as the waypoint).

A loop on disk wins over the cue of that name (`ObjectSounds`). The launcher swaps the whole folder, so packaged installs were never
affected, but `Poe2Accessibility/sounds/` (gitignored, copied to the dev host by every build) was extracted over the last run: its
`loops/exit.wav` of 2026-10-30 kept the old crossing playing in dev sessions until 2026-11-07 (user: "está con el sonido antiguo"). The
extractor now drops loops it no longer makes there; the build only adds, so a loop dropped later must also go from the host's folder.

Since 2026-11-02 an exit has no bed: testers could hardly hear the frozen crossing beside a checkpoint's
bright bed. It is a one-shot, `CaptainSandstorm/Whoosh_Foley_GoblinGiantBoss_02` (a low rush of air, 0.9 s), repeated
faster and louder as the player nears it, the user's pick among seven game wind and whirl sounds after six dark
candidates (stone doors, knocks, synth notes) were all rejected; extracted at RMS 0.10 (`Cue.Rms`: raw it is 0.31 with
full-scale peaks, the checkpoint 0.10). The sounds step is version 4, so every install extracts again once.

The `warning` and `heartbeat` placeholders were dropped (2026-10-24): the game plays its own low-life heartbeat.

## Deployed-object locators (2026-10-29)
Skill audio lives in per-skill banks named `Four_<Skill>` (bank sample names are readable from the FSB5 name table; the
banks hold no `.ot`/`.ao` strings). Found by listing `FMOD/Desktop` for `TempestBell`, `FrozenLocus`, `FrostWall`.

| cue | bank | sample | length / peak | why |
|---|---|---|---|---|
| tempest_bell | Four_TempestBell | TempestBell_FX_OnHit_01 | 1.78 s, stereo, peak 0.46 | a single bell toll that rings out (envelope 0.46 -> 0.01 over ~1.2 s) |
| ice_crystal | Four_FrozenLocus | FrozenLocus_FX_Appear_01 | 1.66 s, stereo, peak 0.70 | the crystal forming; the bank has no idle sound for it |

- `Four_TempestBell` (81 samples): `OnHit_01..06` (1.8-2 s), `Impact_01..06` (2-2.5 s), `Pulse_01..06`, `SmallBellPulse/Break_*`
  (2 s), `BellLong_01..06` (5-6 s), `SmallBellLoop_01..09` (7.86 s, a loop), `Swish*`, `Return_*`, `Added{Fire,Cold,Lightning}_*`
  (5 s elemental layers). `Four_Aurora_/Breach_/Djinn_/DiseaseVector_/StoneBender_TempestBell` are league/MTX variants.
  The bell has no true idle sound in the bank: `SmallBellLoop_*` is the loop the event plays while it hangs (not tried);
  the other `OnHit` takes are interchangeable alternatives.
- `Four_FrozenLocus` (25): `FrozenLocus_FX_Appear_01..06` (1.7-2 s), `Explode_01..06` (2-2.7 s), `Hit_01..06` (1.6 s),
  `FrozenGround_FX_Loop_01..07` (5 s, the frozen ground, not the crystal). `Four_FrostWall` is the Frost Wall's own bank.
- Not auditioned by ear; chosen by name and envelope.

## Item drop sounds (2026-10-24)
Every item base has its own drop sound; `item-sounds` reads which from the game's tables (offsets from the PoE2
`poe-tool-dev/dat-schema`, checked against the row sizes 360 / 968 / 33):
- `BaseItemTypes`: `Id` +0, `SoundEffect` +88 (scrolls, some quest items), `ItemVisualIdentity` +124.
- `ItemVisualIdentity`: `InventorySoundEffect` +24 (most equipment, gems, flasks: `FourSkillGemDrop.ogg`),
  `DropSoundEffect` +919 (only a generic rarity layer: `Currency.ogg`, `Item_Quest.ogg`).
- `SoundEffects`: `Id` +0, `SoundFile` +8 (the drop event), `SoundFile_2D` +16 (use/move event).
- Event → sample by name against `ItemDrops.bank` (lowercased, punctuation and a leading `Four` dropped; `Item`/`Items`
  prefix allowed; use/move/equip events rejected). Orbs carry only a use event (`OrbUse_Chaos`): the drop sample is
  derived (`OrbDrop_Chaos_01`). Result: 3,583 bases on 113 samples ("Scroll of Wisdom" → `Scroll_Drop_01`, a belt →
  `Belt_Drop_01`, an uncut gem → `UncutSkillGem_Drop_01`). Unmapped: gold (`GoldCoinDrop.ogg` vs `GoldDrop_01..08`),
  the generic `Item_Quest.ogg` / `Currency.ogg` bases, and league events living in other banks (fossils, runes, maps):
  the plugin falls back to its generic cues for them. Not auditioned by ear; the Studio API (`fmodstudio.dll` ships
  with the game) could render events exactly if a match turns out wrong.
- Other cues surveyed: no campaign checkpoint bank besides `BossCheckpoints`; `Waypoint4.0` has `WaypointActive_01`
  (likely the idle sound); `Chest_Rarity` also has `Chests_Rarity_Rare_Open_01`.

All except `gold` are capped at 1.5 s. Choices are by name only and not yet auditioned; swap entries in the `Cues` table.
Other useful finds: `Four_ItemFilter` (AlertSound_01..16, the item-filter drop alerts), `UI_General` (GenericFail,
ObjectInteractFail, Travel), `QuestStatus`. A name survey of every sample can be regenerated by scanning FSB5 name tables.

## Charge and combo candidates (2026-10-30)
Surveyed with `bank-samples` for a louder charge/combo cue than the synthesized ticks (user: too much speech, wants
cues instead); auditioned on the page https://claude.ai/artifact/1fXBPHVbfmPkZrh7Dxh669. Picked: `ChargeGain_Fork_01`
(cue `power_charge`, launcher sounds step v3); none of the combo or lose candidates.
- `Four_Charges`: the base game's own `Charge_Power_ChargeGain_01` (1.8 s), `Charge_Frenzy_Gain_01`, `Charge_Consume_01..06`
  (1.0 s). No endurance or spirit gain sample in it.
- `Four_ElectroChargesMTX` (an electric charge cosmetic): `ChargeGain_`, `ChargeConsume_`, `ChargeExpire_` in three
  styles `Fork`, `Gold`, `Goo`, 6 takes each (gain ~1.0-1.1 s). `Four_ArcaneChargesMTX`: `Arcane_ChargeGained/Consume/Expire_01..06`.
  `ChargesDaemon`: `Lightning_Shot/Cast/Boom/Loop_*` (mono, peaks at 1.0).
- Combo: `Four_QuarterstaffCombo` holds `QuarterstaffCombo_Slash_01..06` (1 s), `Finisher_OnAct_01..04` (1.25 s),
  `Slash_Finisher_01..04` (2 s). Musical stingers: `BreachStingers` (`Gameplay_Breach_WaveStart/EncounterEnrage/
  EncounterCollapse_Stinger_01`, `BreachMonsterAggro_01..04`, `TeaserLayer_UI_*`) and `Four_EssenceStingers`
  (`EssenceMonoliths_Stinger_Activate[Unique]_01`), all 3-4 s: only their first second or so works as a hit.

## The Diablo IV item set (`assets/diablo4/`, shipped, 2026-11-04)
Testers asked for sound sets as in the D2R mod; setting `ItemSounds`, default the game's own (`ItemSoundSet`). The 13 WAVs are
the loops that mod builds (`build/item-sounds/`, from `sounds/item loop sounds/loot/` and `exploration/shrine.ogg`, so with its
boosts for the short clicks and its quest bell cut to 0.7 s), played as it plays them: each item, chest, body or shrine heard
keeps one voice going (`GameSounds.LoadLoops`, `AudioCueService.Bed`), from a random place in the recording so two of one sound
are not in step. Rendered to the mixer's format (44.1 kHz, mono, 16-bit):
- Resampled by cutting the spectrum to the new length, which keeps the sound periodic: the last sample still leads into the first.
- Peak at 0.9 x 0.4 / 0.7: D2R normalizes to 0.9 and plays a near item at 0.4, and the object volume here defaults to 70 %. The
  chest 4 dB over that (D2R's `ChestLevelDb`). No level is applied on loading.

What the first version got wrong (v2026.11.04; the user: distorted, heard to stop and start). It cut the loudest 0.9 s of the 14 s
textures and repeated it every 600-3200 ms, as the game's cues are:
- Near, the level swung 8.5 dB at every repeat; from 1.2 s on there was silence between the repeats.
- It was not clipping: the clips peaked around -7 dB, and four items at once reached -5 dB, under the limiter's ceiling.
- Pitch is the direction here (0.8-1.26), and these recordings are bright. What no linear filter explains, under 12 kHz, at pitch
  1.26: gold -24 dB, magic -28 with linear reading; -27, -33 with cubic; -54, -64 with a 16-tap sinc, which `LoopReader` now is for
  every bed. Cues keep the linear reading: they are short, and the game's own sounds were never heard as distorted.

A tester found them shrill and clashing as they neared loot (2026-11-06), on the latest version. Pitch never follows distance:
the 1.26 is a thing straight up on screen, and it stays until 150 units away while the gain rises. `ItemSoundSet.LoopPitch`
stops the loops at D2R's own 1.12; down stays at 0.8, so up and down still differ. The game's own sounds keep the 1.26 the user
asked for on 2026-10-24. Not heard yet by the user or the tester.

D2R also limits the plain sum of its loops' gains to 1.25 near items. Starting at unrelated places they add in power, so that left
four near items quieter than one (-31 against -28 dB) and each 10 dB down, and this mod's far gain of 0.3 would set it off for
items merely scattered over the screen. It protected nothing here: ten loops at full gain peak at -0.3 dB, under the ceiling, at
-15 dB overall. `ItemSoundSet.PileScale` limits their power instead: four come to -25 dB, ten to -23.

Checked offline with the real `Bed`, `LoopReader` and `PeakLimiter` (no device): no gaps walking past an item, every seam under
the sound's own largest step at pitch 1 and 1.26, a stopped voice gone in 160 ms without a click. Not heard in a game yet.

Map: gold; flask → potion; currency → rune; scrolls (the game's `Scroll_Drop` sample) → scroll; gem; quest item; equipment and
other items by rarity → normal, magic, rare, legendary; chest; corpse; shrine. Uniques have their own scanner kind for this.
Exits, portals, waypoints, breakables, switches and altars keep the game's sounds: the D2R setting is about items, and those
were tuned here by ear. Last Epoch and the D2 drops were left out (user's request). The distance curve stays this mod's on screen,
as for the idle beds: items going silent on screen was a complaint here (2026-10-27). Past it, see [how sounds end](#how-sounds-end-2026-11-07).

## Quest items (2026-11-07)
A quest item keeps its own drop sound with the quest sound laid over it at the rarity layer's gain, in the game's own set: the Runic Belt's
`RunedBelt_Drop_01` alone was not told from a belt (user). It is the game's layering too: `ItemVisualIdentity.DropSoundEffect` gives
quest items `Item_Quest.ogg` beside the base's sound. Every `kind` "quest" gets it, books and scrolls among them. Not heard yet.

Everything else under a quest icon sounds too (user 2026-11-07, "para todos"): with no sound of its own it plays `quest_item`, with one
(a chest, in the game's set) the quest sound goes over it. The icon is the only sign read: the map device always carries `QuestObject`
(`show_league_flag` only hides the icon, and no hidden flag of `MinimapIcon` is known), so it sounds in the hideout too.

An object or device the quest tracker names sounds the same while it names it (`QuestMatcher`, `ObjectSounds.WhileWanted`, user 2026-11-07):
the Relic Altar (`Act2/2_13/Objects/SanctumRelicDevice`) carries no quest icon, only "Coloca tu barya en el Altar de reliquias".

A plain object named only by a `WorldDescription` sounds the same, whatever the tracker says: the game gives it a box of its own because it is there to be used (Blood
Chalice, `SanctumEncounterKillCounter`; Sand Hourglass, `SanctumEncounterTimerLever`; user 2026-11-07, after several of them one by one). A league device of that table
has its own rules and stays silent: out of play it would go on sounding.

A lever built as a plain object (`SanctumGenericLever`, Trial rooms) has the quest sound, chosen over the switch sound because it must stand out (user
2026-11-07); one built as a chest keeps the switch sound.

A named barrier sounds the same when a campaign area made it (`Metadata/Terrain/Gallows/Act*`, user 2026-11-07): the Ancient Seal at
Balbala's door (`Act2/2_2/Objects/BossDoor`), with secret doors, the Mawdun explodable wall, the Act 4 gates and prison doors. 33 of the
271 `.ot` with `TriggerableBlockage` have a `display_text`; the
plain doors of dungeons, hideouts, leagues and maps stay silent. Such an entry keeps the kind "barrier", which the guide reads, and marks
the sound in `ScanEntry.Sound`.

## How sounds end (2026-11-07)
A tester: every sound stopped dead on walking off it, gold in both sets, town portals, waypoints; they remembered it fading. The
code had not changed there, the step had always been in it: 30 % of the level at the edge of the range (1300), then silence, in the bed's
11 ms glide. Four things cut, all softened:
- `ProximityFade`: unchanged up to the range, then to nothing over 600 more units (squared), for object sounds and placed
  objects. Measured through the real `Bed` on the portal loop, walking off at 388 units/s: before, a 75 dB fall within 100 ms at
  -41 dB; after, -41, -49, -51, -55, -65, -69 dB every 250 ms. Live: the waypoint still heard at 18 steps, gone at 20.
- `Bed.Stop`: a 120 ms release (it was the 11 ms glide), so a picked up item or an opened menu dies away: 60 dB down in 759 ms, not 92.
- Cues let go early (`StopAll`, more than `MaxVoices`) fade over 20 ms instead of stopping mid-wave. `MaxVoices` went from 16 to 32:
  its comment assumed 0.1 s cues, the game's last 0.9 s.
- The loader's 0.9 s cap left recordings still ringing behind a 50 ms fade (dB under their peak at the cut: ritual and corpse
  -12, shrine -13, portal -14, unique -15, quest item -16, waypoint -18, chest -20). A recording that is cut now dies away over its last
  0.3 s; one that ends by itself keeps the 50 ms.
Repeated cues are spaced 3.2 s apart at the far edge, about 1200 units of walking, so gold in the game's own set fades in one or
two quieter hits, not smoothly. The interval was left alone: it was tuned by ear.

## Harmful ground alarm (2026-11-04)
A tester asked for something more urgent than the 110 Hz pulse. The user went through alarms (too shrill), a family built like the
unblockable cue, and chose notes a semitone apart, lowered in steps to 73.5 + 78 Hz. An octave down the unblockable's seven harmonics
stop near 550 Hz, so it has fourteen.

Measured against a mock fight (the game's hit and shout samples, the mod's enemy cues) in third-octave bands, at about one loudness:
the old pulse, the seven-harmonic and the fourteen-harmonic versions all peak about 7-8 dB over it, so that figure does not tell them
apart. They differ in the mids: at 630 / 1000 Hz the old pulse sits -13 / -20 dB under the fight, seven harmonics -9 / -18, fourteen
-5 / -10; the new alarm also has a hard attack and grit where the old one swelled. Not judged in a real fight yet.

## The mod's own recordings (`Poe2Accessibility/assets/`, shipped)
- `combo.wav` (2026-10-30): the user's "combo build.mp3" (its note E2), lowered 4 semitones to C by resampling, stereo
  44.1 kHz, first 1.5 s faded from 1.1 s. `ChargeWatch` plays it and `power_charge` unpanned in stereo
  (`GameSounds.GetStereo`, `AudioCueService.Play(StereoSound…)`; user: the player's own news has no direction), the
  combo at the mixer's pitch a semitone higher per point (`PlayerCharges.GainPitch`).
- `cull.wav` (2026-10-30): freesound_community "old zildjian gong, quite natural" (34294, 54 s), from its strike,
  twice the speed at the same pitch (ffmpeg `atempo=2`), held 0.22 s then decaying (tau 0.17 s) to 0.85 s. Three other
  takes (1.5x same pitch, 1.5x and 2x as a sampler) are on the page above.
- The loader caps every sound at 0.9 s (`GameSoundSeconds`), so a longer recording is cut there.
