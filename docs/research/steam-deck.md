# Steam Deck (SteamOS, Proton): what running the mod there would take (2026-11-08)

Web study only; nothing was tried on a Deck. PROVEN = read in a source, UNCERTAIN = inferred or unverified.
Windows handhelds (ROG Ally, Legion Go) run the mod as on a desktop.

| Question | Finding |
|---|---|
| PoE2 on the Deck | PROVEN: listed Playable, later reported Deck Verified ([RPGSite](https://www.rpgsite.net/news/21468-path-of-exile-2-steam-deck-verified-valve-rating), [SteamDeckHQ](https://steamdeckhq.com/path-of-exile-2-gets-steam-deck-verified/)); about 40 fps. |
| Launch options | UNCERTAIN: under Proton `%command%` is `proton waitforexitandrun <game.exe>`, so `"Poe2Access.exe" %command%` would not start; a Linux script has to start the game and run `proton run Poe2Access.exe` in the same prefix (same `STEAM_COMPAT_DATA_PATH`). |
| Reading memory | PROVEN (2010 design): Wine's `ReadProcessMemory` goes to the wineserver, which reads `/proc/<pid>/mem` ([thread](https://www.winehq.org/pipermail/wine-bugs/2010-July/241481.html)). UNCERTAIN: today's path, SteamOS's Yama `ptrace_scope`. Each read is a server round trip, so fewer, larger reads matter even more there. |
| .NET under Wine | UNCERTAIN: no known-issue list for a console-less .NET 10 program; test the named event and mutex, the high-resolution waitable timer (the host falls back to plain waits) and `waveOut`. |
| Speech | PROVEN: Steam's own screen reader speaks only Steam's UI; SteamOS 3.7.10 added Orca and espeak-ng. PRISM has Linux backends (Orca, Speech Dispatcher, Spiel; [README](https://github.com/ethindp/prism)) but only in a Linux build, not inside Wine. Wine has no usable SAPI voice by default. [nvda2speechd](https://sources.debian.org/src/nvda2speechd/0.1-11/README.md) replaces `nvdaControllerClient64.dll` and forwards to speech-dispatcher; PRISM's NVDA backend might use it (untested). |

**Options:** (a) everything inside Proton, a launch script plus a speech bridge (nvda2speechd or a small pipe-to-speech-dispatcher
server): days of work, the likely route; (b) a native Linux build reading the Wine process with `process_vm_readv` and PRISM's
Linux backends: weeks, and ptrace permissions may block it.

**First experiment (about an hour, on a Deck):** a small exe run in the PoE2 prefix that opens `PathOfExileSteam.exe` with
`PROCESS_VM_READ` and times 10,000 small reads. If that works, option (a).
