namespace Poe2Accessibility
{
    /// <summary>
    ///     Every memory offset this plugin derived itself, in one place, each with its provenance.
    ///     Offsets that GameHelper2 already publishes are used through its GameOffsets structs instead.
    ///     Validation method for all of them: UI tree dumps of the live game (docs/research/evidence/),
    ///     diffed across screens and across joystick focus moves. PoE2 0.5.5b, 2026-09-14.
    /// </summary>
    public static class GameLayout
    {
        // Properties with a setter are where they were measured until LayoutProbe or InGameFields finds them moved by a
        // game update (docs/compatibility.md); constants are checked only by the readings that use them.
        /// <summary>Fields of a UI element beyond what GameHelper2's UiElementBaseOffset covers.</summary>
        public static class Element
        {
            /// <summary>std::wstring, layout control name ("login_button", "textField"...). 888/4104 nodes on the login dump.</summary>
            public static int NameOffset { get; internal set; } = 0x98;

            /// <summary>std::wstring, font face; present on text-bearing elements only.</summary>
            public static int FontOffset { get; internal set; } = 0xC0;

            /// <summary>std::wstring, display text WITH markup ("save &lt;&lt;xbox_button_x&gt;&gt;"). Label/text-field subclass only (0x700-byte objects, no children).</summary>
            public static int TextWithTagsOffset { get; internal set; } = 0x360;

            /// <summary>std::wstring, display text with markup stripped (tags become spaces). This is what gets spoken.</summary>
            public static int TextOffset { get; internal set; } = 0x490;

            /// <summary>std::wstring, second copy of the stripped text; used as a consistency check.</summary>
            public static int TextCopyOffset { get; internal set; } = 0x538;

            /// <summary>
            ///     Packed ARGB colour of a label's text: an unaffordable vendor price FFE40000, "Buy Item" FFBDBD9A, stat
            ///     lines FFA0A0A0 (four labels of one item tooltip diffed, 2026-10-23).
            /// </summary>
            public static int TextColorOffset { get; internal set; } = 0x6D0;

            /// <summary>Flags bit set on every interactive widget (buttons, combo boxes, text fields) and nothing else.</summary>
            public static int InteractiveFlagBit { get; internal set; } = 0x1B;

            /// <summary>
            ///     In game, set on what the controller cursor can land on (in-game controls lack <see cref="InteractiveFlagBit"/>):
            ///     equipment slots, Skills rows, the spirit panel and the slots of the skill bind screen, and on nothing
            ///     else of those panels (bind screen dump, 2026-10-24).
            /// </summary>
            public static int NavigableFlagBit { get; internal set; } = 0x0E;

            /// <summary>Flags bit set on elements that accept typed text (combo boxes and text fields).</summary>
            public static int TextInputFlagBit { get; internal set; } = 0x10;

            /// <summary>Panel title ("Inventory", "Skills", "Passives", "Stash"…); the only strings found there in game dumps.</summary>
            public const int PanelTitleOffset = 0x2E8;

            /// <summary>
            ///     std::vector of 0x18-byte entries whose first qword is a tooltip root element: its parent is the owner
            ///     but it is not among the owner's children; the game shows the focused element's tooltip (2026-10-23).
            /// </summary>
            public static int TooltipsOffset { get; internal set; } = 0x120;

            public const int TooltipEntrySize = 0x18;

            /// <summary>Every UI element points at its state's UI manager here (2026-10-23).</summary>
            public static int ManagerOffset { get; internal set; } = 0x90;

            /// <summary>Text-field placeholder ("Type your character name here..."), the only label some fields have (2026-10-23).</summary>
            public const int PlaceholderOffset = 0x798;

            /// <summary>Set while a control can be used: the terms window's Continue gained it when I Agree was ticked (2026-10-23).</summary>
            public static int EnabledFlagBit { get; internal set; } = 0x0D;

            /// <summary>Bytes of an element worth scanning for subclass strings (click sounds); labels are 0x700 bytes.</summary>
            public const int ProbeBytes = 0x800;

