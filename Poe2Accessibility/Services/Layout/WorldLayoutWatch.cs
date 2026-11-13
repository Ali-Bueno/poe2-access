namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameReader.Utils;

    /// <summary>
    ///     Reports where GameHelper found the area's fields (<see cref="AreaInstanceLayout"/>): a patch moves them nearly
    ///     every time, and a world that stays unread in play must be said, not guessed from silence (docs/compatibility.md).
    /// </summary>
    public sealed partial class WorldLayoutWatch
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
    }
}
