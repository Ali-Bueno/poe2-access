namespace Poe2Accessibility
{
    using System;
    using System.Linq;
    using GameHelper.RemoteEnums;

    /// <summary>
    ///     Every spoken string lives here (single translatable layer; no text inside services or hooks).
    /// </summary>
    public static class Announcements
    {
        public const string PluginReady = "Path of Exile 2 accessibility ready.";
        public const string GameDetected = "Game detected.";
        public const string UiDumpStarted = "Dumping user interface tree.";
        public const string UiDumpDone = "User interface dump written.";
        public const string UiDumpNoRoot = "No user interface root found for this screen; diagnostic file written.";
        public const string UiDumpFailed = "User interface dump failed.";
        public const string NothingToRepeat = "Nothing to repeat.";
        public const string SettingsReloaded = "Settings reloaded.";
        public const string SettingsInvalid = "Settings file could not be read, keeping current settings.";
        public const string UiDumpBlockedByDiagnostics = "Disable memory diagnostics in Game Helper settings before dumping.";

        public static string HotkeyCollision(string key, string action) =>
            $"Warning: hotkey {key} for {action} is already used by Game Helper. Change it in settings.";

        public static string ForState(GameStateTypes state) => state switch
        {
            GameStateTypes.PreGameState => "Splash screen.",
            GameStateTypes.LoginState => "Login screen.",
            GameStateTypes.WaitingState => "Connecting.",
            GameStateTypes.SelectCharacterState => "Character selection.",
            GameStateTypes.CreateCharacterState => "Character creation.",
            GameStateTypes.DeleteCharacterState => "Delete character.",
            GameStateTypes.ChangePasswordState => "Change password.",
            GameStateTypes.CreditsState => "Credits.",
            GameStateTypes.LoadingState => "Loading.",
            GameStateTypes.AreaLoadingState => "Loading area.",
            GameStateTypes.InGameState => "In game.",
            GameStateTypes.EscapeState => "Game menu.",
            GameStateTypes.GameNotLoaded => "Game closed.",
            _ => "Unknown screen.",
        };

        /// <summary>Fallback when a combo's entry text cannot be decoded yet: position within the list.</summary>
        public static string ComboPosition(int oneBasedIndex, int count) => $"option {oneBasedIndex} of {count}";

        /// <summary>Sliders without an on-screen label: the raw number the game uses, as a percentage when the range is 0 to 100.</summary>
        public static string SliderValue(int value, int min, int max) =>
            min == 0 && max == 100 ? $"{value} percent" : $"{value}";

        public const string Checked = "checked";
        public const string Unchecked = "unchecked";

        public const string NoHints = "No button hints on screen.";

        public const string NothingToRead = "Nothing to read here.";

        public const string NothingFound = "Nothing found here.";

        /// <summary>Life fell below a threshold: "Life 25 percent".</summary>
        public static string LowLife(int percent) => $"Life {percent} percent";

        /// <summary>The left stick's eight directions, by <see cref="Services.StickDirection"/> octant.</summary>
        public static readonly string[] StickDirections = { "right", "up right", "up", "up left", "left", "down left", "down", "down right" };

        public static string ScanCategoryName(Services.ScanCategory category) => category switch
        {
            Services.ScanCategory.Exits => "Exits",
            Services.ScanCategory.Waypoints => "Waypoints",
            Services.ScanCategory.Npcs => "Characters",
            Services.ScanCategory.Enemies => "Enemies",
            Services.ScanCategory.Objects => "Objects",
            Services.ScanCategory.Chests => "Chests and shrines",
            Services.ScanCategory.Items => "Items",
            Services.ScanCategory.Players => "Players",
            _ => category.ToString(),
        };

        /// <summary>
        ///     D2R order: name, distance, direction ("Wounded Man, 9 steps, up left"); with a turn ahead, the steps
        ///     until it and the way it turns ("116 steps, down 3, then left"); "here" when on top of it; "no route"
        ///     when only the straight line is known.
        /// </summary>
        public static string ScanEntry(string name, int steps, string direction, int legSteps, string? then, bool routed)
        {
            if (steps < 1)
            {
                return $"{name}, here";
            }

            var leg = then == null ? direction : $"{direction} {legSteps}, then {then}";
            return $"{name}, {steps} {(steps == 1 ? "step" : "steps")}, {leg}{(routed ? string.Empty : ", no route")}";
        }

        /// <summary>Another player: character name, then class ("Yarky, Witch").</summary>
        public static string OtherPlayer(string name, string characterClass) =>
            characterClass.Length == 0 || characterClass == name ? name : $"{name}, {characterClass}";

        public static string EnemyWithRarity(string name, GameHelper.RemoteEnums.Rarity rarity) => $"{name}, {rarity.ToString().ToLowerInvariant()}";

        /// <summary>A control that is shown but cannot be used yet (locked button).</summary>
        public static string Unavailable(string label) => $"{label}, {UnavailableState}";

        public const string UnavailableState = "unavailable";

        /// <summary>Glyph file (same name in every controller family) → (Xbox name, PlayStation name).</summary>
        private static readonly System.Collections.Generic.Dictionary<string, (string Xbox, string PlayStation)> Buttons = new()
        {
            ["Button4"] = ("A", "Cross"),
            ["Button3"] = ("B", "Circle"),
            ["Button1"] = ("X", "Square"),
            ["Button2"] = ("Y", "Triangle"),
            ["ShoulderButtonLeft"] = ("LB", "L1"),
            ["ShoulderButtonRight"] = ("RB", "R1"),
            ["TriggerLeft"] = ("LT", "L2"),
            ["TriggerRight"] = ("RT", "R2"),
            ["LeftStickButton2"] = ("left stick click", "L3"),
            ["RightStickButton2"] = ("right stick click", "R3"),
            ["LeftStick2"] = ("left stick", "left stick"),
            ["RightStick2"] = ("right stick", "right stick"),
            ["DPad"] = ("D-pad", "D-pad"),
            ["DPadUp"] = ("D-pad up", "D-pad up"),
            ["DPadDown"] = ("D-pad down", "D-pad down"),
            ["DPadLeft"] = ("D-pad left", "D-pad left"),
            ["DPadRight"] = ("D-pad right", "D-pad right"),
            ["DPadUpDown"] = ("D-pad up and down", "D-pad up and down"),
            ["DPadLeftRight"] = ("D-pad left and right", "D-pad left and right"),
            ["MenuButton"] = ("Menu", "Options"),
            ["ViewButton"] = ("View", "Create"),
        };

        /// <summary>Spoken controller button for a glyph file; unknown glyphs keep their file name.</summary>
        public static string ButtonName(string glyphFile, bool playStation) =>
            Buttons.TryGetValue(glyphFile, out var names) ? (playStation ? names.PlayStation : names.Xbox) : glyphFile;

        public const string Empty = "empty";

        public const string EmptyCell = "empty";
        public const string UnknownItem = "unknown item";

        /// <summary>
        ///     Item text and position for a grid cell: equipment slots name the slot ("Gloves, empty") and have no
        ///     position, flasks say their slot number, the main inventory and other grids (stash, vendor) column and row.
        /// </summary>
        public static (string Text, string Value) InventoryCell(GameHelper.RemoteEnums.InventoryName? inventory, string item, int column, int row)
        {
            if (inventory == GameHelper.RemoteEnums.InventoryName.Flask1)
            {
                return (item, $"flask slot {column + 1}");
            }

            // Stash tabs are not player inventories, yet GameHelper's map can name one after an unrelated slot.
            return inventory is { } slot && SlotName(slot) is { } name ? ($"{name}, {item}", string.Empty) : (item, GridPosition(column, row));
        }

        public static string? SlotName(GameHelper.RemoteEnums.InventoryName inventory) => inventory switch
        {
            GameHelper.RemoteEnums.InventoryName.BodyArmour1 => "Body armour",
            GameHelper.RemoteEnums.InventoryName.Weapon1 => "Main hand",
            GameHelper.RemoteEnums.InventoryName.Offhand1 => "Off hand",
            GameHelper.RemoteEnums.InventoryName.Weapon2 => "Second weapon set, main hand",
            GameHelper.RemoteEnums.InventoryName.Offhand2 => "Second weapon set, off hand",
            GameHelper.RemoteEnums.InventoryName.Helm1 => "Helmet",
            GameHelper.RemoteEnums.InventoryName.Amulet1 => "Amulet",
            GameHelper.RemoteEnums.InventoryName.Ring1 => "Left ring",
            GameHelper.RemoteEnums.InventoryName.Ring2 => "Right ring",
            GameHelper.RemoteEnums.InventoryName.Gloves1 => "Gloves",
            GameHelper.RemoteEnums.InventoryName.Boots1 => "Boots",
            GameHelper.RemoteEnums.InventoryName.Belt1 => "Belt",
            _ => null,
        };

        /// <summary>One-based cell of the inventory grid, read after the item.</summary>
        public static string GridPosition(int column, int row) => $"column {column + 1}, row {row + 1}";

        /// <summary>Base name, stack size when more than one, rarity unless normal.</summary>
        public static string Item(string name, GameHelper.RemoteEnums.Rarity rarity, int count)
        {
            var parts = new System.Collections.Generic.List<string> { name.Length > 0 ? name : UnknownItem };
            if (count > 1)
            {
                parts.Add(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (rarity != GameHelper.RemoteEnums.Rarity.Normal)
            {
                parts.Add(rarity.ToString().ToLowerInvariant());
            }

            return string.Join(", ", parts);
        }

        /// <summary>Echo of typing in a text field: the typed characters, or what was deleted.</summary>
        public static string Typed(string added, string removed) =>
            added.Length > 0 ? Character(added) : $"deleted {Character(removed)}";

        public static string ValueOrEmpty(string value) => value.Length == 0 ? Empty : value;

        /// <summary>A lone space is silent in most screen readers, so it is named.</summary>
        private static string Character(string text) => text == " " ? "space" : text;

        /// <summary>Joins two buttons shown for the same action.</summary>
        public const string ButtonSeparator = " or ";

        /// <summary>Joins buttons pressed together ("RT plus L3").</summary>
        public const string ButtonCombination = " plus ";

        public const string Gold = "gold";

        public static string LevelUp(int level) => $"Level {level}";

        /// <summary>What the player just walked up to: "Checkpoint", "Una, Stash".</summary>
        public static string Nearby(System.Collections.Generic.IEnumerable<string> names) => string.Join(", ", names);

        /// <summary>An amount shown beside a currency icon ("128 gold"); a price drawn red is one the player cannot pay.</summary>
        public static string Amount(string amount, string currency, bool cannotAfford) =>
            cannotAfford ? $"{amount} {currency}, not enough" : $"{amount} {currency}";

        /// <summary>A label with the controller button bound to it ("Select, A").</summary>
        public static string WithButton(string label, string button) => button.Length == 0 ? label : $"{label}, {button}";

        /// <summary>"100%" or "(0/1)" still need a full stop; these already end a sentence or lead into the next.</summary>
        private const string SentenceEnds = ".!?:;";

        /// <summary>Screen texts read in a row: each one is its own sentence unless it already ends in punctuation.</summary>
        public static string Sentences(System.Collections.Generic.IEnumerable<string> parts)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (builder.Length > 0)
                {
                    builder.Append(SentenceEnds.Contains(builder[^1]) ? " " : ". ");
                }

                builder.Append(part);
            }

            return builder.ToString();
        }

        /// <summary>Parts of one announcement: comma-separated, but a part that ends a sentence is not followed by a comma.</summary>
        private static string Clauses(System.Collections.Generic.IEnumerable<string> parts)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (builder.Length > 0)
                {
                    builder.Append(SentenceEnds.Contains(builder[^1]) ? " " : ", ");
                }

                builder.Append(part);
            }

            return builder.ToString();
        }

        public const string GameAttached = "Game Helper attached to the game.";
        public const string GameNotAttached = "Game Helper is not attached to the game.";
        public const string NoGameProcess = "No Path of Exile process is running.";

        public static string Area(string areaName) => $"Area: {areaName}.";

        /// <summary>Menu rule: name, then state/value, then role. Value-bearing controls read "label, value, role".</summary>
        /// <param name="fullTooltip">Also read a long tooltip (item mods, gem stats) after the position.</param>
        public static string Focused(Models.AccessibleUiNode node, bool fullTooltip = false)
        {
            if (node.Role == Models.UiRole.Link)
            {
                return Link(node);
            }

            var role = node.Role switch
            {
                Models.UiRole.Button => "button",
                Models.UiRole.ComboBox => "combo box",
                Models.UiRole.TextField => "edit field",
                Models.UiRole.CheckBox => "check box",
                Models.UiRole.Slider => "slider",
                Models.UiRole.Tab => "tab",
                Models.UiRole.Link => "link",
                _ => string.Empty,
            };
            var parts = new System.Collections.Generic.List<string>(3);
            if (node.Text.Length > 0)
            {
                parts.Add(node.Text);
            }

            if (node.Value.Length > 0)
            {
                parts.Add(node.Value);
            }

            if (role.Length > 0 && !node.IsHovered)
            {
                parts.Add(role);
            }

            if (!node.IsEnabled)
            {
                parts.Add(UnavailableState);
            }

            if ((node.IsHovered || node.Role == Models.UiRole.Link) && node.Tooltip.Count > 0)
            {
                parts.Add(Sentences(node.Tooltip));
            }
            else if (IsShortTooltip(node.Tooltip))
            {
                parts.Add(Sentences(node.Tooltip));
            }

            if (node.Position.Length > 0)
            {
                parts.Add(node.Position);
            }

            if (fullTooltip && !node.IsHovered && node.Tooltip.Count > 0 && !IsShortTooltip(node.Tooltip))
            {
                parts.Add(Sentences(node.Tooltip));
            }

            return parts.Count == 0 ? "Unnamed element" : Clauses(parts);
        }

        /// <summary>"Evasion Rating, 7. Evasion, link. Evasion Rating grants a chance …": the line, the keyword, its definition.</summary>
        private static string Link(Models.AccessibleUiNode node)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (node.Value.Length > 0)
            {
                parts.Add(node.Value);
            }

            parts.Add($"{node.Text}, link");
            parts.AddRange(node.Tooltip);
            return Sentences(parts);
        }

        public static string ListPosition(int oneBasedIndex, int count) => $"{oneBasedIndex} of {count}";

        /// <summary>Cells of one row or entry: a caption ending in ':' runs into its value ("LVL: 1, Punch").</summary>
        public static string Cells(System.Collections.Generic.IEnumerable<string> cells)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var cell in cells)
            {
                if (builder.Length > 0)
                {
                    builder.Append(builder[^1] == ':' ? " " : ", ");
                }

                builder.Append(cell);
            }

            return builder.ToString();
        }

        /// <summary>A label wrapped over several lines by the layout (the world map legend) read as one line.</summary>
        public static string Flatten(string text) =>
            text.Contains('\n') ? JoinLines(text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : text;

        /// <summary>
        ///     Lines of one text: a line starting in lower case continues the previous one (layout wrapping, "Socket
        ///     a Support Gem of any / colour to …"); any other starts a new sentence ("1 Strength = +2 Life / 5 Strength …").
        /// </summary>
        public static string JoinLines(System.Collections.Generic.IEnumerable<string> lines)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var line in lines)
            {
                if (builder.Length > 0)
                {
                    var continues = char.IsLower(line[0]) || SentenceEnds.Contains(builder[^1]);
                    builder.Append(continues ? " " : ". ");
                }

                builder.Append(line);
            }

            return builder.ToString();
        }

        /// <summary>One table row: "Maximum Life, 42"; a caption ending in ':' runs into its value ("LVL: 1").</summary>
        public static string LabelValue(string label, string value) => label.EndsWith(':') ? $"{label} {value}" : $"{label}, {value}";

        /// <summary>Tooltips up to this size are read with the focus (D2R: name, state, tooltip, position); longer ones on request.</summary>
        private const int MaxAutoTooltipLines = 2;

        private const int MaxAutoTooltipChars = 200;

        public const string NoDetails = "No details.";

        public static bool IsShortTooltip(System.Collections.Generic.IReadOnlyList<string> tooltip) =>
            tooltip.Count is > 0 and <= MaxAutoTooltipLines && tooltip.Sum(line => line.Length) <= MaxAutoTooltipChars;

        /// <summary>The focused element's full tooltip (skill gems, rich descriptions).</summary>
        public static string Details(Models.AccessibleUiNode node) =>
            node.Tooltip.Count == 0 ? NoDetails : Sentences(node.Tooltip);

        /// <summary>Entering a panel: its title before the focused item.</summary>
        public static string InContext(string context, string focused) =>
            context.Length == 0 ? focused : focused.Length == 0 ? context : $"{context}. {focused}";

        /// <summary>Numbered tabs ("-1-" vendor pages, "1" default stash tabs) read "tab 1"; named tabs keep their name.</summary>
        public static string TabTitle(string caption)
        {
            var number = caption.Trim('-', ' ');
            return number.Length > 0 && number.All(char.IsDigit) ? $"tab {number}" : caption;
        }

        public static string Status(bool attached, GameStateTypes state, string areaName, string backend, string processReport)
        {
            if (!attached)
            {
                var detail = string.IsNullOrEmpty(processReport) ? NoGameProcess : processReport;
                return $"{GameNotAttached} {detail} Speech backend {backend}.";
            }

            return string.IsNullOrEmpty(areaName)
                ? $"{ForState(state)} Speech backend {backend}."
                : $"{ForState(state)} {Area(areaName)} Speech backend {backend}.";
        }

        public static string GameProcessMatches(string processName) =>
            $"Process {processName} is running with the expected window title; Game Helper should attach within seconds.";

        public static string GameProcessTitleMismatch(string processName, string title) =>
            string.IsNullOrEmpty(title)
                ? $"Process {processName} is running but has no main window yet."
                : $"Process {processName} is running but its window title is \"{title}\", not \"Path of Exile 2\", so Game Helper will not attach.";

        public static string GameProcessInaccessible(string processName) =>
            $"Process {processName} is running but cannot be inspected; run Game Helper as administrator.";
    }
}
