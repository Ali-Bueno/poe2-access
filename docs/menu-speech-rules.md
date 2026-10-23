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
| Tab change | only the tab name | yes | todo |
| Popup / modal with no focus inside | title + message + controls with state ("create, unavailable") | yes | done |
| Popup closes | the focused item underneath again | yes | done |
| Panel opened with nothing focused | its prompt and legends (bound buttons) | yes | done (legend glyphs todo) |
| Disabled control | append "unavailable"; never skip it | — | done |
| Text appears in the owning layer (description, tooltip, class info) | the new text, after the control | queued; interrupt when the screen has no focus (navigation result) | done |
| Live counters (timers, queue position) | never automatically | — | done (digit-only changes ignored) |
| Text field | label (row label or placeholder), "edit field", contents; echo typed characters | — | label done, echo todo |
| Password field | never read the contents; say the length | — | todo |
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
