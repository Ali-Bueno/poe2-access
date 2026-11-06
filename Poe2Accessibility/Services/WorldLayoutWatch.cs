namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using GameHelper;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameHelper.Utils;
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

        private readonly CompatibilityReport report;
        private readonly PluginLog log;
        private readonly string buildFile;
        private long lostSince = -1;
        private long nextLocateAt;
        private int sweeps;
        private string? survived;
        private bool positionsRetried;
        private bool levelSeen;
        private IntPtr sweptArea;
        private long settledAt;

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
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var layout = area.Layout;
            if (layout.HasWorld && area.AwakeEntities.Count > 0)
            {
                this.lostSince = -1;
                this.Report(layout, true);
                this.SweepStructs(area);
            }
            else if (this.lostSince < 0)
            {
                this.lostSince = Environment.TickCount64;
            }
            else if (Environment.TickCount64 - this.lostSince >= LostAfterMs && Environment.TickCount64 >= this.nextLocateAt)
            {
                this.nextLocateAt = Environment.TickCount64 + LocateEveryMs;
                if (layout.HasWorld)
                {
                    this.report.Missing("entity lists"); // the fields fit and still nothing lives in them
                }
                else if (!OffsetHealer.LocateArea())
                {
                    this.Report(layout, false);
                }
            }
        }

        /// <summary>A new game process: everything is judged again.</summary>
        public void Reset() => (this.lostSince, this.levelSeen, this.sweptArea, this.settledAt, this.sweeps, this.positionsRetried) = (-1, false, IntPtr.Zero, 0, 0, false);

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
            foreach (var outcome in outcomes)
            {
                this.Note(outcome);
            }

            this.NoteBuild(outcomes);
            if (!this.positionsRetried && outcomes.Any(outcome => outcome.Name == OffsetHealer.PositionsPart && outcome.Health == StructHealth.NotJudged))
            {
                // The camera moved in this sweep: another pass judges the positions through it.
                this.positionsRetried = true;
                this.sweptArea = IntPtr.Zero;
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
            if (this.sweeps >= MaxSweeps || area.Address == this.sweptArea || area.AwakeEntities.Count < MinEntities)
            {
                this.settledAt = area.Address == this.sweptArea ? this.settledAt : 0;
                return;
            }

            // Components fill in a moment after the area appears: judged on a live world, not a half-read one.
            var now = Environment.TickCount64;
            if (this.settledAt == 0)
            {
                this.settledAt = now;
            }
            else if (now - this.settledAt >= SettleMs)
            {
                this.sweptArea = area.Address;
                this.settledAt = 0;
                this.sweeps++;
                this.Sweep();
            }
        }

        /// <summary>A build not judged before, with nothing lost: the player hears the mod came through the patch.</summary>
        private void NoteBuild(IReadOnlyList<StructOutcome> outcomes)
        {
            if (this.GameBuild.Length == 0 || outcomes.Any(outcome => outcome.Health == StructHealth.Lost))
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
            switch (outcome.Health)
            {
                case StructHealth.Intact:
                    this.report.Found(outcome.Name);
                    break;
                case StructHealth.Moved:
                    this.report.Moved(outcome.Name, outcome.From, outcome.To);
                    break;
                case StructHealth.Lost:
                    this.log.Info($"Struct {outcome.Name}: {outcome.Detail}");
                    this.report.Missing(outcome.Name);
                    break;
            }
        }

        private void Report(AreaInstanceLayout layout, bool hasWorld)
        {
            var measured = AreaFields.Measured;
            var (found, now) = (layout.LastFound, layout.Fields);
            this.Part("local player", found.LocalPlayer, measured.LocalPlayer, now.LocalPlayer);
            this.Part("entity lists", found.Entities, measured.Entities, now.Entities);
            this.Part("terrain", found.Terrain, measured.Terrain, now.Terrain);

            // A level found once stays where it is: an area that misses the shape alone says nothing about the build.
            this.levelSeen |= found.Level >= 0;
            if (hasWorld)
            {
                this.Part("area level", this.levelSeen ? now.Level : -1, measured.Level, now.Level);
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
