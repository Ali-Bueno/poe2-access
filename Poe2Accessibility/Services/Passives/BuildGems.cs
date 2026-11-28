namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>Where a gem the build asks for is.</summary>
    public enum GemState
    {
        /// <summary>Socketed in the skill the build puts it in.</summary>
        Socketed,

        /// <summary>Cut and lying in the inventory.</summary>
        InBag,

        /// <summary>The skill holds another tier of it ("Rage I" where the build asks for "Rage II").</summary>
        OtherTier,

        /// <summary>Socketed in another skill.</summary>
        InOtherSkill,

        /// <summary>Not owned.</summary>
        Missing,
    }

    /// <summary>A skill gem the character has, with the supports socketed in it (gem keys, <see cref="BuildGem.Key"/>).</summary>
    public sealed record OwnedSkill(string Key, IReadOnlyList<string> Supports);

    /// <summary>A gem item by its key, with what its sockets hold (a meta gem holds skills, and supports for them).</summary>
    public sealed record HeldGem(string Key, IReadOnlyList<HeldGem> Socketed);

    /// <summary>The character's gems: the skills with their supports, and the cut gems lying in the inventory.</summary>
    public sealed record GemHoldings(IReadOnlyList<OwnedSkill> Skills, IReadOnlyCollection<string> Bag)
    {
        public static GemHoldings None { get; } = new(Array.Empty<OwnedSkill>(), Array.Empty<string>());
    }

    /// <summary>One support of a build skill: where it is, and the gem held instead (another tier's key, or the skill it sits in).</summary>
    public sealed record SupportStatus(BuildGem Gem, GemState State, string Held);

    /// <summary>One skill of the build: owned or not, cut but not yet a skill, and its supports.</summary>
    public sealed record SkillStatus(BuildSkill Skill, bool Owned, IReadOnlyList<SupportStatus> Supports);

    /// <summary>
    ///     A build skill's supports sorted by where they are, each group in the build's order, and the supports socketed in
    ///     the skill that the build does not put in it (gem keys).
    /// </summary>
    public sealed record SupportGroups(
        IReadOnlyList<SupportStatus> Socketed,
        IReadOnlyList<SupportStatus> InBag,
        IReadOnlyList<SupportStatus> OtherTier,
        IReadOnlyList<SupportStatus> InOtherSkill,
        IReadOnlyList<SupportStatus> Missing,
        IReadOnlyList<string> Outside)
    {
        /// <summary>The supports the build puts in the skill.</summary>
        public int Wanted => this.Socketed.Count + this.InBag.Count + this.OtherTier.Count + this.InOtherSkill.Count + this.Missing.Count;
    }

    /// <summary>What a skill's tab of the support engraving window has of the support offered there.</summary>
    public enum TabSupport
    {
        /// <summary>The tab's skill has it socketed (the game refuses a second one: "Skill already has this Support socketed").</summary>
        Has,

        /// <summary>The tab's skill holds another tier of it.</summary>
        HasOtherTier,

        /// <summary>The build puts it in the tab's skill, which does not have it.</summary>
        Needs,

        /// <summary>The build puts another tier of it in the tab's skill, which does not have it.</summary>
        NeedsOtherTier,

        /// <summary>The build does not put it in the tab's skill.</summary>
        NotHere,
    }

    /// <summary>A skill tab's answer: the tier held instead (a gem key), and whether a cut copy lies in the inventory.</summary>
    public sealed record TabSupportAnswer(TabSupport State, string Held, bool InBag);

    /// <summary>The build's gems set against what the character has. Pure.</summary>
    public static class BuildGems
    {
        /// <summary>
        ///     A support offered on a skill's tab of the engraving window, which sockets it into that skill: what that skill has
        ///     of it. <paramref name="labelled"/> tells whether the choice's label names a gem key; <paramref name="offeredKey"/>
        ///     is the offered gem's own key when the game's files name it (empty when not), so a tier the build does not name
        ///     (Show All's "Rage I" where it asks for Rage II) is still told by its family.
        /// </summary>
        public static TabSupportAnswer OnSkillTab(BuildPlan plan, int level, string skillKey, Func<string, bool> labelled, string offeredKey, GemHoldings holdings)
        {
            bool Offered(string key) => key == offeredKey || labelled(key);
            var socketed = holdings.Skills.FirstOrDefault(held => held.Key == skillKey)?.Supports ?? Array.Empty<string>();
            if (socketed.Any(Offered))
            {
                return new TabSupportAnswer(TabSupport.Has, string.Empty, false);
            }

            var planned = SkillOf(plan, level, skillKey)?.Supports.Where(support => support.ToLevel >= level).ToList() ?? new List<BuildGem>();
            var wanted = planned.FirstOrDefault(support => Offered(support.Key));
            var family = offeredKey.Length > 0 ? FamilyOf(offeredKey)
                : (wanted ?? plan.Skills.Where(skill => skill.Gem.ToLevel >= level).SelectMany(skill => skill.Supports)
                    .FirstOrDefault(support => support.ToLevel >= level && labelled(support.Key))) is { } named ? FamilyOf(named.Key) : string.Empty;
            if (family.Length > 0 && socketed.FirstOrDefault(key => FamilyOf(key) == family) is { } tier)
            {
                return new TabSupportAnswer(TabSupport.HasOtherTier, tier, false);
            }

            if (wanted != null)
            {
                return new TabSupportAnswer(TabSupport.Needs, string.Empty, holdings.Bag.Contains(wanted.Key));
            }

            return family.Length > 0 && planned.FirstOrDefault(support => FamilyOf(support.Key) == family) is { } other
                ? new TabSupportAnswer(TabSupport.NeedsOtherTier, other.Key, false)
                : new TabSupportAnswer(TabSupport.NotHere, string.Empty, false);
        }

        /// <summary>
        ///     A support offered on a tab that is no skill's (Strength Gems): the build skills that use it or another tier of it,
        ///     each once, and whether each has one of its family socketed.
        /// </summary>
        public static IReadOnlyList<(BuildSkill Skill, bool Has)> ForOtherTab(BuildPlan plan, int level, Func<string, bool> labelled, string offeredKey, GemHoldings holdings)
        {
            var family = offeredKey.Length > 0 ? FamilyOf(offeredKey) : null;
            bool Uses(BuildGem support) => support.ToLevel >= level && (support.Key == offeredKey || labelled(support.Key) || FamilyOf(support.Key) == family);
            return plan.Skills.Where(skill => skill.Gem.ToLevel >= level)
                .Select(skill => (Skill: skill, Support: skill.Supports.FirstOrDefault(Uses)))
                .Where(use => use.Support != null)
                .DistinctBy(use => use.Skill.Gem.Key)
                .Select(use => (use.Skill, holdings.Skills.Any(held => held.Key == use.Skill.Gem.Key && held.Supports.Any(key => FamilyOf(key) == FamilyOf(use.Support!.Key)))))
                .ToList();
        }

        private static readonly Regex Tier = new("(Two|Three|Four|Five|Six)$", RegexOptions.Compiled);

        /// <summary>"Metadata/Items/Gem/SkillGemTwister" → "SkillGemTwister".</summary>
        public static string KeyOf(string id) => id[(id.LastIndexOf('/') + 1)..];

        /// <summary>A skill or support gem's id: gem paths lie under Metadata/Items/Gem/ or /Gems/ (BaseItemTypes, 0.5.5f).</summary>
        public static bool IsGemId(string id) => id.Contains("/Gems/", StringComparison.OrdinalIgnoreCase) || id.Contains("/Gem/", StringComparison.OrdinalIgnoreCase);

        /// <summary>A support gem's key ("SupportGemRageTwo").</summary>
        public static bool IsSupport(string key) => key.StartsWith("SupportGem", StringComparison.Ordinal);

        /// <summary>
        ///     A support gem by the kind the game's files give it; by its key for a gem they lack. Two supports of 0.5.x
        ///     carry no "SupportGem" in their key (SupportOlrothsConviction, SupportEnervation) and read as skills by it.
        /// </summary>
        public static bool IsSupport(string key, GemIndex index) =>
            index.ByBaseItem(key) is { Kind: not GemKind.Other } gem ? gem.Kind == GemKind.Support : IsSupport(key);

        /// <summary>Whether an engraving choice's label is this gem's: its name, alone or before the level ("Destrozahuesos, Nivel 1").</summary>
        public static bool Labels(string label, string name) =>
            name.Length > 0 && label.StartsWith(name, StringComparison.OrdinalIgnoreCase) && (label.Length == name.Length || label[name.Length] == ',');

        /// <summary>The key without its tier ("SupportGemRageTwo" → "SupportGemRage"): tiers of one support share it.</summary>
        public static string FamilyOf(string key) => Tier.Replace(key, string.Empty);

        /// <summary>
        ///     The skill slots' gems as owned skills, each with every gem in its sockets (the build file lists a meta gem's
        ///     skill among its supports), then each skill a meta gem holds that no slot holds, supported by the meta's supports
        ///     too (docs/research/ui-layers.md#a-meta-gems-skills).
        /// </summary>
        public static IReadOnlyList<OwnedSkill> Owned(IReadOnlyList<HeldGem> slots, GemIndex index)
        {
            var owned = slots.Select(slot => new OwnedSkill(slot.Key, slot.Socketed.Select(gem => gem.Key).ToList())).ToList();
            foreach (var slot in slots)
            {
                var supports = slot.Socketed.Where(gem => IsSupport(gem.Key, index)).Select(gem => gem.Key).ToList();
                foreach (var held in slot.Socketed.Where(gem => !IsSupport(gem.Key, index) && owned.All(skill => skill.Key != gem.Key)))
                {
                    owned.Add(new OwnedSkill(held.Key, held.Socketed.Select(gem => gem.Key).Concat(supports).Distinct().ToList()));
                }
            }

            return owned;
        }

        /// <summary>The skills the build uses at this level or later, each set against what the character has.</summary>
        public static IReadOnlyList<SkillStatus> Status(BuildPlan plan, int level, GemHoldings holdings) =>
            plan.Skills.Where(skill => skill.Gem.ToLevel >= level)
                .Select(skill => StatusOf(skill, level, holdings, holdings.Skills.FirstOrDefault(held => held.Key == skill.Gem.Key))).ToList();

        /// <summary>One build skill set against the character's own copy of it (null when not owned) and the rest of the gems.</summary>
        public static SkillStatus StatusOf(BuildSkill skill, int level, GemHoldings holdings, OwnedSkill? owned)
        {
            return new SkillStatus(skill, owned != null,
                skill.Supports.Where(support => support.ToLevel >= level).Select(support => StateOf(support, skill, owned, holdings)).ToList());
        }

        /// <summary>
        ///     A skill's supports by where they are, for a line heard group by group. A socketed gem of another tier than the
        ///     build's belongs to that tier's group, not to the strangers.
        /// </summary>
        public static SupportGroups Grouped(SkillStatus status, OwnedSkill? owned)
        {
            List<SupportStatus> In(GemState state) => status.Supports.Where(support => support.State == state).ToList();
            var otherTier = In(GemState.OtherTier);
            var outside = (owned?.Supports ?? Array.Empty<string>())
                .Where(key => status.Supports.All(support => support.Gem.Key != key) && otherTier.All(support => support.Held != key))
                .Distinct().ToList();
            return new SupportGroups(In(GemState.Socketed), In(GemState.InBag), otherTier, In(GemState.InOtherSkill), In(GemState.Missing), outside);
        }

        /// <summary>The build's skill for a gem the character has, among those still in use at this level.</summary>
        public static BuildSkill? SkillOf(BuildPlan plan, int level, string key) =>
            plan.Skills.FirstOrDefault(skill => skill.Gem.Key == key && skill.Gem.ToLevel >= level);

        /// <summary>The build's skills that socket this support, among those still in use at this level.</summary>
        public static IReadOnlyList<BuildSkill> SkillsUsing(BuildPlan plan, int level, string supportKey) =>
            plan.Skills.Where(skill => skill.Gem.ToLevel >= level && skill.Supports.Any(support => support.Key == supportKey && support.ToLevel >= level)).ToList();

        /// <summary>
        ///     What to get next: the first skill of the build not owned (one the level allows before one it does not, then
        ///     the lowest level), and the first support not socketed in a skill already owned.
        /// </summary>
        public static (BuildGem? Skill, (BuildGem Support, BuildSkill For)? Support) Next(IReadOnlyList<SkillStatus> statuses, int level)
        {
            var skill = statuses.Where(status => !status.Owned).Select(status => status.Skill.Gem)
                .OrderBy(gem => gem.FromLevel <= level ? 0 : gem.FromLevel).FirstOrDefault();
            foreach (var status in statuses.Where(status => status.Owned))
            {
                if (status.Supports.FirstOrDefault(support => support.State != GemState.Socketed && support.Gem.FromLevel <= level) is { } open)
                {
                    return (skill, (open.Gem, status.Skill));
                }
            }

            return (skill, null);
        }

        private static SupportStatus StateOf(BuildGem support, BuildSkill skill, OwnedSkill? owned, GemHoldings holdings)
        {
            var key = support.Key;
            if (owned != null && owned.Supports.Contains(key))
            {
                return new SupportStatus(support, GemState.Socketed, string.Empty);
            }

            if (holdings.Bag.Contains(key))
            {
                return new SupportStatus(support, GemState.InBag, string.Empty);
            }

            var family = FamilyOf(key);
            if ((owned?.Supports ?? Array.Empty<string>()).Concat(holdings.Bag).FirstOrDefault(held => FamilyOf(held) == family) is { } tier)
            {
                return new SupportStatus(support, GemState.OtherTier, tier);
            }

            return holdings.Skills.FirstOrDefault(held => held.Key != skill.Gem.Key && held.Supports.Contains(key)) is { } other
                ? new SupportStatus(support, GemState.InOtherSkill, other.Key)
                : new SupportStatus(support, GemState.Missing, string.Empty);
        }
    }
}
