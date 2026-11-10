namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     The Build Planner's gems for <see cref="UiSemantics"/>: the active build's skills and supports set against the
    ///     character's own, said on the Skills panel's rows and sockets, on gem items and in the engraving window
    ///     (docs/research/ui-layers.md#build-planner-gems-055f-2026-11-10).
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The character's gems and the active plan are read again this often.</summary>
        private const int GemHoldingsMs = 1000;

        /// <summary>Control ids of the Skills panel (dump 2026-11-10): a skill row's gem frame and view, a support socket.</summary>
        private const string SkillGemFrame = "skillgem_socket_frame";
        private const string SkillGemView = "skillgem_inventory_view";
        private const string SupportSocket = "new_support_gem_entry";

        /// <summary>A choice of the gem engraving window (dump 2026-10-31).</summary>
        private const string EngraveOption = "new_option";

        /// <summary>A support socket lies this many parents under its skill's row; the gem view this deep in it.</summary>
        private const int SocketRowHops = 3;
        private const int GemViewDepth = 2;

        /// <summary>Skills every character has without a gem of their own (BaseItemTypes ids, 0.5.5f): no build names them.</summary>
        private static readonly string[] GrantedSkillKeys = { "SkillGemPlayerDefault", "SkillGemUnusable" };

        /// <summary>A skill row's name and level sit in this layout (dump 2026-11-10).</summary>
        private const string SkillTitle = "skill_title_layout";

        /// <summary>The inventories that hold skills: the measured ones, and any a row of the Skills panel shows its gem from.</summary>
        private readonly HashSet<InventoryName> skillInventories = GameLayout.SkillGemInventories.Ids.Select(id => (InventoryName)id).ToHashSet();

        /// <summary>The names the Skills panel's rows showed last: a skill a weapon grants (Parry) has no gem of its own to find.</summary>
        private HashSet<string> panelSkills = new(StringComparer.OrdinalIgnoreCase);

        private (long At, GemHoldings Holdings)? gemHoldings;
        private (long At, BuildPlan? Plan, int Level)? gemPlan;

        /// <summary>The active build with gems and the character's level; null without one.</summary>
        private (BuildPlan Plan, int Level)? GemPlan()
        {
            var now = Environment.TickCount64;
            if (this.gemPlan is not { } cached || now - cached.At >= GemHoldingsMs)
            {
                cached = Seats.Asking.TryGetComponent<Player>(out var player)
                    ? (now, this.buildPlans.Active(player.Name), player.Level)
                    : (now, null, 0);
                this.gemPlan = cached;
            }

            return cached.Plan is { Skills.Count: > 0 } plan ? (plan, cached.Level) : null;
        }

        /// <summary>
        ///     The asking player's gems: the skills of the skill inventories (a stash tab seen this session is an inventory too,
        ///     and its gems are no skills), those of the build the Skills panel names without a gem, and the bag's cut gems.
        /// </summary>
        private GemHoldings Holdings()
        {
            var now = Environment.TickCount64;
            if (this.gemHoldings is { } cached && now - cached.At < GemHoldingsMs)
            {
                return cached.Holdings;
            }

            var skills = new List<OwnedSkill>();
            var bag = new HashSet<string>();
            try
            {
                foreach (var (name, address) in this.inventory.Locals?.Of(Seats.Input)?.ToArray() ?? Array.Empty<KeyValuePair<InventoryName, IntPtr>>())
                {
                    if (name != InventoryName.MainInventory1 && !this.skillInventories.Contains(name))
                    {
                        continue;
                    }

                    foreach (var item in ServerInventory.Items(this.reader, address))
                    {
                        var id = this.inventory.ItemBaseId(item.Entity);
                        if (!BuildGems.IsGemId(id))
                        {
                            continue;
                        }

                        if (name == InventoryName.MainInventory1)
                        {
                            bag.Add(BuildGems.KeyOf(id));
                        }
                        else
                        {
                            skills.Add(new OwnedSkill(BuildGems.KeyOf(id), this.SocketedSupports(item.Entity)));
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // The host rebuilt its inventory list meanwhile: the gems as last read.
                return this.gemHoldings?.Holdings ?? GemHoldings.None;
            }

            foreach (var granted in this.GemPlan()?.Plan.Skills.Select(skill => skill.Gem.Key).Distinct() ?? Enumerable.Empty<string>())
            {
                if (skills.All(skill => skill.Key != granted) && this.panelSkills.Contains(GemNames.Of(granted)))
                {
                    skills.Add(new OwnedSkill(granted, Array.Empty<string>()));
                }
            }

            var holdings = new GemHoldings(skills, bag);
            this.gemHoldings = (now, holdings);
            return holdings;
        }

        /// <summary>The support gems socketed in a skill gem item (<see cref="GameLayout.GemSockets"/>), as gem keys.</summary>
        private IReadOnlyList<string> SocketedSupports(IntPtr skillGem)
        {
            var keys = new List<string>();
            if (!this.inventory.ItemComponents(skillGem).TryGetValue(GameLayout.GemSockets.Component, out var sockets) || sockets == IntPtr.Zero)
            {
                return keys;
            }

            for (var at = GameLayout.GemSockets.FirstOffset; at < GameLayout.GemSockets.EndOffset; at += IntPtr.Size)
            {
                if (this.reader.TryRead<IntPtr>(sockets + at, out var gem) && gem != IntPtr.Zero && this.reader.IsValidAddress(gem) &&
                    this.inventory.ItemBaseId(gem) is var id && BuildGems.IsGemId(id) && BuildGems.IsSupport(BuildGems.KeyOf(id)))
                {
                    keys.Add(BuildGems.KeyOf(id));
                }
            }

            return keys;
        }

        /// <summary>
        ///     A node of the Skills panel or the engraving window with the Build Planner's gems: on the panel, how far along
        ///     the build's gems are; on the row, socket or choice under the cursor, its place in the build.
        /// </summary>
        private AccessibleUiNode WithBuildGems(AccessibleUiNode node, bool hovered)
        {
            if (!this.InGame || hovered || !this.elements.TryReadElement(node.Address, out var info))
            {
                return node;
            }

            var inSkills = node.Context == Announcements.SkillGemPanel;
            var lines = (inSkills ? this.SkillPanelBuildLines(info, node.Text) : info.Name == EngraveOption ? this.EngraveBuildLines(node.Text) : Array.Empty<string>()).ToList();
            var notes = inSkills ? node.PanelNotes.Concat(this.BuildGemSummary()).ToList() : node.PanelNotes;
            if (lines.Count == 0 && notes.Count == node.PanelNotes.Count)
            {
                return node;
            }

            // After the title the row already shows (the focus drops a first line it has said); an empty slot keeps its word.
            var tooltip = node.Tooltip.ToList();
            tooltip.InsertRange(tooltip.Count > 0 && node.Text.Contains(tooltip[0], StringComparison.Ordinal) ? 1 : 0, lines);
            return node.With(node.Text.Length == 0 && lines.Count > 0 ? Announcements.Empty : node.Text, tooltip, notes);
        }

        /// <summary>On entering the Skills panel: how much of the build's gems the character has, and what to get next.</summary>
        private IEnumerable<string> BuildGemSummary()
        {
            if (this.GemPlan() is not { } active)
            {
                yield break;
            }

            var statuses = BuildGems.Status(active.Plan, active.Level, this.Holdings());
            var supports = statuses.SelectMany(status => status.Supports).ToList();
            yield return Announcements.BuildGemCount(statuses.Count(status => status.Owned), statuses.Count,
                supports.Count(support => support.State == GemState.Socketed), supports.Count);
            var (skill, support) = BuildGems.Next(statuses, active.Level);
            if (skill != null)
            {
                yield return Announcements.BuildNextSkill(Leveled(skill, active.Level));
            }

            if (support is { } next)
            {
                yield return Announcements.BuildNextSupport(GemNames.Of(next.Support.Key), GemNames.Of(next.For.Gem.Key));
            }
        }

        /// <summary>What the build says of the focused part of the Skills panel: a skill's row, an empty row, a support socket.</summary>
        private IEnumerable<string> SkillPanelBuildLines(UiElementReader.ElementInfo info, string label)
        {
            if (this.GemPlan() is not { } active)
            {
                return Array.Empty<string>();
            }

            if (info.Name == SupportSocket)
            {
                var row = this.elements.ReadAncestors(info, SocketRowHops).FirstOrDefault(ancestor => this.IsSkillRow(ancestor.Address));
                if (row.Address == IntPtr.Zero)
                {
                    return Array.Empty<string>();
                }

                this.NotePanelSkills(row);
                return this.SocketLines(active, this.RowSkillKey(active, row), this.RowSkillGem(row.Address), label);
            }

            if (!this.IsSkillRow(info.Address))
            {
                return Array.Empty<string>();
            }

            this.NotePanelSkills(info);
            var gem = this.RowSkillGem(info.Address);
            if (gem != IntPtr.Zero)
            {
                return this.GemItemLines(gem);
            }

            // No gem of its own: an empty slot (nothing names it; its title keeps a bare "LVL:"), or a skill a weapon
            // grants, told by its name.
            return label.Length == 0 || label == Announcements.Empty ? this.SkillsToGet(active)
                : this.RowSkillKey(active, info) is { Length: > 0 } key ? this.BuildSkillLines(active, key, null)
                : Array.Empty<string>();
        }

        /// <summary>The texts of a skill row's title ("NIVEL:", "1", "Parada"); none on an empty row.</summary>
        private List<string> RowTitles(in UiElementReader.ElementInfo row) =>
            this.elements.FindNamed(row.Address, SkillTitle, 1) is { } title
                ? this.elements.ReadChildren(title).Select(child => this.elements.TryReadText(child, out var text) ? text.Trim() : string.Empty).Where(text => text.Length > 0).ToList()
                : new List<string>();

        /// <summary>The build's skill a row shows: by its gem, else by the name in its title; empty when the build has none such.</summary>
        private string RowSkillKey((BuildPlan Plan, int Level) active, in UiElementReader.ElementInfo row)
        {
            if (this.RowSkillGem(row.Address) is var gem && gem != IntPtr.Zero)
            {
                return BuildGems.KeyOf(this.inventory.ItemBaseId(gem));
            }

            var titles = this.RowTitles(row);
            return active.Plan.Skills.Where(skill => skill.Gem.ToLevel >= active.Level).Select(skill => skill.Gem.Key)
                .FirstOrDefault(key => titles.Contains(GemNames.Of(key), StringComparer.OrdinalIgnoreCase)) ?? string.Empty;
        }

        /// <summary>Keeps the names the panel's rows show, for the skills that have no gem; what was known of the gems is read again when they change.</summary>
        private void NotePanelSkills(in UiElementReader.ElementInfo row)
        {
            if (!this.elements.TryReadElement(row.Raw.ParentPtr, out var list))
            {
                return;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sibling in this.elements.ReadChildren(list))
            {
                if (this.elements.TryReadElement(sibling, out var other))
                {
                    names.UnionWith(this.RowTitles(other));
                }
            }

            if (!names.SetEquals(this.panelSkills))
            {
                (this.panelSkills, this.gemHoldings) = (names, null);
            }
        }

        private bool IsSkillRow(IntPtr element) =>
            this.elements.TryReadElement(element, out var row) &&
            this.elements.ReadChildren(row).Any(child => this.elements.TryReadElement(child, out var part) && part.Name == SkillGemFrame);

        /// <summary>The skill gem item a row of the Skills panel shows; zero for an empty row.</summary>
        private IntPtr RowSkillGem(IntPtr row)
        {
            if (this.elements.FindNamed(row, SkillGemView, GemViewDepth) is not { } view)
            {
                return IntPtr.Zero;
            }

            if (this.inventory.InventoryOf(view.Address) is { } shown)
            {
                this.skillInventories.Add(shown);
            }

            return this.inventory.ItemEntity(this.inventory.FindItemElement(view.Address));
        }

        /// <summary>What the build says of a gem item: a skill's place in it with its supports, a support's skills, or that it is outside.</summary>
        private IEnumerable<string> GemItemLines(IntPtr entity)
        {
            if (entity == IntPtr.Zero || this.GemPlan() is not { } active)
            {
                yield break;
            }

            var id = this.inventory.ItemBaseId(entity);
            var key = BuildGems.KeyOf(id);
            if (!BuildGems.IsGemId(id) || key.Contains("Uncut", StringComparison.Ordinal) || GrantedSkillKeys.Any(granted => key.StartsWith(granted, StringComparison.Ordinal)))
            {
                yield break;
            }

            if (BuildGems.IsSupport(key))
            {
                var users = BuildGems.SkillsUsing(active.Plan, active.Level, key);
                yield return users.Count > 0 ? Announcements.BuildGemFor(users.Select(skill => GemNames.Of(skill.Gem.Key))) : Announcements.OutsideBuild;
                yield break;
            }

            foreach (var line in this.BuildSkillLines(active, key, new OwnedSkill(key, this.SocketedSupports(entity))))
            {
                yield return line;
            }
        }

        /// <summary>A skill's place in the build with its supports; <paramref name="own"/> is the copy asked about, null for the one the gems hold.</summary>
        private IEnumerable<string> BuildSkillLines((BuildPlan Plan, int Level) active, string key, OwnedSkill? own)
        {
            if (BuildGems.SkillOf(active.Plan, active.Level, key) is not { } planned)
            {
                yield return Announcements.OutsideBuild;
                yield break;
            }

            yield return Announcements.InBuild(false);
            var holdings = this.Holdings();
            var status = BuildGems.StatusOf(planned, active.Level, holdings, own ?? holdings.Skills.FirstOrDefault(held => held.Key == key));
            if (status.Supports.Count > 0)
            {
                yield return Announcements.BuildSupports(status.Supports.Select(support => this.SupportLine(support, active.Level)));
            }
        }

        /// <summary>An empty skill row: the build's skills still to get, those the level allows first.</summary>
        private IEnumerable<string> SkillsToGet((BuildPlan Plan, int Level) active)
        {
            var missing = BuildGems.Status(active.Plan, active.Level, this.Holdings()).Where(status => !status.Owned)
                .Select(status => status.Skill.Gem).OrderBy(gem => gem.FromLevel <= active.Level ? 0 : gem.FromLevel).ToList();
            if (missing.Count > 0)
            {
                yield return Announcements.BuildSkillsToGet(missing.Select(gem => Leveled(gem, active.Level)));
            }
        }

        /// <summary>A support socket of a build skill: the socketed gem's place in the build, or what the build puts in an empty one.</summary>
        private IEnumerable<string> SocketLines((BuildPlan Plan, int Level) active, string skillKey, IntPtr skillGem, string label)
        {
            if (skillKey.Length == 0 || BuildGems.SkillOf(active.Plan, active.Level, skillKey) is not { } planned)
            {
                yield break;
            }

            var holdings = this.Holdings();
            var socketed = skillGem != IntPtr.Zero ? this.SocketedSupports(skillGem)
                : holdings.Skills.FirstOrDefault(held => held.Key == skillKey)?.Supports ?? Array.Empty<string>();
            var status = BuildGems.StatusOf(planned, active.Level, holdings, new OwnedSkill(skillKey, socketed));
            if (socketed.FirstOrDefault(key => string.Equals(GemNames.Of(key), label, StringComparison.OrdinalIgnoreCase)) is { } held)
            {
                // The socket's own gem, told by the name its tooltip gives it.
                var family = BuildGems.FamilyOf(held);
                yield return status.Supports.Any(support => support.Gem.Key == held) ? Announcements.InBuild(false)
                    : status.Supports.FirstOrDefault(support => BuildGems.FamilyOf(support.Gem.Key) == family) is { } tier ? Announcements.BuildAsksFor(GemNames.Of(tier.Gem.Key))
                    : Announcements.OutsideBuild;
                yield break;
            }

            var open = status.Supports.Where(support => support.State != GemState.Socketed).ToList();
            if (open.Count > 0)
            {
                yield return Announcements.BuildSocketWants(open.Select(support => this.SupportLine(support, active.Level)));
            }
        }

        /// <summary>A choice of the engraving window that the build uses, told by its name.</summary>
        private IEnumerable<string> EngraveBuildLines(string label)
        {
            if (this.GemPlan() is not { } active)
            {
                yield break;
            }

            bool Named(BuildGem gem) => gem.ToLevel >= active.Level && string.Equals(GemNames.Of(gem.Key), label, StringComparison.OrdinalIgnoreCase);
            var skills = active.Plan.Skills.Where(skill => skill.Gem.ToLevel >= active.Level).ToList();
            if (skills.Any(skill => Named(skill.Gem)))
            {
                yield return Announcements.InBuild(false);
            }
            else if (skills.Where(skill => skill.Supports.Any(Named)).ToList() is { Count: > 0 } users)
            {
                yield return Announcements.BuildGemFor(users.Select(skill => GemNames.Of(skill.Gem.Key)));
            }
        }

        private string SupportLine(SupportStatus support, int level)
        {
            var name = GemNames.Of(support.Gem.Key);
            return support.State != GemState.Socketed && support.Gem.FromLevel > level
                ? Announcements.GemFromLevel(name, support.Gem.FromLevel)
                : Announcements.BuildSupport(name, support.State, support.Held.Length > 0 ? GemNames.Of(support.Held) : string.Empty);
        }

        private static string Leveled(BuildGem gem, int level) =>
            gem.FromLevel > level ? Announcements.GemFromLevel(GemNames.Of(gem.Key), gem.FromLevel) : GemNames.Of(gem.Key);
    }
}
