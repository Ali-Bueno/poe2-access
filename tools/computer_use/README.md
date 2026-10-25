# Local computer use for PoE2 (dev only)

Lets an assistant or developer drive the game with a **virtual DualShock 4**, read what the plugin
said, and compare it with a screenshot — the test loop for menus without the user. Adapted from the
D2R mod's tool. Nothing here ships with the plugin; the plugin itself never sends input.

Requirements: Python 3.13, ViGEmBus (installed), `py -m pip install vgamepad` (bundles ViGEmClient.dll).

## Session

```powershell
py -B tools/computer_use/pad.py serve --xbox   # background: plugs one virtual pad in for the session
py -B tools/computer_use/launch.py all         # starts PoE2 via Steam if needed, (re)starts GameHelper
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
(GameHelper locks the plugin DLL; `launch.py` drives `Poe2Access.exe restart|stop` from
`tools/Poe2AccessLauncher`, which skips the update check and the "path contains poe" prompt and runs the host
unelevated via `__COMPAT_LAYER=RunAsInvoker` — it only needs read access).

Every action prints JSON with the plugin log lines written since it started (`Speech interrupts|queued`,
`Focus ->`, `Screen <change>`); that log is the test oracle. Research commands go through
`Plugins/Poe2Accessibility/debug/command.txt` (`dump`, `dump visible`, `hints`, `status`, `repeat`;
ResearchMode only) so no key press knocks the game out of controller mode.

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
- `--wait` accepts at most 5 seconds: a larger value fails before sending anything (filtered output hides the error).
- Escape on the login screen quits the game; GameHelper closes with it (`CloseWhenGameExit`), so start both again with
  `launch.py all`.
- Stopping the pad server unplugs the virtual pad: in game that is a disconnect ("tu dispositivo de control se ha
  desconectado") back to the login screen.
- A fresh first start (no `poe2_production_Config.ini`) is tested by moving that file aside with the game closed and
  putting it back afterwards; Steam logs in without a form, the game starts in English.
