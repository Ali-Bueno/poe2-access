namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     The class resources the game's tables name (<see cref="ClassMechanicCatalog"/>), beside <see cref="ChargeWatch"/>'s
    ///     charges and Rage: counted stats (Infernal Flame, Demonflame, crossbow Heat, Infusions held), limits (totems and
    ///     minions placed against their allowance) and the stacks of every named buff (Jade, Tailwind, Unbound Fury, Druidic
    ///     Prowess…), each diff-gated by <see cref="ResourceTracker"/>. Said with the charges' switch and speech level.
    /// </summary>
    public sealed class ResourceWatch
    {
        private const int PollMs = 100;
        private const string StatKey = "stat:";
        private const string LimitKey = "limit:";
        private const string BuffKey = "buff:";

        /// <summary>A buff of one stack that lasts less than this is a skill's own effect (Staggering Force, a charm's resistance), not a resource.</summary>
        public const float LongBuffSeconds = 8f;

        private readonly Func<Entity?> subject;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly ResourceTracker tracker = new();
        private readonly Dictionary<string, string> buffNames = new(StringComparer.Ordinal);
        private readonly HashSet<string> spoken = new(StringComparer.Ordinal);
        private readonly HashSet<string> statNames = new(StringComparer.CurrentCultureIgnoreCase);
        private long lastPoll;

        public ResourceWatch(Func<Entity?> subject, IAccessibilityOutputService output, PluginLog log)
        {
            this.subject = subject;
            this.output = output;
            this.log = log;
        }

        /// <summary>
        ///     A maximum the player's skills set at their level (<see cref="SkillStatSets"/>), by the maximum's Stats names: Rapid
        ///     Shot's Heat ("Calor: 8" was said without one, the player stat reading 0). Null in tests.
        /// </summary>
        public Func<IEnumerable<string>, int?>? SkillMaximum { get; set; }

        /// <summary>The catalog read; the shared one unless a test sets another.</summary>
        public Func<ClassMechanicCatalog> Catalog { get; set; } = () => ClassMechanicCatalog.Current;

        /// <summary>What is held now for the vitals report: stats with a count, limits in use, buffs stacked past one.</summary>
        public IEnumerable<string> Held() => this.tracker.Held.Select(this.Describe).Where(text => text.Length > 0);

        /// <summary>A new area or character: what is held now is not news.</summary>
        public void Reset()
        {
            this.tracker.Reset();
            this.spoken.Clear();
        }

        /// <summary>Per frame; <paramref name="reading"/> false while dead, paused or in a menu (nothing is said).</summary>
        public void Tick(bool reading, AccessibilitySettings settings)
        {
            var now = Environment.TickCount64;
            if (!reading || now - this.lastPoll < PollMs || this.subject() is not { } entity)
            {
                return;
            }

            this.lastPoll = now;
            var catalog = this.Catalog();
            var readings = new List<ResourceTracker.Reading>();
            if (entity.TryGetComponent<Stats>(out var stats))
            {
                this.ReadStats(catalog, stats, readings);
            }

            if (entity.TryGetComponent<Buffs>(out var buffs))
            {
                this.ReadBuffs(catalog, buffs, readings);
            }

            foreach (var news in this.tracker.Update(readings, now))
            {
                this.log.Info($"Resource {news.Key}: {news.Change} {news.Count} of {news.Max}");
                if (this.Worth(news) && settings.ChargeSpeech && this.Phrase(news) is { Length: > 0 } text)
                {
                    this.output.Say(SpeechKind.Charges, text);
                }
            }
        }

        /// <summary>A buff's end is news only when its beginning was said: what an area or a character came with ends silently.</summary>
        private bool Worth(ResourceTracker.News news)
        {
            if (!news.Key.StartsWith(BuffKey, StringComparison.Ordinal))
            {
                return true;
            }

            if (news.Change == ResourceChange.Ended)
            {
                return this.spoken.Remove(news.Key);
            }

            this.spoken.Add(news.Key);
            return true;
        }

        private void ReadStats(ClassMechanicCatalog catalog, Stats stats, List<ResourceTracker.Reading> readings)
        {
            this.statNames.Clear();
            foreach (var resource in catalog.Resources.Where(resource => !PlayerCharges.CoversStat(resource.Name)))
            {
                var current = Value(stats, resource.Current);
                var max = Math.Max(Value(stats, resource.VirtualMaximum), Value(stats, resource.Maximum));
                if (max == 0 && current > 0 && this.SkillMaximum?.Invoke(ClassMechanicCatalog.MaximumNames(resource.Name)) is int fromSkill && fromSkill > 0)
                {
                    max = fromSkill;
                }

                if (current > 0 || max > 0)
                {
                    readings.Add(new ResourceTracker.Reading(StatKey + resource.Name, current, max, ResourceTracker.ModeFor(max)));
                    this.statNames.Add(Announcements.ResourceName(resource.Name));
                }
            }

            foreach (var limit in catalog.Limits)
            {
                var allowed = Value(stats, limit.Allowed);
                if (allowed > 0)
                {
                    readings.Add(new ResourceTracker.Reading(LimitKey + limit.Name, Value(stats, limit.Active), allowed, ResourceMode.Edges));
                }
            }
        }

        private void ReadBuffs(ClassMechanicCatalog catalog, Buffs buffs, List<ResourceTracker.Reading> readings)
        {
            var language = Localization.Language;
            foreach (var (id, effect) in buffs.StatusEffects)
            {
                if (PlayerCharges.FromBuffId(id) != null || StatusAilments.FromBuffId(id) != null || catalog.BuffType(id) is -1 or ClassMechanicCatalog.FlaskBuffType)
                {
                    continue;
                }

                var key = BuffKey + BuffIds.Base(id);
                var stacks = PlayerCharges.Held(effect.Charges);
                var name = catalog.BuffName(id, language);
                // A brief single buff is a skill's own effect; a buff a stat already counts (Heat, Demonflame) is told once, with its maximum.
                if ((!this.tracker.IsHeld(key) && stacks <= 1 && effect.TotalTime > 0 && effect.TotalTime < LongBuffSeconds) || this.statNames.Contains(name))
                {
                    continue;
                }

                this.buffNames[key] = name;
                readings.Add(new ResourceTracker.Reading(key, stacks, 0, ResourceMode.Counted));
            }
        }

        private static int Value(Stats stats, int key) => key > 0 ? PlayerStats.Value(stats, (GameStats)key) : 0;

        private string? Phrase(ResourceTracker.News news)
        {
            var name = this.NameOf(news.Key);
            if (news.Key.StartsWith(BuffKey, StringComparison.Ordinal))
            {
                return news.Change switch
                {
                    ResourceChange.Begun => name,
                    ResourceChange.Ended => Announcements.ResourceEnded(name),
                    _ => Announcements.ResourceCount(name, news.Count, 0),
                };
            }

            return news.Change switch
            {
                ResourceChange.Full => Announcements.ResourceFull(name, news.Count),
                ResourceChange.Empty or ResourceChange.Ended => Announcements.ResourceEmpty(name),
                _ => Announcements.ResourceCount(name, news.Count, news.Max),
            };
        }

        private string Describe((string Key, int Count, int Max) held)
        {
            if (held.Key.StartsWith(BuffKey, StringComparison.Ordinal))
            {
                return held.Count > 1 ? Announcements.ResourceCount(this.NameOf(held.Key), held.Count, 0) : string.Empty;
            }

            return Announcements.ResourceCount(this.NameOf(held.Key), held.Count, held.Max);
        }

        private string NameOf(string key) =>
            key.StartsWith(StatKey, StringComparison.Ordinal) ? Announcements.ResourceName(key[StatKey.Length..])
            : key.StartsWith(LimitKey, StringComparison.Ordinal) ? Announcements.LimitName(key[LimitKey.Length..])
            : this.buffNames.GetValueOrDefault(key, key[BuffKey.Length..]);
    }
}
