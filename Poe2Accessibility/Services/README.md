# Services, by folder

Every class is in the namespace `Poe2Accessibility.Services` whatever its folder; the folders are for finding a file.
What each class does is in [`STATUS.md`](../../STATUS.md#code-map). A file linked into another project (the tests, the
executable, the extractor) is named by its path there: move one and `dotnet build` at the root says which link broke.

| Folder | What is in it |
|---|---|
| `Audio` | The mixer, the synthesised cues, the radar and wall sounds, the game's own sounds for objects and items |
| `Combat` | Attacks and dodge warnings, bosses and their mechanics, enemy cues and states, stun, cull, shields, weak points |
| `Coop` | Local co-op: seats, whose pad and inventory, the partner's alerts |
| `Core` | The game's state, memory reading, the log, settings and hotkeys, tick costs |
| `Input` | The pad as the game reads it, the mod button's gestures and commands, button names |
| `Inventory` | Grids and items, moves and pick-ups, trades, gold, search highlights, unmet requirements |
| `Layout` | Patch survival: layout probes and searches, field checks, the compatibility report |
| `Passives` | The passive tree: the stick rule and its check, the finder's map, build plans, minions |
| `Player` | Vitals, flasks, charges, skills and cooldowns, ailments, death, experience, class mechanics |
| `Research` | UI tree dumps, the research session, the dev command file, traces |
| `Scanner` | What is around: the entity catalog, routes and the guide, landmarks, places, the map, exploring, quest targets |
| `Screen` | Screen snapshots and what changed on them, the narrator, chat, the game's news and countdowns |
| `Speech` | The PRISM binding and the one output sink, speech priority, text helpers |
| `Trials` | The Trial of the Sekhemas and of Chaos: rooms, doors, traps, honour |
| `Ui` | Focus and what a control is: roles, labels, values, tooltips, lists, cards, the element reader |