            /// <summary>
            ///     Check-box subclass: byte that is 1 while ticked, 0 otherwise. Found by diffing two research records of
            ///     the same options-menu check box taken before and after toggling it (2026-09-15 03:53); the two
            ///     bytes after it stayed 1 (other bool properties).
            /// </summary>
            public const int CheckBoxStateOffset = 0x60A;
        }

        /// <summary>
        ///     Image elements: +0x240 points to a shared texture resource whose +0x8 std::wstring is the art key,
        ///     e.g. "Art/.../UIImages/Common/Gamepad/XboxOne/Button4.dds|0,0,58,58|0" (hint-bar glyphs and ~1700
        ///     in-game images, 2026-10-23). Some element classes keep two floats there, hence the key checks.
        /// </summary>
        public static class Image
        {
            public static int TextureOffset { get; internal set; } = 0x240;

            /// <summary>Same pointer in the smaller image class of the skill bar's button glyphs (TriggerLeft.dds …, 2026-10-23).</summary>
            public static int AltTextureOffset { get; internal set; } = 0x238;
            public const int TextureKeyOffset = 0x8;
            public const string ArtPrefix = "Art/";
            public const string TextureMarker = ".dds|";

            /// <summary>Currency item icons; a vendor price is this icon beside the amount (2026-10-23).</summary>
            public const string CurrencyArtPrefix = "Art/2DItems/Currency/";

            /// <summary>
            ///     Quest reward icons, one per kind ("RewardSupportGem", "RewardGold"…; twelve in the 0.5.5e bundles, listed
            ///     with tools/Poe2UiExtract paths).
            /// </summary>
            public const string QuestRewardArtPrefix = "Art/2DItems/QuestItems/Reward";

            /// <summary>Gold's icon: "Art/2DItems/Currency/Ruthless/CoinPileTier1.dds" on prices and the inventory's gold counter.</summary>
            public const string GoldArtMarker = "/CoinPile";
        }

        /// <summary>
        ///     Slider subclass (0x410-byte objects). Validated across the options menu (2026-09-15): Sharpness
        ///     150 in [0,1000] is displayed "15%", Scene Brightness 500 in [0,1000] is "+ 0.0", volumes 100 in [0,100].
        /// </summary>
        public static class Slider
        {
            public const int ValueOffset = 0x37C;
            public const int MaxOffset = 0x388;
            public const int MinOffset = 0x38C;
        }

        /// <summary>
        ///     Combo-box subclass (0x800-byte objects). Validated across 16 options-menu combos (2026-09-15):
        ///     the selected index is always below the entry count (Item Filter 0 of 1, Input Method 2 of 3 =
        ///     controller). Entry layout inside the 16-byte records is probed live (ComboBoxReader).
        /// </summary>
        public static class ComboBox
        {
            /// <summary>std::vector of 16-byte entries (begin/end/capacity), capacities follow MSVC's growth series at this stride.</summary>
            public const int ItemsVectorOffset = 0x688;

            /// <summary>Second, parallel vector with the same element count (contents unknown, probed too).</summary>
            public const int ParallelVectorOffset = 0x6A0;

            public const int EntryStride = 16;

            /// <summary>int64 selected (committed) index; high dword always 0.</summary>
            public const int SelectedIndexOffset = 0x518;

            /// <summary>Second {index, tagged pointer} record right after the first; hypothesis: the entry highlighted while the list is open.</summary>
            public const int HighlightIndexOffset = 0x528;

            /// <summary>Byte, 1 while the combo is open (in-game options open in place: A, the D-pad steps, A or B); VSync and Mode, 2026-10-24.</summary>
            public const int OpenFlagOffset = 0x538;

            public const int MaxEntries = 256;
        }

        /// <summary>NPC component (2026-10-24, Clearfell).</summary>
        public static class NpcComponent
        {
            /// <summary>
            ///     Dat reference {row, file} to the MiscAnimated.dat row of the marker over the NPC's head: row +0 → the
            ///     id "NPCQuestMarker" (Quest_Indicator effect) on RenlyIntro, null on the seven other town NPCs.
            /// </summary>
            public static int MarkerRowOffset { get; internal set; } = 0x50;

            public const string QuestMarkerId = "NPCQuestMarker";
        }

