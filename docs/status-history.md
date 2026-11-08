# Status history

What `STATUS.md` used to keep under "Next step": each finished chunk, newest first, as written at the time.
Moved here on 2026-11-08 so the status stays a dashboard; git has the rest.

Then (2026-11-08): what the mod says is read from game data, never the window ([data sources](docs/research/data-sources.md)). The world is placed by the camera's own matrix with its aspect divided out (`ScreenProjection`, `CameraView.Draw`; 0.00000 heights from the window's drawing at 16:9 and 5:4); a world map pin is told from its MapPins row when its popup was not heard, and its level when only that was missing (`MapPinNarrator`, endgame-ui.md §6); passive nodes sit on the tree's canvas, so the stick's picks are the same at every window size (the game's landings were identical at 5:4, 21:9 and 16:9; the model 100/120 and 107/120 on random walks); Culling Strike follows the player's threshold stats; reading order and option entries use laid-out rects. Sweeps at 16:9, 21:9 and 5:4 diffed: only 5:4's hidden Smallcaps captions remain, the game's own. Next, asked by the user (2026-11-08): read more from the game's code and files instead of any geometry, and judge whether to drop GameHelper for an own host that survives updates better. (1) The stick's exact rule, found in the executable (RVA 0x9B9BA0; [passive tree](docs/research/passive-tree.md), last bullet of "Stick movement"): linked nodes within 20 degrees first, else the lowest `(5 - 4 dot) * distance` within 45, only nodes inside the window; 240/240 on the recorded pushes against the model's 208. Implement it in `PassiveStick` (linked flag and edge order, the window and the panel's centre), after confirming the edge order live. Report, tools and the 240 pushes: D:\code\decompilers\projects\poe2-research (`re\stick-rule.md`). No RTTI in this build (vtable-8 holds no locator). (2) Ghidra 12.0 is installed at D:\code\decompilers\ghidra_12.0_PUBLIC (12.2 is out, 2026-10-16): run a full headless analysis of PathOfExileSteam.exe into a durable project there (`support\analyzeHeadless.bat <dir> poe2 -import <exe> -max-cpu 12`, GHIDRA_MAXMEM=20G; the PE is not packed, TLS callbacks at 0x142EAC188). (3) The GameHelper question: inventory what the plugin uses from it (attach, 6 signature patterns, states, components, UI elements, frame loop) against what already heals itself ([patch survival](docs/research/patch-survival-survey.md), [compatibility](docs/compatibility.md)), then recommend. Release pending: everything since v2026.11.07-8 (window independence, camera matrix, map pins, culling stats, passive canvas) is committed and pushed but not released.
Then (2026-11-07): nothing heard depends on the window any more ([camera view](docs/research/camera-view.md)): what counts as on screen is a 16:9 screen around the player from the camera measured at six window sizes (`CameraView`), the matrix's directions fall back on it, pixel tolerances became the game's own units, the sheet names numbers whose captions a 5:4 window hides, and a sheet number that comes a frame late re-reads its widget. Five window sizes were walked and diffed; the monitor-off case could not be measured (this PC went to standby).
Then (2026-11-07, released as v2026.11.07-8): the Trials' honour every tenth and at nothing, sacred water and keys said and in the vitals (`TrialHud`, `AnnounceTrialCounters`), traps by file and state (`TrialTraps`, `TrapWatch`, `TrialTrapCues`, in the scanner and on approach), the buffs that drain honour as harmful ground, Resolve Weakness and Binding Chains said, no false "hidden death screen" at a failed run ([notes](docs/research/endgame-ui.md)). The traps come from the game's files, none met live: read the "Trial trap seen" lines of the next gauntlet room.
Then (2026-11-07, released as v2026.11.07-8): a tester's two reports reproduced with the game in English at 1280x1024. The sheet's attributes and resistances read no number: captions hide at some sizes and the value was matched by place; now from the player's Stats. A build node with a note read the note alone: it owns two tooltips; now both, stats first. Both read live; the exact "Unallocated" line replaces the old short-line guess, which could drop a real short stat.
First (2026-11-07, released as v2026.11.07-7): the Trial's honour is said as it falls (`HonourWatch`, setting `AnnounceHonour`); not heard yet, and whether the HUD line is always among the texts read is to be seen.
Then (2026-11-07, released as v2026.11.07-7): a plain object named only by a WorldDescription sounds as a quest object, such as the Blood Chalice (`Classify`, [notes](docs/research/game-audio.md)).
Then (2026-11-07, released as v2026.11.07-7): the guide locks by itself on the crystal of the room quickest to reach, in turn, and lets go with the room done (`TimedGuide`, `EntityCatalog.WalkTo`, setting `GuideTimed`), as their sound goes through walls; the user completed such a room with the nearest first; the next crystal is heard from 4000 (`ScanEntry.Timed`); the clock was heard live, 18, 10 and 5.
Then (2026-11-07, released as v2026.11.07-7): the Trial's map says each room's column and standing (`TrialMap`, [notes](docs/research/endgame-ui.md)); links between rooms and the player's room past the entrance still unread.
Then (2026-11-07, released as v2026.11.07-7): the Relic Altar sounds after a failed run, with the Trial's map, floor doors and rewards (`MarakethSanctumRoomSelector`, [notes](docs/research/minimap.md)).
Then (2026-11-07, released as v2026.11.07-6): a Trial lever sounds as a quest object, a crystal is listed only under its objective, and a buff's "0:06" is no longer taken for the clock (`Classify`, `CountdownWatch`).
Then (2026-11-07, released as v2026.11.07-5): the Trial's Death Crystals are listed, named and sound like quest objects, and the HUD's clock is said (`world-titles.json`, `CountdownWatch`, [notes](docs/research/minimap.md)); the user's run died of the clock, 20 s unheard, in room 2; none of it seen live. The real Relic Altar, heard live under its stand-in's name, now takes it from the same table.
Then (2026-11-07, released as v2026.11.07-5): the quest tracker says only the quest that changed (heard live while paging), a page the player turns cuts what is being said, known by the number on its legend (`QuestTrackerPage.Turned`, `SpeechFloor.Take`), and an object the tracker names sounds as a quest object while it does (`ObjectSounds.WhileWanted`).
Then (2026-11-07, released as v2026.11.07-4): a friend of the user heard the "positions" compatibility warning with every sound normal and no game update. `Placed` remaps nothing on a miss, so the warning changed nothing; the judge needs the camera on the player, so a slow first load or a cutscene can fail it, while 106 sweeps here never did. The cause is inferred, not reproduced: the friend's log (`Layout: positions NOT FOUND`, then `where measured`) would settle it. `JudgePosition` now leaves it not judged while the measured field holds a point of the map (host patch).
Then (2026-11-07, released as v2026.11.07-3): Balbala's Runic Seals are listed on a next run of the Traitor's Passage (`Classify`: a quest icon is no barrier; [notes](docs/research/minimap.md); not seen, the user's were pressed already). The user hears every object under a quest icon and the campaign's named barriers, such as the Ancient Seal, with the quest sound (`MinimapMarks.KindUnder`, `IsStoryBarrier`, [notes](docs/research/game-audio.md#quest-items-2026-11-07)). Risu's cage was named and opened live ("Jaula de Risu, A"; `EntityCatalog.MarkLabel`, [notes](docs/research/minimap.md)); its sound not confirmed by ear.
Then (2026-11-07, released as v2026.11.07-3): the user hears a quest item with the quest sound over its own (`ObjectSounds.Layer`, [notes](docs/research/game-audio.md#quest-items-2026-11-07)).
Then (2026-11-07, released as v2026.11.07-2, confirmed by the user in play): the camp's portal spot no longer reads a list of areas (user: "un montón de áreas"). Portals were remembered as landmarks and saved per area for good, so every portal ever seen there came back, sounded and could take the exits' one voice; now one is forgotten once the player is near its place and it is not listed (`ScanEntry.CanVanish`), and never inherited by a new instance.
Then (2026-11-07, released as v2026.11.07): the user and the tester judge by ear how sounds die away (`ProximityFade.TailWorld` 600 and the 120 ms release are a first guess; gold in the game's own set only gets quieter hits), and the user tries the tree's finder on the D-pad. The target is only said on the node's last line, so a long node reads it late: shorten if it annoys.
Then (2026-11-06, night, released as v2026.11.06-9): pressing Y again and again on a talisman cuts the last form, seen in the log
in play (`FocusDetails.LineSwapped`; a number ticking in a tooltip must not cut). User test still due: a game notice must not silence
the inventory, no notice has come since the fix. Using the Winter Wolf's Head held the cursor's speech 8.4 s: the chat notice queued behind the item's text as a line moves
could not end, its hold counted from the end of that text (`MaxAheadMs` 6 s + 2.3 s). Notices now cut in and yield to moves, and the
screen's copy of one is dropped (`SpeechFloor.Echoes`). Both forms are said now, confirmed by the user: a tooltip line the focus held
was never news, even replaced in place (`FocusDetails.NewTooltipLines`). Cinematics: the F6 dump and the watcher show no subtitle text in the UI tree
over 13 s of the one after creating a character ([notes](docs/research/cinematic-subtitles.md)); next, one left to run to its end.
Then (2026-11-06, evening, released as v2026.11.06-8): a respawn read the whole HUD and chat (user, on death); while an
area loads only the loading screen speaks now, confirmed by the user in play.
Then (2026-11-06, evening, released as v2026.11.06-8): the warning "cannot find the area's level" at
the Riverbank reproduced on the user's new character; the level is one byte and a flag beside it read as 257
([notes](docs/research/ui-layers.md#area-level-on-a-new-character-055f-2026-11-06)). Fixed and checked live on that
character: "Área: La orilla del río, nivel 1", real instance hash, no warning. Diablo IV loops stop at pitch 1.12, not heard yet.
Then (2026-11-06, afternoon, released as v2026.11.06-8): ask the tester who could not type a name what
they typed and heard, and whether Steam runs in Big Picture. Not reproduced as a block: typing worked here, and every silent
dead end found is now said. The account window (new accounts: name on Y, Create on LB) was never tested live.
First (2026-11-06, morning, released as v2026.11.06-7): the user hears the objects' own sounds in play (`object-sounds`; 617 objects, none heard by ear: the lookup was proved live on real entities with boulders standing in, and the levels measured), and on the next patch check `Code fields: 50 of 50 read`, `Layout: poise` and `Layout: walkable` in the log.
After each patch: `py -B -I tools/exe_reader/reader.py`, keep its output in `docs/research/evidence/`, and if the user can get old exes, run it on them: that is what would decide whether a run-time reader is worth a decoder dependency ([study](docs/research/exe-layouts.md#what-would-measure-it)).
First (2026-11-06, night): the user hears the dodge warning on Faridun Olroth's `MoltenSlam` and Doryani's beam, calm until now, and Rage on a character that has it (the keys were checked live, Rage itself was not: no such character). `AnimationFieldSearch` and `JudgeReaction` were each simulated live.
First (2026-11-06, 0.5.5f, released as v2026.11.06-2, checked from Steam through the launcher's own update): the user hears everything again in play; it was all driven by the bot, sounds counted with the `sounds` dev command. Not exercised that night: Frozen Locus's crystal, parry, unblockable and harmful ground cues, a trade with a player. On the next real game patch, watch the log's "Layout:" and "Struct sweep" lines: that is the first true test of the searches on a patch not seen while writing them.
First (2026-11-05, released as v2026.11.05-2): the user hears one "Congelado" per freeze on the next boss of several bodies. The hidden Count was seen live at 20:36: not counted as a fight, 0 enemy targets, the six drops scanned and picked up.
First (2026-11-05, released as v2026.11.05): the user confirms in play the three fixes: no world sounds at the gem's confirmation or in the tree, the NPC options read at once after a quest (the page after the conversation; not seen live, no quest at hand), and the three new volumes. A layer silencing the game by mistake would show as "Panel over the game: …" with nothing open: none in a live walk.
Next idea (2026-11-05): the user hears the map hints in play; the road sign of the Hunting Grounds says no side on a first visit, since the exit is unknown until seen, and the camps' reveal needs checking in Act 3 ([map hints](docs/research/map-hints.md)).
First (2026-11-05, released as v2026.11.05): the user hears the lost hint (`LostHint`; `LostHintSeconds` 180 and `SoundsMs` 4000 are guesses to tune by ear, and it was never run live). "Camino a …" and the constant loop are gone at their request. Also not heard yet: the radar's tin at a doorway, D2R's "another chamber" (`PassageScan.Doorway`, `RadarRooms`; fired twice in a 53 s walk, `Radar Down: Exit`; measured on two outdoor grids only: dump an indoor one in the Manor with dev command `terrain` and tune `DoorwayWidthWorld`), "Área: …, nivel N. Esquina inferior izquierda." on entering (`MapCorner`, `AreaCorner`; seen live at Ogham Village: "Esquina superior derecha"), a discovery's line holding through a fight (`DiscoveryNarrator`, urgent: "Ya no estás enfriado" cut "Nueva salida: Mansión de Ogham" 15 ms in, and it is said once ever), the radar's exit in its wall and off its lane (`PassageScan.ExitToward`). The scripted walker stays unused until the user says otherwise (`tools/computer_use/README.md`).
First (2026-11-04, twentieth batch, released as v2026.11.04-2): the user hears the Diablo IV set as loops in play. It was heard to stop, start and distort
(repeated cuts of the recordings); the loops were only checked offline, since the user was playing at the time. Also not heard: a
finished ritual altar is silent now (`MinimapMarks.DeviceKind`; the log shows its icon changing live, 15:12 to 15:14 that day). A boss waiting in its arena no longer counts as a fight: seen live at 18:44 (dev command `fight`: Lachlann out of play, not counted, no fight) and in the user's play from 18:46 (items and the altar named beside him again), released as v2026.11.04-3. A not yet done altar beside a waiting boss still to hear.
First (2026-11-04, nineteenth batch, released as v2026.11.04): hear the new harmful ground alarm in a real fight.
First (2026-11-04, eighteenth batch, released as v2026.11.04): the Steam launch option works, seen in the user's own start that
day (`--- start`, then the game with `--nopatch`, and the launcher still alive following it). With the game open from Steam, update
by stopping only the host: the launcher is what Steam counts as running, so move it aside as `Updater.Replace` does instead of
closing it, then `Poe2Access.exe restart` (done live for v2026.11.04-2; the host attached to the running game). Still to hear: a
unique on the ground (`Items_UniqueDrop_01`, the user's pick, A on
https://claude.ai/artifact/BwkQFbDHU1bEpxfqkPwpa2). The Diablo IV set read and saved in the menu, its 13 samples load; not heard in play.
First (2026-11-02, seventeenth batch): the scanner keeps a fixed campaign map's exits and checkpoints in a new instance
(Clearfell), the game's exit and its named tile are one entry, "Área: X, nivel N" waits for the instance it names (a
new Clearfell said the town's 15), and the salvage bench is found and read first. Not heard yet: a real salvage's
"Reciclado: … Recibes: …" (no salvageable loose item; Renly's +7% crossbow was not bought). Exits now sound as a
repeated low rush of air (`CaptainSandstorm/Whoosh_Foley_GoblinGiantBoss_02`, the user's pick on
https://claude.ai/artifact/SK2WgvTwUM7jpTkgbFjpkL) instead of a bed lost beside a checkpoint's; not heard in game yet.
First (2026-11-01, sixteenth batch, released): area memory by instance hash + layout (above); cue volumes step by 5;
loud cues and the whole mix go through a look-ahead limiter instead of a saturating soft clip (a tester heard the dodge
warning distort when turned up). Not yet heard by the user: the dodge at 200-300 %.
First (2026-10-31, fifteenth batch, released): skills ready after their cooldown, with the bolt's click the user chose
(C on https://claude.ai/artifact/7Hfug5uzj2CU3mZwNzFdZb). A multi-use skill (Explosive Grenade, 3 uses) is named only
when a use returns after all were spent: not heard live (not on yarkey's bar).
First (2026-10-31, fourteenth batch, deployed into the user's `dist`: DLL, translations): the passive tree's stick-true link
directions at several zooms, a downloaded build guide's `.build` in the BuildPlanner folder ("en la build", steps, notes,
the route to the next node), the nested settings menu (Sound by kind), chests and ritual altars quiet in a boss fight,
and engraving an uncut support gem (category said on LB/RB, "…, engarzada en …" after). Minions not heard live (a Monk).
Supports socketed in a skill are not readable on its Skills row (no text, no item pointer within two hops of
`new_support_gem_entry`, 2026-10-31): Manage Sockets reads them.
First (2026-10-30, thirteenth batch, deployed into the user's `dist`: DLL, translations): hear "Volumen de cada sonido"
(a flask at 50 % against equipment), a boss or rare fight with drops on the ground (silent until 2 s after, then
named) and a talking boss whose taunts are no longer read, and "Dirección de los enemigos que aparecen" off ("Ahogado, 3").
First (2026-10-30): the user opens `dist`'s `Poe2Access.exe` (the v2026.10.30-2 launcher, put in alone over v2026.10.29-3):
the log should say "latest v2026.10.30-2" from `release.json` and the update install; that judges the manifest live.
Next (2026-10-29, twelfth batch, deployed into the user's `dist`): hear the culling gong on a Killing Palm Monk (rares named),
the charge ticks and counts (power heard live), Rage full/empty once "Furia al impactar" is taken, the other resources on
other classes, and the sound glossary at raised volumes ("…, Volumen de enemigos"). An Enraged buff id will show in a
"Player buff seen" line. Also in this batch, untested live: the bind screen's spirit panel naming each value's weapon set
("set de armas I: 0 de espíritu", `WeaponSetRows`; which box shows while managing set II, docs/research/skill-bar.md) and
a layout id no longer spoken before a caption ("action binding display", a tester).
Next (2026-10-29, eleventh batch, deployed into the user's `dist`): in Freythorn, hear the remaining altars named on approach
and listed in Misión and Encuentros, then "Altar de ritual del jefe" in the arena; a "Minimap mark not listed" log line is a
gap to classify. The quest page is said on area changes and on returning to the game window (heard live).
Next (2026-10-29, tenth batch: minimap marks, boss rooms, unexplored loop and radar mark, the shop, Freythorn's ground
rules): user test in the Tomb and the Mausoleum ("Sala del jefe: Asinia" and the Mausoleum's arena among Puntos de
interés once within 400 cells, the objective leading there), the unexplored loop and the radar's two pips in a maze,
signs ("Letrero: …") on the road to Ogham, the shop (no purchase was made in tests: hear the buy confirmation), and the
Voodoo King's "¡Quédate quieto!" / "¡Sigue moviéndote!". Not seen live: a `BossRoomMinimapIcon` object (none was in the
client's lists, also not with the Crowbell alive at its arena; such an object without a Render component would be
skipped by the catalog, whose positions come from Render), the "Taller de Renly" icon, the big title cards of area parts.
Before that, (2026-10-28, ninth batch, deployed into the user's `dist`: DLL, translations, `hazards.txt`): the user chose sample A
of the unblockable cue and of the hazard alarm (samples page v3 https://claude.ai/artifact/WZBmymWdgsrWpj5Yd2aRWk, already
in the mod); hear them in a boss fight (the Miller's charge: stabs at the wind-up; caustic or burning ground: the pulse
until stepping out). Before that, (eighth batch, deployed: DLL, translations, flagged `hit-times.txt` in the plugin
catalog and `app/GameData`): the user chose parry reverse hit A; test with a buckler/targe character: does the stop fall
right (tune "Antelación del aviso de Parada"), and is a red-glow attack (the Miller's charge) left to the dodge warning?
Then the stun bar while blocking (Warrior yarkidius, Raise Shield), energy shield news on an ES character, and the dodge
warning at 200-300 % "Volumen de esquiva y Parada". Before that, (2026-10-28, seventh batch): user test while selling: the cell after a "Vendido" follows the notice instead of waiting out its 2.5 s hold. Before that, (2026-10-28, sixth batch): character creation's name, league and Continue read in turn with the stick after B (user, live; a stale hover no longer wins over the highlight); game-patch regeneration 33 s instead of 85 s. Before that, (2026-10-28, fifth batch): mod updates no longer regenerate the game data (only steps whose version was raised; a raised version is how a new sound reaches players now, not `StampFormat`); reaches players with the next release, whose first start carries over the old stamp (nothing for format 7, the sounds alone, ~2 s, for 3-6). Before that, (2026-10-28, fourth batch, deployed into the user's `dist`: DLL): user test of own chat lines read back in every channel; the Huntress skills' missing descriptions in the engrave list need the tester's log (no uncut skill gem on the user's characters to reproduce; listing texts are now filtered only when they lie in sibling rows of the focused list, so a details panel beside a grid is never dropped). Before that, (2026-10-28, third batch, deployed into the user's `dist` in place: DLL, translations): user test of the trade market
listings, the new dodge warning (blade, gong, bass drop) against the radar, "Other players' sounds" off, and the GameHelper window gone from
Alt+Tab; the updater works only from the next release on (its package carries `app/version.txt`). Before that,
(2026-10-28, second batch, deployed into the user's `dist` in place: DLL, translations): user test of the move
notices as deeds (sold, bought, stash, dropped outside town, used), messages held against the cursor, speech stopping
when a panel or NPC closes back to play, and the tab said on LB/RB with the cursor in the inventory. Before that,
(2026-10-28, deployed into the user's `dist` in place: DLL, translations, `sounds/corpse.wav`): user test of every
chest and body sounding at once (bodies with their own rummage: the Ogham village's victims), the trade invitation message
heard whole while in a party, the Quest category without places or tile notes, and the fuller dodge warning in a boss fight.
The dev build now deploys `sounds/loops/` and `sounds/breakables/` too (only top-level and `items/` were copied before). Live
check 2026-10-28 00:35-00:39 (dev host, yarkey in Pueblo de Ogham): sounds load with `corpse`, the impaled victims are
`Impalement01`, the scanner lists exits, checkpoint and "Donde moriste" with nothing false under Quest. yarkey died while the
host was restarted (idle by the checkpoint) and was resurrected there before quitting.
Before (2026-10-27, deployed into the user's `dist` in place: DLL + `sounds/breakables/`): user test of the chat after a
fresh game start (a whisper from a friend read by itself, "De …: …") and of the per-material breakable sounds (all at once,
softer; rocks). Ritual favours window, Act 1 (dump 2026-10-27 22:27, window open): no defer or reroll there. The defer
(`layby_pay_button`), reroll (textless `[…,18,0,2]` / `[…,18,0,12]`) and "ofrecer tributo al rey" (`tribute_button`)
buttons are all hidden; favours offer only "Comprar objeto, X" in their tooltip; the gamepad legend
(GamepadLegendRitualSuspendItem "Reservar objeto", ConfirmPrepaySelection "Confirmar selección") never showed; ClientStrings
has an Act 1 help text of its own (RitualRewardWindowHelpPopupAct1), and reroll is "once per area" for a cost
(RitualRewardWindowRerollPopup). The "Modo de reservas" banner `[…,18,0,13]` is flagged visible with a rect, so it is
read on open as the game shows it. To do when a ritual offers deferral (later act or endgame): an F6 dump to check the
defer legend and the reroll button are read. Crossbow loaded ammo (user wish, low priority): not in the UI as text — the
F6 dump taken while loading (2026-10-27 22:53) differs from the 22:27 one only in XP, life/mana and a status counter
under `skill_bar` `[…,19,1]`; slot icons are not probed strings, and no player buff names the ammo. Next step if wanted: a
live trace of the player's skills / stats while loading ammo (GameHelper Actor skills, Stats) to find the loaded type.
Then: user test of the late 2026-10-26 batch, deployed into the user's `dist` in place (DLL, two sounds, extractor DLL):
the Encounters category on a Ritual altar (is the altar listed and named "Altar de ritual, A" when the favours
open?), the breaking sound on rocks and eggs (if real chests break too, `IsLabelVisible` is not the split), a lone item
named again when walked back to, L3 + R3 releasing the guide, two NPC taps, the player double tap and the down call.
Then user test in combat of the 2026-10-26 enemy state sounds and the Primed for Stun wind-up (Warrior with
Destrozahuesos; is it too frequent, are the seven sounds told apart?); then find the Freeze / Electrocution / Pin buildups
with `stuntrace` against a cold or lightning build. Then: user test (DLL and package of 2026-10-26 12:30 in `dist`) of the 2026-10-26 lesson priority (inventory with a pending passive point) and skill details in
the engrave list. Then: user test of local co-op with two real controllers (both toggles, a revive by holding A; the revive and
respawn lines are unit-tested, not heard live). Then: user test of the 2026-10-25 UI sweep fixes (`docs/research/ui-sweep-2026-10-25.md`) and of the player 2 radar
in real couch co-op (two pads; J2 joins at character selection, "Cooperativo local: lectura para el jugador 2" in the mod's settings). Characters: "kehgan" (Monk, level 2, Clearfell Encampment; a town NPC shows a quest marker), "yarkbruja" (Witch, level 3,
Clearfell Encampment, no NPC marker), "yarkey" (Mercenary, level 12, Pueblo de Ogham; the user's current character). Start in town from character selection:
`desktop.py combo l2 cross --seconds 1.8` (away from town the confirmation's Cancel is focused: D-pad right, then A). Next: user test of the 2026-10-25 batch (places and the Lugares category, the other
languages, projectile dodge timing, quieter area loads, the new package layout) and of the 2026-10-24 one (wall tones,
"what A uses", weapon swap, map / chat / player menu). Cinematics and their subtitles (`enable_subtitles=true`) are
untested: voiced NPC lines come as chat lines and are read; a cinematic's text has never been on screen in a test. Places: measure the other tile rotation selectors and place the
rooms of generated areas (`docs/research/places.md`, Open). After a game patch (then; the script is gone since the host lives in `host/`, see `docs/patch-day.md`): `pwsh tools/package/Update-GameHelper.ps1`, then the dev command
`layout` through the screens listed in `docs/compatibility.md` (self-test); no part is left unchecked. Not yet heard
live: the loot filter's quieter settings (kehgan carries no normal item; a white drop with "solo valiosos" set
must stay silent).
