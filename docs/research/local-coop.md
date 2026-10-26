# Local (couch) co-op and online parties (PoE2 0.5.5e, 2026-10-25)

Measured live with two virtual Xbox pads (`pad.py serve --xbox` and `pad.py serve --xbox --second`) in the Clearfell
Encampment (a town instance shared with ~10 online players) and in Clearfell. Code: `Seats`, `InputSeat`,
`LocalPlayers` (+ pure `LocalPlayerList`), `LocalInventories`, `PlayerAlerts`, `SeatOutput`, `CoopDowns`,
`GameLayout.LocalPlayers`, `GameLayout.UiManager.PlayerStride`, `GameLayout.InGameUi.PartnerSkillBarShift`.

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

## Each player's cursor and controller (UI manager)

Player 2's block of the UI manager lies **0x240** past player 1's (`GameLayout.UiManager.PlayerStride`):

| | player 1 | player 2 | measured |
|---|---|---|---|
| focused element (current) | +0x748 | +0x988 | J2's character list at selection; J2's inventory grid in town |
| hovered element | +0x760 | +0x9A0 | only +0x9A0 changed while J2 moved its character cursor (UI manager 0x27A15991250) |
| pad state (uint buttons, 6 floats) | +0x768 | +0x9A8 | +0x9AC = 1.0 with J2's left stick right; in town +0x9A8 = 0x400 (A) while J2 held A (0x27A0F585A00) |

Diffs of 0x1000 bytes of the UI manager (before and after each input) showed nothing else move with J2's input but a
timer at +0xC00 and J2's own cursor state at +0x9F0 (+0x7B0 for J1). `InputSeat` picks whose block the mod reads:
player 1 unless player 2 is read; player 2 read alone takes over at its first press; both read, the player who pressed
last (a held stick is not a press; a new UI manager is a baseline, else an area load flipped the seat back and forth).

## Each player's inventories (server data)

GameHelper reads `ServerData +0x48` (a vector with one player data per local player) only at entry 0. In co-op it held
two entries, each with 117 inventories at +0x320 (`InventoryArrayStruct`, id + pointer). Entry 1 is J2's: its
MainInventory1 (0x279B6917310) is what J2's inventory panel grid points to at +0x6F8. `LocalInventories.Of(2)` reads it
(entry 0 stays GameHelper's own map). Pick-ups, hand, moves and flasks read the watched player's entry; a host
`Inventory` just made or moved is silent for a second (`InventoryWatch.ReaderSettleMs`: its first reads are empty or
stale, and gave a false "Recogido" when J2's inventory began to be watched).

## HUD

- The HUD splits in two `per_player_hud` (HUD child 5, children 0 left = J1, 1 right = J2, `HUD/COOP/HUD_L_Back` and
  `HUD_R_Back`). The layer container's skill-bar field (+0x610) still holds J1's `skill_bar`; J2's is at +0x7B0
  (`PartnerSkillBarShift` 0x1A0 past J1's field, checked by name and by being another element). A co-op bar nests its
  twelve slot frames (two rows of six) in its first child, each frame a background then the two slots; alone, the
  frames are children 1..12 of the bar with the two slots only (`UiSemantics.SlotElements` takes either).
- J2's inventory is its own layer (right half), with J2's name and level ("yarkbruja, Niv. 3") and the shared gold
  ("113 (compartido)"). What either player opens is on the shared screen and is read as a screen change.
- The objective box shows the party's quest state, not J1's alone; it is read as drawn.

## Deaths (Clearfell, 2026-10-25)

- A local player who dies while the other lives gets **no death screen**: the body carries a world label
  `Metadata/MiscellaneousObjects/ReviveIcon` "acércate para revivir", the chat says "yarkbruja ha muerto.", that side of
  the HUD goes red, and the camera keeps following the living player (the body can be off screen). Walking up gives the
  interact prompt "Revivir, A" and "MANTÉN PRESIONADO A PARA REVIVIR" (taking damage interrupts it).
- Once both are dead, the usual death screen opens ("Has muerto | resucitar en el punto de control"); resurrecting
  loads a new instance with both alive (new entity ids).
- `CoopDowns` says "yarkbruja ha caído. Ve hasta su cuerpo para revivir" (heard live) and "Has caído. El otro jugador
  puede revivirte", and who is back on their feet; a loading screen or another area instance starts it over (a respawn
  was first said as two revivals). `DeathWatch` waits for the death screen only once no local player is left alive.

## Online parties

The mod follows `AreaInstance.Player` everywhere unless player 2 is read alone (`Seats.Followed`), which is the client's
own character whatever the party order. Other players only appear as the scanner's Players category, the NPC tap and
party chat (online, the scanner listed "brunaarcher, Exploradora, nivel 10" in town).