        /// <summary>
        ///     Entity Actor component: heavy stun buildup ("poise" in Stats.dat) is the first of three entries of 0x1C bytes
        ///     from 0x4D8 — {int 5, float 8.0, int 0, int threshold, int buildup, float, float seconds before it decays}. The
        ///     threshold (24 for a 13-life urchin, 40 for a 20-life wolf, 60 for a 36-life hag) and the buildup, which rises
        ///     with every hit, blocked ones too, decays and empties as the heavy stun lands (54 of 60, then a hit: stunned),
        ///     from the stun traces of 2026-10-26.
        /// </summary>
        public static class ActorComponent
        {
            public static int PoiseThresholdOffset { get; internal set; } = 0x4E4;

            public static int PoiseOffset { get; internal set; } = 0x4E8;

            /// <summary>std::vector of the actor's skills, 16-byte entries whose first pointer is an ActiveSkillDetails (GameHelper ActorOffsets.ActiveSkillsPtr).</summary>
            public const int SkillsOffset = 0xB08;

            public const int SkillEntrySize = 0x10;
        }

        /// <summary>Entity Render component.</summary>
        public static class RenderComponent
        {
            /// <summary>std::wstring display name ("Wounded Man"; for the player, the class: "Witch"), 2026-10-23.</summary>
            public static int NameOffset { get; internal set; } = 0x160;
        }

        /// <summary>
        ///     Entity Targetable component. GameHelper's flags (+0x51…) read zero for everything in 0.5.5e; in a
        ///     Clearfell dump (2026-10-23) +0x69 is 1 for what is in play (Renly, Una, stash, waypoint, bench, exit,
        ///     players) and 0 for story variants of the same NPC that are not (HoodedMentorInjured, UnaVisitedAct2).
        /// </summary>
        public static class TargetableComponent
        {
            public static int IsTargetableOffset { get; internal set; } = 0x69;

            /// <summary>
            ///     1 on the one entity A uses now, the one whose label shows the A glyph (GameHelper's
            ///     IsTargettedByPlayer +0x53, moved like the flag above): the waypoint the character stood on, then the
            ///     notice board walked up to (Clearfell Encampment, 2026-10-24).
            /// </summary>
            public static int IsTargetedOffset { get; internal set; } = 0x6B;
        }

        /// <summary>Button bits of the pad state (each button held while sampling the field, 2026-10-23).</summary>
        public static class PadButtons
        {
            /// <summary>The left trigger's bit as measured; the check (PadButtonCheck) judges the order on it.</summary>
            public const int MeasuredLeftTriggerBit = 14;

            /// <summary>How far every bit moved from where it was measured (a renumbering of the buttons moves them all).</summary>
            public static int Shift { get; internal set; }

            public static uint DPadUp => Bit(0);
            public static uint DPadDown => Bit(1);
            public static uint DPadLeft => Bit(2);
            public static uint DPadRight => Bit(3);
            public static uint Start => Bit(4);
            public static uint Back => Bit(5);
            public static uint LeftStick => Bit(6);
            public static uint RightStick => Bit(7);
            public static uint LeftShoulder => Bit(8);
            public static uint RightShoulder => Bit(9);
            public static uint A => Bit(10);
            public static uint B => Bit(11);
            public static uint X => Bit(12);
            public static uint Y => Bit(13);
            public static uint LeftTrigger => Bit(MeasuredLeftTriggerBit);
            public static uint RightTrigger => Bit(15);

            private static uint Bit(int measured) => measured + Shift is >= 0 and < 32 ? 1u << (measured + Shift) : 0;
        }

        /// <summary>
        ///     The quest map (Misiones): "WorldScreen" holds the "map_control" widget, whose first child is the map canvas,
        ///     and the surface/underground toggle, a button known by its click sounds (DepthToggleDown.ogg /
        ///     DepthToggleUp.ogg). The canvas byte is 1 while the surface shows and 0 underground: it flipped with each of
        ///     four presses of the toggle in act 1, nothing else of the panel did (2026-10-25).
        /// </summary>
        public static class WorldMap
        {
            public const string ControlName = "map_control";

            public const string DepthToggleSound = "DepthToggle";

            public const int SurfaceShownOffset = 0x566;
        }

