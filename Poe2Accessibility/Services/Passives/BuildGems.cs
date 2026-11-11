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

    /// <summary>The build's gems set against what the character has. Pure.</summary>
    public static class BuildGems
    {
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
