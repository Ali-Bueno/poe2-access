# Skill cooldowns (PoE2 0.5.5e, 2026-10-31)

Read by `SkillCooldownWatch` (constants in `GameLayout.ActorComponent` / `GameLayout.SkillSlot.SkillIdOffset`); dev command
`cooldowns` logs every entry with its skill's name and the named skill ids.

## Layout

- MEASURED: **`Actor+0xB20`** = std::vector of cooldown entries, **0x48 bytes** each (GameHelper `ActorOffsets.CooldownsPtr`,
  `ActiveSkillCooldown`; still right in 0.5.5e, unlike its skill-details name row, docs/research/skill-bar.md).
  - `+0x10` std::vector of the uses now recharging, 16-byte elements: its count is the uses spent.
  - `+0x30` int32 uses the skill holds; `+0x34` int32 cooldown length in ms (research only).
  - `+0x3C` uint32 skill id = **`ActiveSkillDetails+0x40`** of the skill in `Actor+0xB08` (GameHelper
    `UnknownIdAndEquipmentInfo`); every id seen matched one listed skill.
- MEASURED: a skill can be used while uses spent < uses held (GameHelper `CannotBeUsed`). The entry stays when the cooldown
  ends: the uses vector just empties.
- MEASURED: each skill is listed twice, once per weapon set (id ending `…40` and `…41`), each with its own cooldown entry;
  both fill and empty together (Infernal Cry), so the watch says the name once.

## Evidence (dev command `cooldowns`, live)

| Character | Skill | Uses | Cooldown | Ready heard |
|---|---|---|---|---|
| yarkidius (Warrior) | Grito infernal `0x85050040/41` | 1 | 8000 ms | cast 06:56:58.9, "Grito infernal disponible" 06:57:06.7 |
| yarkey (Mercenary) | Granada aturdidora `0x85060040/41` | 1 | 5000 ms | thrown 06:59:42.3, "Granada aturdidora disponible" 06:59:47.3 |
| yarkey (Mercenary) | Granada explosiva `0x85010040/41` | 3 | 5000 ms | not on the bar, not thrown |

Entries exist from the start for skills with a cooldown (0/1 before any use); skills without one (Boneshatter, ammo
skills, Dodge Roll) have none.

## Behaviour

- News when a skill goes from spent (every use recharging) to usable after at least `SkillCooldowns.MinSpentMs`
  (1.5 s): a multi-use skill (Explosive Grenade, 3 uses) is named when one use comes back after all were spent, not
  for each use.
- Hidden parts of a skill also carry names (Infernal Cry's "Combustión", Iron Ward's nova); none had a cooldown entry.
