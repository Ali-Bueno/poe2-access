# Local computer use for PoE2 (dev only)

Lets an assistant or developer drive the game with a **virtual DualShock 4**, read what the plugin
said, and compare it with a screenshot — the test loop for menus without the user. Adapted from the
D2R mod's tool. Nothing here ships with the plugin; the plugin itself never sends input.

Requirements: Python 3.13, ViGEmBus (installed), `py -m pip install vgamepad` (bundles ViGEmClient.dll).

## Session

```powershell
py -B tools/computer_use/pad.py serve --xbox   # background: plugs one virtual pad in for the session
py -B tools/computer_use/launch.py all         # starts PoE2 via Steam if needed, (re)starts the host
py -B tools/computer_use/desktop.py list       # game PID
py -B tools/computer_use/desktop.py --pid N focus
py -B tools/computer_use/desktop.py --pid N --wait 0.8 pad dpad-down    # press, wait, print new plugin log lines
py -B tools/computer_use/desktop.py --pid N --wait 1 stick left right
py -B tools/computer_use/desktop.py --pid N capture                     # half-size PNG (--scale 1|2|4)
py -B tools/computer_use/pad.py quit
```

Couch co-op: `pad.py serve --xbox --second` plugs a second pad on its own pipe; `desktop.py --second …` drives it and
`pad.py quit --second` unplugs it (in a co-op game that disconnects both players). Joining is at character selection
([local co-op](../../docs/research/local-coop.md)).

Plugin rebuild loop: `launch.py stop` → `dotnet build Poe2Accessibility -c Release` → `launch.py host`
(the mod locks the plugin DLL; `launch.py` drives the development `Poe2Access.exe` in `host/Poe2Access/bin/Release/net10.0-windows/win-x64`:
`restart` starts it detached and returns once the plugin attached, `stop` runs `Poe2Access.exe stop`; it runs unelevated — it
only needs read access).

Dev commands for a patch day: `layout`, `structs` (the host's struct sweep), `sounds` (what played since last asked and the
output peak: the only proof of audio). A panel opened by the radial: `chord share right up|up-right|right|down-right|down|down-left|left|up-left --seconds 0.8 --select`
= inventory, cosmetics, skills, not available, bind screen, character, passives, quests. RT fires a crossbow's loaded ammo (X, Y… only load it).

Every action prints JSON with the plugin log lines written since it started (`Speech interrupts|queued`,
`Focus ->`, `Screen <change>`); that log is the test oracle. Research commands go through
`Plugins/Poe2Accessibility/debug/command.txt` (`dump`, `dump visible`, `hints`, `status`, `repeat`;
ResearchMode only) so no key press knocks the game out of controller mode.

