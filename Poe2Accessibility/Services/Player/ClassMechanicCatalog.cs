namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json.Linq;

    /// <summary>
    ///     What the game's tables say a class builds up and spends (catalog/class-mechanics.json, `Poe2UiExtract
    ///     class-mechanics`; docs/research/class-mechanics.md): counted resources as Stats pairs current/maximum, limits as
    ///     active/allowed, the names per language of the buffs that are not harmful, and the skill types of each granted
    ///     effect the player's Actor can list. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class ClassMechanicCatalog
    {
        /// <summary>BuffDefinitions type of a flask effect: the flasks are <see cref="FlaskWatch"/>'s.</summary>
        public const int FlaskBuffType = 4;

        private const string Fallback = "en";

        private readonly Dictionary<string, (int Type, Dictionary<string, string> Names)> buffs;
        private readonly Dictionary<string, string[]> skills;

        private ClassMechanicCatalog(
            List<Resource> resources, List<Limit> limits, Dictionary<string, (int, Dictionary<string, string>)> buffs, Dictionary<string, string[]> skills)
        {
            this.Resources = resources;
            this.Limits = limits;
            this.buffs = buffs;
            this.isBuff = buffs.ContainsKey;
            this.skills = skills;
        }

        /// <summary>A counted resource: its name after "current_" and the Stats keys of its value and maximum (0 when the table has none).</summary>
        public sealed record Resource(string Name, int Current, int Maximum, int VirtualMaximum);

        /// <summary>A limit: its name after "number_of_active_" and the Stats keys of the active and allowed counts.</summary>
        public sealed record Limit(string Name, int Active, int Allowed);

        /// <summary>Gets the catalog in use: empty until loaded.</summary>
        public static ClassMechanicCatalog Current { get; private set; } = Empty();

        public IReadOnlyList<Resource> Resources { get; }

        public IReadOnlyList<Limit> Limits { get; }

        public int BuffCount => this.buffs.Count;

        public int SkillCount => this.skills.Count;

        public static ClassMechanicCatalog Empty() => new(new(), new(), new(StringComparer.Ordinal), new(StringComparer.Ordinal));

        /// <summary>
        ///     The Stats names a resource's maximum can have, as the extractor pairs them (`MaximumPrefixes` in
        ///     tools/Poe2UiExtract/ClassMechanics.cs, the raised one left out): a maximum a skill's level sets rather than a player
        ///     stat is found by them (Rapid Shot's "maximum_number_of_crossbow_heat_stacks", 25 + the gem's level).
        /// </summary>
        public static IEnumerable<string> MaximumNames(string name) => new[] { "maximum_", "max_", "base_maximum_" }.Select(prefix => prefix + name);

        /// <summary>
        ///     The value of the first of <paramref name="names"/> in a stat list and its values, paired by place
        ///     (GrantedEffectStatSetsPerLevel's additional stats); null when it is not there or the lists do not pair.
        /// </summary>
        public static int? StatValue(IReadOnlyList<string?> stats, IReadOnlyList<int> values, IEnumerable<string> names)
        {
            if (stats.Count != values.Count)
            {
                return null;
            }

            foreach (var name in names)
            {
                for (var i = 0; i < stats.Count; i++)
                {
                    if (string.Equals(stats[i], name, StringComparison.Ordinal))
                    {
                        return values[i];
                    }
                }
            }

            return null;
        }

        /// <summary>Loads the catalog if it is there; without it nothing of this domain is said.</summary>
        public static ClassMechanicCatalog Load(string path)
        {
            try
            {
                Current = File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
            {
                Current = Empty();
            }

            return Current;
        }

        public static ClassMechanicCatalog Parse(string json)
        {
            var root = JObject.Parse(json);
            var resources = (root["stats"] as JArray ?? new JArray())
                .Select(entry => new Resource(entry.Value<string>("name") ?? string.Empty, entry.Value<int>("current"), entry.Value<int>("maximum"), entry.Value<int>("virtualMaximum")))
                .Where(resource => resource.Name.Length > 0 && resource.Current > 0)
                .ToList();
            var limits = (root["limits"] as JArray ?? new JArray())
                .Select(entry => new Limit(entry.Value<string>("name") ?? string.Empty, entry.Value<int>("active"), entry.Value<int>("allowed")))
                .Where(limit => limit.Name.Length > 0 && limit.Active > 0 && limit.Allowed > 0)
                .ToList();
            var buffs = new Dictionary<string, (int, Dictionary<string, string>)>(StringComparer.Ordinal);
            foreach (var (id, entry) in root["buffs"] as JObject ?? new JObject())
            {
                var names = (entry?["names"] as JObject ?? new JObject()).Properties().ToDictionary(name => name.Name, name => name.Value.ToString(), StringComparer.Ordinal);
                buffs[id] = (entry?.Value<int>("type") ?? 0, names);
            }

            var skills = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var (id, entry) in root["skills"] as JObject ?? new JObject())
            {
                skills[id] = (entry as JArray ?? new JArray()).Select(type => type.ToString()).ToArray();
            }

            return new ClassMechanicCatalog(resources, limits, buffs, skills);
        }

        /// <summary>The BuffDefinitions type of a named buff; -1 for a buff the catalog does not name.</summary>
        public int BuffType(string buffId) => this.buffs.TryGetValue(this.BuffKey(buffId), out var buff) ? buff.Type : -1;

        /// <summary>The catalog's id for a buff key of the host (<see cref="BuffIds.In"/>): one key per buff for whoever counts it.</summary>
        public string BuffKey(string buffId) => BuffIds.In(buffId, this.isBuff);

        /// <summary>Kept: asked for every buff of the player at each poll.</summary>
        private readonly Func<string, bool> isBuff;

        /// <summary>The game's name for a buff in the language (English when it has none there), empty for any other buff.</summary>
        public string BuffName(string buffId, string language)
        {
            if (!this.buffs.TryGetValue(this.BuffKey(buffId), out var buff))
            {
                return string.Empty;
            }

            return buff.Names.TryGetValue(language, out var name) ? name : buff.Names.GetValueOrDefault(Fallback, string.Empty);
        }

        /// <summary>The ActiveSkillType names of a granted effect ("Banner", "Grenade", "Warcry"…); empty when the catalog lacks it.</summary>
        public IReadOnlyList<string> SkillTypes(string grantedEffectId) => this.skills.GetValueOrDefault(grantedEffectId, Array.Empty<string>());

        public bool SkillHasType(string grantedEffectId, string type) => this.SkillTypes(grantedEffectId).Contains(type, StringComparer.Ordinal);
    }
}