        /// <summary>
        ///     Fields of GameHelper's controller UiRootStruct element (G = InGameState+0x318), whose children are the
        ///     in-game layers; measured 2026-10-23 (docs/research/ui-layers.md).
        /// </summary>
        public static class InGameUi
        {
            /// <summary>The chat layer (holds the chat filter regex); GameHelper zeroes its own ChatParent in controller mode.</summary>
            public const int ChatLayerOffset = 0xA60;

            /// <summary>
            ///     The objective / tutorial box above the skill bar ("Talk to the wounded man", "Press &lt;&lt;A&gt;&gt; to talk
            ///     to non-player characters."), child 14 of G (2026-10-23).
            /// </summary>
            public const int TutorialLayerOffset = 0xDD8;

            /// <summary>
            ///     The inventory panel root (full-screen, unnamed; "panels_parent" below it). Its gold counter — an amount
            ///     text with the coin-pile image as its child — keeps the value while the panel is hidden (2026-10-23).
            /// </summary>
            public const int InventoryPanelOffset = 0x378;

            /// <summary>The inventory panel's container of panels (inventory, character, skills, quests…), each with its title.</summary>
            public const string PanelsParentName = "panels_parent";

            /// <summary>The microtransaction shop's page, one of panels_parent's panels (UI dump 2026-10-29).</summary>
            public const string ShopPageName = "EshopMainPage";

            /// <summary>A shop item's card: "name_label", "price_label" (points) and flags such as "new_flag_label" (¡NUEVO!).</summary>
            public const string ShopEntryName = "EShopItemEntry";

            public const string ShopEntryNameLabel = "name_label";

            /// <summary>A shop category's card ("Pestañas de alijo", "27/27"): its highlight frame's parent is its "item_frame".</summary>
            public const string ShopCategoryName = "eshop_category_item";

            public const string ShopCategoryFrameName = "item_frame";

            /// <summary>A featured banner of the shop's first tab (item_slot > "item" > "item_picture", a downloaded image).</summary>
            public const string ShopBannerName = "item";

            public const string ShopBannerPictureName = "item_picture";

            public const string ShopEntryPriceLabel = "price_label";

            /// <summary>The HUD ("HUD", child 12 of G): quest tracker, flask charges, ground-item tooltips (2026-10-23).</summary>
            public const int HudLayerOffset = 0x558;

            /// <summary>The large map over the world ("map_overlay", child 0 of G; dump 2026-10-24).</summary>
            public const int MapOverlayOffset = 0xB88;

            /// <summary>
            ///     The death screen ("death_screen": title_text, respawn buttons), child 24 of G while shown; the same element
            ///     across deaths in an area (dumps and screen logs, 2026-10-24).
            /// </summary>
            public const int DeathScreenOffset = 0xCE8;

            /// <summary>The HUD skill bar ("skill_bar"): children 1..12 are the slot frames in screen order (docs/research/skill-bar.md).</summary>
            public const int SkillBarOffset = 0x610;

            public const string SkillBarName = "skill_bar";

            /// <summary>
            ///     The HUD's quest tracker: pages under "quests_display_pages_root", each quest a "title_label" and its
            ///     "quest_info" lines, 5 to 8 levels down; only the shown page is drawn (UI dump 2026-10-27 22:53).
            /// </summary>
            public const string QuestPagesName = "quests_display_pages_root";

            public const string QuestTitleName = "title_label";

            public const string QuestLineName = "quest_info";

            public const int QuestLineDepth = 8;

            /// <summary>
            ///     Local co-op: player 2's skill bar this far past player 1's field (+0x7B0 with player 1's at +0x610; both
            ///     named skill_bar, in two per_player_hud; Clearfell, 2026-10-25, docs/research/local-coop.md).
            /// </summary>
            public const int PartnerSkillBarShift = 0x1A0;

            /// <summary>
            ///     The centred line for refusals ("Ya tienes una gema de habilidad engarzada de este tipo."): a label that is
            ///     itself an in-game layer, hidden between messages (hud_console.ui `message_label`, 2026-10-24).
            /// </summary>
            public const string MessageLabelName = "message_label";

            /// <summary>
            ///     The ring a hint-bar icon fills while its button is held: only hold actions carry it ("Comprobar precio" and
            ///     "Enlazar al chat" beside "Tirar" on the same Y, inventory hint bar dump 2026-10-24).
            /// </summary>
            public const string HoldProgressName = "progress_frame";

