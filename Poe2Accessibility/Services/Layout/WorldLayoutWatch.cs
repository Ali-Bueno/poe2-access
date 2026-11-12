namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameReader.Utils;
    using GameOffsets.Objects.Components;

    /// <summary>
    ///     Reports where GameHelper found the area's fields (<see cref="AreaInstanceLayout"/>): a patch moves them nearly
    ///     every time, and a world that stays unread in play must be said, not guessed from silence (docs/compatibility.md).
    /// </summary>
    public sealed class WorldLayoutWatch
    {
        /// <summary>The fields are empty for a moment while an area loads; only a world missing this long is lost.</summary>
        private const long LostAfterMs = 8000;

        /// <summary>A sweep is worth nothing on a world of a few entities: components need something live to be judged on.</summary>
        private const int MinEntities = 10;

        private const long SettleMs = 2000;
        private const long LocateEveryMs = 3000;

        /// <summary>The layout holds for the whole process; a few areas give every struct something live to be judged on.</summary>
        private const int MaxSweeps = 3;

        private const string MonsterFolder = "Metadata/Monsters/";
        private const long WatchActorsEveryMs = 100;
        private const int MaxActorsWatched = 32;
        private const string PoisePart = "poise";
        private const string WalkablePart = "walkable";

        /// <summary>The part the code fields are said under (Poe2AccessibilityCore.Compatibility): the invariants' fields are of those.</summary>
        private const string CodeFieldsPart = "code fields";

        // The heavy stun record's search reaches this far around where it was measured.
        private const int PoiseBlockStart = 0x400;
        private const int PoiseBlockSize = 0x200;

        /// <summary>Readings of a full room with no record before it is said lost; the search goes on.</summary>
        private const int PoiseMissesToSay = 30;

        /// <summary>
        ///     With no record near where measured the whole Actor is searched (its last field measured is the cooldown list at
        ///     0xB20), no more often than this: it reads every actor around in full.
        /// </summary>
        private const int PoiseWideBytes = 0x1000;

        private const long PoiseWideEveryMs = 5000;

        private readonly CompatibilityReport report;
        private readonly PluginLog log;
        private readonly string buildFile;
        private long lostSince = -1;
        private long nextLocateAt;
        private int sweeps;
        private string? survived;
        private bool positionsRetried;
        private bool levelSeen;
        private long sweptArea;
        private long settledAt;
        private AnimationFieldSearch? animation = new();
        private bool reactionJudged;
        private StructHealth? animationSaid;
        private long nextActorsAt;
        private bool poiseJudged;
        private readonly AreaMisses poiseMisses = new(PoiseMissesToSay, PoiseAreasToSay);
        private long nextPoiseWideAt;
        private WalkableCheck? walkable = new();
        private readonly Dictionary<string, StructHealth> invariantsSaid = new(StringComparer.Ordinal);
        private readonly SecondLook lostLook = new();
        private readonly LossWitnesses levelMisses = new();
        private readonly MissCount lostLooks = new(LostLooksToSay);
        private readonly MissCount reactionLost = new(ReactionLostToSay);
        private long recheckAt;
        private int rechecksLeft;
        private readonly MissCount walkableFails = new(WalkableFailsToSay);

        private const int PoiseAreasToSay = 2;

        /// <summary>Looks with no world, <see cref="LocateEveryMs"/> apart after <see cref="LostAfterMs"/>, before the world is said lost.</summary>
        private const int LostLooksToSay = 2;

        /// <summary>Readings in a row that must say the reaction lost (ten a second) before it is said: one bad read of the player is not a moved field.</summary>
        private const int ReactionLostToSay = 5;

        /// <summary>A part a sweep found lost is swept again this long after, in the same area, before it is said (<see cref="SecondLook"/>).</summary>
        private const long RecheckMs = 3000;

        /// <summary>Second looks one area's sweep may ask for: parts that read lost by turns must not keep the sweep running.</summary>
        private const int MaxRechecks = 2;
        private const int WalkableFailsToSay = 2;

        public WorldLayoutWatch(CompatibilityReport report, PluginLog log, string buildFile)
        {
            this.report = report;
            this.log = log;
            this.buildFile = buildFile;
        }

        /// <summary>The game's exe as size and time, set when a game process is found.</summary>
        public string GameBuild { get; set; } = string.Empty;

        /// <summary>Per frame: <paramref name="inGame"/> is whether the player stands in an area.</summary>
        public void Tick(bool inGame)
        {
            if (!inGame)
            {
                this.lostSince = -1;
                this.lostLooks.Clear();
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var layout = area.Layout;
            if (layout.HasWorld && area.AwakeEntities.Count > 0)
            {
                this.lostSince = -1;
                this.lostLooks.Clear();
                this.Report(layout, true);
                this.SweepStructs(area);
                this.WatchActors(area);
                this.JudgeStatKeys(area);
            }
            else if (this.lostSince < 0)
            {
                this.lostSince = Environment.TickCount64;
            }
            else if (Environment.TickCount64 - this.lostSince >= LostAfterMs && Environment.TickCount64 >= this.nextLocateAt)
            {
                // One look says nothing yet (a slow load can leave the state in play with no world a little longer): the
                // second, LocateEveryMs later, with still no world and no area found, is the verdict.
                this.nextLocateAt = Environment.TickCount64 + LocateEveryMs;
                var found = !layout.HasWorld && OffsetHealer.LocateArea();
                if (found || !this.lostLooks.Reached())
                {
                    return;
                }

                if (layout.HasWorld)
                {
                    this.report.Missing("entity lists"); // the fields fit and still nothing lives in them
                }
                else
                {
                    this.Report(layout, false);
                }
            }
        }

        /// <summary>A new game process: everything is judged again.</summary>
        public void Reset()
        {
            (this.lostSince, this.levelSeen, this.sweptArea, this.settledAt, this.sweeps, this.positionsRetried) = (-1, false, 0, 0, 0, false);
            this.animation = new AnimationFieldSearch();
            this.reactionJudged = false;
            this.animationSaid = null;
            this.poiseJudged = false;
            this.poiseMisses.Clear();
            this.nextPoiseWideAt = 0;
            this.invariantsSaid.Clear();
            this.lostLook.Reset();
            this.levelMisses.Clear();
            this.lostLooks.Clear();
            this.reactionLost.Clear();
            this.recheckAt = 0;
            this.walkable = new WalkableCheck();
            this.walkableFails.Clear();
            this.statKeys.Reset();
            this.nextStatKeysAt = 0;
        }

        /// <summary>The part the stat keys belong to: what they read is the character's stats.</summary>
        private const string StatsPart = "stats";

        /// <summary>The key rule is looked at this often until judged: a second reading must come well after the first.</summary>
        private const int StatKeysEveryMs = 3000;

        private readonly StatKeyCheck statKeys = new();
        private long nextStatKeysAt;

        /// <summary>
        ///     Once per game process: the <c>level</c> stat's key must read the level the Player component holds
        ///     (<see cref="StatKeyCheck"/>). It holds, logged; two readings read something else, the stats are said lost, as
        ///     every stat the mod asks for by the catalog's key (rage, resistances, attributes) is then another stat.
        /// </summary>
        private void JudgeStatKeys(AreaInstance area)
        {
            var now = Environment.TickCount64;
            if (this.statKeys.Verdict != StatKeyVerdict.Pending || now < this.nextStatKeysAt)
            {
                return;
            }

            this.nextStatKeysAt = now + StatKeysEveryMs;
            if (!area.Player.TryGetComponent<Player>(out var hero) || !area.Player.TryGetComponent<Stats>(out var stats))
            {
                return;
            }

            var key = GameIds.Current.Stat("level", StatKeyCheck.LevelKey);
            var read = PlayerStats.Value(stats, (GameReader.RemoteEnums.GameStats)key);
            var count = stats.StatsChangedByBuffAndActions.Count + stats.StatsChangedByItems.Count;
            if (!this.statKeys.Observe(hero.Level, read, count))
            {
                return;
            }

            if (this.statKeys.Verdict == StatKeyVerdict.Holds)
            {
                this.log.Info($"Stat keys: key {key} reads the character's level ({read}): a stat's key is its row plus one");
                return;
            }

            this.log.Warn($"Stat keys: key {key} reads {read} where the character's level is {hero.Level}, twice, among {count} stats: a stat's key is no longer its row plus one");
            this.report.Missing(StatsPart);
        }

        /// <summary>Gets the animation search's state, for the dev command "structs".</summary>
        public string AnimationSearchState => this.animation?.Describe() ?? "animation judged";

        /// <summary>Until the animation is judged, a look at what the actors around are doing (<see cref="AnimationFieldSearch"/>).</summary>
        private void WatchActors(AreaInstance area)
        {
            var now = Environment.TickCount64;
            if ((this.animation == null && this.reactionJudged && this.poiseJudged && this.walkable == null) || now < this.nextActorsAt)
            {
                return;
            }

            this.nextActorsAt = now + WatchActorsEveryMs;
            this.JudgeReaction(area);
            this.JudgePoise(area);
            this.JudgeWalkable(area);
            if (this.animation == null)
            {
                return;
            }

            var enemies = area.AwakeEntities.Values
                .Where(EntityCatalog.IsAliveEnemy)
                .Select(entity => entity.TryGetComponent<Actor>(out var actor) ? actor.Address : IntPtr.Zero)
                .Where(address => address != IntPtr.Zero)
                .Take(MaxActorsWatched);
            var player = area.Player.TryGetComponent<Actor>(out var own) ? own.Address : IntPtr.Zero;
            if (OffsetHealer.JudgeAnimation(this.animation, player, enemies) is { } outcome)
            {
                // Lost is said once and the search goes on: the next fight can still find the pair and repair it.
                this.animation = outcome.Health == StructHealth.Lost ? this.animation : null;
                this.Note(outcome);
                if (outcome.Health != this.animationSaid)
                {
                    this.animationSaid = outcome.Health;
                    this.log.Info($"Animation field: {outcome.Health} at +0x{outcome.To:X} {outcome.Detail}");
                }
            }
        }

        /// <summary>Where the heavy stun record stands, by its head in the actors around (<see cref="PoiseSearch"/>).</summary>
        private void JudgePoise(AreaInstance area)
        {
            if (this.poiseJudged)
            {
                return;
            }

            var actors = area.AwakeEntities.Values
                .Where(EntityCatalog.IsAliveEnemy)
                .Select(entity => entity.TryGetComponent<Actor>(out var actor) ? actor.Address : IntPtr.Zero)
                .Where(address => address != IntPtr.Zero)
                .Take(MaxActorsWatched)
                .Select(address => PoiseSearch.Heads(OffsetHealer.ReadBlock(address + PoiseBlockStart, PoiseBlockSize)))
                .ToList();
            if (actors.Count < PoiseSearch.MinActors)
            {
                return;
            }

            var measured = GameLayout.ActorComponent.MeasuredPoiseHead;
            if ((PoiseSearch.Agreed(actors, measured - PoiseBlockStart) ?? this.PoiseFar(area)) is not { } found)
            {
                // One area's monsters can all lack the record (totems, the stun-immune): missing only once a second area agrees.
                // An area is its instance's address and hash: the game can load the next one into the instance it had.
                var here = LossWitnesses.AreaKey(area.Address.ToInt64(), area.AreaHash);
                if (this.poiseMisses.Miss(here))
                {
                    this.report.Missing(PoisePart);
                }

                return;
            }

            var head = found + PoiseBlockStart;
            GameLayout.ActorComponent.PoiseThresholdOffset = head + PoiseSearch.ThresholdFromHead;
            GameLayout.ActorComponent.PoiseOffset = head + PoiseSearch.BuildupFromHead;
            this.poiseJudged = true;
            if (head == measured)
            {
                this.report.Found(PoisePart);
            }
            else
            {
                this.report.Moved(PoisePart, measured, head);
            }
        }

        /// <summary>The heavy stun record anywhere in the actors around, as a place of the block the near search reads; null when none or several.</summary>
        private int? PoiseFar(AreaInstance area)
        {
            var now = Environment.TickCount64;
            if (now < this.nextPoiseWideAt)
            {
                return null;
            }

            this.nextPoiseWideAt = now + PoiseWideEveryMs;
            var actors = area.AwakeEntities.Values
                .Where(EntityCatalog.IsAliveEnemy)
                .Select(entity => entity.TryGetComponent<Actor>(out var actor) ? actor.Address : IntPtr.Zero)
                .Where(address => address != IntPtr.Zero)
                .Take(MaxActorsWatched)
                .Select(address => PoiseSearch.Heads(OffsetHealer.ReadBlock(address, PoiseWideBytes)))
                .ToList();
            return PoiseSearch.AgreedOnly(actors) is { } head ? head - PoiseBlockStart : null;
        }

        /// <summary>The cells the player walks on must read walkable (<see cref="WalkableCheck"/>).</summary>
        private void JudgeWalkable(AreaInstance area)
        {
            if (this.walkable == null || !area.Player.TryGetComponent<Render>(out var render))
            {
                return;
            }

            var (x, y) = ((int)render.GridPosition.X, (int)render.GridPosition.Y);
            var held = this.walkable.Add(x, y, RoutePlanner.IsWalkable(area.GridWalkableData, area.TerrainMetadata.BytesPerRow, x, y));
            if (held == true)
            {
                this.walkable = null;
                this.report.Found(WalkablePart);
            }
            else if (held == false && this.walkableFails.Once())
            {
                // The first cells after a load can be read from the grid of the area left: one failed window is not a verdict.
                this.report.Missing(WalkablePart);
            }
        }

        private void JudgeReaction(AreaInstance area)
        {
            if (this.reactionJudged)
            {
                return;
            }

            var monsters = area.AwakeEntities.Values
                .Where(entity => entity.Path.StartsWith(MonsterFolder, StringComparison.Ordinal))
                .Select(entity => entity.TryGetComponent<Positioned>(out var positioned) ? positioned.Address : IntPtr.Zero)
                .Where(address => address != IntPtr.Zero)
                .Take(MaxActorsWatched)
                .ToList();
            if (OffsetHealer.JudgeReaction(area.Player, monsters) is { } outcome)
            {
                this.reactionJudged = outcome.Health != StructHealth.Lost;
                if (this.reactionJudged)
                {
                    this.reactionLost.Clear();
                }

                if (this.reactionJudged || this.reactionLost.Reached())
                {
                    this.Note(outcome);
                }
            }
        }

        /// <summary>The line that says a patch was survived, once; null otherwise.</summary>
        public string? TakeSurvived()
        {
            var line = this.survived;
            this.survived = null;
            return line;
        }

        /// <summary>Checks the game's structs and logs each outcome (dev command "structs").</summary>
        public void Sweep()
        {
            var started = Environment.TickCount64;
            var outcomes = OffsetHealer.Sweep();

            // A part lost in one sweep is swept once more before it is said; the invariants count entities over areas instead.
            static bool IsInvariant(StructOutcome outcome) => outcome.Name.StartsWith(OffsetHealer.InvariantPrefix, StringComparison.Ordinal);
            var lost = outcomes.Where(outcome => outcome.Health == StructHealth.Lost && !IsInvariant(outcome)).Select(outcome => outcome.Name).ToList();
            var confirmed = this.lostLook.Confirm(lost);
            var unconfirmed = lost.Where(name => !confirmed.Contains(name)).ToList();
            foreach (var outcome in outcomes)
            {
                if (unconfirmed.Contains(outcome.Name) && outcome.Health == StructHealth.Lost)
                {
                    this.log.Info($"Struct {outcome.Name}: lost in this sweep ({outcome.Detail}); swept again before it is said");
                    continue;
                }

                this.Note(outcome);
            }

            this.recheckAt = unconfirmed.Count > 0 && this.rechecksLeft > 0 ? started + RecheckMs : 0;
            this.NoteBuild(outcomes);
            if (!this.positionsRetried && outcomes.Any(outcome => outcome.Name == OffsetHealer.PositionsPart && outcome.Health == StructHealth.NotJudged))
            {
                // The camera moved in this sweep or was not on the player yet: another pass judges the positions through it.
                this.positionsRetried = true;
                this.sweptArea = 0;
                this.sweeps--;
            }

            this.log.Info($"Struct sweep: {outcomes.Count(o => o.Health == StructHealth.Intact)} intact, " +
                          $"{outcomes.Count(o => o.Health == StructHealth.Moved)} moved, {outcomes.Count(o => o.Health == StructHealth.Lost)} lost, " +
                          $"{outcomes.Count(o => o.Health == StructHealth.NotJudged)} not judged, in {Environment.TickCount64 - started} ms");
            GameLayout.ActorComponent.SkillsOffset = OffsetHealer.Follow(typeof(ActorOffset), GameLayout.ActorComponent.MeasuredSkillsOffset);
            GameLayout.ActorComponent.CooldownsOffset = OffsetHealer.Follow(typeof(ActorOffset), GameLayout.ActorComponent.MeasuredCooldownsOffset);
        }

        private void SweepStructs(AreaInstance area)
        {
            var now = Environment.TickCount64;
            if (area.AreaHash.Length == 0)
            {
                return; // the instance is not read yet: swept once it is, under the key it keeps
            }

            // By address and hash: another area loaded into the same instance is another area to sweep.
            var here = LossWitnesses.AreaKey(area.Address.ToInt64(), area.AreaHash);
            if (this.recheckAt != 0 && here == this.sweptArea)
            {
                // The second look at what the last sweep found lost, in the same area.
                if (now >= this.recheckAt && area.AwakeEntities.Count >= MinEntities)
                {
                    this.rechecksLeft--;
                    this.Sweep();
                }

                return;
            }

            // A second look still owed is taken in the next area even when the three sweeps are spent.
            if ((this.sweeps >= MaxSweeps && this.recheckAt == 0) || here == this.sweptArea || area.AwakeEntities.Count < MinEntities)
            {
                this.settledAt = here == this.sweptArea ? this.settledAt : 0;
                return;
            }

            // Components fill in a moment after the area appears: judged on a live world, not a half-read one.
            if (this.settledAt == 0)
            {
                this.settledAt = now;
            }
            else if (now - this.settledAt >= SettleMs)
            {
                this.sweptArea = here;
                this.settledAt = 0;
                this.sweeps++;
                this.rechecksLeft = MaxRechecks;
                this.Sweep();
            }
        }

        /// <summary>
        ///     A build not judged before, with nothing lost here nor in the other checks: the player hears the mod came through
        ///     the patch. With a part missing the build stays unjudged, so the line is said once it is found again.
        /// </summary>
        private void NoteBuild(IReadOnlyList<StructOutcome> outcomes)
        {
            if (this.GameBuild.Length == 0 || this.report.AnyMissing || outcomes.Any(outcome => outcome.Health == StructHealth.Lost))
            {
                return;
            }

            var known = File.Exists(this.buildFile) ? File.ReadAllText(this.buildFile).Trim() : string.Empty;
            if (known != this.GameBuild)
            {
                File.WriteAllText(this.buildFile, this.GameBuild);
                this.survived = known.Length > 0 ? Announcements.GameUpdateSurvived : null;
            }
        }

        private void Note(StructOutcome outcome)
        {
            if (outcome.Name.StartsWith(OffsetHealer.InvariantPrefix, StringComparison.Ordinal))
            {
                this.NoteInvariant(outcome);
                return;
            }

            if (outcome.Health == StructHealth.Lost)
            {
                this.log.Info($"Struct {outcome.Name}: {outcome.Detail}");
            }

            if (outcome.Name == StatsPart && outcome.Health != StructHealth.Lost && this.statKeys.Verdict == StatKeyVerdict.Lost)
            {
                return; // the sweep found the list; what its keys mean is still lost, and stays said
            }

            this.report.Record(outcome);
        }

        /// <summary>
        ///     A live invariant's verdict, logged when it changes. A rule that fails is said as the code fields are: its field is
        ///     one only the game's code places, and the part's wording ("some readings may be wrong") is what it means.
        /// </summary>
        private void NoteInvariant(StructOutcome outcome)
        {
            if (outcome.Health == StructHealth.NotJudged || (this.invariantsSaid.TryGetValue(outcome.Name, out var said) && said == outcome.Health))
            {
                return;
            }

            this.invariantsSaid[outcome.Name] = outcome.Health;
            if (outcome.Health == StructHealth.Lost)
            {
                this.log.Warn($"Layout: {outcome.Name} FAILS ({outcome.Detail}, in two areas or more): its field is not where it is read");
                this.report.Missing(CodeFieldsPart);
            }
            else
            {
                this.log.Info($"Layout: {outcome.Name} holds ({outcome.Detail})");
            }
        }

        private void Report(AreaInstanceLayout layout, bool hasWorld)
        {
            var measured = AreaFields.Measured;
            var (found, now) = (layout.LastFound, layout.Fields);
            this.Part("local player", found.LocalPlayer, measured.LocalPlayer, now.LocalPlayer);
            this.Part("entity lists", found.Entities, measured.Entities, now.Entities);
            this.Part("terrain", found.Terrain, measured.Terrain, now.Terrain);
            if (found.Terrain >= 0)
            {
                var (shape, was) = (layout.TerrainShape, AreaInstanceLayout.MeasuredTerrainShape);
                this.Part("terrain tiles", shape.Record, was.Record, shape.Record);
                this.Part("terrain cells", shape.CellsPerTile, was.CellsPerTile, shape.CellsPerTile);
            }

            // A level found once stays where it is: an area that misses the shape alone says nothing about the build.
            this.levelSeen |= found.Level >= 0;
            if (hasWorld && this.levelSeen)
            {
                this.Part("area level", now.Level, measured.Level, now.Level);
            }
            else if (hasWorld && Core.States.InGameStateObject.CurrentAreaInstance is { AreaHash.Length: > 0 } instance &&
                     this.levelMisses.Against(LossWitnesses.AreaKey(instance.Address.ToInt64(), instance.AreaHash)))
            {
                // Never seen, in two areas: one area alone can miss the level's shape (its vectors) with the field in place.
                this.report.Missing("area level");
            }
        }

        private void Part(string part, int found, int measured, int now)
        {
            if (found < 0)
            {
                this.report.Missing(part);
            }
            else if (now == measured)
            {
                this.report.Found(part);
            }
            else
            {
                this.report.Moved(part, measured, now);
            }
        }
    }
}
