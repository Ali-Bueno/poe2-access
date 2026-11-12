# Rules for code agents — Path of Exile 2 accessibility plugin

1. Read the host's reader (`host/GameReader`, GameHelper2's memory model in the mod's own host) before implementing duplicate memory readers.
2. Do not use WriteProcessMemory.
3. Do not add byte patches or cheat functionality.
4. Do not implement auto-movement, auto-combat, auto-loot or auto-target.
5. Keep accessibility logic separated from memory-layout code.
6. Prefer host events/caches over polling memory every frame.
7. Every new memory field must document source struct, offset provenance and validation method.
8. Never guess an offset.
9. Validate pointers/addresses before dereferencing.
10. Expect area changes to invalidate entity/component addresses.
11. Avoid TTS spam; speech must be event/change driven.
12. Cache entity snapshots and diff them instead of announcing every frame.
13. Terrain radar must aggregate cells before producing audio.
14. Keep all player actions manual.
15. Commit each successful research experiment separately.
16. Write findings to `docs/research/*.md`.
17. Preserve raw evidence/logs for uncertain structures (`Poe2Accessibility/debug/`, `logs/`).
18. If the game patches and a read breaks, stop and validate offsets rather than adding hacks.
19. Screen-reader output goes through PRISM (the mod's own binding, `PrismLibrary`, of the shipped `third_party/prism`
    release) behind `IAccessibilityOutputService` only.

## Working with the user

20. Project knowledge and the user's working preferences live in the repository (this file, `STATUS.md`, `docs/`),
    never in an assistant's own memory.
21. Unattended tests (the user away): once everything asked has been tested and works, quit the game (game menu, "Salir
    de Path of Exile"), then `py -B tools/computer_use/pad.py quit` and `py -B tools/computer_use/launch.py stop`.
22. Leave nothing pending: fix every finding of a test or sweep before reporting, then commit, push and deploy; leave
    something open only when truly blocked, and say why.
23. A new sound needs the user's ear: render candidates with the plugin's own synth code (a scratch project referencing
    the tests project) and publish one Artifact page with a "Reproducir" button per clip, beside the sounds it must not be
    confused with; iterate on the same URL. The user listens remotely on an iPhone; rising tones sound cartoonish to
    them, serious, emergency-like cues suit.
24. Inline Python patch scripts sent through the shell lose doubled backslashes: use raw strings, write the script to a
    file first, or use the editor for lines with escapes.
25. Tests that need combat: do not fight with scripted input (the bot died twice to Rotten Wolves in Clearfell,
    2026-10-23); take a character that survives or avoid the enemies.
26. Never plan a feature on the user's own play (said many times; again on 2026-11-12, about boss mechanics): no "fight
    it first and the log will tell", no learning from how the user plays. What a boss or a mechanic does is found
    beforehand, from the game's data and other sources (docs/research/boss-mechanics.md §12-§13), so the user meets it
    already spoken. A log of the user's session may confirm a finding afterwards; it is never the way to it, and the
    user is not asked to play for it.