            /// <summary>The skill picker's panel (A on a Bind Screen button), the control id above its cells (UI dump 2026-10-24).</summary>
            public const string SkillPickerName = "action_binding_display";

            /// <summary>The "Set Skill Bind" screen, kept hidden in game; its bars hold every binding's button glyph.</summary>
            public const int BindDisplayOffset = 0x788;

            public const string BindDisplayName = "bind_management_display";
        }

        /// <summary>
        ///     Passive tree (0.5.5e, measured 2026-10-23, docs/research/passive-tree.md): node element → PSG node record →
        ///     PassiveSkills row; the tree state object holds the graph file and the allocated ids; GameHelper's
        ///     SkillTreeNodeUiElement (+0x2A0) and its child path are stale.
        /// </summary>
        public static class PassiveTree
        {
            /// <summary>On a node element: its PassiveSkillGraph node record.</summary>
            public const int NodeRecordOffset = 0x2C8;

            /// <summary>Record +0x00: foreign key to the PassiveSkills row.</summary>
            public const int RecordSkillKeyOffset = 0x0;

            public const string SkillTable = "Data/Balance/PassiveSkills.dat";

            /// <summary>Node element → group → canvas; canvas +0x3D8 = the tree state object T.</summary>
            public const int CanvasStateOffset = 0x3D8;

            /// <summary>T +0x160 → the graph file (name at +8); file +0x28 → the graph content C.</summary>
            public static int StateGraphFileOffset { get; internal set; } = 0x160;

            public const string GraphFileName = "Metadata/PassiveSkillGraph.psg";

            public static int GraphContentOffset { get; internal set; } = 0x28;

            /// <summary>C +0x58: std::vector of edge pointers; edge +0 from record, +8 to record (out-edges only per record).</summary>
            public static int EdgeListOffset { get; internal set; } = 0x58;

            public const int EdgeToOffset = 0x8;

            /// <summary>T +0x1A8: std::vector of 4-byte entries whose low u16 is an allocated node's graph id.</summary>
            public static int AllocatedOffset { get; internal set; } = 0x1A8;

            public const int AllocatedEntrySize = 4;

            /// <summary>PassiveSkills row (unaligned): u16 graph id; each column is followed by DatColumns.</summary>
            public static int RowGraphIdOffset { get; internal set; } = 0x30;

            public static int RowNameOffset { get; internal set; } = 0x32;

            /// <summary>(count, pointer) to 16-byte keys into Characters.dat — set only on class start nodes.</summary>
            public static int RowCharactersOffset { get; internal set; } = 0x3A;

            public static int RowKeystoneOffset { get; internal set; } = 0x4A;

            public static int RowNotableOffset { get; internal set; } = 0x4B;

            /// <summary>"Mastery" decoration: drawn as a large icon, its links are not drawn.</summary>
            public static int RowIconOnlyOffset { get; internal set; } = 0x54;

            public static int RowJewelSocketOffset { get; internal set; } = 0x65;

            /// <summary>Bytes of the row read at once: up to the last column read.</summary>
            public static int RowBytes => System.Math.Max(System.Math.Max(System.Math.Max(RowNameOffset + 8, RowCharactersOffset + KeySize), RowGraphIdOffset + 2),
                System.Math.Max(System.Math.Max(RowKeystoneOffset, RowNotableOffset), System.Math.Max(RowIconOnlyOffset, RowJewelSocketOffset)) + 1);

            /// <summary>Characters row +0x00: its Id, the player's metadata path ("Metadata/Characters/StrDex/StrDexFourb").</summary>
            public const int CharacterIdOffset = 0x0;

            /// <summary>Characters row +0x08: the class's localized name ("Monje"; dat-strings characters, 2026-10-24).</summary>
            public const int CharacterNameOffset = 0x8;

            public const int KeySize = 0x10;
        }

