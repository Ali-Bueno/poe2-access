namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     The world map's pins from the game's data (docs/research/endgame-ui.md): the act canvas keeps the MapPins row of
    ///     the pin under the cursor, each pin element its own row, and a row names its WorldAreas row. So a pin is told
    ///     whether or not the game builds its popup, which it does not for some pins in a 5:4 window (the Trial's).
    /// </summary>
    public sealed partial class UiSemantics
    {
        private const string WorldScreenName = "WorldScreen";

        /// <summary>What an area holds (a boss, a chest, its trial) has a content icon: "WorldMapContentBoss"; a town's markers none.</summary>
        private const string ContentPrefix = "WorldMapContent";

        /// <summary>The longest pin text read (the longest description is about 300 characters).</summary>
        private const int MaxPinTextChars = 1000;

        /// <summary>Elements looked at for the pin's popup, beside the map: the popup is a few levels under the panel.</summary>
        private const int MaxPopupNodes = 600;

        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        /// <summary>Where a pin element keeps its MapPins key, found again by the table its file names.</summary>
        private static readonly DatKeySite PinKey = new("map pin key", GameLayout.MapPinRow.ElementPinOffset, GameLayout.WorldMap.PinTable);

        /// <summary>The game's "Monster Level: {0}" of a pin's popup in the client's language, empty without the table.</summary>
        public Func<string>? PinLevelLabel { get; set; }

        /// <summary>The game's "Town" of a town pin's popup, empty without the table.</summary>
        public Func<string>? PinTownLabel { get; set; }

        /// <summary>The world map (panels_parent > a shown group > "WorldScreen") while it is shown, else zero.</summary>
        public IntPtr WorldScreen(IntPtr inventoryPanel)
        {
            if (!this.elements.TryReadElement(inventoryPanel, out var root) || !root.IsVisibleFlag)
            {
                return IntPtr.Zero;
            }

            foreach (var child in this.elements.ReadChildren(root))
            {
                if (!this.elements.TryReadElement(child, out var parent) || parent.Name != GameLayout.InGameUi.PanelsParentName)
                {
                    continue;
                }

                foreach (var group in this.elements.ReadChildren(parent))
                {
                    if (!this.elements.TryReadElement(group, out var groupInfo) || !groupInfo.IsVisibleFlag)
                    {
                        continue;
                    }

                    foreach (var panel in this.elements.ReadChildren(groupInfo))
                    {
                        if (this.elements.TryReadElement(panel, out var info) && info.IsVisibleFlag && info.Name == WorldScreenName)
                        {
                            return panel;
                        }
                    }
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>The MapPins row of the pin under the cursor on the shown act's canvas, zero for none.</summary>
        public IntPtr HoveredPin(IntPtr worldScreen) => this.ShownCanvas(worldScreen) is { } canvas
            ? DatKey.Row(this.reader, this.elements, canvas + GameLayout.WorldMap.HoveredPinOffset, GameLayout.WorldMap.PinTable) ?? IntPtr.Zero
            : IntPtr.Zero;

        /// <summary>
        ///     A pin as its popup tells it, from its data: "Prueba de las sekhemas. Nivel de monstruos: 22. Quienes querían ser
        ///     sekhemas… Prueba de ascendencia" (name, level or "Town", words, then what else the map shows of that area).
        /// </summary>
        public string DescribePin(IntPtr worldScreen, IntPtr row)
        {
            var (name, kind, words, contents) = this.PinParts(worldScreen, row);
            return name.Length == 0 ? string.Empty : Announcements.Sentences(new[] { name, kind, words }.Where(part => part.Length > 0).Concat(contents).Distinct());
        }

        /// <summary>
        ///     A pin's popup from its data, part by part: its name, its kind (the monster level, or "Town"), its words and the
        ///     area's other shown pins; empty name for no pin.
        /// </summary>
        public (string Name, string Kind, string Words, IReadOnlyList<string> Contents) PinParts(IntPtr worldScreen, IntPtr row)
        {
            var name = this.PinText(row, GameLayout.MapPinRow.NameOffset);
            if (name.Length == 0)
            {
                return (string.Empty, string.Empty, string.Empty, Array.Empty<string>());
            }

            var kind = string.Empty;
            var area = DatKey.Row(this.reader, this.elements, row + GameLayout.MapPinRow.AreaKeyOffset, GameLayout.WorldMap.AreaTable);
            if (area is { } areaRow && this.reader.TryRead<byte>(areaRow + GameLayout.WorldAreaRow.IsTownOffset, out var town) && town != 0)
            {
                kind = this.PinTownLabel?.Invoke() is { Length: > 0 } townWord ? townWord : Announcements.PinTown;
            }
            else if (area is { } levelRow && this.reader.TryRead<int>(levelRow + GameLayout.WorldAreaRow.LevelOffset, out var level) && level is > 0 and <= 100)
            {
                kind = this.PinLevelLabel?.Invoke() is { Length: > 0 } format && format.Contains("{0}", StringComparison.Ordinal)
                    ? format.Replace("{0}", level.ToString(System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal)
                    : Announcements.PinLevel(level);
            }

            var words = this.PinText(row, GameLayout.MapPinRow.DescriptionOffset);
            var contents = area is { } sameArea ? this.PinsOfArea(worldScreen, sameArea, row).Where(other => other != name).ToList() : new List<string>();
            return (name, kind, words, contents);
        }

        /// <summary>Whether the game shows the pin's popup: a text beside the map with the pin's words (or its name, without words).</summary>
        public bool ShowsPinPopup(IntPtr worldScreen, IntPtr row)
        {
            var marker = this.PinText(row, GameLayout.MapPinRow.DescriptionOffset) is { Length: > 0 } words ? words : this.PinText(row, GameLayout.MapPinRow.NameOffset);
            if (marker.Length == 0 || !this.elements.TryReadElement(worldScreen, out var screen))
            {
                return false;
            }

            var pending = new Stack<UiElementReader.ElementInfo>();
            pending.Push(screen);
            for (var seen = 0; pending.Count > 0 && seen < MaxPopupNodes; seen++)
            {
                var info = pending.Pop();
                if (this.elements.TryReadText(info.Address, out var text) && Plain(text) == marker)
                {
                    return true;
                }

                foreach (var child in this.elements.ReadChildren(info))
                {
                    if (this.elements.TryReadElement(child, out var childInfo) && childInfo.IsVisibleFlag && childInfo.Name != GameLayout.WorldMap.ControlName)
                    {
                        pending.Push(childInfo);
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     What a pin's popup says that nothing else does: the start of its words' first line (or its name, without words),
        ///     to tell whether the popup was heard.
        /// </summary>
        public string PinMarker(IntPtr row)
        {
            const int MarkerChars = 40;
            var raw = row != IntPtr.Zero && this.reader.TryRead<IntPtr>(row + GameLayout.MapPinRow.DescriptionOffset, out var text) && text != IntPtr.Zero
                ? this.elements.ReadUtf16BufferAt(text, MaxPinTextChars) ?? string.Empty
                : string.Empty;
            var first = Plain(raw.Split('\n')[0]);
            first = first.Length > 0 ? first : this.PinText(row, GameLayout.MapPinRow.NameOffset);
            return first.Length > MarkerChars ? first[..MarkerChars] : first;
        }

        /// <summary>Research (dev command "mappin"): the hovered pin's row, its description from data, and whether the game shows its popup.</summary>
        public string PinReport(IntPtr worldScreen)
        {
            var row = this.HoveredPin(worldScreen);
            var id = row != IntPtr.Zero && this.reader.TryRead<IntPtr>(row, out var text) ? this.elements.ReadUtf16BufferAt(text, 64) : null;
            return row == IntPtr.Zero ? $"no hovered pin (world screen @{worldScreen:X})" : $"{id}: \"{this.DescribePin(worldScreen, row)}\", popup shown {this.ShowsPinPopup(worldScreen, row)}";
        }

        private static string Plain(string text) => Spaces.Replace(text, " ").Trim();

        private string PinText(IntPtr row, int offset) =>
            row != IntPtr.Zero && this.reader.TryRead<IntPtr>(row + offset, out var text) && text != IntPtr.Zero
                ? Plain(this.elements.ReadUtf16BufferAt(text, MaxPinTextChars) ?? string.Empty)
                : string.Empty;

        /// <summary>The shown act's canvas: map_control's shown child that keeps a hovered pin.</summary>
        private IntPtr? ShownCanvas(IntPtr worldScreen)
        {
            if (!this.elements.TryReadElement(worldScreen, out var screen))
            {
                return null;
            }

            foreach (var child in this.elements.ReadChildren(screen))
            {
                if (!this.elements.TryReadElement(child, out var control) || control.Name != GameLayout.WorldMap.ControlName)
                {
                    continue;
                }

                foreach (var canvas in this.elements.ReadChildren(control))
                {
                    if (this.elements.TryReadElement(canvas, out var info) && info.IsVisibleFlag &&
                        DatKey.Row(this.reader, this.elements, canvas + GameLayout.WorldMap.HoveredPinOffset, GameLayout.WorldMap.PinTable) != null)
                    {
                        return canvas;
                    }
                }
            }

            return null;
        }

        /// <summary>The names of the other shown pins of an area (its bosses, its trial), as the popup lists them, in map order.</summary>
        private IEnumerable<string> PinsOfArea(IntPtr worldScreen, IntPtr area, IntPtr except)
        {
            if (this.ShownCanvas(worldScreen) is not { } canvas || !this.elements.TryReadElement(canvas, out var canvasInfo))
            {
                yield break;
            }

            var named = new HashSet<string>();
            foreach (var layer in this.elements.ReadChildren(canvasInfo))
            {
                if (!this.elements.TryReadElement(layer, out var layerInfo) || !layerInfo.IsVisibleFlag)
                {
                    continue;
                }

                foreach (var pin in this.elements.ReadChildren(layerInfo))
                {
                    if (this.elements.TryReadElement(pin, out var pinInfo) && pinInfo.IsVisibleFlag &&
                        PinKey.Row(this.reader, this.elements, pin) is { } row && row != except &&
                        DatKey.Row(this.reader, this.elements, row + GameLayout.MapPinRow.AreaKeyOffset, GameLayout.WorldMap.AreaTable) == area &&
                        this.PinText(row, GameLayout.MapPinRow.ContentOffset).StartsWith(ContentPrefix, StringComparison.Ordinal) &&
                        this.PinText(row, GameLayout.MapPinRow.NameOffset) is { Length: > 0 } name && named.Add(name))
                    {
                        yield return name;
                    }
                }
            }
        }
    }
}
