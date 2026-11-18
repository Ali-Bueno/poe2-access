# Cinematic subtitles (0.5.5f, 2026-11-06; narration found 2026-11-15)

Can the mod read the subtitles of the pre-rendered cinematics? No: the game draws none and ships none. The act transitions carry
an English voice-over whose text is nowhere in the game's data, so the mod has to bring the text itself.
**MEASURED** = found in the files or the logs; **INFERRED** = consistent, not proven; **OPEN** = unknown.

## What the files say

- MEASURED: the videos are loose Bink 2 files in `<install>\Art\Videos\` (321 `.bk2`, 55 at the top level). The cinematics, from
  their headers (all 3840x2160, one audio track):

  | Video | Signature | Length |
  |---|---|---|
  | `Act1_Transition.bk2` | KB2j | 32.8 s |
  | `Act2_Transition.bk2` | KB2j | 39.5 s |
  | `Act3_Transition.bk2` | KB2j | 28.1 s |
  | `Act4_Interlude_Transition.bk2` | KB2j | 35.2 s |
  | `Interlude_Endgame_Transition.bk2` | KB2j | 39.5 s |
  | `FortressCinematic.bk2` | KB2n | 27.8 s |
  | `cinematic_video.bk2` | KB2n | 298.2 s |

- MEASURED: the game is built to load subtitles and has none to load. The executable imports `BinkLoadSubtitles` and
  `BinkCurrentSubtitle` and holds the suffixes `_EN.srt`, `_en_US.srt`, `_ES.srt`, `_es_ES.srt` (the same pair for FR, DE, KO, JA,
  ZHCN, TH, ZHTW, PTBR, RU), a `<subtitle>{` markup and `SubtitleAdditionalLineSpacing`. No `.srt`, `.vtt`, `.ass`, `.sub` or `.ssa`
  exists in the install or among the 4,325,172 bundled paths; nothing bundled sits under `art/videos`.
- MEASURED: the option exists: "Enable Subtitles", `enable_subtitles` in `poe2_production_Config.ini`, on for the user.
- MEASURED: which video a travel plays is a script in `NPCTalk` (2,227 rows of 423 bytes; the script string is at row cells 92, 156
  and 164, the travel script at 76, column 0 is the `NPCs` row), the same in every language copy:

  | NPCTalk row | NPC | Option | Travels to | `SetAreaTransitionVideoPath` |
  |---|---|---|---|---|
  | 286 | `Four_Act1/HoodedMentor` | "Follow the Beast's trail" | `G2_1` | `Act1_Transition.bk2` |
  | 411 | `Four_Act2/MarakethMatriarchCouncil` | "Travel to the Sandswept Marsh" | `G3_1` | `Act2_Transition.bk2` |
  | 527 | `Four_Act3/AlvaZigguratEncampment` | "Travel to Kingsmarch" | `G4_town` | `Act3_Transition.bk2` |
  | 679 | `Four_Act4/HoodedMentorKingsmarch` | "Travel to Oriath" | `G_Endgame_Town` | `Interlude_Endgame_Transition.bk2` |

  `Act4_Interlude_Transition.bk2` is in no table: the executable names it itself. OPEN: from which area and to which it plays.
- MEASURED: the narration's text is not in the data: none of its sentences is in any of the 2,996 `data/balance` tables (English and
  the nine language folders, 219 tables each) nor in 21,998 other bundled text files, as UTF-16 or UTF-8. `NPCTextAudio` ids ending
  `_Cinematic` are other lines.

## The narration (speech-to-text of the audio tracks, 2026-11-15)

The Hooded One speaks over each transition, in English only. Two models agree except where marked `[?]`; names are spelt as the
game spells them.

- **Act1** (0.4-26.8 s): The Countess has escaped with the Beast. Every attempt to control it has ended in cataclysm. We must pursue
  her to the plains of Vastiri. We should find a `[? the]` Maraketh caravan. Only they can lead us through that harsh and
  unforgiving land.
- **Act2** (0-34.9 s): The Beast has grown too powerful. We can no longer contain it through mortal means. There was once an empire
  that embraced corruption. They studied it, mastered it, and became the greatest civilization the world has ever known. But it led
  to their destruction. Our allies will continue the pursuit of the Beast, but we shall undertake a different search. We must go to
  the ruins of the Vaal Empire, in search of a weapon against the coming cataclysm.
- **Act3** (0-24 s): Doryani has one piece of an ancient weapon that can control corruption. The other three pieces lie somewhere
  in the countless islands of the Karui archipelago. `[? But]` Their current leader is distrustful of outsiders. He would never give
  us passage. Our only path forward lies through Kingsmarch. Perhaps there we shall find a ship and crew brave enough to take us
  where we need to go.
- **Act4_Interlude** (0.9-29.9 s): The Countess leads the Twilight Order, and we now know she has taken the Beast to Oriath. By
  using the weapon, the Karui have been cured of their blood fever, and have pledged to aid our cause. But we need a sizable army
  for the battle ahead. It is time to rally our allies. We must recruit the Ezomytes, the Maraketh, and the living Vaal that
  Doryani speaks of. Then, our siege may stand a chance.
- **Interlude_Endgame** (0.8-37.9 s): With our forces united, we set sail from Kingsmarch and head for Oriath. But as we
  approached, the unthinkable took place. We were too late. My worst fears have come to pass. The world is consumed by madness once
  more. Our last hope for refuge is the Ziggurat of Utzaal. Perhaps Doryani's preparations will shield us while we plan our next
  move. It appears Doryani truly does have a plan for every contingency.

How: `ffmpeg -i <video>.bk2 -vn -ac 1 -ar 16000 <out>.wav`, then faster-whisper (`medium`, and `large-v3-turbo` with the names as
`initial_prompt`). ffmpeg 8.1 opens KB2j and refuses KB2n ("Invalid data"), so OPEN: what `FortressCinematic.bk2` and
`cinematic_video.bk2` say, and which video plays after creating a character.

## Seen live

**2026-11-06, creating a Druid.** Evidence: an F6 dump taken during the cinematic and the screen watcher running through it.
- MEASURED: no state of its own: `CreateCharacterState` holds from "Esperando respuesta..." to `LoadingState`, about 13 s.
- MEASURED: on screen, only `root_control`, `visible_area` and `Skip_Cutscene` (`[0,13]`, also at state+0x2B8) with its label
  "Presiona <<xbox_button_a>> para saltar". No other text in those 13 s, and no `.bk2` path in the dump.

**2026-11-15, Act 1 to Act 2** (the Hooded One in `G1_town`, "Entrando a Alrededores del Vastiri..."). Evidence:
[the log lines and the watcher's events](evidence/cinematic-act1-transition-20261115.txt); 32 screenshots of the game window, one
every 0.8 s, were looked at and not kept.
- MEASURED: the video plays inside `AreaLoadingState`, which lasts 33.4 s (12:05:05.3 to 12:05:38.7) against 2 to 4 s for the plain loads before it:
  the first frame of the video is on screen 0.4 s after the state begins, the last seconds are black, then the area.
- MEASURED: no subtitle is drawn. Every frame shows ink drawings over a map of the journey and, at the bottom, "Presiona A para
  saltar"; the watcher sees that label 2.5 s into the state (layer 5) and no other text but the loading tips.
- MEASURED: the mod speaks over the narration: "Cargando área." and two of the three loading tips (layer 2, hidden under the video).
- MEASURED: the video is `Act1_Transition.bk2`: its last-access time moved at 12:06:07.3, 29 s after the video ended, so that time
  is written when the game lets the file go and cannot name a video while it plays.
- MEASURED: the area row the mod tracks named `G2_1` when the state ended. OPEN: whether the loading state already holds the
  destination while the video plays (the mod reads that row only in game).

**2026-11-17, Act 2 to Act 3** (the Matriarchs in `G2_town`, "Entrando a Ciénaga de arena..."). Evidence: the play copy's
`accessibility.log.old`, 18:11:51 to 18:12:32.
- MEASURED: the load lasted 40.9 s (the longest plain load in three days of logs: 13.7 s; from a town: 1.5 to 9.3 s, most
  under 4 s). "Loading: G3_1 known 0 ms in (leaving G2_town)".
- MEASURED: the skip prompt showed only 39.3 s in (layer 5, "Presiona A para saltar"), at the video's end: the mod, waiting for
  it, said "Cinemática." and all seven lines at once, under the area's announcement. Nothing was heard as the narrator spoke.
  The loading tips (layer 2, at 10, 20 and 30 s) were said over him. OPEN: why the prompt showed at 2.5 s in Act 1 and at
  39.3 s here (a button pressed in Act 1 is the likeliest).
- So (2026-11-18) a load from a town to a travel's video area is its video once it has lasted 4 s, prompt or not.

## What the mod does (2026-11-15)

The owner's word: the lines one by one as the narrator says them, and nothing in English, whose voice is understood.

1. **A cinematic is on** once a load from a town (`*_town`) to an area below has lasted 4 s
   (`CinematicTracker.TravelVideoAfterMs`; only "Cinemática." is said then), or while a visible element named `Skip_Cutscene` is in the tree: the screen watcher marks the layer that
   holds it (`ScreenLayer.SkipsCinematic`, whatever its depth), and it lasts until the state ends. Said once: "Cinemática." and the
   prompt's own text. The narrator leaves that layer and, in a load, the loading screen's lines unsaid meanwhile.
2. **Which one:** by the area being loaded, from the `NPCTalk` rows above (`Cinematics.VideoTo`). MEASURED live on two plain loads:
   the loading state holds the destination from the first tick of `AreaLoadingState` ("Loading: G2_town known 0 ms in (leaving no
   area)", "Loading: G2_1 known 0 ms in (leaving G2_town)", loads of 2.7 and 2.2 s); the log writes that line at every load.
3. **The text:** `Announcements.Cinematics.cs`, English as transcribed and Spanish in the game's own words for its names
   (`NPCTextAudio`: "la bestia", "Marca del rey", "Orden del crepúsculo", "ezomitas", "imperio vaal"). English says none of it; another language
   says a line only if its language file carries one under the English sentence (none does yet).
4. **When:** each sentence at the second it starts in the video, counted from the start of the load (the first frame is on screen
   0.4 s later). The prompt shows about 2.5 s in, so the first sentence comes late and the rest on time; a line whose moment has
   passed is still said.
5. Switch "Cinemáticas: el aviso para saltarlas y las frases del narrador" (`AnnounceCinematics`, Chat).

**Not heard in a game:** a cinematic plays once per character and act. The pure part is under test (`CinematicTests`); the plugin
loaded and ran two loads with it and no error line. The first log lines to read at the next one: "Cinematic: skip prompt on layer",
"Cinematic narration:".

**Left, and why:** `Act4_Interlude_Transition` (its narration is above, its trigger is in the executable and unknown);
`FortressCinematic`, `cinematic_video` and the video after creating a character (audio not readable with ffmpeg, so only their
prompt is said); the narration in the eight other languages (not translated).
