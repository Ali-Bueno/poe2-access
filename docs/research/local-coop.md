# Local (couch) co-op and online parties (PoE2 0.5.5e, 2026-10-25)

Measured live with two virtual Xbox pads (`pad.py serve --xbox` and `pad.py serve --xbox --second`) in the Clearfell
Encampment, a town instance shared with ~10 online players. Code: `LocalPlayers` (+ pure `LocalPlayerList`),
`GameLayout.LocalPlayers`, the partner radar in `Poe2AccessibilityCore.Gameplay`.

## Joining

- In game, the second pad's A does nothing. At character selection it switches the screen to "Modo cooperativo local
  activado" with a J1 and a J2 list; each player picks (hold A); J2 shows "Esperando al otro jugador..." until J1 starts.
- J2 cannot leave alone: "Selección de personajes" from J2's menu warns "Esto echará a los dos jugadores del juego",
  and unplugging J2's pad disconnects both to the login screen ("tu dispositivo de control se ha desconectado").

## Which entity is which

- GameHelper's `AreaInstance.Player` (`LocalPlayerStruct.LocalPlayerPtr`, AreaInstance+0x5B0+0x20) is **player 1**,
  the local client's own character. Every other player, local J2 included, is `EntitySubtypes.PlayerOther`; GameHelper's
  own Radar plugin guesses J2 as the first `PlayerOther` (wrong with online players around).
- The local players are a std::vector of entity pointers inside `LocalPlayerStruct` (+0x08 begin, +0x10 end,
  +0x18 capacity end), its inline storage starting at `LocalPlayerPtr`:

  | | begin | end | capacity | entries |
  |---|---|---|---|---|
  | two players (AreaInstance 0x2A9DF0E3000) | +0x5D0 | +0x5E0 | +0x5E0 | yarkey (id 6458, PlayerSelf), yarkbruja (id 6459) |
  | alone (AreaInstance 0x2AA9EEDC000) | +0x5D0 | +0x5D8 | +0x5E0 | yarkey; +0x5D8 holds stale bytes |

  Found with `peek.py find` of both entity addresses in the AreaInstance (also at +0x5F8/+0x600, a second list of the
  same two, unused). The inline capacity of two matches the two seats. `LocalPlayerList.Second` trusts the list only
  when entry 0 is GameHelper's player and it holds at most two entries.

## What reaches the mod from J2

- J2's buttons are not in the pad state the mod reads (UI manager +0x768): J2's L3 did nothing, J1's L3 answered.
- J2's menu cursor is not the mod's focus (no `Focus ->` while J2 moved in its game menu), but what J2 opens is on the
  shared screen and is read as a screen change ("Menú del juego. Grupo actual. Yarkey. Yarkbruja…").
- OPEN: where J2's focus and pad state live. J2 opens its own panels, so a blind J2 needs them read to equip; the UI
  manager's +0x748 (focus) and +0x768 (pad) are J1's.
- The objective box shows the party's quest state, not J1's alone; it is read as drawn.
- The HUD splits in two `per_player_hud` (HUD child 5, children 0 left = J1, 1 right = J2, `HUD/COOP/HUD_L_Back` and
  `HUD_R_Back`). The layer container's skill-bar field (+0x610) still holds J1's `skill_bar`; J2's is at +0x7B0. A co-op
  bar nests its twelve slot frames (two rows of six) in its first child, each frame a background then the two slots;
  alone, the frames are children 1..12 of the bar with the two slots only (`UiSemantics.SlotElements` takes either).

## Online parties

The mod follows `AreaInstance.Player` everywhere (radar, vitals, deaths, buffs, level, inventory), which is the
client's own character whatever the party order. Other players only appear as the scanner's Players category, the NPC
tap and party chat.
