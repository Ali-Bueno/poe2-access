namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     The world map's quest marks, from the game's own rows: each quest button of the map's list keeps its step, the step
    ///     keeps the pins the map marks with "Quest Location", and each area keeps the areas next to it. Nothing is matched by
    ///     a quest's words, so a patch that rewrites quests is read as it is (docs/research/quest-goals.md, the world map's
    ///     quest marker). Every read is checked and a doubtful one says nothing.
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Where a quest button keeps its Quest row; its step's key follows it (<see cref="GameLayout.QuestList"/>).</summary>
        private static readonly DatKeySite QuestKey = new("quest list quest key", GameLayout.QuestList.QuestKeyOffset, GameLayout.QuestList.QuestTable);

        /// <summary>The list is a child of the map panel (WorldScreen > quest_selector, 2026-11-27).</summary>
        private const int QuestListDepth = 2;

        /// <summary>Its buttons: quest_selector > frame > list > button.</summary>
        private const int QuestButtonDepth = 3;

        /// <summary>A button's title is its first text, two levels down.</summary>
        private const int QuestTitleDepth = 4;

        /// <summary>An area lists a few areas next to it (0.5.5f: four at most); more is a column read wrong.</summary>
        private const int MaxConnections = 16;

        /// <summary>
        ///     The places the shown act's map marks for the quests of its list, each with its nearest active waypoints, in the
        ///     list's order; and the canvas they are drawn on (zero without one). A mark on another act's map is not this one's.
        /// </summary>
        public (IntPtr Canvas, IReadOnlyList<QuestMark> Marks) QuestMarks(IntPtr worldScreen)
        {
            if (this.ShownCanvas(worldScreen) is not { } canvas)
            {
                return (IntPtr.Zero, Array.Empty<QuestMark>());
            }

            var now = Environment.TickCount64;
            if (this.questMarksKept is { } kept && kept.Screen == worldScreen && kept.Canvas == canvas && now - kept.At < QuestMarksKeptMs)
            {
                return (canvas, kept.Marks);
            }

            var read = this.ReadQuestMarks(worldScreen, canvas);
            this.questMarksKept = (worldScreen, canvas, now, read);
            return (canvas, read);
        }

        /// <summary>
        ///     How long one reading of a canvas's marks, or of the areas it shows, serves the next: the focused quest of the list
        ///     is asked every 150 ms and each pin the cursor rests on asks again, each a walk of the canvas's pins (about a
        ///     hundred, two keys each) and of the ways between areas. A mark changes with a quest step, an area with a visit.
        /// </summary>
        private const int QuestMarksKeptMs = 1000;

        private (IntPtr Screen, IntPtr Canvas, long At, IReadOnlyList<QuestMark> Marks)? questMarksKept;
        private (IntPtr Canvas, long At, HashSet<IntPtr> Areas)? shownAreasKept;

        /// <summary>The areas the canvas shows, read at most once in <see cref="QuestMarksKeptMs"/>.</summary>
        private HashSet<IntPtr> ShownAreasOf(IntPtr canvas)
        {
            var now = Environment.TickCount64;
            if (this.shownAreasKept is { } kept && kept.Canvas == canvas && now - kept.At < QuestMarksKeptMs)
            {
                return kept.Areas;
            }

            var areas = this.ShownAreas(this.PinsOf(canvas).ToList());
            this.shownAreasKept = (canvas, now, areas);
            return areas;
        }

        private IReadOnlyList<QuestMark> ReadQuestMarks(IntPtr worldScreen, IntPtr canvas)
        {

            var quests = this.QuestSteps(worldScreen);
            if (quests.Count == 0)
            {
                return Array.Empty<QuestMark>();
            }

            var pins = this.PinsOf(canvas).ToList();
            var shown = this.ShownAreas(pins);
            var titles = new Dictionary<IntPtr, List<string>>();
            var order = new List<IntPtr>();
            foreach (var (title, rows) in quests)
            {
                foreach (var row in rows)
                {
                    if (this.AreaOfPin(row) is not { } area || !shown.Contains(area))
                    {
                        continue;
                    }

                    if (!titles.TryGetValue(area, out var named))
                    {
                        titles[area] = named = new List<string>();
                        order.Add(area);
                    }

                    if (title.Length > 0 && !named.Contains(title))
                    {
                        named.Add(title);
                    }
                }
            }

            var travel = this.IsTravelMap(pins);
            var marks = new List<QuestMark>(order.Count);
            foreach (var area in order)
            {
                var state = this.AreaPinState(pins, area);
                var (steps, names, side) = (QuestMark.NotKnown, new List<string>(), string.Empty);
                if (!travel && state == MapAreaState.Waypoint)
                {
                    steps = 0;
                }
                else if (!travel && this.Neighbours(area, shown, out _) is { Count: > 0 })
                {
                    // Only a list that reads, and leads to a place the map shows, is walked. "No waypoint near" is said only
                    // of a walk that lost nothing: an area between that the map does not show yet, or a list that did not
                    // read, may hide the waypoint a sighted player sees two areas on (a second reader, 2026-11-28).
                    var whole = true;
                    IEnumerable<IntPtr> Next(IntPtr each)
                    {
                        var next = this.Neighbours(each, shown, out var all);
                        whole &= all;
                        return next ?? new List<IntPtr>();
                    }

                    var (found, goals) = QuestMarkRoutes.Nearest(area, Next, each => this.AreaPinState(pins, each) == MapAreaState.Waypoint);
                    names = goals.Select(goal => this.PinText(goal, GameLayout.WorldAreaRow.NameOffset)).Where(name => name.Length > 0).ToList();
                    steps = found == QuestMark.NoneNear ? (whole ? found : QuestMark.NotKnown) : names.Count == goals.Count ? found : QuestMark.NotKnown;
                    side = names.Count > 0 && steps > 0 ? this.PinSide(pins, area, goals[0]) : string.Empty;
                }

                marks.Add(new QuestMark(titles[area], this.PinText(area, GameLayout.WorldAreaRow.NameOffset), state, area, names, steps, side));
            }

            return marks;
        }

        /// <summary>The areas whose own pin the map shows: the only places a mark, a way or a waypoint is said of.</summary>
        private HashSet<IntPtr> ShownAreas(IReadOnlyList<(UiElementReader.ElementInfo Pin, IntPtr Row, IntPtr Area)> pins) =>
            pins.Where(pin => pin.Pin.IsVisibleFlag).Select(pin => pin.Area).Distinct().Where(area => this.AreaPinState(pins, area) != MapAreaState.None).ToHashSet();

        /// <summary>A travel map (the ship's, the caravan's) draws no waypoints: its pins say so (<see cref="GameLayout.MapPinRow.ElementTravelOffset"/>).</summary>
        private bool IsTravelMap(IReadOnlyList<(UiElementReader.ElementInfo Pin, IntPtr Row, IntPtr Area)> pins) =>
            pins.Any(pin => pin.Pin.IsVisibleFlag &&
                this.reader.TryRead<byte>(pin.Pin.Address + PinKey.Offset - GameLayout.MapPinRow.ElementPinOffset + GameLayout.MapPinRow.ElementTravelOffset, out var travel) &&
                travel != 0);

        /// <summary>Whether a quest of the map's list says where it is marked (setting AnnounceQuestMarks).</summary>
        public Func<bool> QuestMarksShown { get; set; } = () => true;

        /// <summary>The act's canvas the map shows, zero for none: it changes when another act's map is shown.</summary>
        public IntPtr MapCanvas(IntPtr worldScreen) => this.ShownCanvas(worldScreen) ?? IntPtr.Zero;

        /// <summary>The mark on the area of the pin under the cursor (a MapPins row), null when the map marks none there.</summary>
        public QuestMark? QuestMarkOf(IntPtr worldScreen, IntPtr pinRow) =>
            this.AreaOfPin(pinRow) is { } area ? this.QuestMarks(worldScreen).Marks.FirstOrDefault(mark => mark.Area == area) : null;

        /// <summary>
        ///     The places a quest button of the map's list marks on the act the map shows, as their areas' names; empty for any
        ///     other control, a finished quest, or a step the shown map marks nowhere (a sighted player sees no ring there).
        /// </summary>
        public IReadOnlyList<string> QuestPlaces(in UiElementReader.ElementInfo button)
        {
            var ancestors = this.elements.ReadAncestors(button, QuestButtonDepth + 1);
            var list = ancestors.FindIndex(ancestor => ancestor.Name == GameLayout.QuestList.ControlName);
            // Only a button where the list keeps them (quest_selector > frame > list > button) is asked for its key by the
            // search that learns where the key is: the crosshair and the quick-travel buttons sit under the same control.
            if (list != QuestButtonDepth - 1 || list + 1 >= ancestors.Count || this.ShownCanvas(ancestors[list + 1].Address) is not { } canvas ||
                QuestKey.Row(this.reader, this.elements, button.Address) == null || this.StepOf(button.Address) is not { } step)
            {
                return Array.Empty<string>();
            }

            var shown = this.ShownAreasOf(canvas);
            return this.StepPins(step).Select(this.AreaOfPin).OfType<IntPtr>().Where(shown.Contains)
                .Select(area => this.PinText(area, GameLayout.WorldAreaRow.NameOffset)).Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        }

        /// <summary>Each quest of the map's list on a step that marks pins: its title and the MapPins rows of those pins.</summary>
        private List<(string Title, IReadOnlyList<IntPtr> Pins)> QuestSteps(IntPtr worldScreen)
        {
            var found = new List<(string, IReadOnlyList<IntPtr>)>();
            if (!this.elements.TryReadElement(worldScreen, out var screen))
            {
                return found;
            }

            var list = this.Descendants(screen, QuestListDepth).Select(each => each.Info).FirstOrDefault(info => info.Name == GameLayout.QuestList.ControlName);
            if (list.Address == IntPtr.Zero)
            {
                return found;
            }

            // The quests in hand come first, then a separator that holds nothing, then the finished ones (0.5.5h, the
            // Interlude's list: four, the separator, five). A quest in hand is on a step: when a list has some before its
            // separator and none reads a step, the step's key is not where it was.
            var buttons = new List<(IntPtr Parent, IntPtr Quest, bool OnStep, bool Marks)>();
            var separators = new Dictionary<IntPtr, int>();
            foreach (var (button, depth, parent) in this.Descendants(list, QuestButtonDepth))
            {
                if (depth != QuestButtonDepth)
                {
                    continue;
                }

                // A button holds its title and texts; the separator between active and finished quests holds nothing.
                if (this.elements.ReadChildren(button).Length == 0)
                {
                    separators.TryAdd(parent, buttons.Count(seen => seen.Parent == parent));
                    continue;
                }

                if (QuestKey.Row(this.reader, this.elements, button.Address) is not { } quest)
                {
                    continue;
                }

                var step = this.StepOf(button.Address);
                var pins = step is { } on ? this.StepPins(on) : Array.Empty<IntPtr>();
                buttons.Add((parent, quest, step != null, pins.Count > 0));
                if (pins.Count > 0)
                {
                    found.Add((this.FindDescendantText(button, QuestTitleDepth, null), pins));
                }
            }

            foreach (var (parent, before) in separators)
            {
                this.JudgeQuestSteps(buttons.Where(seen => seen.Parent == parent).Take(before).ToList());
            }

            return found;
        }

        private readonly LossWitnesses questStepLoss = new();
        private readonly HashSet<long> questStepsLogged = new();

        /// <summary>Different lists whose reading is logged in a session: one line each.</summary>
        private const int MaxQuestListsLogged = 16;

        /// <summary>The log, for one line a list: how many of its quests in hand read a step, and how many steps mark a place.</summary>
        public Action<string>? QuestMarksLog { get; set; }

        /// <summary>
        ///     The quests in hand of one list: any on a step and the key reads; none on two different lists and it is said lost
        ///     (<see cref="LossWitnesses"/>), as every quest then looks finished and the map marks nothing with nothing said.
        /// </summary>
        private void JudgeQuestSteps(IReadOnlyList<(IntPtr Parent, IntPtr Quest, bool OnStep, bool Marks)> inHand)
        {
            if (inHand.Count == 0)
            {
                return;
            }

            var witness = LossWitnesses.KeyOf(inHand.Select(quest => quest.Quest.ToInt64()));
            if (this.questStepsLogged.Count < MaxQuestListsLogged && this.questStepsLogged.Add(witness))
            {
                this.QuestMarksLog?.Invoke(
                    $"Quest marks: {inHand.Count} quests in hand of a list, {inHand.Count(quest => quest.OnStep)} on a step, {inHand.Count(quest => quest.Marks)} marking a place");
            }

            if (inHand.Any(quest => quest.OnStep))
            {
                this.questStepLoss.Clear();
                CompatibilityReport.Current?.Found(QuestMark.StepsPart);
            }
            else if (this.questStepLoss.Against(witness))
            {
                CompatibilityReport.Current?.Missing(QuestMark.StepsPart);
            }
        }

        /// <summary>
        ///     Every element under <paramref name="root"/> down to <paramref name="maxDepth"/> levels, in the order drawn, with the
        ///     element it is a child of; visible or not (the list is built off screen).
        /// </summary>
        private IEnumerable<(UiElementReader.ElementInfo Info, int Depth, IntPtr Parent)> Descendants(UiElementReader.ElementInfo root, int maxDepth)
        {
            var stack = new Stack<(UiElementReader.ElementInfo, int, IntPtr)>();
            stack.Push((root, 0, IntPtr.Zero));
            while (stack.Count > 0)
            {
                var (info, depth, parent) = stack.Pop();
                if (depth > 0)
                {
                    yield return (info, depth, parent);
                }

                if (depth >= maxDepth)
                {
                    continue;
                }

                var children = this.elements.ReadChildren(info);
                for (var i = children.Length - 1; i >= 0; i--)
                {
                    if (this.elements.TryReadElement(children[i], out var child))
                    {
                        stack.Push((child, depth + 1, info.Address));
                    }
                }
            }
        }

        /// <summary>The step a quest button is on: the key after its Quest key; null for a finished quest, whose key is empty.</summary>
        private IntPtr? StepOf(IntPtr button) => QuestKey.ProvedMove is not { } moved ? null
            : DatKey.Row(this.reader, this.elements, button + GameLayout.QuestList.StepKeyOffset + moved, GameLayout.QuestStateRow.Table);

        /// <summary>The MapPins rows a step marks; none when any of its keys does not read as one (the column read wrong).</summary>
        private IReadOnlyList<IntPtr> StepPins(IntPtr step)
        {
            var at = step + GameLayout.QuestStateRow.MapPinsOffset;
            if (!this.reader.TryRead<long>(at, out var count) || count is < 1 or > GameLayout.QuestList.MaxStepPins ||
                !this.reader.TryRead<IntPtr>(at + sizeof(long), out var keys) || !this.reader.IsValidAddress(keys))
            {
                return Array.Empty<IntPtr>();
            }

            var pins = new List<IntPtr>((int)count);
            for (var i = 0; i < count; i++)
            {
                if (DatKey.Row(this.reader, this.elements, keys + (i * GameLayout.QuestList.KeySize), GameLayout.WorldMap.PinTable) is not { } pin)
                {
                    return Array.Empty<IntPtr>();
                }

                pins.Add(pin);
            }

            return pins;
        }

        private IntPtr? AreaOfPin(IntPtr pinRow) =>
            DatKey.Row(this.reader, this.elements, pinRow + GameLayout.MapPinRow.AreaKeyOffset, GameLayout.WorldMap.AreaTable);

        /// <summary>
        ///     The areas next to one that the map shows; null when the list does not read as one (a column read wrong). Its items
        ///     are bare row pointers, taken only when a shown pin's area is one.
        /// </summary>
        /// <param name="whole">Whether the list read and every area of it is one the map shows: nothing was left out of the walk.</param>
        private List<IntPtr>? Neighbours(IntPtr area, IReadOnlySet<IntPtr> shown, out bool whole)
        {
            whole = false;
            var at = area + GameLayout.WorldAreaRow.ConnectionsOffset;
            if (!this.reader.TryRead<long>(at, out var count) || count is < 1 or > MaxConnections ||
                !this.reader.TryRead<IntPtr>(at + sizeof(long), out var items) || !this.reader.IsValidAddress(items))
            {
                return null;
            }

            var next = new List<IntPtr>((int)count);
            for (var i = 0; i < count; i++)
            {
                if (this.reader.TryRead<IntPtr>(items + (i * GameLayout.WorldAreaRow.ConnectionSize), out var item) && shown.Contains(item))
                {
                    next.Add(item);
                }
            }

            whole = next.Count == count;
            return next;
        }

        /// <summary>Where one area's own pin lies on the map from another's ("below it on the map"), empty when either is not placed.</summary>
        private string PinSide(IReadOnlyList<(UiElementReader.ElementInfo Pin, IntPtr Row, IntPtr Area)> pins, IntPtr from, IntPtr to) =>
            this.PinCentre(pins, from) is { } here && this.PinCentre(pins, to) is { } there && Vector2.Distance(here, there) >= 1
                ? Announcements.OnTheMapFrom(PassiveStick.Nearest(there - here))
                : string.Empty;

        private Vector2? PinCentre(IReadOnlyList<(UiElementReader.ElementInfo Pin, IntPtr Row, IntPtr Area)> pins, IntPtr area)
        {
            var id = this.PinRowId(area);
            foreach (var (pin, row, pinArea) in pins)
            {
                if (pinArea == area && id.Length > 0 && this.PinRowId(row) == id && this.LayoutRect(pin.Address) is { } rect)
                {
                    return rect.Position + (rect.Size / 2);
                }
            }

            return null;
        }
    }
}