`fight_trace.py` records each monster's action, clip and place and the player's life and place (read-only, JSON lines);
`dodge_audit.py` sets it against the plugin log's `Dodge warning` lines (VerboseLogging) and prints the dodge warning's recall,
false warnings and timing ([dodge timing](../../docs/research/dodge-timing.md#live-protocol)).

`peek.py` reads game memory (read-only) for offset research: `ptr`, `wstr`, `hex`, `find` on chains
like `31A928F1010+988`.

## Controls

`pad NAME`: cross, circle, square, triangle, l1, r1, l2, r2, l3, r3, options, share, ps, touchpad,
dpad-up/down/left/right (hold `--seconds`, ≤ 2 s). `stick left|right up|down|left|right`.
`combo HELD PRESSED [--seconds S]` holds one control and presses another meanwhile (`combo l2 cross --seconds 1.8`
starts the game in town from character selection: the game wants A held; a character away from town gets a
confirmation whose Cancel is focused).
`chord BUTTON left|right DIRECTION [--seconds S] [--select]` holds a button and pushes a stick meanwhile; `--select`
lets go of the button while the stick still points, which opens what the radial menu points at
(`chord share right up-right --seconds 0.8 --select` opens Cosmetics). Changing `pad.py` needs `pad.py quit` and a
new `serve`, which disconnects the game to the login screen.
`type TEXT` types into a focused field (only the length is logged — safe for passwords).
`key`, `click`, `move`, `scroll` exist but a key press switches the game to keyboard mode.

## Pitfalls seen

- Every action requires the game in the foreground; `focus` taps Alt first so Windows allows the switch.
  If the user touches another window, actions stop with "lost focus" (nothing is sent).
- While the virtual pad is plugged in it is player 1 and the user's own controller gets no response: run
  `pad.py quit` before handing the game back (2026-10-23).
- The game shows Xbox glyphs for the virtual DS4 and a "P2 Connect a Controller" prompt while two pads
  are connected (the plugin filters it).
- Text entry: Triangle/Cross opens the field; type; Enter submits (character creation submitted with Enter).
- Character selection shows "J2 Presiona A para unirte" and ignores A (Client.txt: "Trying to show popup without a
  valid player index"). With the Xbox pad (`serve --xbox`) plugged in before the game starts, Enter on the keyboard
  starts the game and the pad drives it; the virtual DS4 never got past that screen (2026-10-24). Entering with the
  mouse binds the keyboard instead, and the input method can only change out of the game.
- **The start that works** (2026-11-05): `pad.py serve --xbox` running before the game starts, then at login one `pad cross`
  (it says "Método de entrada configurado para Control/Mando" and does not log in) and `key enter`: the last character
  enters and the pad is player 1, stick and menus. Enter alone gives a keyboard HUD the pad cannot reach, where a click on
  the ground does not walk either; a pad press at selection after Enter joins as J2 ("Modo cooperativo local activado",
  B leaves it). `stick left vec:X,Y --seconds S` walks in any screen direction, up to 2 s a call.
- With the DualSense paired, that `pad cross` logs in straight away (2026-11-05): then one `key enter` at selection and no pad
  input until the area has loaded, or the pad joins as J2. Check it walks: an unchanged `cell=` in the `beacon` command means it does not.
- A second `key enter` at character selection also entered the game, but then A at the Transportador only cast a skill and
  no map opened (2026-11-05): after the login, check the pad before relying on it.
- A start from Steam with a newer release waits on two launcher dialogs (install it, then "updated"): `Get-Process Poe2Access` shows a window title while one is open, and the log stops at "Installed …, latest …".
- Create Character and the characters want A held (`pad cross --seconds 1.6`); a short press does nothing.
- LB at login opens the patch notes in the browser as a child of the game: after the game closes Steam still says "App
  Running" and a new launch fails (AppError_16 in `logs/console_log.txt`) until that browser closes (2026-11-06).
- Exit at login quits at once. Never test names that could be free: Enter or A on Continue creates the character; an
  existing one ("yarkey") tests the whole refusal safely.
- Never stand still among monsters reading logs: on 2026-11-06 a level 19 Mercenary died in a level 6 area that way.
- Scripted walking on the live servers: packs kill a character that only walks, and one session ended in "Abnormal
  disconnect" 40 s after a death with no reason given (Client.txt, 2026-11-05). Cause unknown; keep it short.
- The pad server stopped by itself on 2026-11-08 while a scripted walker that drove `desktop.py` was killed mid-command; the
  game then showed "Conecta un mando o presiona A…". A new `pad.py serve --xbox` and one `pad cross` resumed play (cause not
  confirmed; stop a script that sends input before killing it).
- ffmpeg's `gdigrab` records the DX12 game window black; `-f lavfi -i ddagrab=output_idx=0` (Desktop Duplication) works.
- Steam's launch options start the `dist` host: `dist/Poe2Access/Poe2Access.exe stop`, then `launch.py host`, to test the build output.
- `--wait` accepts at most 5 seconds: a larger value fails before sending anything (filtered output hides the error).
- Escape on the login screen quits the game; the host closes with it, so start both again with
  `launch.py all`.
- Stopping the pad server unplugs the virtual pad: in game that is a disconnect ("tu dispositivo de control se ha
  desconectado") back to the login screen.
- A fresh first start (no `poe2_production_Config.ini`) is tested by moving that file aside with the game closed and
  putting it back afterwards; Steam logs in without a form, the game starts in English.
