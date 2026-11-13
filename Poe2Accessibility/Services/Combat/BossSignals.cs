namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>What a boss's own states and buffs say of its fight, beyond its turns (<see cref="BossNews"/>).</summary>
    public enum BossSignalKind
    {
        /// <summary>Its arena changes; <see cref="BossSignal.Key"/> is the kind of change ("burn", "collapse", "change").</summary>
        Arena,

        /// <summary>A buff that empowers or protects it began; the key is the buff's id.</summary>
        Empowered,

        /// <summary>That buff ended.</summary>
        EmpowerEnded,

        /// <summary>A counter of the fight moved; the key is its state, with its value and its top when the files give one.</summary>
        Progress,

        /// <summary>The element of its damage to come; the key is the element, or <see cref="BossMechanicsCatalog.UnknownElement"/>.</summary>
        Element,
    }

    public readonly record struct BossSignal(BossSignalKind Kind, string Key, long Value = 0, int? Max = null);

    /// <summary>
    ///     One boss's signals, each said once as it changes while the fight is on (docs/research/boss-mechanics.md §14: AR,
    ///     EM, MG, EL): what its states hold when first read is where it starts, not news. Pure.
    /// </summary>
    public sealed class BossSignalTracker
    {
        /// <summary>A buff gone this long (ms) has ended; shorter is the game renewing it.</summary>
        public const int BuffHoldMs = 500;

        /// <summary>The same state changes the arena or the element again no sooner than this (ms): a state that flips with each blow is one change.</summary>
        public const int ArenaGapMs = 3000;

        /// <summary>A counter with no known top is said at most this often (ms), with its latest value.</summary>
        public const int ProgressGapMs = 3000;

        /// <summary>A counter with a known top is said as it passes each of this many parts of it.</summary>
        public const int ProgressSteps = 4;

        private readonly Dictionary<string, long> values = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> arenaSaidAt = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> elementSaidAt = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (long Value, long At)> progressSaid = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> empoweredSeenAt = new(StringComparer.Ordinal);

        public IReadOnlyList<BossSignal> Update(BossMechanicsCatalog catalog, string boss, IReadOnlyList<(string Name, long Value)> states,
                                                IReadOnlyCollection<string> buffs, bool engaged, long now)
        {
            var news = new List<BossSignal>();
            foreach (var (state, value) in states)
            {
                var known = this.values.TryGetValue(state, out var before);
                this.values[state] = value;
                if (!engaged)
                {
                    continue;
                }

                if (known && value != before)
                {
                    this.Changed(catalog, boss, state, before, value, now, news);
                }

                this.Count(catalog, boss, state, value, known && value != before, now, news);
            }

            this.Empowering(catalog, boss, buffs, engaged, now, news);
            return news;
        }

        private void Changed(BossMechanicsCatalog catalog, string boss, string state, long before, long value, long now, List<BossSignal> news)
        {
            // Rising from nothing: a state that goes on counting (a burn's progress) changed the arena once.
            if (before == 0 && value != 0 && catalog.ArenaKind(boss, state) is { } kind && now - this.arenaSaidAt.GetValueOrDefault(state, long.MinValue / 2) >= ArenaGapMs)
            {
                this.arenaSaidAt[state] = now;
                news.Add(new BossSignal(BossSignalKind.Arena, kind));
            }

            if (value != 0 && catalog.Element(boss, state, value) is { } element && now - this.elementSaidAt.GetValueOrDefault(state, long.MinValue / 2) >= ArenaGapMs)
            {
                this.elementSaidAt[state] = now;
                news.Add(new BossSignal(BossSignalKind.Element, element, value));
            }
        }

        /// <summary>A counter's value: each part of a known top it passes, or its latest value now and then.</summary>
        private void Count(BossMechanicsCatalog catalog, string boss, string state, long value, bool changed, long now, List<BossSignal> news)
        {
            var (counts, max) = catalog.Progress(boss, state);
            if (!counts)
            {
                return;
            }

            if (!this.progressSaid.TryGetValue(state, out var said))
            {
                // Where the fight finds it: only what it does from here is news.
                this.progressSaid[state] = said = (changed ? long.MinValue : value, long.MinValue / 2);
            }

            if (value == said.Value || value <= 0)
            {
                return;
            }

            var due = max is { } top
                ? Step(value, top) != Step(Math.Max(said.Value, 0), top)
                : now - said.At >= ProgressGapMs;
            if (due)
            {
                this.progressSaid[state] = (value, now);
                news.Add(new BossSignal(BossSignalKind.Progress, state, value, max));
            }
        }

        /// <summary>Which part of its top a value has reached (0 to <see cref="ProgressSteps"/>).</summary>
        private static long Step(long value, int top) => Math.Clamp(value, 0, top) * ProgressSteps / top;

        private void Empowering(BossMechanicsCatalog catalog, string boss, IReadOnlyCollection<string> buffs, bool engaged, long now, List<BossSignal> news)
        {
            foreach (var buff in buffs.Where(buff => catalog.IsEmpowering(boss, buff)))
            {
                if (engaged && !this.empoweredSeenAt.ContainsKey(buff))
                {
                    news.Add(new BossSignal(BossSignalKind.Empowered, buff));
                }

                if (engaged)
                {
                    this.empoweredSeenAt[buff] = now;
                }
            }

            foreach (var ended in this.empoweredSeenAt.Where(pair => now - pair.Value >= BuffHoldMs).Select(pair => pair.Key).ToList())
            {
                this.empoweredSeenAt.Remove(ended);
                news.Add(new BossSignal(BossSignalKind.EmpowerEnded, ended));
            }
        }
    }
}
