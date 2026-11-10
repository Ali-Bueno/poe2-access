namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>One passive of a build plan: its PassiveSkills id, place in the file, weapon set and the author's note.</summary>
    public sealed record BuildPassive(string Id, int Order, int WeaponSet, string Note);

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

        /// <summary>Gets the build's skills ("skills": id, level_interval, support_skills), empty when the file has none.</summary>
        public IReadOnlyList<BuildSkill> Skills { get; init; } = Array.Empty<BuildSkill>();

        /// <summary>
        ///     Whether that many marked nodes can be a build: the tree's mark is one byte with no second sign, and moved by a
        ///     patch any byte that often reads 1 would mark the tree; no build takes a quarter of it (docs/research/passive-tree.md).
        /// </summary>
        public static bool MarksPlausible(int marked, int nodes) => marked * 4 <= nodes;

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
                return new BuildPlan(name, list.Where(passive => passive.Id.Length > 0).ToList()) { Skills = ParseSkills(root) };
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
