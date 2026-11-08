# Menu speech rules (inherited from the D2R and LeAccess mods)

These rules came out of real tester reports in `D:\code\c++\diablo 2 access project` (D2R) and
`D:\code\unity and such\LeAccess` (Last Epoch). They apply to every PoE2 screen; a screen-specific
reader exists only for a genuine quirk. Status column: **done** = implemented in this plugin,
**todo** = still to build.

## Pipeline

| Rule | Source | Status |
|---|---|---|
| Classify widgets, never screens: archetype readers (button, check box, slider, combo, text field, list row, grid cell, tab) + a generic tree-walk fallback; nothing ends in silence | D2R generic-strategy | partly (focus archetypes done; lists/grids todo) |
| Read the game's own data (control text, enabled flag, row data), never guess from visuals or sibling order | LeAccess creation/settings | done for flags/text |
| Only the layer that owns input speaks; a popup excludes the screen behind it | both | done (`ScreenSnapshot.TopInteractive`) |
| Never trust open/close events alone: detect who owns input by polling | D2R menus.md | done (`ScreenWatcher`) |
| A change must be seen twice before it is spoken (screens build over frames) | LeAccess | done (`ScreenDiffer`) |
| Fault isolation: one failing reader never stops the others; log every failed resolve once | both | partly |

## What to say

| Situation | Say | Interrupt | Status |
|---|---|---|---|
| New screen / menu | title, then the focused item; never list all items | yes | partly (title todo) |
| Focus moves | name, value/state, role, then position ("3 of 12") last | yes | done except position |
| Value changes in place (slider, toggle, combo) | only the new value | yes | done |
| Tab change | only the tab name | yes | done |
| Tabbed screen opens (options) | title, tab, focus; never the page's labels | yes | done |
| A press the game ignores in silence (character name) | why, once per press | yes | done |
| A long text closes (terms) | the focus underneath again | yes | done |
| An area loads | only the loading screen's own lines; the HUD and chat stay in the tree and are never read | — | done |
| Popup / modal with no focus inside | title + message + controls with state ("create, unavailable") | yes | done |
| Popup closes | the focused item underneath again | yes | done |
| Panel opened with nothing focused | its prompt and legends (bound buttons) | yes | done (legend glyphs todo) |
| Disabled control | append "unavailable"; never skip it | — | done |
| Text appears in the owning layer (description, tooltip, class info) | the new text, after the control | queued; interrupt when the screen has no focus (navigation result) | done |
| Live counters (timers, queue position) | never automatically | — | done (digit-only changes ignored) |
| Text field | label (row label or placeholder), "edit field", contents; echo typed characters | — | done (label; typed or deleted characters echoed by `ScreenNarrator.OnValueChanged`) |
| Password field | never read the contents; say the length | — | done (`UiSemantics`, `Announcements.PasswordLength`; commit 34f9512) |
| Hint bar | spoken on request; with a no-focus popup, name the button bound to each action, not the glyph | — | on request done (glyph names todo) |
| Promo / news text (MOTD, deal of the day) | only on request | — | done (baseline silence) |
| Full-text screens (terms, loading tips, help) | title + summary automatically, full text on request | — | todo (text capped at 1024 chars) |

## Pitfalls already paid for elsewhere

- Internal control ids ("gateway", "motd", "name_textbox") are not labels: use visible captions,
  tooltips, placeholders, the row label, or localised strings (D2R tester news 10-16).
- A late speech from the previous context must not overwrite the new context's title (D2R 10-12c).
- Release the active context when its panel closes, or the next screen stays silent (D2R 10-18b).
- Focus can land ~0.5–1 s after a panel opens: wait for it and speak title + item together (D2R).
- Hidden/preloaded panels must never match as focus (D2R emote wheel, LeAccess hidden templates).
- A dropdown with an inactive option template is still one control (LeAccess settings).
- Persistent HUD prompts are not announcements during gameplay (LeAccess menu-reading audit).
- Screen-reader modifier keys must not knock the game out of controller mode (LeAccess).
- PRISM/NVDA speak can block 300–800 ms: speech runs on its own thread (D2R) — done.
- Every spoken line is logged with its interrupt flag; that log is the test oracle (D2R) — done.

## In game (different from menus)

The HUD, minimap, world labels and chat are always visible and have controls, so "highest layer
with controls" does not find the owning panel there. In game the layers present on arrival are the
base; only panels/popups opened later take the context, and chat is never read automatically
(a chat reader with channel filters is its own feature). Typed panels from GameHelper
(`ImportantUiElements`: ChatParent, LeftPanel, RightPanel, LargeMap, Atlas…) identify them.

A panel in game does not always hold the focus: the engrave confirmation box ("Mejorar. Palma mortal. Confirmar, A"), the
world map and the passive tree between two nodes have none. The world's sounds and news go by `WorldGate`: quiet while there
is a focus or a panel over the game (`ScreenSnapshot.PanelOver`: the top layer with controls that is neither the HUD nor the
tutorial box), back 400 ms after both end. Panels whose controls lack the interactive flag (inventory, NPC options) are
covered by their focus. Local co-op goes by the focus alone: a panel may be the other player's. Each change is logged
("Panel over the game: …"): that line tells a panel from a layer that silences the game by mistake.

News of the quest tracker holds the floor: at an NPC it kept the options silent for seven seconds (Leitis, 2026-11-04, 18:05:51). In a
menu it waits (`QuestTrackerPage`) and is said once the menu closes, the latest page only. Of a page that keeps a quest of the last one
only the rest is said. A page the player turns, known by the number on `page_cycling_legend`, holds nothing: it cuts what is being said
and drops what waited (`SpeechFloor.Take`), or turned pages queue up behind each other (user 2026-11-07).

### Trade windows: disenchanting

"Desencantar objetos" (Una, Zarka: the Y option of their dialogue) is the game's own trade window, with its words: "La oferta
de Una", "Tu oferta", "Aceptar comercio, RB", and in the chat "Intercambio cancelado." / "Intercambio aceptado.". Nothing
to translate: that is how the game calls it.

- The pad's cursor reaches the player's grid and never the other side's, so what the deal gives could not be read.
  `TradeOfferWatch` says it when it changes ("La oferta de Una: Esquirla de transmutación", "La oferta de Una: nada"), one
  poll after the cell's own line, which would cut it. It finds the window by shape from the focused grid: two grids, then
  their two titles. A trade with a player has the same shape, not tried.
- An offered item stays in the inventory until the deal is accepted; then it, the old stack and the new stack change at once.
  `ItemMoves` pairs an arrival with the departure of the same name first, or the stack that grew reads as "Usado" and the
  flask as nothing. What leaves during a trade is "Entregado: …".
- The game's own notices (a chat line with no channel and no speaker, `ChatDiff.IsNotice`) are kept, and a kept line outlives
  the stop of a panel closing: "Intercambio aceptado." arrives some 200 ms before the close is seen.
- With a pad, A on an item marks it where it lies ("Reemplazar, A. Devolver objeto, B"): the cursor inventory stays empty, so
  no "En la mano". Rejected items go back with no message, for a sighted player too: normal items are not disenchanted
  (a Lapis Ring's blue line is its implicit mod). Live 2026-11-06 with three magic flasks bought from Una.
