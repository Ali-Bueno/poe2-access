namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>The scanner's listing: the entries of the area sorted into categories, kept fresh and sighted, nearest first.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>A place and a listed thing of its kind this near are one (the town well's tile lay 7 units from its entity, 2026-11-10).</summary>
        private const float SamePlaceWorld = 2.5f * StepWorldUnits;

        private static readonly ScanCategory[] Landmarks = { ScanCategory.Exits, ScanCategory.PointsOfInterest, ScanCategory.Waypoints, ScanCategory.Npcs };

        /// <summary>
        ///     Remembered while out of the game's range too, but dropped once the player comes back near the place and
        ///     the game no longer lists them (chest opened, item picked up).
        /// </summary>
        private static readonly ScanCategory[] Loot = { ScanCategory.Chests, ScanCategory.Items };

        /// <summary>The game keeps entities this close to the player awake; a loot entry missing inside it is gone.</summary>
        private const float AwakeWorld = 1000f;

        private const int TrackMs = 250;

        /// <summary>One listing serves every caller for this long: they poll every 250 ms or more, and each rebuild walks every entity.</summary>
        private const int EntriesFreshMs = 100;

        private (long At, IntPtr Area, string Instance, IReadOnlyList<ScanEntry> List)? listed;

        /// <summary>The working collections of a listing, kept for the next; null while one is being made (see <see cref="Entries"/>).</summary>
        private ListingScratch? listing = new();

        /// <summary>What the objective was found to name, between listings (<see cref="QuestMatcher.Memo"/>).</summary>
        private readonly QuestMatcher.Memo questMemo = new();
        private readonly CarriedQuestSteps carriedSteps = new();

        private long trackedAt = long.MinValue / 2;

        /// <summary>
        ///     Living monsters, characters and players in one pass whose Render names judge where the names are (see
        ///     <see cref="LayoutProbe"/>). Gathered over time, the dead adds of a boss fight left freed renders that read
        ///     nothing: "the names of characters and monsters" were reported lost mid-fight (2026-10-24).
        /// </summary>
        private const int RendersToJudge = 15;

        /// <summary>Per frame while playing: the ground around the player counts as seen (unexplored category).</summary>
        public void Track()
        {
            var now = Environment.TickCount64;
            if (now - this.trackedAt < TrackMs)
            {
                return;
            }

            this.trackedAt = now;
            this.Rebind(null);
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (area.AreaHash != this.instance)
            {
                return;
            }

            var row = area.TerrainMetadata.BytesPerRow;
            if (row > 0 && Seats.Followed.TryGetComponent<Render>(out var player))
            {
                this.explored.Visit(new Vector2(player.GridPosition.X, player.GridPosition.Y), row * 2, area.GridWalkableData.Length / row);
            }

            if (this.Memory != null && this.instance.Length > 0 && now - this.savedAt >= SaveExploredMs)
            {
                this.savedAt = now;
                this.Memory.Save(this.instance, this.Snapshot());
            }
        }

        /// <summary>Everything the scanner can name, nearest first.</summary>
        public IReadOnlyList<ScanEntry> Entries()
        {
            this.Rebind(null);
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var now = Environment.TickCount64;
            if (this.listed is { } recent && recent.Area == area.Address && recent.Instance == this.instance && now - recent.At < EntriesFreshMs)
            {
                return recent.List;
            }

            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return Array.Empty<ScanEntry>();
            }

            // Ten listings a second: their working collections are kept, and nothing returned or remembered points into
            // them (the list handed out is built new). A listing asked for from inside one takes collections of its own.
            var scratch = this.listing ?? new ListingScratch();
            this.listing = null;
            try
            {
                return this.List(area, player, now, scratch);
            }
            finally
            {
                scratch.Clear();
                this.listing = scratch;
            }
        }

        private ScanEntry? questExit;

        /// <summary>
        ///     The exit the quest's step leads through, seen or not (a tile the layout names at arrival): what the game's own
        ///     compass points to, as the signal seals' (<see cref="MapHintNarrator"/>); null when the step names none.
        /// </summary>
        public ScanEntry? QuestExit() => this.questExit;

        /// <summary>How many entity paths whose classification failed are logged (one line each).</summary>
        private const int MaxLoggedFaults = 32;

        private readonly HashSet<string> faultedPaths = new(StringComparer.Ordinal);

        private bool TryClassify(Entity entity, Render render, out ScanEntry? entry)
        {
            try
            {
                entry = this.Classify(entity, render);
                return true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                entry = null;
                var path = entity.Path ?? string.Empty;
                if (this.faultedPaths.Count < MaxLoggedFaults && this.faultedPaths.Add(path))
                {
                    this.log.Warn($"Scanner: could not classify {path}: {ex.Message}");
                }

                return false;
            }
        }

        private List<ScanEntry> List(AreaInstance area, Render player, long now, ListingScratch scratch)
        {
            this.bossWays.Clear();
            this.notedTiles.Clear();
            var found = scratch.Found;
            // Known to the game but not yet to the player: only the quest marker may point at them.
            var unseen = scratch.Unseen;
            var renders = scratch.Renders;
            var sampling = this.Fields?.StartPass() ?? false;
            this.filterHides = this.FollowItemFilter && this.GameHidesFilteredItems() == true;
            var town = this.InTown();
            var areaName = this.AreaName();
            foreach (var (_, entity) in area.AwakeEntities)
            {
                if (!entity.IsValid || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                this.CheckLayout(entity, render, renders);
                if (sampling)
                {
                    this.Fields!.Sample(entity, render);
                }

                if (!this.TryClassify(entity, render, out var classified))
                {
                    continue; // one unreadable entity does not take the listing down
                }

                if (classified is not { } entry)
                {
                    this.remembered.Remove(entity.Id); // a story variant left play (Una after the quest step)
                    this.doors.Forget(entity.Id); // opened: the game takes its target away
                    continue;
                }

                if (entry.Category == ScanCategory.Exits && !town && entry.Name == areaName)
                {
                    this.NoteWayBack(entry, area.WorldToGridConvertor);
                }

                var drawn = this.DrawnOnMinimap(entity, entry, render, player, town);
                if (drawn && town && entry.Category == ScanCategory.Npcs && this.Unreachable(render, player))
                {
                    // A town character off screen on ground no walk reaches: the encampment's past Alva, 2,000 cells off
                    // beside the owner's own (2026-11-19, "ni siquiera puedo llegar a ella"); the present's Servi, Oswald, Hooded
                    // One and Alva by the time portal, heard from the past half though their ground was explored (2026-11-20).
                    // Listed again once joined or on screen.
                    this.sighted.Remove(entry.Id);
                    this.remembered.Remove(entry.Id);
                    unseen.Add(entry);
                    continue;
                }

                if (!this.Sighted(entry, render.WorldPosition, area.WorldToGridConvertor, drawn))
                {
                    unseen.Add(entry);
                    continue;
                }

                found[entry.Id] = entry;
                if (entry.Sound == ClosedDoors.Kind)
                {
                    this.doors.Shut(entry.Id, new Vector3(entry.WorldX, entry.WorldY, entry.WorldZ));
                }
                if (Landmarks.Contains(entry.Category) || Loot.Contains(entry.Category))
                {
                    this.remembered[entry.Id] = entry;
                }
            }

            if (sampling)
            {
                this.Fields!.EndPass();
            }

            if (this.Probe != null && renders.Count >= RendersToJudge)
            {
                this.Probe.ProbeRenderNames(renders);
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            scratch.Remembered.AddRange(this.remembered.Values); // a copy: the loop forgets some
            foreach (var landmark in scratch.Remembered)
            {
                if (found.ContainsKey(landmark.Id))
                {
                    continue;
                }

                if ((landmark.CanVanish || landmark.Category == ScanCategory.Npcs) && Vector2.Distance(here, new Vector2(landmark.WorldX, landmark.WorldY)) < AwakeWorld)
                {
                    // Near its place and no longer listed: opened, picked up, closed or used; a character gone on, as Alva in the
                    // past through Utzaal's door, still listed 2 steps off after the game had dropped her (the owner, 2026-11-20).
                    this.remembered.Remove(landmark.Id);
                    continue;
                }

                if (landmark.Id is >= InheritedIdBase and < DeathPlaceId && !KeepInherited(landmark, here, found.Values, unseen))
                {
                    this.remembered.Remove(landmark.Id); // the game's own is listed now, or this instance has none there
                    continue;
                }

                if (town && (landmark.Category == ScanCategory.Npcs || landmark.Kind == ScanEntry.PortalKind) &&
                    this.OnGroundApart(new Vector2(landmark.WorldX, landmark.WorldY) / area.WorldToGridConvertor, new Vector2(player.GridPosition.X, player.GridPosition.Y)))
                {
                    // Remembered from the camp's other half: the present's Servi, Oswald and Hooded One, 2,000 cells off and out of
                    // the game's range, were listed from the past half (the owner, 2026-11-20), and as portals by the memory an
                    // older build saved; no walk reaches a portal there either. Kept for the way back.
                    continue;
                }

                found.Add(landmark.Id, landmark);
            }

            var toCells = area.WorldToGridConvertor;
            var tileLandmarks = this.TileLandmarks(area, player.WorldPosition.Z).ToList();
            var tileRooms = tileLandmarks.Count(entry => entry.Kind == MinimapMarks.BossRoomKind);
            var gameRooms = tileRooms == 0 ? null : found.Values.Where(entry => entry.Kind == MinimapMarks.BossRoomKind && IsEntity(entry))
                .Select(entry => (entry.Subject, entry.Name == Announcements.BossRoomDefeated(entry.Subject))).ToList();
            foreach (var tileLandmark in tileLandmarks)
            {
                var landmark = tileLandmark;
                if (landmark.Kind == MinimapMarks.BossRoomKind && MinimapMarks.RoomDefeated(landmark.Subject, tileRooms, gameRooms!, this.DefeatedBosses()))
                {
                    // The table's label and its reward note are of a boss still to fight: said as slain, the note left out.
                    landmark = landmark with { Name = Announcements.BossRoomDefeated(landmark.Subject) };
                }

                if (landmark.Category == ScanCategory.Exits && ListsExit(found.Values, landmark, toCells))
                {
                    continue; // the exit's own tile beside the game's exit: one place, said once (two "El bosque singular", 2026-11-02)
                }

                if (landmark.Category == ScanCategory.Exits && this.NoteOnExit(found, landmark, toCells))
                {
                    continue;
                }

                var place = new StdTuple3D<float> { X = landmark.WorldX, Y = landmark.WorldY, Z = landmark.WorldZ };
                if (this.Pointed(landmark, player, area.WorldToGridConvertor) || this.Sighted(landmark, place, area.WorldToGridConvertor, this.LayoutRevealed()))
                {
                    found.TryAdd(landmark.Id, landmark);
                }
                else if (landmark.Kind != MinimapMarks.BossRoomKind)
                {
                    // A boss room the minimap neither shows nor points to is unknown to the quest marker too.
                    unseen.Add(landmark);
                }
            }

            // A place anchored on a thing already listed (the well, the stash, a waypoint) is that thing, heard twice.
            var things = scratch.Things;
            foreach (var entry in found.Values)
            {
                if (IsEntity(entry) && entry.Kind.Length > 0)
                {
                    things.Add(entry);
                }
            }

            foreach (var place in this.PlaceEntries?.Invoke(player.WorldPosition.Z) ?? Array.Empty<ScanEntry>())
            {
                var at = new StdTuple3D<float> { X = place.WorldX, Y = place.WorldY, Z = place.WorldZ };
                if (StandsOnOneOf(things, place, at))
                {
                    continue;
                }

                if (this.Sighted(place, at, area.WorldToGridConvertor, false))
                {
                    found.TryAdd(place.Id, place);
                }
            }

            if (this.deathPlace is { } death && death.Area == area.AreaHash)
            {
                found.TryAdd(death.Entry.Id, death.Entry);
            }

            foreach (var way in this.OpenedWayEntries?.Invoke(player.WorldPosition.X, player.WorldPosition.Y) ?? Array.Empty<ScanEntry>())
            {
                found.TryAdd(way.Id, way); // under its entity's id, which lists nothing while it stands open
            }

            foreach (var frontier in this.Unexplored(area, player))
            {
                found.TryAdd(frontier.Id, frontier);
            }

            foreach (var door in this.UnopenedDoors(area, player))
            {
                found.TryAdd(door.Id, door);
            }

            // Ground a carry set apart (a tomb and the land around it): no walk joins the two (docs/research/map-pieces.md).
            var offPiece = this.OffPlayersPiece();
            if (offPiece != null)
            {
                foreach (var id in found.Values.Where(entry => offPiece(GridOf(entry, toCells))).Select(entry => entry.Id).ToList())
                {
                    found.Remove(id);
                }
            }

            var origin = player.WorldPosition;
            var sorted = found.Values.OrderBy(entry => ExplorationMap.Rank(entry.Kind)).ThenBy(entry => Distance(origin, entry)).ToList();
            // The objective is known wherever the game knows it, but said only once seen: the rest is for the player to find,
            // with the lost hint for a last resort (user 2026-11-05, over the marker of 2026-10-24; docs/radar.md).
            var questPool = sorted.Concat(unseen.Where(entry => !found.ContainsKey(entry.Id))).OrderBy(entry => Distance(origin, entry)).ToList();
            var tracker = this.QuestTexts();
            this.tried.NoteQuest(this.QuestObjectives()); // a new quest step may have opened a barrier found shut
            var questLines = this.QuestGoals.QuestLinesOf(tracker, Localization.Language);
            var questTexts = this.carriedSteps.With(tracker, area.AreaHash, this.QuestItemsCarried(), () => questLines);
            this.questTargets = QuestMatcher.Targets(questPool, questTexts, this.AreaName(), this.cameFrom, this.questMemo, this.QuestLeadsTo(questTexts), questLines);
            this.questExit = questPool.FirstOrDefault(entry => entry.Category == ScanCategory.Exits && this.questTargets.Any(target => target.Id == entry.Id));
            var trails = (this.TrailEntries?.Invoke() ?? Array.Empty<ScanEntry>()).ToList();
            if (offPiece != null)
            {
                // Only the goals on other ground: said through the way there when it is known, else as they were (a goal in a
                // tomb not entered yet), so the quest is never left without a goal.
                var away = this.questTargets.Concat(trails).Where(entry => offPiece(GridOf(entry, toCells))).ToList();
                var nearTargets = this.questTargets.Where(entry => !offPiece(GridOf(entry, toCells))).ToList();
                var nearTrails = trails.Where(entry => !offPiece(GridOf(entry, toCells))).ToList();
                if (away.Count == 0 || nearTargets.Count > 0 || nearTrails.Count > 0)
                {
                    (this.questTargets, trails) = (nearTargets, nearTrails);
                }
                else if (this.WayThrough(away, found.Values, player, toCells) is { } through)
                {
                    found.TryAdd(through.Id, through);
                    (this.questTargets, trails) = (new[] { through }, new List<ScanEntry>());
                }
            }

            sorted.InsertRange(0, trails);
            sorted.InsertRange(0, this.questTargets.Where(target => target.QuestMarked || found.ContainsKey(target.Id)));
            this.listed = (now, area.Address, this.instance, sorted);
            return sorted;
        }

        /// <summary>
        ///     The areas next door that the tracker's step leads through, as their exits are named (<see cref="QuestGoalCatalog"/>):
        ///     worked out once for the same texts in the same area.
        /// </summary>
        private IReadOnlyCollection<string> QuestLeadsTo(IReadOnlyList<string> texts)
        {
            var area = this.areaId();
            if (this.leadsTo.Area != area || this.leadsTo.Language != Localization.Language || !this.leadsTo.Texts.SequenceEqual(texts))
            {
                var goalIds = this.QuestGoals.GoalsOf(texts, Localization.Language);
                var names = this.QuestGoals.StepsToward(area, goalIds).Concat(this.QuestGoals.NextDoor(area, this.QuestGoals.TravelGoalsOf(texts, Localization.Language))).Distinct()
                    .Select(this.landmarks.AreaNameOf).Where(name => name.Length > 0).ToList();
                var before = this.leadsTo.Names;
                var goals = goalIds.Concat(this.QuestGoals.TravelGoalsOf(texts, Localization.Language)).Select(this.landmarks.AreaNameOf).Where(name => name.Length > 0).ToList();
                this.leadsTo = (area, Localization.Language, texts.ToArray(), names, goals);
                if (names.Count > 0 && !names.SequenceEqual(before)) // the tracker's texts hold timers: said once per change of step
                {
                    this.log.Info($"Quest step leads through: {string.Join(", ", names)} (from {area})");
                }
            }

            return this.leadsTo.Names;
        }

        /// <summary>Whether a listed place is the quest's: what it asks for, or an exit straight to the area it asks for; not an exit to an area in between.</summary>
        public bool IsQuestGoal(ScanEntry entry, IReadOnlyCollection<uint> questIds) =>
            questIds.Contains(entry.Id) &&
            (entry.Category != ScanCategory.Exits || this.leadsTo.Names.Count == 0 || this.leadsTo.Goals.Contains(entry.Name, StringComparer.OrdinalIgnoreCase));

        /// <summary>Whether the game's own exit of a named exit tile is among the listed (<see cref="SameExit"/>).</summary>
        private static bool ListsExit(Dictionary<uint, ScanEntry>.ValueCollection listed, ScanEntry tile, float worldPerCell)
        {
            foreach (var exit in listed)
            {
                if (exit.Category == ScanCategory.Exits && SameExit(exit, tile, worldPerCell))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Whether a named exit tile's note names a listed game exit on it in more words: that exit takes the note's name, and
        ///     a note leading to the boss marks it (<see cref="BossWay"/>); Holten Estate's "Stairs" were "Boss stairs" (2026-11-26).
        /// </summary>
        private bool NoteOnExit(Dictionary<uint, ScanEntry> found, ScanEntry tile, float worldPerCell)
        {
            var exits = found.Values.Where(entry => entry.Category == ScanCategory.Exits && IsEntity(entry)).ToList();
            var cells = exits.Select(exit => (exit.Name, new Vector2(exit.WorldX, exit.WorldY) / worldPerCell)).ToList();
            var at = MinimapMarks.NotedExit(cells, tile.Name, new Vector2(tile.WorldX, tile.WorldY) / worldPerCell, NoteOnExitCells);
            if (at < 0)
            {
                return false;
            }

            found[exits[at].Id] = exits[at] with { Name = tile.Name };
            this.notedTiles.Add(tile.Id);
            if (this.bossWays.Contains(tile.Id))
            {
                this.bossWays.Add(exits[at].Id);
            }

            return true;
        }

        /// <summary>Whether a place is anchored on a listed thing of its kind (<see cref="SamePlaceWorld"/>).</summary>
        private static bool StandsOnOneOf(List<ScanEntry> things, ScanEntry place, StdTuple3D<float> at)
        {
            foreach (var thing in things)
            {
                if (thing.Kind == place.Kind && Distance(at, thing) <= SamePlaceWorld)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The collections one listing fills and reads before it returns.</summary>
        private sealed class ListingScratch
        {
            public Dictionary<uint, ScanEntry> Found { get; } = new();

            public List<ScanEntry> Unseen { get; } = new();

            public List<IntPtr> Renders { get; } = new();

            public List<ScanEntry> Remembered { get; } = new();

            public List<ScanEntry> Things { get; } = new();

            public void Clear()
            {
                this.Found.Clear();
                this.Unseen.Clear();
                this.Renders.Clear();
                this.Remembered.Clear();
                this.Things.Clear();
            }
        }

        /// <summary>
        ///     What a sighted player knows of: an enemy only while the camera shows it (the minimap draws no monsters),
        ///     other players always (the map draws the party), what the minimap draws an icon for (exits, waypoints,
        ///     checkpoints, NPCs, the stash, shrines) once the game has it, anything else from the moment it is on screen
        ///     or its ground has been explored.
        /// </summary>
        private bool Sighted(ScanEntry entry, StdTuple3D<float> place, float worldToGrid, bool onMinimap)
        {
            if (entry.Category == ScanCategory.Enemies && !onMinimap)
            {
                return ScreenProjection.InView(place);
            }

            if (entry.Category == ScanCategory.Players || this.sighted.Contains(entry.Id))
            {
                return true;
            }

            if (!onMinimap && !ScreenProjection.InView(place) && !this.explored.IsSeen(new Vector2(place.X, place.Y) / worldToGrid))
            {
                return false;
            }

            this.sighted.Add(entry.Id);
            return true;
        }

        /// <summary>
        ///     A boss room the minimap points to before it is seen: an arrow at the minimap's edge while the player is within the
        ///     icon's pointer distance (<see cref="MinimapMarks.CampaignBossPointerCells"/>, <see cref="MinimapMarks.MapBossPointerCells"/>).
        ///     A sighted player follows it to the boss of a maze (user 2026-10-29), so the scanner lists the room from then on.
        /// </summary>
        private bool Pointed(ScanEntry entry, Render player, float worldToGrid)
        {
            if (entry.Kind != MinimapMarks.BossRoomKind ||
                Vector2.Distance(new Vector2(player.GridPosition.X, player.GridPosition.Y), new Vector2(entry.WorldX, entry.WorldY) / worldToGrid) > TileRoomReach(entry))
            {
                return false;
            }

            this.sighted.Add(entry.Id);
            return true;
        }

        /// <summary>The arrow's reach to a boss room known from the area's tiles, which has no icon row to read it from.</summary>
        private static float TileRoomReach(ScanEntry entry) => entry.Id >= ArenaIdBase ? MinimapMarks.MapBossPointerCells : MinimapMarks.CampaignBossPointerCells;

        /// <summary>Not on screen and on a piece of ground the player's does not join.</summary>
        private bool Unreachable(Render render, Render player) =>
            !ScreenProjection.InView(render.WorldPosition) &&
            this.OnGroundApart(new Vector2(render.GridPosition.X, render.GridPosition.Y), new Vector2(player.GridPosition.X, player.GridPosition.Y));

        /// <summary>The entity carries a MinimapIcons.dat icon the minimap draws; out of town a character only within its icon's pointer reach.</summary>
        private bool DrawnOnMinimap(Entity entity, ScanEntry entry, Render render, Render player, bool town)
        {
            if (!entity.TryGetComponent<MinimapIcon>(out var icon) || string.IsNullOrEmpty(icon.IconName))
            {
                return false;
            }

            var character = entry.Category == ScanCategory.Npcs;
            var exit = entry.Category == ScanCategory.Exits && entry.Kind == QuestMatcher.ExitKind && !this.RoomsApart();
            return MinimapPointer.Drawn(
                town,
                character,
                exit,
                Vector2.Distance(new Vector2(player.GridPosition.X, player.GridPosition.Y), new Vector2(render.GridPosition.X, render.GridPosition.Y)),
                town || !(character || exit) ? 0 : MinimapIconLabel.PointerCells(this.memory, icon));
        }

        private static float Distance(StdTuple3D<float> origin, ScanEntry entry) =>
            new Vector2(entry.WorldX - origin.X, entry.WorldY - origin.Y).Length();
    }
}