        /// <summary>
        ///     A skill bar slot element (two per slot frame: primary and secondary set; same class in the bind screen)
        ///     and the dat chain from its skill icon to the skill's localized name; measured on 0.5.5e, 2026-10-23
        ///     (docs/research/skill-bar.md).
        /// </summary>
        public static class SkillSlot
        {
            /// <summary>int32 binding index: 0..11 primary set, 12..23 the same buttons in the secondary set.</summary>
            public const int BindingIndexOffset = 0x370;

            public const int ButtonsPerSet = 12;

            /// <summary>On the slot's icon child: the ActiveSkillDetails listed in the player's skills (Actor+0xB08).</summary>
            public const int SkillDetailsOffset = 0x2D8;

            /// <summary>A dat foreign key is (row, file); the file's table name is a std::wstring at file+8.</summary>
            public const int KeyFileOffset = 0x8;

            public const int FileNameOffset = 0x8;

            /// <summary>ActiveSkillDetails → GrantedEffectsPerLevel (GameHelper's +0x48 reads ActionTypes in 0.5.5e).</summary>
            public const int LevelKeyOffset = 0x58;

            public const string LevelTable = "Data/Balance/GrantedEffectsPerLevel.dat";

            public static int EffectKeyOffset { get; internal set; } = 0x0;

            public const string EffectTable = "Data/Balance/GrantedEffects.dat";

            /// <summary>GrantedEffects row: pointer to the UTF-16 Id, its first column ("ParryPlayer", row 433; dat-strings 2026-10-28).</summary>
            public const int EffectIdOffset = 0x0;

            /// <summary>GrantedEffects → ActiveSkills (unaligned).</summary>
            public static int SkillKeyOffset { get; internal set; } = 0x4F;

            public const string SkillTable = "Data/Balance/ActiveSkills.dat";

            /// <summary>ActiveSkills row: pointer to the NUL-terminated UTF-16 displayed name ("Saeta básica").</summary>
            public static int DisplayedNameOffset { get; internal set; } = 0x8;
        }

        /// <summary>
        ///     Tab button (FourTabButton click sound): byte set on the selected tab (options tabs UI → Sound; vendor stock
        ///     pages and stash tabs, 2026-10-23).
        /// </summary>
        public static class TabButton
        {
            public const int SelectedOffset = 0x391;

            /// <summary>Layout id of the child holding a tab container's pages (vendor TabContainer, stash top_level_tabs).</summary>
            public const string PagesContainerName = "pages_container";
        }

        /// <summary>
        ///     Selectable list widget that keeps the focus while its selection moves (character selection list):
        ///     int32 index of the selected entry among the entry holder's children (0 → 1 when moving from
        ///     "Create Character" to the character, 2026-10-23).
        /// </summary>
        public static class SelectableList
        {
            public const int SelectedIndexOffset = 0x388;
        }

        /// <summary>
        ///     A gamepad button menu that keeps the focus on itself (the menu A opens on another player: invite,
        ///     whisper, trade, add friend, report). int: the chosen button among its visible buttons, 1 → 3 as the
        ///     D-pad moved from "whisper" to "add as friend" (2026-10-24); the buttons' own memory does not change.
        /// </summary>
        public static class ButtonMenu
        {
            public const int SelectedIndexOffset = 0x348;
        }

        /// <summary>
        ///     A section of an options page (a heading over a list of rows; Gráficos has four). The byte is set on the
        ///     page's active section, the qword is that section's highlighted row, disabled rows included, which the game
        ///     highlights but never focuses: Gráficos went 0 → 11 down the first section, the "Pantalla" (1),
        ///     "Resolución de la ventana" (5) and "HDR" (9) rows too, then the flag moved to the next section, whose row
        ///     started at 0 (2026-10-25). Row → list → page → section.
        /// </summary>
        public static class OptionsSection
        {
            public const int ActiveOffset = 0x368;
            public const int RowIndexOffset = 0x370;
        }

        /// <summary>Inventory grid element (focused while the cursor is anywhere in the grid).</summary>
        public static class InventoryGrid
        {
            /// <summary>float: cell size in layout units (80).</summary>
            public const int CellSizeOffset = 0x33C;

            /// <summary>Item element under the cursor; zero on an empty cell.</summary>
            public const int HoveredItemOffset = 0x350;

            /// <summary>Two int32: column, row of the cursor cell (zero-based).</summary>
            public const int CellOffset = 0x358;

