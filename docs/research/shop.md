# Microtransaction shop (2026-10-29, 0.5.5e)

UI dumps of the shop opened from the game menu ("Tienda de microtransacciones"), yarkey with 300 points.

## Where it is
- A panel of the inventory panel root: `panels_parent` > (unnamed, `eshop_message_box`) > `EshopMainPage`
  (`GameLayout.InGameUi.ShopPageName`, `UiSemantics.ShopPage`). Top bar: `topbar_username`, `player_mtx_points_label`
  ("300", read on opening), tab title `label_tab_title` ("Destacados", "Ofertas especiales", "Tienda", "Hermandad"…).
- The game focuses nothing in it (in-game focus M+0x748 stays null): the chosen card shows a child the others hide.
  `FocusTracker` looks for those markers under the shop page only (`InGameHighlightRoot`), searched again every 300 ms
  while none shows (tabs and scrolled entries are rebuilt).

## Cards and their markers
| Page | Card | Marker (visible on the chosen one) | Texts |
|---|---|---|---|
| Featured (first tab) | `item_slot` > `item` | `highlight_picture` (moves between items) | none: a downloaded image, `…/ShopImages/public_news_2026-09-21_CotABox_small.png` |
| Special offers | unnamed card with `name_label` | its last child, unnamed, card-sized | `tips_label`, `count_label` "173/173", `name_label` "Novedades" |
| Shop categories | `eshop_category_item` > `item_frame` | `highlight_rect` | `count_label` "27/27", `name_label` "Pestañas de alijo" |
| Items of a category | `EShopItemEntry` (has the interactive bit, click sound SelectMicroPress) | `highlight` | `name_label`, `price_label` (points) under `buy_button` > `price_bar`, `new_flag_label` "¡NUEVO!", "x14", "¡Ahorras 435 puntos!" |

- Grid: each card in its own cell, cells in rows of three, rows in one list (58 rows for 173 items; the last row's
  spare cell holds no shown card). The position counts shown cards in earlier rows (`ShopEntryPosition`).
- Featured banners are named from the image file (`CardText.ShopImageTitle`): date prefix, size suffix, "client",
  "shop", "assets", "Thumb" and "PoE2" dropped, camel case split ("Early Access Supporter Packs", "Cot A Box" for
  Curse of the Allflame). A on one opens the category it advertises, read as usual.
- Hint bar: "Seleccionar, A" on categories, "Comprar, A. Añadir a la lista de deseados, mantener X" on items. A buy was
  never pressed (the user's points); its confirmation is unread.
- Cards of the list scrolled into view are not read when they appear (their "¡NUEVO!" flags came 200 ms later and were
  said alone): `UiSemantics.InAnotherShopCard` leaves out what appears in another card than the chosen one.
- The items' list root (`items_root`) holds the font name "Fontin" where panels keep their title; it was said before
  the first item. `UiSemantics.TitleAt` leaves out font faces.
