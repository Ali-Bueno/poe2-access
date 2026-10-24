namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>An enemy the cues may voice: offset from the player on screen (x right, y down, in half-screens) and nearness 0 (far) … 1 (contact).</summary>
    public sealed record CueTarget(uint Id, CueSynth.Layer Layer, float ScreenX, float ScreenY, float Nearness);

    /// <summary>
    ///     D2R reach ping gate: fires once when the nearest enemy comes within reach, then stays quiet until every
    ///     enemy is beyond reach plus the release margin (no ping per step while a fight sways in and out).
    /// </summary>
    public sealed class ReachGate
    {
        private bool inside;

        /// <summary>True when the ping is due; <paramref name="nearestEdge"/> is null when no enemy is in sight.</summary>
        public bool Update(float? nearestEdge, float reach, float release)
        {
            if (nearestEdge is { } edge && edge <= reach)
            {
                var due = !this.inside;
                this.inside = true;
                return due;
            }

            if (nearestEdge is not { } far || far > reach + release)
            {
                this.inside = false;
            }

            return false;
        }

        public void Reset() => this.inside = false;
    }

    /// <summary>
    ///     Which enemy cue to start now (D2R entity-cues design): one stream for the nearest unique, one for the nearest
    ///     elite, the rest for the nearest others, plus one for the nearest friend (NPC or player). A stream pulses faster as its target nears, due streams take turns
    ///     (most overdue first) and starts keep a minimum gap. A stream keeps its cadence when its target changes, so
    ///     the nearest enemy swapping makes no burst.
    /// </summary>
    public sealed class EnemyCueScheduler
    {
        private const long Never = long.MinValue / 2;

        private readonly int maxStreams;
        private readonly long minGapMs;
        private readonly long nearIntervalMs;
        private readonly long farIntervalMs;
        private readonly Dictionary<string, long> lastStart = new();
        private long lastAny = Never;

        public EnemyCueScheduler(int maxStreams, long minGapMs, long nearIntervalMs, long farIntervalMs)
        {
            this.maxStreams = maxStreams;
            this.minGapMs = minGapMs;
            this.nearIntervalMs = nearIntervalMs;
            this.farIntervalMs = farIntervalMs;
        }

        /// <summary>The target whose cue starts now, or null.</summary>
        public CueTarget? Next(long nowMs, IReadOnlyList<CueTarget> targets)
        {
            if (targets.Count == 0 || nowMs - this.lastAny < this.minGapMs)
            {
                return null;
            }

            CueTarget? chosen = null;
            var chosenStream = string.Empty;
            var mostOverdue = long.MinValue;
            foreach (var (stream, target) in this.Streams(targets))
            {
                var last = this.lastStart.TryGetValue(stream, out var at) ? at : Never;
                var overdue = nowMs - last - this.Interval(target.Nearness);
                if (overdue >= 0 && overdue > mostOverdue)
                {
                    mostOverdue = overdue;
                    chosen = target;
                    chosenStream = stream;
                }
            }

            if (chosen != null)
            {
                this.lastStart[chosenStream] = nowMs;
                this.lastAny = nowMs;
            }

            return chosen;
        }

        /// <summary>Silence: the next enemy seen starts at once.</summary>
        public void Reset()
        {
            this.lastStart.Clear();
            this.lastAny = Never;
        }

        public long Interval(float nearness) =>
            this.farIntervalMs - (long)((this.farIntervalMs - this.nearIntervalMs) * Math.Clamp(nearness, 0f, 1f));

        private IEnumerable<(string Stream, CueTarget Target)> Streams(IReadOnlyList<CueTarget> targets)
        {
            var nearestFirst = targets.OrderByDescending(target => target.Nearness).ToList();
            if (nearestFirst.FirstOrDefault(target => target.Layer == CueSynth.Layer.Friend) is { } friend)
            {
                yield return ("friend", friend); // the nearest NPC or player, on top of the enemy streams
            }

            nearestFirst.RemoveAll(target => target.Layer == CueSynth.Layer.Friend);
            var boss = nearestFirst.FirstOrDefault(target => target.Layer == CueSynth.Layer.Boss);
            var elite = nearestFirst.FirstOrDefault(target => target.Layer == CueSynth.Layer.Elite);
            var used = 0;
            if (boss != null && used < this.maxStreams)
            {
                used++;
                yield return ("boss", boss);
            }

            if (elite != null && used < this.maxStreams)
            {
                used++;
                yield return ("elite", elite);
            }

            var rank = 0;
            foreach (var target in nearestFirst)
            {
                if (used >= this.maxStreams)
                {
                    yield break;
                }

                if (ReferenceEquals(target, boss) || ReferenceEquals(target, elite))
                {
                    continue;
                }

                used++;
                yield return ($"rank{rank++}", target);
            }
        }
    }
}