            /// <summary>Server inventory object shown by the grid (GameHelper InventoryStruct: TotalBoxes at +0x150); 2026-10-23.</summary>
            public const int ServerInventoryOffset = 0x6F8;
        }

        /// <summary>
        ///     Mods.dat row (the ModsPtr of GameHelper's ModArrayStruct): +0 Id, +0x8 hash, +0xA ModType key, +0x1A
        ///     level, +0x1E four stat keys, +0x5E domain, +0x62 Name — the display name in the client language with
        ///     gender branches ("&lt;if:MS&gt;{de Rabia}&lt;elif:FS&gt;{de Rabia}…"). Row size 0x2B5; read from a live
        ///     table walk on 2026-10-23 (MonsterGainsFrenzyChargeAsKilled1, domain 3).
        /// </summary>
        public static class ModsDat
        {
            public static int NameOffset { get; internal set; } = 0x62;
        }

        /// <summary>
        ///     MinimapIcon component and its MinimapIcons.dat row (docs/research/minimap.md): +0x20 the row (GameHelper reads
        ///     the id at row +0); the row's label at +0x1A ("Corazón de la tumba" on WifeActive, 0.5.5e Spanish table).
        /// </summary>
        public static class MinimapIconRow
        {
            public const int ComponentRowOffset = 0x20;

            /// <summary>Row size of MinimapIcons.datc64 (1049 rows of 0x2A bytes, 0.5.5e); the rows sit back to back in memory.</summary>
            public const int RowSize = 0x2A;

            public static int LabelOffset { get; internal set; } = 0x1A;
        }

        /// <summary>
        ///     The HUD's right-hand column ("tertiary_hud_layout", a direct child of the HUD layer): the league encounter
        ///     tracker ("Encuentro de ritual", "100 Tributo", "0 rituales restantes") above the quest tracker pages
        ///     (UI dump 2026-10-26, Clearfell after a ritual).
        /// </summary>
        public static class EncounterTracker
        {
            public const string ColumnName = "tertiary_hud_layout";

            /// <summary>The quest tracker and its page legend share the column: not the encounter.</summary>
            public static readonly string[] SkippedNames = { "quests_display_pages_root", "page_cycling_legend" };

            /// <summary>The column sits this deep under the HUD layer (1) — a little slack for a game patch.</summary>
            public const int SearchDepth = 3;
        }

        /// <summary>
        ///     Character creation's class selector ("ClassVariationSelector"): no focus; the left stick moves a highlight
        ///     along the first row of its icon grid and wraps (live 2026-10-23: Exploradora 0, Guerrero 1, Druida 2,
        ///     Bruja 3, Mercenario 4, Monje 5; the class id beside it: 2, 6, 11, 1, 9, 10).
        /// </summary>
        public static class ClassSelector
        {
            public const string Name = "ClassVariationSelector";

            /// <summary>The class id (its Characters.dat row) at each place of the selector's first row, as measured (see above).</summary>
            public static readonly int[] FirstRowClassIds = { 2, 6, 11, 1, 9, 10 };
            public const int ClassIdOffset = 0x390;
            public const int IndexOffset = 0x394;
        }

        /// <summary>
        ///     The area's tile grid (GameHelper's TerrainStruct.TileDetailsPtr: TileStructure records, row by row; each
        ///     points at a TgtFileStruct holding the tile's .tdt path), as measured with GameHelper's structs on 0.5.5e
        ///     (Act 1 town, 990 sub-tiles). Read raw: when a patch moves them, PlaceNarrator reads where TileGridSearch found them.
        /// </summary>
        public static class TileGrid
        {
            /// <summary>Record size (one per sub-tile).</summary>
            public const int Stride = 0x38;

            /// <summary>Pointer to the tile file (TileStructure.TgtFilePtr).</summary>
            public const int FileOffset = 0x8;

            /// <summary>std::wstring path in the tile file, after its vtable (TgtFileStruct.TgtPath).</summary>
            public const int PathOffset = 0x8;

            /// <summary>The sub-tile's x within its tile; y at +1, the rotation selector at +2 (tileIdX, tileIdY, RotationSelector).</summary>
            public const int IdsOffset = 0x34;
        }

