namespace Poe2Accessibility.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameReader.Utils;

    /// <summary>The per-area judgements of <see cref="WorldLayoutWatch"/>: stat keys, actors, poise, walkable cells and reactions.</summary>
    public sealed partial class WorldLayoutWatch
    {
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
    }
}
