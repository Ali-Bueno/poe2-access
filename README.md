# Path of Exile 2 Access

An unofficial accessibility mod that makes Path of Exile 2 playable for blind players. It reads the game aloud
through your screen reader and adds audio cues for what a sighted player sees on screen. It never presses a key,
moves the character or plays for you: every action is your own.

## Download

**[Download the latest version (Poe2Access.zip)](https://github.com/Ali-Bueno/poe2-access/releases/latest/download/Poe2Access.zip)**,
or the smaller [Poe2Access.7z](https://github.com/Ali-Bueno/poe2-access/releases/latest/download/Poe2Access.7z)
(Windows 11 opens it; Windows 10 needs 7-Zip or WinRAR). These links always point to the newest version. Older
versions and their changes: [Releases](https://github.com/Ali-Bueno/poe2-access/releases).

## Warning

This is a test version and it can fail. It is not made or approved by Grinding Gear Games. It reads the game's
memory, read-only, from its own single program, `Poe2Access.exe` (its memory reading is built on
[GameHelper2](https://github.com/Gordin/GameHelper2)'s code): it never writes to the game, injects code or touches
network traffic. Use it at your own risk.

## Requirements

- Windows 10 or 11, 64-bit.
- Path of Exile 2 on Steam, or the Korean client (Kakao Games).
- A screen reader. NVDA is recommended; without one, the Windows voice is used.
- An Xbox or PlayStation controller: the mod is built for playing with a controller.

Nothing else needs installing: everything is inside the folder.

Linux and Steam Deck (Proton): unzip the mod in your home folder and run `sh install-linux.sh` in it (with Steam
closed). It checks speech (Speech Dispatcher, or Orca) and sets the game's launch option to
`sh "<mod folder>/poe2access-proton.sh" %command%`; then the mod starts with the game from Steam's Play button, speaks
through Speech Dispatcher or Orca, and asks its questions in zenity windows that Orca reads. `sh poe2access-stop.sh`
stops it. Speech, memory reading and dialogs are checked under Proton; a full game session is still to be tried. The
details are in [docs/proton.md](docs/proton.md).

## Getting started

1. Unzip the folder anywhere. If Windows says it protected your PC when you open the mod, choose "More info", then
   "Run anyway" (or, before unzipping, open the downloaded file's Properties, tick "Unblock" and accept).
2. Start Steam. With the Korean client, `Poe2Access.exe` asks you instead to start the game from the Kakao Games
   website (it needs your login there), then press OK; and it asks to run as administrator (the Windows prompt): the
   Korean client lets only an administrator read it. With both clients installed, it starts the Steam one; to play the
   Korean one, start it first.
   If Steam (or the game) runs as administrator, the game runs as administrator too, and `Poe2Access.exe` asks to run
   as administrator as well (the Windows prompt), since only an administrator can read it; accept it, or run Steam
   without administrator rights.
3. Run `Poe2Access.exe`. It starts the game if it is not running, then the mod. The first time, and after each game
   update, it prepares the sounds and data from your own installation, and says so ("Updating the game data after
   the patch", then "Game data ready"): about a minute the first time, less after a game update. A new
   version of the mod keeps them (it redoes only what it changed, in seconds) when it updates itself or is unzipped over
   the old folder; a new, empty folder prepares them again.
4. You hear that the mod is ready, then that the game was detected.
5. At the game's title screen, press a button on the **controller**, not the mouse or keyboard: the game picks the
   control method then.

To start the game and the mod with Steam's Play button, open the game's Properties in Steam and, under General,
write this in Launch Options, with the path of your own copy:

```
"C:\Games\Poe2Access\Poe2Access.exe" %command%
```

In File Explorer, Ctrl+Shift+C on `Poe2Access.exe` copies its path with the quotes. Running `Poe2Access.exe` yourself
keeps working as before.

From then on `Poe2Access.exe` looks for a newer version when it starts and asks before installing it, keeping your
settings (the setting "Check for updates at start" turns the question off; `Poe2Access.exe update` looks now).
The mod closes with the game; `Stop the mod.bat` stops it too. It speaks the language the game is set to (all ten
of the game's languages). The full instructions are in `README.txt`, inside the download.

## What it makes accessible

- **Menus and screens:** menus, the inventory, item tooltips, skill gems, the passive tree with a finder for keystones and notables, quests and their
  rewards, NPC dialogue, chat and the mod's own settings.
- **Finding your way:** a radar for walls, open floor and exits; a scanner for the quest, exits, places, waypoints,
  characters, enemies, items and chests; and a guide ping that leads you along a free path and tells you the turns.
- **Combat:** positional sounds for enemies; a warning when a rare monster or a boss attacks you, timed by the
  attack's own animation and only when it is aimed at you, within reach and on screen; the boss's life and weak
  point; life, mana, flasks and status effects; skills ready again after their cooldown; your class's resources
  (charges, Heat and the like); and a sound for enemy states that skills pay off on (primed for stun, heavy stunned,
  frozen, electrocuted, pinned, armour broken, dazed, parried, culling strike).
- **Boss fights:** what a boss is doing that you cannot see: when it vanishes and comes back, turns invulnerable,
  changes phase or enrages, and the way out of its danger areas as it winds up ("Out!", "To the centre!",
  "Behind it!", "Move!", or a stick direction). The bosses and their mechanics come from the game's own files, so new
  bosses are covered without a mod update.
- **Game updates:** after a game patch the mod finds its way into the game's memory again by itself and rebuilds its
  data from your installation; anything it cannot find is said once, with what stops working.
- **Item sounds:** the game's own sounds by default, or the Diablo IV set of the Diablo II Resurrected mod (the
  Sound menu's "Item sound set").
- **Local co-op:** two controllers on one PC, with speech for either player or both.

Every control: [docs/controls.md](docs/controls.md). The mod's L3 menu (hold L3 for a second and a half) has the
help, every setting and a glossary to listen to each sound.

## Reporting a problem

Send these two files, with what you were doing and at what time:

- `app\Poe2Access.log`
- `app\Plugins\Poe2Accessibility\logs\accessibility.log`

## Credits

- [GameHelper2](https://github.com/Gordin/GameHelper2) (GPLv3): the mod's host (`host/`) is built on its memory-reading
  code, with changes that find its offsets again after a game patch.
- [PRISM](https://github.com/ethindp/prism), the screen reader output.

## For developers

- Build and check: `pwsh tools/build.ps1` builds the executable, the plugin (deployed into the executable's build folder,
  `host/Poe2Access/bin/Release/net10.0-windows/win-x64`) and the tests, then runs the tests and the phrases check
  (`-SkipTests`, `-Configuration Debug`). `dotnet build` at the root builds the same, without the checks. Run with
  `Poe2Access.exe` from that folder (`tools/README.md`).
- Test package: `pwsh tools/package/New-TestPackage.ps1`. Release (builds, checks and packages it, then makes it the latest
  download): `pwsh tools/package/Publish-Release.ps1`, first with `-DryRun`.
- Where the project stands and what was learned about the game: [STATUS.md](STATUS.md). The accessibility playbook
  and engineering rules this mod follows: [CLAUDE.md](CLAUDE.md), [PRINCIPLES.md](PRINCIPLES.md) and
  [reference/](reference/README.md).
