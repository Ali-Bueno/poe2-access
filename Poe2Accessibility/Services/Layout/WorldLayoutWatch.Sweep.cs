namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using GameReader;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameReader.Utils;
    using GameOffsets.Objects.Components;

    /// <summary>The struct sweep and its report in <see cref="WorldLayoutWatch"/>.</summary>
    public sealed partial class WorldLayoutWatch
    {
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