        /// <summary>Inventory item element (grid child or slot content).</summary>
        public static class InventoryItem
        {
            /// <summary>Item entity (GameHelper ItemStruct); validated by its Metadata/Items path.</summary>
            public const int EntityOffset = 0x4E0;
        }

        public static class AreaLoadingState
        {
            /// <summary>Current WorldArea.dat row ("G1_1", "The Riverbank"); GameHelper's +0xF40 is null in 0.5.5e (2026-10-23).</summary>
            public const int CurrentAreaRowOffset = 0xEA0;
        }

        /// <summary>
        ///     Game-state objects (Core.States.AllStates). Each state points at the global UI manager from its own
        ///     slot; UiManagerLocator finds that slot at runtime by shape, this is only the one seen so far.
        /// </summary>
        public static class InGameState
        {
            /// <summary>Pointer to the in-game UI manager (inventory, panels, HUD focus); measured 2026-10-23.</summary>
            public const int UiManagerOffset = 0x310;
        }

        /// <summary>
        ///     The area's local players (couch co-op): a std::vector of entity pointers inside GameHelper's
        ///     LocalPlayerStruct whose inline storage is GameHelper's LocalPlayerPtr, so entry 0 is GameHelper's player.
        ///     Clearfell with two local players among online ones, 2026-10-25: begin +0x5D0, end +0x5E0, entries the two
        ///     characters' entities (peek.py `find` of both addresses, docs/research/local-coop.md).
        /// </summary>
        public static class LocalPlayers
        {
            /// <summary>The list's begin and end pointers, from the start of GameHelper's LocalPlayerStruct.</summary>
            public const int ListOffset = 0x08;

            /// <summary>Couch co-op seats: character selection shows J1 and J2 (2026-10-25).</summary>
            public const int MaxPlayers = 2;
        }

        /// <summary>How far into a state object the UI-manager pointer is looked for (LoginState's is at 0x2B8).</summary>
        public const int StateObjectScanBytes = 0x2000;

        /// <summary>
        ///     UI manager: one per state that owns UI (pre-game it sits inside a larger object at +0x240, which is
        ///     why older notes say +0x8C8/+0x988/+0x990). Found by diffing login dumps while the joystick moved and
        ///     confirmed by what the game activated (<see cref="CurrentOffset"/>, else <see cref="LastFocusedOffset"/>);
        ///     validated on login, inventory, skills and game menu (docs/research/ui-layers.md).
        /// </summary>
        public static class UiManager
        {
            /// <summary>Pointer to the state's UI root element (parentless).</summary>
            public static int RootOffset { get; internal set; } = 0x688;

            /// <summary>Element under the controller cursor — the activation target; null when nothing has focus.</summary>
            public static int CurrentOffset { get; internal set; } = 0x748;

            /// <summary>Previously focused element (kept, hidden, after a panel closes); lags one step behind.</summary>
            public static int LastFocusedOffset { get; internal set; } = 0x750;

            /// <summary>
            ///     Element under the free cursor while nothing has focus (passive tree: the hovered node, whose tooltip
            ///     the game builds right after; 2026-10-23).
            /// </summary>
            public static int HoveredOffset { get; internal set; } = 0x760;

            /// <summary>
            ///     The pad state the game received this frame: uint32 buttons (<see cref="PadButtons"/>), then floats
            ///     left x, left y, right x, right y (up and right positive), left and right trigger (0..1); +0x784 holds
            ///     the previous frame (virtual DualShock 4 through Steam Input, game shows "Xbox Controller #1",
            ///     2026-10-23).
            /// </summary>
            public static int PadStateOffset { get; internal set; } = 0x768;

            /// <summary>
            ///     Local co-op: player 2's focus, hover and pad state lie this far past player 1's (current +0x988, hovered
            ///     +0x9A0, pad +0x9A8). Measured 2026-10-25: at character selection only +0x9A0 moved with player 2's cursor
            ///     and +0x9AC was 1.0 while player 2's left stick was pushed right; in town +0x9A8 held A (0x400) while player
            ///     2 held A, and player 2's inventory slot was at +0x988 (docs/research/local-coop.md).
            /// </summary>
            public const int PlayerStride = 0x240;
        }
    }
}
