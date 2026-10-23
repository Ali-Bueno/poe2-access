# Rules for code agents — Path of Exile 2 accessibility plugin

1. Read existing GameHelper2 code (`external/GameHelper2`) before implementing duplicate memory readers.
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
19. Screen-reader output goes through PRISM (Prismatoid binding) behind `IAccessibilityOutputService` only.
