PATH OF EXILE 2 ACCESS - TEST VERSION

A test mod that makes Path of Exile 2 playable without seeing the screen. It reads the menus, the inventory, the dialogue and what happens in the game through your screen reader, and adds sounds to find your way: a wall radar, enemies, items, exits and a guide toward whatever you choose.

WARNING
This is an unofficial tool. It reads the game's memory, but it never changes it or presses anything for you. Grinding Gear Games has not approved it: use it at your own risk. It is a test version and it can fail.

WHAT YOU NEED
- Windows 10 or 11, 64-bit.
- Path of Exile 2 on Steam, or the Korean client (Kakao Games). The mod adapts by itself to most game updates. If an update changes something it cannot find, it tells you when it starts ("Compatibility warning: cannot find..."). If that happens, wait for a new version of the mod.
- A screen reader. NVDA is recommended. Without one, the Windows voice is used.
- An Xbox or PlayStation controller. The mod is built for playing with a controller.
Nothing else needs installing: everything is inside this folder. Poe2Access.exe starts the mod; everything else is in the "app" folder and does not need touching.
The mod speaks the language the game is set to (all ten of the game's languages).

GETTING STARTED
1. Unzip the folder anywhere.
   If Windows says "Windows protected your PC" when you open the mod, choose "More info", then "Run anyway". To avoid it, before unzipping, open the Properties of the downloaded file (.zip or .7z), tick "Unblock" and accept.
2. Start Steam.
   With the Korean client (Kakao Games), Poe2Access.exe asks you instead to start the game from the Kakao Games website, because the game needs your login there; start it, then press OK. It also asks to run as administrator (the Windows prompt); accept it: the Korean client lets only an administrator read it, and stopping the mod asks the same. With both clients installed, Poe2Access.exe starts the Steam one; to play the Korean one, start it first, then run Poe2Access.exe.
3. Double-click Poe2Access.exe. It starts the game if it was not running, then the mod. The first time, and after each game update, it prepares the sounds and data from your own installation. It takes about half a minute, and you hear nothing meanwhile; wait. New versions of the mod keep what was prepared (they redo only what they changed, in seconds) when they update themselves or are unzipped over the old folder; a new, empty folder prepares everything again.
4. You will hear "Path of Exile 2 accessibility ready" and, when the mod connects to the game, "Game detected" with the screen you are on.
5. At the game's title screen, press a button on the CONTROLLER, not the mouse or keyboard. The game picks the control method at that moment, and if you go in with the mouse, it ignores the controller until you return to the main menu.

BASIC CONTROLS
The mod button is L3, the left stick click.
- In menus: tap L3 for the next line of what has focus, double-tap for the previous one, and hold it to read the whole description.
- While playing: tap L3 to hear where the scanner's choice is. Double-tap for life, mana, status effects, the boss's life, your level, the area level (its monsters' level), your gold and how much you have explored. Entering an area says its level: "Area: Clearfell, level 2". In the scanner, other players carry their level. Hold it to read the screen and the skill bar.
- Scanner: hold L3 and move the right stick. Up and down change the category (quest, exits, places such as bridges, stairs or carts, unexplored areas, waypoints and checkpoints, characters, enemies, items, chests...). Left and right go through it, from nearest to farthest.
- Guide: hold L3 and click the right stick. A ping leads you toward the chosen thing along a free path and tells you the turns. Do it again to turn it off.
- Mod settings: hold L3 for a second and a half. There you find the help, every setting and a glossary where you can listen to each sound.
- When you die, Y shows or hides the death screen. A revives you at the checkpoint.
- Local co-op (two controllers on the same PC): at the end of the mod's settings, "Local co-op: read for player 1" (on) and "read for player 2" (off) choose who the mod reads for: one, the other or both. With both, the mod reads the menus and commands of whoever pressed last, each player hears their own life, level and status effects with their name first, and player 2 has their own radar with sounds of its own: plucked strings for walls, obstacles and exits, and a whistle for the free path. If a player goes down, the other revives them by holding A next to their body; the mod says who went down.
You will find every control in "Controls.md".

TO CLOSE IT
The mod closes by itself when you quit the game. You can also use "Stop the mod.bat".

UPDATES
When it starts, Poe2Access.exe looks for a newer version and asks before installing it. Answer Yes and wait about a minute in silence: a message says when it is done, and the mod starts. Your settings are kept. To stop the question, turn off "Check for updates at start" at the end of the mod's settings; to look for one yourself, run "Poe2Access.exe update" (for example from a shortcut).

IF SOMETHING FAILS
Send these two files to whoever gave you the mod:
- app\Poe2Access.log
- app\Plugins\Poe2Accessibility\logs\accessibility.log
If you can, also say what you were doing and at what time.
Newest version and source code: https://github.com/Ali-Bueno/poe2-access

CREDITS
- GameHelper2 (GPLv3 license): https://github.com/Gordin/GameHelper2
- PRISM, for the screen reader output: https://github.com/ethindp/prism
