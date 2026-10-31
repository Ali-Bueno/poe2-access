namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>One passive of a build plan: its PassiveSkills id, place in the file, weapon set and the author's note.</summary>
    public sealed record BuildPassive(string Id, int Order, int WeaponSet, string Note);

    /// <summary>
    ///     A Build Planner file (`*.build`, JSON, GGG developer docs "Build Planner" v1): the passives in the author's order
    ///     with their notes, which the game draws beside the node outside the UI tree (2026-10-31). Pure.
    /// </summary>
    public sealed record BuildPlan(string Name, IReadOnlyList<BuildPassive> Passives)
    {
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
                return new BuildPlan(name, list.Where(passive => passive.Id.Length > 0).ToList());
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>A build note as it is read: markup out, and the attribute an attribute node's note shows only by colour named.</summary>
    public static class BuildNotes
    {
        private static readonly Regex Tag = new(@"<[^<>{}]*>\{|\}|<[^<>{}]*>", RegexOptions.Compiled);

        /// <summary>An attribute node's PassiveSkills id ("strength17", "intelligence21_", "attributes65": the +5 to any attribute nodes).</summary>
        public static bool IsAttributeNode(string id) => Regex.IsMatch(id, @"^(strength|dexterity|intelligence|attributes)\d+_*$");

        /// <summary>"&lt;m&gt;{&lt;red&gt;{Strength +5 is recommended}}" → "Strength +5 is recommended".</summary>
        public static string Plain(string markup) => Regex.Replace(Tag.Replace(markup, string.Empty), @"\s+", " ").Trim();

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
