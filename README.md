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
memory, read-only, through [GameHelper2](https://github.com/Gordin/GameHelper2): it never writes to the game,
injects code or touches network traffic. Use it at your own risk.

## Requirements

- Windows 10 or 11, 64-bit.
- Path of Exile 2 on Steam.
- A screen reader. NVDA is recommended; without one, the Windows voice is used.
- An Xbox or PlayStation controller: the mod is built for playing with a controller.

Nothing else needs installing: everything is inside the folder.

## Getting started

1. Unzip the folder anywhere. If Windows says it protected your PC when you open the mod, choose "More info", then
   "Run anyway" (or, before unzipping, open the downloaded file's Properties, tick "Unblock" and accept).
2. Start Steam.
3. Run `Poe2Access.exe`. It starts the game if it is not running, then the mod. The first time, and after each game
   update, it prepares the sounds and data from your own installation: about a minute and a half of silence.
4. You hear that the mod is ready, then that the game was detected.
5. At the game's title screen, press a button on the **controller**, not the mouse or keyboard: the game picks the
   control method then.

From then on `Poe2Access.exe` looks for a newer version when it starts and asks before installing it, keeping your
settings (the setting "Check for updates at start" turns the question off; `Poe2Access.exe update` looks now).
The mod closes with the game; `Stop the mod.bat` stops it too. It speaks the language the game is set to (all ten
of the game's languages). The full instructions are in `README.txt`, inside the download.

## What it makes accessible

- **Menus and screens:** menus, the inventory, item tooltips, skill gems, the passive tree, quests and their
  rewards, NPC dialogue, chat and the mod's own settings.
- **Finding your way:** a radar for walls, open floor and exits; a scanner for the quest, exits, places, waypoints,
  characters, enemies, items and chests; and a guide ping that leads you along a free path and tells you the turns.
- **Combat:** positional sounds for enemies, warnings when a rare monster or a boss starts an attack, the boss's
  life and weak point, life, mana, flasks and status effects, and a sound for enemy states that skills pay off on
  (primed for stun, heavy stunned, frozen, electrocuted, pinned, armour broken, dazed, parried, culling strike).
- **Local co-op:** two controllers on one PC, with speech for either player or both.

Every control: [docs/controls.md](docs/controls.md). The mod's L3 menu (hold L3 for a second and a half) has the
help, every setting and a glossary to listen to each sound.

## Reporting a problem

Send these two files, with what you were doing and at what time:

- `app\Poe2Access.log`
- `app\Plugins\Poe2Accessibility\logs\accessibility.log`

## Credits

- [GameHelper2](https://github.com/Gordin/GameHelper2) (GPLv3), the overlay the mod runs in, shipped unmodified.
- [PRISM](https://github.com/ethindp/prism), the screen reader output.

## For developers

- Build: `dotnet build Poe2Accessibility -c Release`; tests: `dotnet run --project tests/Poe2Accessibility.Tests -c Release`.
- Test package: `tools/package/New-TestPackage.ps1`. Release (builds it and makes it the latest download):
  `tools/package/Publish-Release.ps1`.
- Where the project stands and what was learned about the game: [STATUS.md](STATUS.md). The accessibility playbook
  and engineering rules this mod follows: [CLAUDE.md](CLAUDE.md), [PRINCIPLES.md](PRINCIPLES.md) and
  [reference/](reference/README.md).
