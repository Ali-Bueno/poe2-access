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
| exit | UI_General | WorldAreaTransition_02, frozen | 6 s of 120 ms grains from its 0.05–0.5 s (granular freeze) |

Area transitions have no idle sound of their own, and the crossing sounds (`WorldAreaTransition_02`, `Travel_1..3`)
are one-shots that die out within 2 s, so the exit loop holds the crossing's loud start still (user's choice
2026-10-27). Spectra did not predict what sounds alike: the Delirium encounter-object idle loop (a 400–3200 Hz
shimmer, far from the ritual stinger's spectrum) was heard as the ritual sound, and a torch
(`Act3_Braziers/FireTorch_loop_01`) did not suit an exit (user 2026-10-27). Rejected for exits: wind loops (the radar's open floor is a
noise breath), `Doodads/SubTransition_01` (a sub-bass throb once a second that dies out, like the weak-point
heartbeat), `PortalScroll_Effects_Id_IdleInactive_*` (about -39 dB and uneven), the tormented-spirit wisp loop
(same spectrum as the waypoint).

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

## The mod's own recordings (`Poe2Accessibility/assets/`, shipped)
- `combo.wav` (2026-10-30): the user's "combo build.mp3" (its note E2), lowered 4 semitones to C by resampling, stereo
  44.1 kHz, first 1.5 s faded from 1.1 s. `ChargeWatch` plays it and `power_charge` unpanned in stereo
  (`GameSounds.GetStereo`, `AudioCueService.Play(StereoSound…)`; user: the player's own news has no direction), the
  combo at the mixer's pitch a semitone higher per point (`PlayerCharges.GainPitch`).
- `cull.wav` (2026-10-30): freesound_community "old zildjian gong, quite natural" (34294, 54 s), from its strike,
  twice the speed at the same pitch (ffmpeg `atempo=2`), held 0.22 s then decaying (tau 0.17 s) to 0.85 s. Three other
  takes (1.5x same pitch, 1.5x and 2x as a sampler) are on the page above.
- The loader caps every sound at 0.9 s (`GameSoundSeconds`), so a longer recording is cut there.
