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

        /// <summary>The longest pin text read (the longest description is about 300 characters).</summary>
        private const int MaxPinTextChars = 1000;

        /// <summary>Elements looked at for the pin's popup, beside the map: the popup is a few levels under the panel.</summary>
        private const int MaxPopupNodes = 600;

        /// <summary>Until the hovered pin's place is proved, the canvas's keys are looked at this often (the cursor rests on a pin longer).</summary>
        private const int HoveredPinLookMs = 150;

        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        /// <summary>Where a pin element keeps its MapPins key, found again by the table its file names.</summary>
        private static readonly DatKeySite PinKey = new("map pin key", GameLayout.MapPinRow.ElementPinOffset, GameLayout.WorldMap.PinTable);

        private long nextHoveredPinLook;

        /// <summary>The game's "Monster Level: {0}" of a pin's popup in the client's language, empty without the table.</summary>
        public Func<string>? PinLevelLabel { get; set; }

        /// <summary>The game's "Town" of a town pin's popup, empty without the table.</summary>
        public Func<string>? PinTownLabel { get; set; }

        /// <summary>The map legend's word for a picture (<see cref="MapContentIcons"/>), empty without the table.</summary>
        public Func<string, string> MapLegendWord { get; set; } = _ => string.Empty;

        /// <summary>Where a row of the list of points of interest (R3 on the map) keeps its pin's MapPins key.</summary>
        private static readonly DatKeySite PointOfInterestKey = new("point of interest key", GameLayout.MapPinRow.PointOfInterestOffset, GameLayout.WorldMap.PinTable);

        /// <summary>The world map (panels_parent > a shown group > "WorldScreen") while it is shown, else zero.</summary>
        public IntPtr WorldScreen(IntPtr inventoryPanel)
        {
            foreach (var (panel, _) in this.ShownPanels(inventoryPanel))
            {
                if (panel.Name == WorldScreenName)
                {
                    return panel.Address;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>The MapPins row of the pin under the cursor on the shown act's canvas, zero for none.</summary>
        public IntPtr HoveredPin(IntPtr worldScreen) => this.ShownCanvas(worldScreen) is { } canvas
            ? this.PinKeyRow(canvas, this.HoveredPinAt(canvas)) ?? IntPtr.Zero
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
                // A stop of the caravan ("Keth", pin G2_town_marker_keth) is a pin of the town's area that is not the town:
                // "Pueblo" followed every destination of the desert map (measured 2026-11-12; docs/research/ui-layers.md).
                kind = this.PinText(areaRow, GameLayout.WorldAreaRow.NameOffset) != name ? string.Empty
                    : this.PinTownLabel?.Invoke() is { Length: > 0 } townWord ? townWord : Announcements.PinTown;
            }
            else if (area is { } levelRow && this.reader.TryRead<int>(levelRow + GameLayout.WorldAreaRow.LevelOffset, out var level) && level is > 0 and <= 100)
            {
                kind = this.PopupLevel(worldScreen) is { Length: > 0 } shownLevel ? shownLevel
                    : this.PinLevelLabel?.Invoke() is { Length: > 0 } format && format.Contains("{0}", StringComparison.Ordinal)
                    ? format.Replace("{0}", level.ToString(System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal)
                    : Announcements.PinLevel(level);
            }

            var words = this.PinText(row, GameLayout.MapPinRow.DescriptionOffset);
            var contents = area is { } sameArea && this.ShownCanvas(worldScreen) is { } canvas
                ? this.ThingsOf(this.PinsOf(canvas), sameArea, row).Where(thing => thing.Name != name).Select(thing => MapContentIcons.Describe(thing.Name, thing.Art, this.MapLegendWord)).ToList()
                : new List<string>();
            return (name, kind, words, contents);
        }

        /// <summary>The areas behind one on its own side, walking (<see cref="QuestGoalCatalog.Behind"/>): area id and the towns' ids to area ids.</summary>
        public Func<string, IReadOnlySet<string>, IReadOnlyList<string>> AreasBehind { get; set; } = (_, _) => Array.Empty<string>();

        /// <summary>
        ///     What the world map's pictures say of a pin's area: whether it was entered, and each thing within it with its state.
        ///     A travel map (the ship's, the caravan's) draws its own marker over the area and a popup without rows, so the
        ///     area's own pin and the pins within it are read, and those of the areas behind it: an island's caverns, the Lost
        ///     City under Keth (docs/research/endgame-ui.md §7).
        /// </summary>
        public MapAreaPictures PinArea(IntPtr worldScreen, IntPtr row)
        {
            var name = this.PinText(row, GameLayout.MapPinRow.NameOffset);
            if (name.Length == 0 || this.ShownCanvas(worldScreen) is not { } canvas)
            {
                return MapAreaPictures.Empty;
            }

            var pins = this.PinsOf(canvas).ToList();
            var area = DatKey.Row(this.reader, this.elements, row + GameLayout.MapPinRow.AreaKeyOffset, GameLayout.WorldMap.AreaTable);
            if (area is { } town && this.IsTown(town))
            {
                // A pin of a town's area that is not the town is a stop of the caravan: its area is the one whose pin, or the
                // area itself, has that name (the tables write some apart: "El acorazado" and "El Acorazado").
                area = this.PinText(town, GameLayout.WorldAreaRow.NameOffset) == name ? null
                    : pins.Where(pin => !this.IsTown(pin.Area) && (SameName(this.PinText(pin.Row, GameLayout.MapPinRow.NameOffset), name) ||
                        SameName(this.PinText(pin.Area, GameLayout.WorldAreaRow.NameOffset), name))).Select(pin => (IntPtr?)pin.Area).FirstOrDefault();
            }

            if (area is not { } areaRow)
            {
                return MapAreaPictures.Empty;
            }

            // A name that is another area's, on the marker of this one, is the tables' mistake: in Spanish the ship's marker of
            // "Isla del clan" reads "Marca del rey", the town's (0.5.5f; the only one in the ten languages, docs/research/endgame-ui.md §7).
            var areaName = this.PinText(areaRow, GameLayout.WorldAreaRow.NameOffset);
            var misnamed = areaName.Length > 0 && !SameName(areaName, name) && MapContentIcons.Read(this.PinText(row, GameLayout.MapPinRow.ContentOffset)) == null &&
                pins.Any(pin => pin.Area != areaRow && SameName(this.PinText(pin.Area, GameLayout.WorldAreaRow.NameOffset), name));

            var areaId = this.PinRowId(areaRow);
            var areas = new List<IntPtr> { areaRow };
            if (this.PinRowId(row) != areaId && MapContentIcons.Read(this.PinText(row, GameLayout.MapPinRow.ContentOffset)) == null)
            {
                var byId = new Dictionary<string, IntPtr>();
                foreach (var pin in pins)
                {
                    byId.TryAdd(this.PinRowId(pin.Area), pin.Area);
                }

                var towns = byId.Where(pair => this.IsTown(pair.Value)).Select(pair => pair.Key).ToHashSet();
                areas.AddRange(this.AreasBehind(areaId, towns).Where(byId.ContainsKey).Select(id => byId[id]));
            }

            var (pending, done) = (new List<(string Name, string Line)>(), new List<string>());
            foreach (var each in areas)
            {
                var place = each == areaRow ? string.Empty : this.PinText(each, GameLayout.WorldAreaRow.NameOffset);
                if (place.Length > 0 && this.AreaPinState(pins, each) == MapAreaState.Unknown)
                {
                    pending.Add((place, Announcements.MapAreaOf(place, MapAreaState.Unknown)));
                }

                foreach (var (thing, art) in this.ThingsOf(pins, each, row).Where(thing => thing.Name != name))
                {
                    if (MapContentIcons.Read(art) is { Done: true })
                    {
                        done.Add(thing);
                    }
                    else
                    {
                        pending.Add((thing, MapContentIcons.Describe(place.Length > 0 ? thing + ", " + place : thing, art, this.MapLegendWord)));
                    }
                }
            }

            return new MapAreaPictures(misnamed ? areaName : string.Empty, this.AreaPinState(pins, areaRow), pending, done);
        }

        /// <summary>The picture a row of the list of points of interest shows, as its area and state; empty for any other control.</summary>
        private string MapPointOfInterest(UiElementReader.ElementInfo info)
        {
            foreach (var child in this.elements.ReadChildren(info))
            {
                if (this.elements.TryReadElement(child, out var picture) && picture.IsVisibleFlag &&
                    this.elements.ReadTextureKey(picture) is { } art && MapContentIcons.Read(art) is { } content)
                {
                    var area = PointOfInterestKey.Row(this.reader, this.elements, info.Address) is { } pin
                        ? DatKey.Row(this.reader, this.elements, pin + GameLayout.MapPinRow.AreaKeyOffset, GameLayout.WorldMap.AreaTable)
                        : null;
                    var place = area is { } areaRow ? this.PinText(areaRow, GameLayout.WorldAreaRow.NameOffset) : string.Empty;
                    return place + Announcements.MapContentState(content.Done, this.MapLegendWord(content.Icon))[(place.Length > 0 ? 0 : 2)..];
                }
            }

            return string.Empty;
        }

        private static bool SameName(string one, string other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

        private bool IsTown(IntPtr area) => this.reader.TryRead<byte>(area + GameLayout.WorldAreaRow.IsTownOffset, out var town) && town != 0;

        /// <summary>Every pin element of a canvas with its MapPins row and its area's.</summary>
        private IEnumerable<(UiElementReader.ElementInfo Pin, IntPtr Row, IntPtr Area)> PinsOf(IntPtr canvas)
        {
            if (!this.elements.TryReadElement(canvas, out var canvasInfo))
            {
                yield break;
            }

            foreach (var layer in this.elements.ReadChildren(canvasInfo))
            {
                if (!this.elements.TryReadElement(layer, out var layerInfo) || !layerInfo.IsVisibleFlag)
                {
                    continue;
                }

                foreach (var pin in this.elements.ReadChildren(layerInfo))
                {
                    if (this.elements.TryReadElement(pin, out var info) && PinKey.Row(this.reader, this.elements, pin) is { } row &&
                        DatKey.Row(this.reader, this.elements, row + GameLayout.MapPinRow.AreaKeyOffset, GameLayout.WorldMap.AreaTable) is { } area)
                    {
                        yield return (info, row, area);
                    }
                }
            }
        }

        /// <summary>
        ///     The state of an area by its own pin (the one whose Id is the area's): the pin keeps bytes for known, entered and
        ///     waypoint active. Taken only when all three read as flags and agree (nothing is entered unknown), so a patch that
        ///     moves them says nothing instead of something false.
        /// </summary>
        private MapAreaState AreaPinState(IReadOnlyList<(UiElementReader.ElementInfo Pin, IntPtr Row, IntPtr Area)> pins, IntPtr area)
        {
            var areaId = this.PinRowId(area);
            foreach (var (pin, row, pinArea) in pins)
            {
                if (pinArea != area || !pin.IsVisibleFlag || areaId.Length == 0 || this.PinRowId(row) != areaId)
                {
                    continue;
                }

                var at = pin.Address + PinKey.Offset - GameLayout.MapPinRow.ElementPinOffset;
                if (!this.reader.TryRead<byte>(at + GameLayout.MapPinRow.ElementTravelOffset, out var travel) || travel != 0)
                {
                    // On a travel map every stop's area is drawn as entered, the one never entered too: what it draws within
                    // the area tells, its things are drawn from the first visit on.
                    return this.AreaStateByThings(pins, area);
                }

                return this.reader.TryRead<byte>(at + GameLayout.MapPinRow.ElementKnownOffset, out var known) &&
                    this.reader.TryRead<byte>(at + GameLayout.MapPinRow.ElementVisitedOffset, out var visited) &&
                    this.reader.TryRead<byte>(at + GameLayout.MapPinRow.ElementWaypointOffset, out var waypoint)
                    ? MapAreaStates.Of(known, visited, waypoint)
                    : MapAreaState.None;
            }

            return MapAreaState.None;
        }

        /// <summary>Entered when something within the area is drawn, not when it has things and none is, nothing to say when it has none.</summary>
        private MapAreaState AreaStateByThings(IReadOnlyList<(UiElementReader.ElementInfo Pin, IntPtr Row, IntPtr Area)> pins, IntPtr area)
        {
            var things = pins.Where(pin => pin.Area == area && MapContentIcons.Read(this.PinText(pin.Row, GameLayout.MapPinRow.ContentOffset)) != null).ToList();
            return things.Count == 0 ? MapAreaState.None : things.Any(thing => thing.Pin.IsVisibleFlag) ? MapAreaState.Entered : MapAreaState.Unknown;
        }

        /// <summary>A dat row's Id, its first column.</summary>
        private string PinRowId(IntPtr row) => this.reader.TryRead<IntPtr>(row, out var text) && text != IntPtr.Zero
            ? this.elements.ReadUtf16BufferAt(text, 64) ?? string.Empty
            : string.Empty;

        /// <summary>
        ///     The monster level the pin's popup shows, empty for none: Act 4's islands take the level of the order they are
        ///     played in, so the table's is not theirs ("Nivel de monstruos: 50" was followed by 46, 2026-11-25).
        /// </summary>
        private string PopupLevel(IntPtr worldScreen)
        {
            var start = this.PinLevelStart;
            return start.Length == 0 ? string.Empty : this.PopupText(worldScreen, text => text.StartsWith(start, StringComparison.Ordinal)) ?? string.Empty;
        }

        /// <summary>What a pin's monster level starts with ("Nivel de monstruos:"), empty without the table.</summary>
        public string PinLevelStart => this.PinLevelLabel?.Invoke() is { } label && label.IndexOf("{0}", StringComparison.Ordinal) is > 0 and var hole
            ? Plain(label[..hole])
            : string.Empty;

        /// <summary>How far above a control its pin is looked for: the caravan map's button is a child of a child of its pin.</summary>
        private const int PinAboveHops = 2;

        /// <summary>
        ///     The name of the pin a control belongs to, empty for a control of none: a pin of the caravan's map draws a
        ///     button of its own, with no text, art nor tooltip, which read "Vacío, botón" (docs/research/ui-layers.md).
        /// </summary>
        private string PinNameAbove(UiElementReader.ElementInfo control)
        {
            var above = control.Raw.ParentPtr;
            for (var hop = 0; hop < PinAboveHops && this.elements.TryReadElement(above, out var parent); hop++)
            {
                if (DatKey.Row(this.reader, this.elements, parent.Address + PinKey.Offset, GameLayout.WorldMap.PinTable) is { } row)
                {
                    return this.PinText(row, GameLayout.MapPinRow.NameOffset);
                }

                above = parent.Raw.ParentPtr;
            }

            return string.Empty;
        }

        /// <summary>Whether the game shows the pin's popup: a text beside the map with the pin's words (or its name, without words).</summary>
        public bool ShowsPinPopup(IntPtr worldScreen, IntPtr row)
        {
            var marker = this.PinText(row, GameLayout.MapPinRow.DescriptionOffset) is { Length: > 0 } words ? words : this.PinText(row, GameLayout.MapPinRow.NameOffset);
            return marker.Length > 0 && this.PopupText(worldScreen, text => text == marker) != null;
        }

        /// <summary>The first shown text of the world map, beside its canvases, that fits: the pin's popup; null for none.</summary>
        private string? PopupText(IntPtr worldScreen, Func<string, bool> fits)
        {
            if (!this.elements.TryReadElement(worldScreen, out var screen))
            {
                return null;
            }

            var pending = new Stack<UiElementReader.ElementInfo>();
            pending.Push(screen);
            for (var seen = 0; pending.Count > 0 && seen < MaxPopupNodes; seen++)
            {
                var info = pending.Pop();
                if (this.elements.TryReadText(info.Address, out var text) && Plain(text) is var plain && fits(plain))
                {
                    return plain;
                }

                foreach (var child in this.elements.ReadChildren(info))
                {
                    if (this.elements.TryReadElement(child, out var childInfo) && childInfo.IsVisibleFlag && childInfo.Name != GameLayout.WorldMap.ControlName)
                    {
                        pending.Push(childInfo);
                    }
                }
            }

            return null;
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

        /// <summary>
        ///     The shown act's canvas: map_control's shown child that keeps a key into MapPins where the hovered pin is read, or
        ///     where the current area's is (always set, so a canvas is found with no pin hovered and before the hovered place is
        ///     proved; both move with the canvas's fields).
        /// </summary>
        private IntPtr? ShownCanvas(IntPtr worldScreen)
        {
            if (!this.elements.TryReadElement(worldScreen, out var screen))
            {
                return null;
            }

            var shift = this.elements.Controls?.Current(ControlGroup.HoveredPin) ?? 0;
            var current = shift + GameLayout.WorldMap.CurrentPinOffset - GameLayout.WorldMap.HoveredPinOffset;
            foreach (var child in this.elements.ReadChildren(screen))
            {
                if (!this.elements.TryReadElement(child, out var control) || control.Name != GameLayout.WorldMap.ControlName)
                {
                    continue;
                }

                foreach (var canvas in this.elements.ReadChildren(control))
                {
                    if (this.elements.TryReadElement(canvas, out var info) && info.IsVisibleFlag && this.HoldsPinKey(canvas, shift, current))
                    {
                        return canvas;
                    }
                }
            }

            return null;
        }

        /// <summary>
        ///     Where the canvas keeps the hovered pin, as a shift from where it was measured: of its keys into MapPins, the one that
        ///     changes as the cursor moves over pins (<see cref="ControlFields.ChangingAt"/>), looked at a few times a second until proved.
        /// </summary>
        private int HoveredPinAt(IntPtr canvas)
        {
            if (this.elements.Controls is not { } controls)
            {
                return 0;
            }

            if (controls.IsKnown(ControlGroup.HoveredPin) || Environment.TickCount64 < this.nextHoveredPinLook)
            {
                return controls.Current(ControlGroup.HoveredPin);
            }

            this.nextHoveredPinLook = Environment.TickCount64 + HoveredPinLookMs;
            return controls.ChangingAt(ControlGroup.HoveredPin, canvas, shift => this.PinKeyRow(canvas, shift)?.ToInt64() ?? 0);
        }

        /// <summary>
        ///     Whether the canvas keeps a key into MapPins at the hovered or the current pin's place, as known or, before the
        ///     hovered pin is proved, anywhere within the search's reach: with both fields moved the canvas was never found, so
        ///     the hovered pin's search never ran and no pin was read (the gap pass's simulation, +8, 2026-11-08).
        /// </summary>
        private bool HoldsPinKey(IntPtr canvas, int shift, int current)
        {
            if (this.PinKeyRow(canvas, shift) != null || this.PinKeyRow(canvas, current) != null)
            {
                return true;
            }

            if (this.elements.Controls is not { } controls || controls.IsKnown(ControlGroup.HoveredPin))
            {
                return false;
            }

            var toCurrent = current - shift;
            return OffsetSearch.First(shift, ControlFields.Radius, ControlFields.Step, moved => this.PinKeyRow(canvas, moved + toCurrent) != null, shift - ControlFields.Radius) != null;
        }

        /// <summary>The MapPins row of the key the canvas keeps at the hovered pin's place moved by <paramref name="shift"/>; null when none.</summary>
        private IntPtr? PinKeyRow(IntPtr canvas, int shift) =>
            DatKey.Row(this.reader, this.elements, canvas + GameLayout.WorldMap.HoveredPinOffset + shift, GameLayout.WorldMap.PinTable);

        /// <summary>The other shown pins of an area (its bosses, its trial), in map order: the name and its picture.</summary>
        private IEnumerable<(string Name, string Art)> ThingsOf(IEnumerable<(UiElementReader.ElementInfo Pin, IntPtr Row, IntPtr Area)> pins, IntPtr area, IntPtr except)
        {
            var named = new HashSet<(string, string)>();
            foreach (var (pin, row, pinArea) in pins)
            {
                if (pin.IsVisibleFlag && row != except && pinArea == area &&
                    this.PinText(row, GameLayout.MapPinRow.ContentOffset) is { Length: > 0 } art && MapContentIcons.Read(art) != null &&
                    this.PinText(row, GameLayout.MapPinRow.NameOffset) is { Length: > 0 } name && named.Add((name, art)))
                {
                    yield return (name, art);
                }
            }
        }
    }
}
