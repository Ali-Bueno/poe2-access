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
- Hint bar: "Seleccionar, A" on categories, "Comprar, A. Añadir a la lista de deseados, mantener X" on items; A on an
  item opens its page (below), it does not buy yet.
- A card's other lines: `owned_label` "- Ya lo tienes -" (said without the dashes), `variant_count_label` "x3" on an
  item sold in variants (said "3 variantes"), `ShopPurchase.CardLine`. An owned card's hint is "Equipar cosmético [X]"
  whether or not it is equipped; the card draws no equipped mark.
- Cards of the list scrolled into view are not read when they appear (their "¡NUEVO!" flags came 200 ms later and were
  said alone): `UiSemantics.InAnotherShopCard` leaves out what appears in another card than the chosen one.
- The items' list root (`items_root`) holds the font name "Fontin" where panels keep their title; it was said before
  the first item. `UiSemantics.TitleAt` leaves out font faces.

## The item page and the purchase (2026-11-10, 0.5.5e)

The owner bought "Mascota Perro fantasma" (150 of 300 points) with the dev mod running; the tree was read from memory
with the mod's own element fields (children +0x10, flags +0x168, name +0x98, text +0x490), no new field.

- Where: `EshopMainPage` > `subpage_area` > `AllItems` > `EShopDetailPage` > `bottom_page` > `item_info_frame`
  (`GameLayout.InGameUi.ShopItemPageName`), which shows one of `single_page`, `bundle_page`, `variants_page`; all three
  hold the same parts. Only `single_page` was seen shown.
- Parts of the shown page, under `background`: `introduce_panel_top` (`new_banner_label` "¡NUEVO!", `discount_frame`
  with `discount_label` + `off_label` "de descuento", `introduction_title` the item's name, `introduction_desc` its
  text, buttons `more_info_btn` and `item_probabilities_btn`); `detail_seperate_line_top` (the item's card
  `single_item_entry`, built like a list card) > `detail_seperate_line_bottom` > the buy button.
- Buy button `buy_button_bottom` (game focus, role button): `price_frame` > `detail_gold_button_frame_text` "150"
  beside a coin, `origin_price_label` (struck through, hidden without a discount), `confirm_tip`,
  `requires_points_tip` (seen hidden only: its words are unknown) and, as its child, the button below it,
  `equip_button_bottom` "equipar" — so the button's texts read together said "…, equipar".
- Buying: A on the button changes it in place, no dialog: the caption becomes "Confirmar" and `confirm_tip` shows
  "Confirma tu compra de 150 puntos"; A again buys, B closes the page. Then the caption is the price again, both cards
  show "- Ya lo tienes -", a line "Compra exitosa" appears and the points label goes 300 → 150.
- Equip button: A says "Mascota Perro fantasma equipado." (a line on the page); again, "Ya lo tienes equipado".
- The points: `EshopMainPage` > `bar_area` > `topbar_right` > `gold_icon` > `player_mtx_points_label`. It is filled in
  0.6 s after the shop opens and is a bare number, which the screen news leave out: `ShopBalanceWatch` says it when
  shown and when it changes (`ScreenNarrator.CheckShopBalance`).
- What is said (`UiSemantics.ShopBuyButtonOf`, pure `ShopPurchase.Button`): "Comprar Mascota Perro fantasma, 150
  puntos, tienes 300 puntos, botón"; pressed, "Confirmar, Confirma tu compra de 150 puntos, Mascota Perro fantasma,
  tienes 300 puntos, botón". The page's title is the context of its controls and its text and flags are said on entering
  it (`ShopItemPageOf`).
- Open: with the cursor on the page's own card the game marks nothing the mod finds, and the list's card under the page
  is read instead (the same item on a single page; a bundle's contents would be wrong). On entering a category every
  card's name is read once before the highlight is found, without prices ("x3" as "Zoom x3"). A bundle's and a
  variant's page, a discount and the too-few-points line were never shown.
