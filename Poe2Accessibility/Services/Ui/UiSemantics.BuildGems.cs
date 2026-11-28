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

        /// <summary>A slot's gem, the skills a meta gem holds and their supports: the socket levels read.</summary>
        private const int MetaDepth = 2;

        /// <summary>Skills every character has without a gem of their own (BaseItemTypes ids, 0.5.5f): no build names them.</summary>
        private static readonly string[] GrantedSkillKeys = { "SkillGemPlayerDefault", "SkillGemUnusable" };

        /// <summary>A skill row's name and level sit in this layout (dump 2026-11-10).</summary>
        private const string SkillTitle = "skill_title_layout";

        /// <summary>The inventories that hold skills: the measured ones, and any a row of the Skills panel shows its gem from.</summary>
        private readonly HashSet<InventoryName> skillInventories = GameLayout.SkillGemInventories.Ids.Select(id => (InventoryName)id).ToHashSet();

        /// <summary>The names the Skills panel's rows showed last: a skill a weapon grants (Parry) has no gem of its own to find.</summary>
        private HashSet<string> panelSkills = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The character whose Skills panel <see cref="panelSkills"/> was read from.</summary>
        private string panelSkillsOf = string.Empty;

        private (long At, string Character, GemHoldings Holdings)? gemHoldings;

        /// <summary>The skills of <see cref="Holdings"/> a weapon grants (Parry), known by the Skills panel's names: their sockets are not read.</summary>
        private HashSet<string> gemlessSkills = new(StringComparer.Ordinal);
        private (long At, string Character, BuildPlan? Plan, int Level)? gemPlan;

        /// <summary>The character whose controller asks: the tree, the gems and the build read are its own. Empty when unknown.</summary>
        private static string AskingCharacter() => Seats.Asking.TryGetComponent<Player>(out var player) ? player.Name : string.Empty;

        /// <summary>The active build with gems and the character's level; null without one.</summary>
        private (BuildPlan Plan, int Level)? GemPlan() => this.ActivePlan() is { } active && active.Plan.Skills.Count > 0 ? active : null;

        /// <summary>The asking character's active build and level; null without a build.</summary>
        private (BuildPlan Plan, int Level)? ActivePlan()
        {
            var now = Environment.TickCount64;
            var character = AskingCharacter();
            if (this.gemPlan is not { } cached || cached.Character != character || now - cached.At >= GemHoldingsMs)
            {
                // A character without a name read has no build of its own to judge its gems by.
                cached = character.Length > 0 && Seats.Asking.TryGetComponent<Player>(out var player)
                    ? (now, character, this.buildPlans.Active(character), player.Level)
                    : (now, character, null, 0);
                this.gemPlan = cached;
            }

            return cached.Plan is { } plan ? (plan, cached.Level) : null;
        }

        /// <summary>
        ///     The asking player's gems: the skills of the skill inventories (a stash tab seen this session is an inventory too,
        ///     and its gems are no skills), those of the build the Skills panel names without a gem, and the bag's cut gems.
        /// </summary>
        private GemHoldings Holdings()
        {
            var now = Environment.TickCount64;
            var character = AskingCharacter();
            if (this.gemHoldings is { } cached && cached.Character == character && now - cached.At < GemHoldingsMs)
            {
                return cached.Holdings;
            }

            if (character != this.panelSkillsOf)
            {
                (this.panelSkills, this.panelSkillsOf) = (new HashSet<string>(StringComparer.OrdinalIgnoreCase), character);
            }

            var slots = new List<HeldGem>();
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
                            slots.Add(this.Held(item.Entity, id, MetaDepth));
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // The host rebuilt its inventory list meanwhile: this character's gems as last read.
                return this.gemHoldings is { } last && last.Character == character ? last.Holdings : GemHoldings.None;
            }

            var skills = BuildGems.Owned(slots, GemIndex.Shared).ToList();
            var gemless = new HashSet<string>(StringComparer.Ordinal);
            foreach (var granted in this.GemPlan()?.Plan.Skills.Select(skill => skill.Gem.Key).Distinct() ?? Enumerable.Empty<string>())
            {
                if (skills.All(skill => skill.Key != granted) && GemNames.RowNames(granted).Any(this.panelSkills.Contains))
                {
                    skills.Add(new OwnedSkill(granted, Array.Empty<string>()));
                    gemless.Add(granted);
                }
            }

            this.gemlessSkills = gemless;

            var holdings = new GemHoldings(skills, bag);
            this.gemHoldings = (now, character, holdings);
            return holdings;
        }

        /// <summary>The gems socketed in a gem item, as gem keys: its supports, and a meta gem's skills.</summary>
        private IReadOnlyList<string> SocketedGems(IntPtr gemItem) => this.Held(gemItem, string.Empty, 1).Socketed.Select(gem => gem.Key).ToList();

        /// <summary>
        ///     A gem item with what its sockets hold (<see cref="GameLayout.Sockets"/>), <paramref name="depth"/> levels down;
        ///     a support's sockets are not read.
        /// </summary>
        private HeldGem Held(IntPtr gemItem, string id, int depth)
        {
            var socketed = new List<HeldGem>();
            if (depth > 0 && this.inventory.ItemComponents(gemItem).TryGetValue(GameLayout.Sockets.Component, out var sockets) && sockets != IntPtr.Zero)
            {
                for (var at = GameLayout.Sockets.FirstOffset; at < GameLayout.Sockets.EndOffset; at += IntPtr.Size)
                {
                    if (this.reader.TryRead<IntPtr>(sockets + at, out var gem) && gem != IntPtr.Zero && this.reader.IsValidAddress(gem) &&
                        this.inventory.ItemBaseId(gem) is var inner && BuildGems.IsGemId(inner))
                    {
                        socketed.Add(this.Held(gem, inner, BuildGems.IsSupport(BuildGems.KeyOf(inner), GemIndex.Shared) ? 0 : depth - 1));
                    }
                }
            }

            return new HeldGem(BuildGems.KeyOf(id), socketed);
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
            var lines = (info.Name == EngraveOption ? this.EngraveBuildLines(node.Text, info) : inSkills ? this.SkillPanelBuildLines(info, node.Text) : Array.Empty<string>()).ToList();
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
            var supports = statuses.SelectMany(status => status.Supports).Where(support => BuildGems.IsSupport(support.Gem.Key, GemIndex.Shared)).ToList();
            yield return Announcements.BuildGemCount(statuses.Count(status => status.Owned), statuses.Count,
                supports.Count(support => support.State == GemState.Socketed), supports.Count);
            var (skill, support) = BuildGems.Next(statuses, active.Level);
            if (skill != null)
            {
                yield return Announcements.BuildNextSkill(Leveled(skill, active.Level));
            }

            if (support is { } next)
            {
                // A meta gem's skill is one of its gems in the build file, not a support.
                yield return BuildGems.IsSupport(next.Support.Key, GemIndex.Shared)
                    ? Announcements.BuildNextSupport(GemNames.Of(next.Support.Key), GemNames.Of(next.For.Gem.Key))
                    : Announcements.BuildNextHeldSkill(GemNames.Of(next.Support.Key), GemNames.Of(next.For.Gem.Key));
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
                .FirstOrDefault(key => GemNames.RowNames(key).Any(name => titles.Contains(name, StringComparer.OrdinalIgnoreCase))) ?? string.Empty;
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

            if (!names.SetEquals(this.panelSkills) || this.panelSkillsOf != AskingCharacter())
            {
                (this.panelSkills, this.panelSkillsOf, this.gemHoldings) = (names, AskingCharacter(), null);
                // Every row's gem inventory, not only the focused row's: the panel's count of socketed supports is said on
                // entering, before the other skills were visited.
                foreach (var sibling in this.elements.ReadChildren(list))
                {
                    this.RowSkillGem(sibling);
                }
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

            if (BuildGems.IsSupport(key, GemIndex.Shared))
            {
                var users = BuildGems.SkillsUsing(active.Plan, active.Level, key);
                yield return users.Count > 0 ? Announcements.BuildGemFor(users.Select(skill => GemNames.Of(skill.Gem.Key))) : Announcements.OutsideBuild;
                yield break;
            }

            foreach (var line in this.BuildSkillLines(active, key, new OwnedSkill(key, this.SocketedGems(entity))))
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
            var owned = own ?? holdings.Skills.FirstOrDefault(held => held.Key == key);
            var groups = BuildGems.Grouped(BuildGems.StatusOf(planned, active.Level, holdings, owned), owned);
            static string Name(SupportStatus support) => GemNames.Of(support.Gem.Key);
            var line = Announcements.BuildSupportGroups(
                groups.Wanted,
                groups.Socketed.Select(Name).ToList(),
                groups.InBag.Select(Name),
                groups.OtherTier.Select(support => Announcements.BuildTierHeld(GemNames.Of(support.Held), Name(support))),
                groups.InOtherSkill.Select(support => Announcements.GemInSkill(Name(support), GemNames.Of(support.Held))),
                groups.Missing.Select(support => Leveled(support.Gem, active.Level)),
                groups.Outside.Select(GemNames.Of),
                planned.Supports.Any(gem => gem.ToLevel >= active.Level && !BuildGems.IsSupport(gem.Key, GemIndex.Shared)));
            if (line.Length > 0)
            {
                yield return line;
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
            var socketed = skillGem != IntPtr.Zero ? this.SocketedGems(skillGem)
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

        /// <summary>
        ///     A choice of the engraving window and the build. A build skill offered: whether the character has it already, an
        ///     uncut gem being spent on the answer (user 2026-11-11). A support on a skill's tab goes into that skill: what that
        ///     skill has of it, and no other skill (tester 2026-11-26: the other skills' "missing" read as something wrong). On
        ///     another tab (Strength Gems), the build skills that use it and those that have it. Nothing is said of having it when
        ///     no skill of the character was read, which is a failed reading and not a character without gems.
        /// </summary>
        private IEnumerable<string> EngraveBuildLines(string label, UiElementReader.ElementInfo option)
        {
            if (this.GemPlan() is not { } active)
            {
                yield break;
            }

            bool Named(BuildGem gem) => gem.ToLevel >= active.Level && BuildGems.Labels(label, GemNames.Of(gem.Key));
            bool Labelled(string key) => BuildGems.Labels(label, GemNames.Of(key));
            static string Name(string key) => GemNames.Of(key);
            var skills = active.Plan.Skills.Where(skill => skill.Gem.ToLevel >= active.Level).ToList();
            var holdings = this.Holdings();
            var read = holdings.Skills.Count > 0;
            if (skills.FirstOrDefault(skill => Named(skill.Gem)) is { } offered)
            {
                yield return Announcements.InBuild(false);
                if (read)
                {
                    yield return Announcements.EngraveSkillHeld(holdings.Skills.Any(held => held.Key == offered.Gem.Key), holdings.Bag.Contains(offered.Gem.Key));
                }

                yield break;
            }

            // The offered gem's own key, by the name the game's files give it: a tier the build does not name is still told.
            var offeredKey = GemIndex.Shared.ByName(label.Split(", ")[0], Localization.Language).FirstOrDefault(gem => gem.Kind == GemKind.Support)?.Key ?? string.Empty;
            if (read && this.EngraveTabSkill(option, holdings) is { Key.Length: > 0 } tab)
            {
                var skill = Name(tab.Key);
                var answer = BuildGems.OnSkillTab(active.Plan, active.Level, tab.Key, Labelled, offeredKey, holdings);
                if (!tab.Readable)
                {
                    // A skill a weapon grants (Parry, Bow Shot): no gem of its own, so what it holds is not read.
                    yield return answer.State is TabSupport.Needs or TabSupport.NeedsOtherTier ? Announcements.InBuildFor(new[] { skill }) : Announcements.NotInBuildFor(skill);
                    yield break;
                }

                yield return answer.State switch
                {
                    TabSupport.Has => Announcements.SkillHasSupport(skill),
                    TabSupport.HasOtherTier => Announcements.SkillHasGem(skill, Name(answer.Held)),
                    TabSupport.Needs => Announcements.SkillNeedsSupport(skill),
                    TabSupport.NeedsOtherTier => Announcements.SkillNeedsGem(skill, Name(answer.Held)),
                    _ => Announcements.NotInBuildFor(skill),
                };
                if (answer.InBag)
                {
                    yield return Announcements.SpareInBag;
                }

                yield break;
            }

            var places = BuildGems.ForOtherTab(active.Plan, active.Level, Labelled, offeredKey, holdings);
            if (places.Count > 0)
            {
                yield return Announcements.InBuildFor(places.Select(place => Name(place.Skill.Gem.Key)).ToList());
                if (places.Where(place => place.Has).Select(place => Name(place.Skill.Gem.Key)).ToList() is { Count: > 0 } holders)
                {
                    yield return Announcements.SkillsHaveIt(holders);
                }
            }
            else if (offeredKey.Length > 0)
            {
                // Only a support: a skill choice of the uncut skill gem window that the build lacks said nothing, and still does.
                yield return Announcements.NotInBuild;
            }
        }

        /// <summary>A choice lies this many parents under the window (scollable_frame > scrollable_frame_contents > new_option); a description text at most this deep.</summary>
        private const int ChoiceWindowHops = 4;
        private const int DescriptionHops = 12;

        /// <summary>The tab and the description shown are the same for every text of one screen change: read once in this while.</summary>
        private const int EngraveMemoMs = 250;

        private (IntPtr Window, long At, string Key, bool Readable) engraveTab;
        private (IntPtr Popup, long At, bool Support) engravePopup;

        /// <summary>
        ///     The skill whose tab of the engraving window a choice is in, by the tab's label, and whether its sockets are read
        ///     (a gem of the character's, not a skill a weapon grants); no key on another tab (Strength Gems, a weapon kind).
        /// </summary>
        private (string Key, bool Readable) EngraveTabSkill(UiElementReader.ElementInfo option, GemHoldings holdings)
        {
            var window = this.elements.ReadAncestors(option, ChoiceWindowHops).FirstOrDefault(ancestor => ancestor.Name == EngraveWindowParts.Window);
            return window.Address != IntPtr.Zero ? this.TabSkillOf(window.Address, holdings) : (string.Empty, false);
        }

        private (string Key, bool Readable) TabSkillOf(IntPtr window, GemHoldings holdings)
        {
            var now = Environment.TickCount64;
            if (this.engraveTab.Window == window && now - this.engraveTab.At < EngraveMemoMs)
            {
                return (this.engraveTab.Key, this.engraveTab.Readable);
            }

            var key = string.Empty;
            var readable = false;
            if (this.elements.FindNamed(window, EngraveWindowParts.Category, 2) is { } label && this.elements.TryReadText(label.Address, out var text) && text.Trim() is { Length: > 0 } tab)
            {
                var held = holdings.Skills.Select(skill => skill.Key)
                    .FirstOrDefault(skill => GemNames.RowNames(skill).Any(name => string.Equals(name, tab, StringComparison.OrdinalIgnoreCase)));
                // A skill a weapon grants that the panel never showed (Bow Shot) still has its gem in the game's files.
                key = held ?? GemIndex.Shared.ByName(tab, Localization.Language).FirstOrDefault(gem => gem.Kind is GemKind.Skill or GemKind.Meta)?.Key ?? string.Empty;
                readable = held != null && !this.gemlessSkills.Contains(held);
            }

            this.engraveTab = (window, now, key, readable);
            return (key, readable);
        }

        /// <summary>What a text of the engraving window is (<see cref="EngraveWindowParts.Classify"/>): a line of a support's description, or anything else.</summary>
        public EngravePart EngravePartOf(IntPtr text)
        {
            if (!this.elements.TryReadElement(text, out var info))
            {
                return EngravePart.Other;
            }

            // Up to the window and no further.
            var chain = new List<string>();
            var popup = IntPtr.Zero;
            var current = info;
            while (chain.Count < DescriptionHops && current.Raw.ParentPtr != IntPtr.Zero && this.elements.TryReadElement(current.Raw.ParentPtr, out current))
            {
                chain.Add(current.Name);
                popup = current.Name == EngraveWindowParts.Description ? current.Address : popup;
                if (current.Name == EngraveWindowParts.Window)
                {
                    break;
                }
            }

            return popup == IntPtr.Zero ? EngravePart.Other : EngraveWindowParts.Classify(chain, this.DescribesSupport(popup));
        }

        /// <summary>Whether the description shown is a support's, by its kind line's markup ("[SupportGem|Support]" in every language).</summary>
        private bool DescribesSupport(IntPtr popup)
        {
            var now = Environment.TickCount64;
            if (this.engravePopup.Popup != popup || now - this.engravePopup.At >= EngraveMemoMs)
            {
                var support = this.elements.FindNamed(popup, EngraveWindowParts.KindLine, 3) is { } kind && this.elements.TryReadText(kind.Address, out _, out var tagged) &&
                              tagged.Contains(EngraveWindowParts.SupportMark, StringComparison.Ordinal);
                this.engravePopup = (popup, now, support);
            }

            return this.engravePopup.Support;
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
