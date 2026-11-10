namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>What a gem is, from its base item's parent path in the game's files.</summary>
    public enum GemKind
    {
        /// <summary>A path the plugin does not know (a kind a patch added).</summary>
        Other,

        /// <summary>ActiveSkillGem: a gem that grants a skill.</summary>
        Skill,

        /// <summary>SupportSkillGem: a gem socketed in a skill.</summary>
        Support,

        /// <summary>MetaSkillGem: a gem that holds and triggers other skills.</summary>
        Meta,
    }

    /// <summary>The attribute a gem's requirement leans on.</summary>
    public enum GemAttribute
    {
        /// <summary>No attribute share at all.</summary>
        None,
        Strength,
        Dexterity,
        Intelligence,

        /// <summary>Two or three attributes share the largest part alike.</summary>
        Mixed,
    }

    /// <summary>
    ///     A gem's identity as <see cref="GemIndex"/> answers it, from the game's files: what the gem is in every copy of the
    ///     game (base name, kind, tags, attribute shares, a support's description), with its texts in a language asked for
    ///     (English when that language has none). Nothing here is a gem in play: its level, quality, sockets, the supports
    ///     linked to it, its damage, cost, cooldown and requirement numbers, met or not, are not in the files' catalog and
    ///     must be read from the game (memory, its tooltip and panel texts), as must the name the game shows when it differs.
    /// </summary>
    public sealed class GemInfo
    {
        /// <summary>The game's own tag of a skill that stays on once used (GemTags Id).</summary>
        public const string PersistentTag = "persistent";

        private readonly GemCatalog catalog;
        private readonly GemCatalog.Gem gem;

        internal GemInfo(GemCatalog catalog, GemCatalog.Gem gem)
        {
            this.catalog = catalog;
            this.gem = gem;
            this.Key = gem.Id[(gem.Id.LastIndexOf('/') + 1)..];
            this.Kind = gem.Kind switch
            {
                "ActiveSkillGem" => GemKind.Skill,
                "SupportSkillGem" => GemKind.Support,
                "MetaSkillGem" => GemKind.Meta,
                _ => GemKind.Other,
            };
            var largest = Math.Max(gem.Strength, Math.Max(gem.Dexterity, gem.Intelligence));
            var leading = new[] { (gem.Strength, GemAttribute.Strength), (gem.Dexterity, GemAttribute.Dexterity), (gem.Intelligence, GemAttribute.Intelligence) }
                .Where(share => share.Item1 == largest).Select(share => share.Item2).ToList();
            this.Attribute = largest <= 0 ? GemAttribute.None : leading.Count == 1 ? leading[0] : GemAttribute.Mixed;
        }

        /// <summary>Gets the gem's BaseItemTypes Id ("Metadata/Items/Gem/SkillGemIceNova").</summary>
        public string Id => this.gem.Id;

        /// <summary>Gets the Id's last part ("SkillGemIceNova"), the key a build plan names a gem by (<see cref="GemNames"/>).</summary>
        public string Key { get; }

        public GemKind Kind { get; }

        /// <summary>Gets the attribute with the largest share of the gem's requirement.</summary>
        public GemAttribute Attribute { get; }

        /// <summary>Gets the shares (0 to 100) of the gem's attribute requirement.</summary>
        public (int Strength, int Dexterity, int Intelligence) Shares => (this.gem.Strength, this.gem.Dexterity, this.gem.Intelligence);

        /// <summary>Gets a value indicating whether the gem carries the game's Persistent tag; the files hold no "spirit gem" kind.</summary>
        public bool IsPersistent => this.gem.Tags.Contains(PersistentTag);

        /// <summary>Gets the gem's GemTags Ids ("cold", "nova"), the unnamed ones too ("grants_active_skill").</summary>
        public IReadOnlyList<string> TagIds => this.gem.Tags;

        /// <summary>Gets the gem's GemEffects Ids.</summary>
        public IReadOnlyList<string> EffectIds => this.gem.Effects;

        /// <summary>Gets the GrantedEffects Ids of the skills the gem grants (a support's own effect too).</summary>
        public IReadOnlyList<string> SkillIds => this.gem.Skills;

        /// <summary>
        ///     The gem's base name, markup out (Thai names carry a size tag); empty when the files name none. Three names in
        ///     0.5.x are patterns the game fills in ("Spectre: {0}"): see <see cref="NameIsPattern"/>.
        /// </summary>
        public string Name(string language) => PlainName(this.catalog.GemName(this.gem.Id, language) ?? string.Empty);

        /// <summary>
        ///     The names the game shows for the skills the gem grants (the files' own skill names, not the gem's), in the
        ///     language, English when it has none; the unnamed ones left out.
        /// </summary>
        public IReadOnlyList<string> SkillNames(string language) =>
            this.gem.Skills.Select(skill => this.catalog.SkillName(skill, language) ?? string.Empty).Where(name => name.Length > 0).Select(PlainName).ToList();

        /// <summary>Whether the name holds a place the game fills in play (the monster a Spectre gem holds): the name shown is then the game's to read.</summary>
        public bool NameIsPattern(string language) => this.Name(language).Contains(Placeholder, StringComparison.Ordinal);

        private const string Placeholder = "{0}";

        /// <summary>A text tag with its braces ("&lt;size:37&gt;{…}") or alone; a placeholder's braces are no tag's.</summary>
        private static readonly Regex Wrapped = new(@"<[^<>{}]*>\{([^{}]*)\}|<[^<>{}]*>", RegexOptions.Compiled);

        /// <summary>A name of the files without its text tags, spaces trimmed.</summary>
        internal static string PlainName(string name) => Wrapped.Replace(name, "$1").Trim();

        /// <summary>The names of the tags the game shows on the gem ("Hechizo", "Hielo"), in the gem's own order.</summary>
        public IReadOnlyList<string> Tags(string language) =>
            this.gem.Tags.Select(tag => this.catalog.TagName(tag, language)).OfType<string>().Where(name => name.Length > 0).ToList();

        /// <summary>What a support does ("Asiste cualquier habilidad que impacte…"); empty for a gem with no such text.</summary>
        public string SupportText(string language) => this.catalog.SupportText(this.gem.Id, language) ?? string.Empty;
    }

    /// <summary>
    ///     The gem behind whichever Id a reader has in hand: its base item Id or that Id's last part, a GemEffects Id, a
    ///     GrantedEffects Id, or its name as the game shows it. Built once over a <see cref="GemCatalog"/>; pure. It answers
    ///     identity only (<see cref="GemInfo"/>): what changes in play is the game's to say.
    /// </summary>
    public sealed class GemIndex
    {
        private static GemIndex? shared;

        private readonly GemCatalog catalog;
        private readonly Dictionary<string, GemInfo> byId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GemInfo> byKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GemInfo> byEffect = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GemInfo> bySkill = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, List<GemInfo>>> byName = new(StringComparer.Ordinal);

        public GemIndex(GemCatalog catalog)
        {
            this.catalog = catalog;
            foreach (var gem in catalog.Gems)
            {
                // Two gems can share an effect or a skill (a unique's copy, unused gems on one placeholder skill; 5 Ids in 0.5.x): the first in Id order answers.
                var info = new GemInfo(catalog, gem);
                this.byId.TryAdd(info.Id, info);
                this.byKey.TryAdd(info.Key, info);
                foreach (var effect in gem.Effects)
                {
                    this.byEffect.TryAdd(effect, info);
                }

                foreach (var skill in gem.Skills)
                {
                    this.bySkill.TryAdd(skill, info);
                }
            }
        }

        /// <summary>Gets the index over the catalog the plugin loaded (<see cref="GemCatalog.Shared"/>); empty without one.</summary>
        public static GemIndex Shared => shared is { } index && ReferenceEquals(index.catalog, GemCatalog.Shared) ? index : shared = new GemIndex(GemCatalog.Shared);

        public int Count => this.byId.Count;

        /// <summary>By BaseItemTypes Id, whole ("Metadata/Items/Gems/SupportGemRageTwo") or its last part; case ignored, as the paths' is mixed.</summary>
        public GemInfo? ByBaseItem(string idOrKey) =>
            this.byId.GetValueOrDefault(idOrKey) ?? this.byKey.GetValueOrDefault(idOrKey[(idOrKey.LastIndexOf('/') + 1)..]);

        /// <summary>By GemEffects Id ("IceNova").</summary>
        public GemInfo? ByGemEffect(string gemEffectId) => this.byEffect.GetValueOrDefault(gemEffectId);

        /// <summary>By the GrantedEffects Id of a skill the gem grants ("IceNovaPlayer", what PlayerSkills lists).</summary>
        public GemInfo? BySkill(string grantedEffectId) => this.bySkill.GetValueOrDefault(grantedEffectId);

        /// <summary>
        ///     Every gem the game shows under this name in the language (case, markup and outer spaces ignored), in Id order: one for
        ///     most names, two or more for 18 of them in 0.5.x (a unique's copy, a weapon's attack by class). English names
        ///     answer when the language has none such.
        /// </summary>
        public IReadOnlyList<GemInfo> ByName(string name, string language)
        {
            var wanted = GemInfo.PlainName(name);
            return this.Named(language).GetValueOrDefault(wanted) ?? (language == "en" ? null : this.Named("en").GetValueOrDefault(wanted)) ?? (IReadOnlyList<GemInfo>)Array.Empty<GemInfo>();
        }

        /// <summary>
        ///     The gem for whatever is in hand, tried as a base item Id or key, a GemEffects Id, a GrantedEffects Id, then a
        ///     name; a name two different gems carry answers only when they are of one kind and attribute (the first then).
        /// </summary>
        public GemInfo? Find(string known, string language)
        {
            if ((this.ByBaseItem(known) ?? this.ByGemEffect(known) ?? this.BySkill(known)) is { } gem)
            {
                return gem;
            }

            var named = this.ByName(known, language);
            return named.Count > 0 && named.All(other => other.Kind == named[0].Kind && other.Attribute == named[0].Attribute) ? named[0] : null;
        }

        private Dictionary<string, List<GemInfo>> Named(string language)
        {
            if (this.byName.TryGetValue(language, out var names))
            {
                return names;
            }

            names = new Dictionary<string, List<GemInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, name) in this.catalog.NamesIn(language))
            {
                if (this.byId.TryGetValue(id, out var gem))
                {
                    var key = GemInfo.PlainName(name);
                    if (!names.TryGetValue(key, out var list))
                    {
                        names[key] = list = new List<GemInfo>();
                    }

                    list.Add(gem);
                }
            }

            this.byName[language] = names;
            return names;
        }
    }
}
