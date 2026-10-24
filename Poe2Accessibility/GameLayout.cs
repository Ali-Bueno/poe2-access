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
            public const int InteractiveFlagBit = 0x1B;

            /// <summary>
            ///     In game, set on what the controller cursor can land on (in-game controls lack <see cref="InteractiveFlagBit"/>):
            ///     equipment slots, Skills rows, the spirit panel and the slots of the skill bind screen, and on nothing
            ///     else of those panels (bind screen dump, 2026-10-24).
            /// </summary>
            public const int NavigableFlagBit = 0x0E;

            /// <summary>Flags bit set on elements that accept typed text (combo boxes and text fields).</summary>
            public const int TextInputFlagBit = 0x10;

            /// <summary>Panel title ("Inventory", "Skills", "Passives", "Stash"…); the only strings found there in game dumps.</summary>
            public static int PanelTitleOffset { get; internal set; } = 0x2E8;

            /// <summary>
            ///     std::vector of 0x18-byte entries whose first qword is a tooltip root element: its parent is the owner
            ///     but it is not among the owner's children; the game shows the focused element's tooltip (2026-10-23).
            /// </summary>
            public static int TooltipsOffset { get; internal set; } = 0x120;

            public const int TooltipEntrySize = 0x18;

            /// <summary>Every UI element points at its state's UI manager here (2026-10-23).</summary>
            public static int ManagerOffset { get; internal set; } = 0x90;

            /// <summary>Text-field placeholder ("Type your character name here..."), the only label some fields have (2026-10-23).</summary>
            public static int PlaceholderOffset { get; internal set; } = 0x798;

            /// <summary>Set while a control can be used: the terms window's Continue gained it when I Agree was ticked (2026-10-23).</summary>
            public const int EnabledFlagBit = 0x0D;

            /// <summary>Bytes of an element worth scanning for subclass strings (click sounds); labels are 0x700 bytes.</summary>
            public const int ProbeBytes = 0x800;

            /// <summary>
            ///     Check-box subclass: byte that is 1 while ticked, 0 otherwise. Found by diffing two research records of
            ///     the same options-menu check box taken before and after toggling it (2026-09-15 03:53); the two
            ///     bytes after it stayed 1 (other bool properties).
            /// </summary>
            public static int CheckBoxStateOffset { get; internal set; } = 0x60A;
        }

        /// <summary>
        ///     Slider subclass (0x410-byte objects). Validated across the options menu (2026-09-15): Sharpness
        ///     150 in [0,1000] is displayed "15%", Scene Brightness 500 in [0,1000] is "+ 0.0", volumes 100 in [0,100].
        /// </summary>
        /// <summary>
        ///     Image elements: +0x240 points to a shared texture resource whose +0x8 std::wstring is the art key,
        ///     e.g. "Art/.../UIImages/Common/Gamepad/XboxOne/Button4.dds|0,0,58,58|0" (hint-bar glyphs and ~1700
        ///     in-game images, 2026-10-23). Some element classes keep two floats there, hence the key checks.
        /// </summary>
        public static class Image
        {
            public const int TextureOffset = 0x240;

            /// <summary>Same pointer in the smaller image class of the skill bar's button glyphs (TriggerLeft.dds …, 2026-10-23).</summary>
            public const int AltTextureOffset = 0x238;
            public const int TextureKeyOffset = 0x8;
            public const string ArtPrefix = "Art/";
            public const string TextureMarker = ".dds|";

            /// <summary>Currency item icons; a vendor price is this icon beside the amount (2026-10-23).</summary>
            public const string CurrencyArtPrefix = "Art/2DItems/Currency/";

            /// <summary>Gold's icon: "Art/2DItems/Currency/Ruthless/CoinPileTier1.dds" on prices and the inventory's gold counter.</summary>
            public const string GoldArtMarker = "/CoinPile";
        }

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
            public const int MarkerRowOffset = 0x50;

            public const string QuestMarkerId = "NPCQuestMarker";
        }

        /// <summary>Item Base component (GameOffsets BaseOffsets).</summary>
        public static class BaseComponent
        {
            /// <summary>Pointer to the name row.</summary>
            public const int NameRowOffset = 0x10;

            /// <summary>
            ///     std::wstring display name inside the name row. GameHelper reads it as a pointer, which is only right
            ///     for names of 8 characters or more: shorter ones are stored inline ("Exhumar", a gem; 2026-10-24).
            /// </summary>
            public const int NameOffset = 0x30;
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
            public const int IsTargetableOffset = 0x69;
        }

        /// <summary>Entity AreaTransition component (2026-10-23 dump).</summary>
        public static class AreaTransitionComponent
        {
            /// <summary>Destination WorldAreas.dat row: +0 id ("G1_2"), +8 name ("Clearfell").</summary>
            public const int DestinationRowOffset = 0x48;
        }

        /// <summary>Button bits of the pad state (each button held while sampling the field, 2026-10-23).</summary>
        public static class PadButtons
        {
            public const uint DPadUp = 0x1;
            public const uint DPadDown = 0x2;
            public const uint DPadLeft = 0x4;
            public const uint DPadRight = 0x8;
            public const uint Start = 0x10;
            public const uint Back = 0x20;
            public const uint LeftStick = 0x40;
            public const uint RightStick = 0x80;
            public const uint LeftShoulder = 0x100;
            public const uint RightShoulder = 0x200;
            public const uint A = 0x400;
            public const uint B = 0x800;
            public const uint X = 0x1000;
            public const uint Y = 0x2000;
            public const uint LeftTrigger = 0x4000;
            public const uint RightTrigger = 0x8000;
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

            /// <summary>The HUD ("HUD", child 12 of G): quest tracker, flask charges, ground-item tooltips (2026-10-23).</summary>
            public const int HudLayerOffset = 0x558;

            /// <summary>
            ///     The death screen ("death_screen": title_text, respawn buttons), child 24 of G while shown; the same element
            ///     across deaths in an area (dumps and screen logs, 2026-10-24).
            /// </summary>
            public const int DeathScreenOffset = 0xCE8;

            /// <summary>NPC dialogue box (speaker, line, Continue / choices), child 57 of G while open (2026-10-23).</summary>
            public const int NpcDialogueLayerOffset = 0x798;

            /// <summary>The HUD skill bar ("skill_bar"): children 1..12 are the slot frames in screen order (docs/research/skill-bar.md).</summary>
            public const int SkillBarOffset = 0x610;

            public const string SkillBarName = "skill_bar";

            /// <summary>
            ///     The centred line for refusals ("Ya tienes una gema de habilidad engarzada de este tipo."): a label that is
            ///     itself an in-game layer, hidden between messages (hud_console.ui `message_label`, 2026-10-24).
            /// </summary>
            public const string MessageLabelName = "message_label";

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
            public static int NodeRecordOffset { get; internal set; } = 0x2C8;

            /// <summary>Record +0x00: foreign key to the PassiveSkills row.</summary>
            public const int RecordSkillKeyOffset = 0x0;

            public const string SkillTable = "Data/Balance/PassiveSkills.dat";

            /// <summary>Node element → group → canvas; canvas +0x3D8 = the tree state object T.</summary>
            public static int CanvasStateOffset { get; internal set; } = 0x3D8;

            /// <summary>T +0x160 → the graph file (name at +8); file +0x28 → the graph content C.</summary>
            public const int StateGraphFileOffset = 0x160;

            public const string GraphFileName = "Metadata/PassiveSkillGraph.psg";

            public const int GraphContentOffset = 0x28;

            /// <summary>C +0x58: std::vector of edge pointers; edge +0 from record, +8 to record (out-edges only per record).</summary>
            public const int EdgeListOffset = 0x58;

            public const int EdgeToOffset = 0x8;

            /// <summary>T +0x1A8: std::vector of 4-byte entries whose low u16 is an allocated node's graph id.</summary>
            public const int AllocatedOffset = 0x1A8;

            public const int AllocatedEntrySize = 4;

            /// <summary>PassiveSkills row (unaligned): graph id, name, class list, kind flags.</summary>
            public static int RowGraphIdOffset => 0x30 + RowShift;

            /// <summary>The name column as measured; <see cref="RowShift"/> follows it when the dat-columns catalog finds it moved.</summary>
            public const int MeasuredRowNameOffset = 0x32;

            public static int RowNameOffset => MeasuredRowNameOffset + RowShift;

            /// <summary>Row +0x3A: (count, pointer) to 16-byte keys into Characters.dat — set only on class start nodes.</summary>
            public static int RowCharactersOffset => 0x3A + RowShift;

            public static int RowKeystoneOffset => 0x4A + RowShift;

            public static int RowNotableOffset => 0x4B + RowShift;

            /// <summary>"Mastery" decoration: drawn as a large icon, its links are not drawn.</summary>
            public static int RowIconOnlyOffset => 0x54 + RowShift;

            public static int RowJewelSocketOffset => 0x65 + RowShift;

            /// <summary>Bytes of the row read at once (up to the jewel socket flag).</summary>
            public static int RowBytes => 0x70 + RowShift;

            /// <summary>How far the name column moved from where it was measured; the row's other fields move with it.</summary>
            public static int RowShift { get; internal set; }

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
            public static int BindingIndexOffset { get; internal set; } = 0x370;

            public const int ButtonsPerSet = 12;

            /// <summary>On the slot's icon child: the ActiveSkillDetails listed in the player's skills (Actor+0xB08).</summary>
            public static int SkillDetailsOffset { get; internal set; } = 0x2D8;

            /// <summary>A dat foreign key is (row, file); the file's table name is a std::wstring at file+8.</summary>
            public const int KeyFileOffset = 0x8;

            public const int FileNameOffset = 0x8;

            /// <summary>ActiveSkillDetails → GrantedEffectsPerLevel (GameHelper's +0x48 reads ActionTypes in 0.5.5e).</summary>
            public const int LevelKeyOffset = 0x58;

            public const string LevelTable = "Data/Balance/GrantedEffectsPerLevel.dat";

            public const int EffectKeyOffset = 0x0;

            public const string EffectTable = "Data/Balance/GrantedEffects.dat";

            /// <summary>GrantedEffects → ActiveSkills (unaligned).</summary>
            public const int SkillKeyOffset = 0x4F;

            public const string SkillTable = "Data/Balance/ActiveSkills.dat";

            /// <summary>ActiveSkills row: pointer to the NUL-terminated UTF-16 displayed name ("Saeta básica").</summary>
            public static int DisplayedNameOffset { get; internal set; } = 0x8;
        }

        /// <summary>
        ///     Selectable list widget that keeps the focus while its selection moves (character selection list):
        ///     int32 index of the selected entry among the entry holder's children (0 → 1 when moving from
        ///     "Create Character" to the character, 2026-10-23).
        /// </summary>
        /// <summary>
        ///     Tab button (FourTabButton click sound): byte set on the selected tab (options tabs UI → Sound; vendor stock
        ///     pages and stash tabs, 2026-10-23).
        /// </summary>
        public static class TabButton
        {
            public static int SelectedOffset { get; internal set; } = 0x391;

            /// <summary>Layout id of the child holding a tab container's pages (vendor TabContainer, stash top_level_tabs).</summary>
            public const string PagesContainerName = "pages_container";
        }

        public static class SelectableList
        {
            public static int SelectedIndexOffset { get; internal set; } = 0x388;
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
        ///     Character creation's class selector ("ClassVariationSelector"): no focus; the left stick moves a highlight
        ///     along the first row of its icon grid and wraps (live 2026-10-23: Exploradora 0, Guerrero 1, Druida 2,
        ///     Bruja 3, Mercenario 4, Monje 5; the class id beside it: 2, 6, 11, 1, 9, 10).
        /// </summary>
        public static class ClassSelector
        {
            public const string Name = "ClassVariationSelector";
            public static int ClassIdOffset { get; internal set; } = 0x390;
            public static int IndexOffset { get; internal set; } = 0x394;
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

        /// <summary>How far into a state object the UI-manager pointer is looked for (LoginState's is at 0x2B8).</summary>
        public const int StateObjectScanBytes = 0x2000;

        /// <summary>
        ///     Global UI manager object referenced by the game states. Found by diffing login-screen dumps
        ///     while the joystick moved, then confirmed by what the game actually activated: the control
        ///     that gets activated is the one at <see cref="CurrentOffset"/>; when that slot is empty (right
        ///     after the screen opens) the game activates the one at <see cref="LastFocusedOffset"/>.
        /// </summary>
        /// <summary>
        ///     UI manager: one per state that owns UI (pre-game it sits inside a larger object at +0x240, which is
        ///     why older notes say +0x8C8/+0x988/+0x990). Validated on login, inventory, skills and game menu
        ///     (docs/research/ui-layers.md).
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
        }
    }
}
