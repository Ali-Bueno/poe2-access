namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>The honour, water and trial-map counters of <see cref="ScreenNarrator"/>: their labels, news and spoken status.</summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>What the Trial's honour line starts with, empty for none known.</summary>
        public Func<string>? HonourLabel { get; set; }

        /// <summary>What the Trial's sacred water is called, empty for none known.</summary>
        public Func<string>? WaterLabel { get; set; }

        /// <summary>Reads the counters beside the Trial's honour line.</summary>
        public Func<IntPtr, IReadOnlyList<string>>? TrialCountersBeside { get; set; }

        /// <summary>The Trial's honour line on the HUD, null away from a run.</summary>
        private ScreenText? HonourLine(ScreenSnapshot snapshot) =>
            this.inGame && this.HonourLabel?.Invoke() is { Length: > 0 } label
                ? snapshot.Find(this.HudLayer)?.Texts.FirstOrDefault(text => text.Text.StartsWith(label + ":", StringComparison.Ordinal))
                : null;

        /// <summary>Says the Trial's honour as it falls past a mark, cutting in: at nothing the run ends. And as a shrine or a fountain restores it, in its turn.</summary>
        public void CheckHonour(ScreenSnapshot snapshot)
        {
            if (this.HonourLine(snapshot) is not { } line)
            {
                return;
            }

            if (this.honour.Heard(line.Text) is { } percent)
            {
                this.output.Urge(Announcements.HonourLeft(this.HonourLabel!.Invoke(), percent));
            }
            else if (this.honour.Rose is { } risen)
            {
                this.Speak("honour restored", Announcements.HonourRestored(this.HonourLabel!.Invoke(), risen), false);
            }
        }

        /// <summary>Says the sacred water and the keys a run gains, none of it urgent.</summary>
        public void CheckTrialCounters(ScreenSnapshot snapshot)
        {
            if (this.HonourLine(snapshot) is not { } line || TrialHud.Parse(this.TrialCountersBeside?.Invoke(line.Address) ?? Array.Empty<string>()) is not { } counters)
            {
                return;
            }

            var (bronze, silver, gold, water) = this.trialCounters.Heard(counters, Environment.TickCount64);
            var news = new[]
            {
                bronze > 0 ? Announcements.BronzeKey(bronze) : string.Empty,
                silver > 0 ? Announcements.SilverKey(silver) : string.Empty,
                gold > 0 ? Announcements.GoldKey(gold) : string.Empty,
                water is { } amount && this.WaterLabel?.Invoke() is { Length: > 0 } label ? Announcements.SacredWater(label, amount, 0) : string.Empty,
            };
            if (news.Any(part => part.Length > 0))
            {
                this.Speak("trial counters", Announcements.Sentences(news), false);
            }
        }

        /// <summary>A run's honour, water and keys for the vitals readout, empty away from a run.</summary>
        public string TrialStatus(ScreenSnapshot snapshot)
        {
            if (this.HonourLine(snapshot) is not { } line || TrialHud.HonourOf(line.Text) is not { } honour)
            {
                return this.TrialMapCounters(snapshot);
            }

            var parts = new List<string> { Announcements.HonourAmount(this.HonourLabel!.Invoke(), honour.Now, honour.Most) };
            parts.AddRange(this.TrialCounterParts(TrialHud.Parse(this.TrialCountersBeside?.Invoke(line.Address) ?? Array.Empty<string>())));
            parts.AddRange(this.TrialEffectParts(line.Address));
            return Announcements.Sentences(parts);
        }

        /// <summary>The afflictions and boons a run carries, as catalog keys, from the slots beside the counters; null when none are found there.</summary>
        public Func<IntPtr, IReadOnlyList<string>?>? TrialEffectsBeside { get; set; }

        /// <summary>What the run carried at the last looks: a gain is said, the first reading is not.</summary>
        private readonly TrialEffectNews trialEffects = new();

        /// <summary>Each affliction and boon carried, by name and with what it does: the HUD shows an icon a kind and names none.</summary>
        private IEnumerable<string> TrialEffectParts(IntPtr beside) =>
            (this.TrialEffectsBeside?.Invoke(beside) ?? Array.Empty<string>())
            .Select(key => Announcements.TrialEntry(TrialCatalog.KindOf(key), this.Trials.Text(key), this.Trials.About(key)))
            .Where(part => part.Length > 0);

        /// <summary>Says an affliction or boon as the run gains it ("Desventaja: Niebla roja. …"): nothing on screen names it then.</summary>
        public void CheckTrialEffects(ScreenSnapshot snapshot)
        {
            if (this.HonourLine(snapshot) is not { } line || this.TrialEffectsBeside?.Invoke(line.Address) is not { } carried)
            {
                return;
            }

            var news = this.trialEffects.Gained(carried).Select(key => Announcements.TrialEntry(TrialCatalog.KindOf(key), this.Trials.Text(key), this.Trials.About(key))).Where(part => part.Length > 0).ToList();
            if (news.Count > 0)
            {
                this.Speak("trial effects", Announcements.Sentences(news), false);
            }
        }

        /// <summary>Which of the Trial's counters a number's element is (<see cref="UiSemantics.TrialCounterKind"/>), negative for none.</summary>
        public Func<IntPtr, int>? TrialCounterKind { get; set; }

        /// <summary>
        ///     The water and the keys from the window over the game, empty when it shows none: the Trial's map hides the HUD and
        ///     its own counters are bare numbers behind icons ("78, 0, 1, 0", user's map 2026-11-12).
        /// </summary>
        private string TrialMapCounters(ScreenSnapshot snapshot)
        {
            if (!this.inGame || this.TrialCounterKind is not { } kindOf || snapshot.TopInteractive is not { } layer || layer.Address == this.HudLayer)
            {
                return string.Empty;
            }

            var counts = new string?[TrialHud.CounterCount];
            var beside = IntPtr.Zero;
            foreach (var text in layer.Texts.Where(text => IsNumber(text.Text)))
            {
                if (kindOf(text.Address) is >= 0 and var kind && kind < counts.Length)
                {
                    counts[kind] = text.Text;
                    beside = text.Address;
                }
            }

            return counts.All(count => count != null)
                ? Announcements.Sentences(this.TrialCounterParts(TrialHud.Parse(counts!)).Concat(this.TrialEffectParts(beside)))
                : string.Empty;
        }

        private IEnumerable<string> TrialCounterParts(TrialCounters? counters)
        {
            if (counters == null)
            {
                yield break;
            }

            if (this.WaterLabel?.Invoke() is { Length: > 0 } label)
            {
                yield return Announcements.SacredWater(label, counters.Water, counters.WaterToCome);
            }

            yield return Announcements.TrialKeys(counters.Bronze, counters.Silver, counters.Gold);
        }

        /// <summary>The tracker's quests, each its title and the lines under it, as they are said.</summary>
        private static List<string> QuestsOf(IEnumerable<ScreenText> tracker)
        {
            var quests = new List<List<ScreenText>>();
            foreach (var text in tracker)
            {
                if (quests.Count == 0 || text.Name == GameLayout.InGameUi.QuestTitleName)
                {
                    quests.Add(new List<ScreenText>());
                }

                quests[^1].Add(text);
            }

            return quests.Select(Describe).ToList();
        }
    }
}
