namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>One passive of a build plan: its PassiveSkills id, place in the file, weapon set and the author's note.</summary>
    public sealed record BuildPassive(string Id, int Order, int WeaponSet, string Note);

    /// <summary>A plan's nodes on the shown tree, each with its zero-based step, and how many of the plan's nodes that tree does not show.</summary>
    public sealed record BuildSteps(IReadOnlyDictionary<string, int> Steps, int NotShown)
    {
        public int Count => this.Steps.Count;

        /// <summary>Gets the ids of the plan's nodes the tree does not show, in the file's order (for the log).</summary>
        public IReadOnlyList<string> NotShownIds { get; init; } = Array.Empty<string>();

        /// <summary>Gets how many of <see cref="NotShown"/> are ascendancy passives: those the tree draws only once the ascendancy is chosen.</summary>
        public int Ascendancy => this.NotShownIds.Count(BuildPlan.IsAscendancyNode);
    }

    /// <summary>One gem of a build plan: its BaseItemTypes id and the character levels it is used at.</summary>
    public sealed record BuildGem(string Id, int FromLevel, int ToLevel)
    {
        /// <summary>The id's last part ("SkillGemTwister"): gem paths lie under Gem/ or Gems/.</summary>
        public string Key => BuildGems.KeyOf(this.Id);
    }

    /// <summary>One skill of a build plan with the supports the author sockets in it, in the file's order.</summary>
    public sealed record BuildSkill(BuildGem Gem, IReadOnlyList<BuildGem> Supports);

    /// <summary>
    ///     A Build Planner file (`*.build`, JSON, GGG developer docs "Build Planner" v1): the passives in the author's order
    ///     with their notes, which the game draws beside the node outside the UI tree (2026-10-31), and the skill gems with
    ///     their supports. Pure.
    /// </summary>
    public sealed record BuildPlan(string Name, IReadOnlyList<BuildPassive> Passives)
    {
        /// <summary>The file's last level when a gem gives no interval.</summary>
        private const int LastLevel = 100;

        /// <summary>The plan's nodes, each once: a file names a node again for a weapon set and for its attribute.</summary>
        private readonly HashSet<string> nodeIds = Passives.Select(passive => passive.Id).ToHashSet(StringComparer.Ordinal);

        /// <summary>Gets the build's skills ("skills": id, level_interval, support_skills), empty when the file has none.</summary>
        public IReadOnlyList<BuildSkill> Skills { get; init; } = Array.Empty<BuildSkill>();

        /// <summary>Gets the build's gear ("inventory_slots": one entry per piece and level interval), empty when the file has none.</summary>
        public IReadOnlyList<BuildGearEntry> Gear { get; init; } = Array.Empty<BuildGearEntry>();

        /// <summary>
        ///     Whether that many marked nodes can be a build: the tree's mark is one byte with no second sign, and moved by a
        ///     patch any byte that often reads 1 would mark the tree; no build takes a quarter of it (docs/research/passive-tree.md).
        /// </summary>
        public static bool MarksPlausible(int marked, int nodes) => marked * 4 <= nodes;

        /// <summary>
        ///     The plan's steps over the nodes the tree shows: each node once, in the file's order. A file names a node again
        ///     for a weapon set and for its attribute, and lists the nodes of a tree not drawn yet (an ascendancy), so it has
        ///     more entries than the tree marks (docs/research/passive-tree.md#the-builds-two-counts).
        /// </summary>
        /// <param name="shown">The PassiveSkills ids of the nodes the tree marks as the build's.</param>
        public BuildSteps StepsOver(IReadOnlySet<string> shown)
        {
            var steps = new Dictionary<string, int>(StringComparer.Ordinal);
            var notShown = new HashSet<string>(StringComparer.Ordinal);
            foreach (var passive in this.Passives)
            {
                if (!shown.Contains(passive.Id))
                {
                    notShown.Add(passive.Id);
                }
                else if (!steps.ContainsKey(passive.Id))
                {
                    steps[passive.Id] = steps.Count;
                }
            }

            return new BuildSteps(steps, notShown.Count) { NotShownIds = this.Passives.Select(passive => passive.Id).Where(notShown.Contains).Distinct().ToList() };
        }

        /// <summary>
        ///     How the plan fits the nodes the tree marks: its nodes among them and its nodes besides, each counted once
        ///     (docs/research/passive-tree.md#which-file-the-marks-are).
        /// </summary>
        public (int Shared, int Others) Fit(IReadOnlyCollection<string> markedIds)
        {
            var shared = this.nodeIds.Count(markedIds.Contains);
            return (shared, this.nodeIds.Count - shared);
        }

        /// <summary>
        ///     An ascendancy's node by its PassiveSkills id: every such row is named "Ascendancy" + class + number + kind
        ///     ("AscendancyMercenary3Notable1_"; the 669 of catalog/passive-tree.json, 0.5.5f, and no other row). yarkey's four
        ///     nodes "not on this tree" were the four of his file's ascendancy, "Mercenary3" (2026-11-10).
        /// </summary>
        public static bool IsAscendancyNode(string id) => id.StartsWith("Ascendancy", StringComparison.Ordinal);

        /// <summary>Null when the text is no build file.</summary>
        public static BuildPlan? Parse(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("passives", out var passives) || passives.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var list = new List<BuildPassive>();
                foreach (var entry in passives.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.String)
                    {
                        list.Add(new BuildPassive(entry.GetString() ?? string.Empty, list.Count, 0, string.Empty));
                    }
                    else if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    {
                        var set = entry.TryGetProperty("weapon_set", out var weaponSet) && weaponSet.TryGetInt32(out var number) ? number : 0;
                        var note = entry.TryGetProperty("additional_text", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() ?? string.Empty : string.Empty;
                        list.Add(new BuildPassive(id.GetString() ?? string.Empty, list.Count, set, note));
                    }
                }

                var name = root.TryGetProperty("name", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() ?? string.Empty : string.Empty;
                return new BuildPlan(name, list.Where(passive => passive.Id.Length > 0).ToList()) { Skills = ParseSkills(root), Gear = BuildGear.Parse(root) };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        ///     The build the game has active for a character: the file path of its "active_builds" entry in the game's
        ///     config ("active_builds=[{"character":"yarkey","path":"file:C:/…/x.build"}]", 0.5.5f); null when it has none
        ///     or the build is not a file (one followed on the website).
        /// </summary>
        public static string? ActivePath(string config, string character)
        {
            const string Key = "active_builds=";
            const string FileScheme = "file:";
            var line = config.Split('\n').Select(text => text.Trim()).FirstOrDefault(text => text.StartsWith(Key, StringComparison.Ordinal));
            if (line == null)
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(line[Key.Length..]);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                foreach (var entry in document.RootElement.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object && Text(entry, "character") == character &&
                        Text(entry, "path") is { } path && path.StartsWith(FileScheme, StringComparison.OrdinalIgnoreCase))
                    {
                        return path[FileScheme.Length..];
                    }
                }
            }
            catch (JsonException)
            {
                // A config being written or of another shape: no active build known.
            }

            return null;
        }

        private static string? Text(JsonElement entry, string name) =>
            entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static IReadOnlyList<BuildSkill> ParseSkills(JsonElement root)
        {
            var skills = new List<BuildSkill>();
            if (!root.TryGetProperty("skills", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return skills;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (ParseGem(entry) is not { } gem)
                {
                    continue;
                }

                var supports = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("support_skills", out var listed) && listed.ValueKind == JsonValueKind.Array
                    ? listed.EnumerateArray().Select(ParseGem).Where(support => support != null).Select(support => support!).ToList()
                    : new List<BuildGem>();
                skills.Add(new BuildSkill(gem, supports));
            }

            return skills;
        }

        private static BuildGem? ParseGem(JsonElement entry)
        {
            var id = entry.ValueKind == JsonValueKind.String ? entry.GetString() : entry.ValueKind == JsonValueKind.Object ? Text(entry, "id") : null;
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var (from, to) = (1, LastLevel);
            if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("level_interval", out var interval) && interval.ValueKind == JsonValueKind.Array &&
                interval.GetArrayLength() == 2 && interval[0].TryGetInt32(out var first) && interval[1].TryGetInt32(out var last))
            {
                (from, to) = (first, last);
            }

            return new BuildGem(id, from, to);
        }
    }

    /// <summary>A build note as it is read: markup out, and the attribute an attribute node's note shows only by colour named.</summary>
    public static class BuildNotes
    {
        /// <summary>A tag with the text it wraps ("&lt;red&gt;{…}"); the text may hold a place the game fills ("{0}"), whose braces are no tag's.</summary>
        private static readonly Regex Wrapped = new(@"<[^<>{}]*>\{((?:[^{}]|\{\d+\})*)\}", RegexOptions.Compiled);

        /// <summary>A tag alone, and a brace left without its pair that is no part of a "{0}".</summary>
        private static readonly Regex Stray = new(@"<[^<>{}]*>\{?|\{(?!\d+\})|(?<!\{\d+)\}", RegexOptions.Compiled);

        /// <summary>An attribute node's PassiveSkills id ("strength17", "intelligence21_", "attributes65": the +5 to any attribute nodes).</summary>
        public static bool IsAttributeNode(string id) => Regex.IsMatch(id, @"^(strength|dexterity|intelligence|attributes)\d+_*$");

        /// <summary>"&lt;m&gt;{&lt;red&gt;{Strength +5 is recommended}}" → "Strength +5 is recommended".</summary>
        public static string Plain(string markup)
        {
            // Innermost first: "<m>{<red>{x}}" loses <red>{…}, then <m>{…}.
            string unwrapped;
            while ((unwrapped = Wrapped.Replace(markup, "$1")) != markup)
            {
                markup = unwrapped;
            }

            return Regex.Replace(Stray.Replace(markup, string.Empty), @"\s+", " ").Trim();
        }

        /// <summary>
        ///     The attribute an attribute node's note points to by colour alone (the game draws strength red, dexterity green,
        ///     intelligence blue): null when the note names one or has none of those colours.
        /// </summary>
        public static Attribute? ColourOnly(string markup, IEnumerable<string> attributeWords)
        {
            var plain = Plain(markup);
            if (attributeWords.Any(word => plain.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var colour = Regex.Match(markup, @"<(red|green|blue|str|dex|int|strength|dexterity|intelligence)>\{", RegexOptions.IgnoreCase);
            return !colour.Success ? null : colour.Groups[1].Value.ToLowerInvariant() switch
            {
                "red" or "str" or "strength" => Attribute.Strength,
                "green" or "dex" or "dexterity" => Attribute.Dexterity,
                _ => Attribute.Intelligence,
            };
        }

        public enum Attribute
        {
            Strength,
            Dexterity,
            Intelligence,
        }
    }
}
