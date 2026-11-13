namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>What a place of a boss's arena is to the player (<see cref="BossPlaces"/>), most pressing first.</summary>
    public enum BossPlaceKind
    {
        /// <summary>Where the player is sheltered from an attack that fills the arena (Asala's barrier).</summary>
        Refuge,

        /// <summary>The true one among a boss's clones.</summary>
        Clone,

        /// <summary>An object that stands between the player and a blast (Zalmarath's sword).</summary>
        Cover,

        /// <summary>An object of the arena the player can use.</summary>
        Use,

        /// <summary>An object of the arena the player can break.</summary>
        Break,
    }

    /// <summary>One place rule of the catalog: for this boss, entities of a path are places of a kind, while a state of theirs is set when one is named.</summary>
    public readonly record struct BossPlaceRule(BossPlaceKind Kind, string Boss, string? State);

    /// <summary>The census's kinds of the boss catalog (docs/research/boss-mechanics.md §14): helpers, arena states, buffs with a text, places, counters.</summary>
    public sealed partial class BossMechanicsCatalog
    {
        /// <summary>An element state whose value's element the files do not name.</summary>
        public const string UnknownElement = "-";

        private readonly HashSet<string> daemons = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> daemonMonsters = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<string>> daemonSkills = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> arenaStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> empowering = new(StringComparer.Ordinal);
        private readonly HashSet<string> feeders = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> shelters = new(StringComparer.Ordinal);
        private readonly HashSet<string> zones = new(StringComparer.Ordinal);
        private readonly HashSet<string> stacks = new(StringComparer.Ordinal);
        private readonly HashSet<string> grabbed = new(StringComparer.Ordinal);
        private readonly HashSet<string> grabs = new(StringComparer.Ordinal);
        private readonly HashSet<string> unavoidable = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int?> progress = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> elements = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<BossPlaceRule>> places = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, (string Name, string Description)>> texts = new(StringComparer.Ordinal);

        /// <summary>How many place rules the catalog holds, for the log.</summary>
        public int Places => this.places.Values.Sum(rules => rules.Count);

        public int Daemons => this.daemons.Count;

        /// <summary>An invisible helper that strikes for this boss (Zalmarath's laser target).</summary>
        public bool IsDaemon(string boss, string monster) => this.daemons.Contains($"{boss}|{monster}");

        /// <summary>The helper's own skills (GrantedEffects ids), whose areas are its danger; none when the files list none.</summary>
        public IReadOnlyList<string> DaemonSkills(string boss, string monster) =>
            this.daemonSkills.TryGetValue($"{boss}|{monster}", out var skills) ? skills : Array.Empty<string>();

        /// <summary>A monster that is some boss's helper: the cheap first question.</summary>
        public bool IsDaemon(string monster) => this.daemonMonsters.Contains(monster);

        /// <summary>The kind of change a state of the boss makes to its arena ("burn", "collapse", "change"); null for any other state.</summary>
        public string? ArenaKind(string boss, string state) => this.arenaStates.GetValueOrDefault($"{boss}|{state}");

        /// <summary>A buff that, on this boss, says something empowers or protects it.</summary>
        public bool IsEmpowering(string boss, string buff) => this.empowering.Contains($"{boss}|{buff}");

        /// <summary>An add whose own skill changes this boss (Kabala's skeleton snakes beam into her and armour her).</summary>
        public bool FeedsBoss(string boss, string monster) => this.feeders.Contains($"{boss}|{monster}");

        /// <summary>A buff that, on the player, means sheltered from the boss's attack.</summary>
        public bool IsShelter(string buff) => this.shelters.Contains(buff);

        /// <summary>A buff an area of the fight gives whoever stands in it.</summary>
        public bool IsZone(string buff) => this.zones.Contains(buff);

        /// <summary>A buff of the fight whose count on the player matters.</summary>
        public bool IsStack(string buff) => this.stacks.Contains(buff);

        /// <summary>A buff that holds the player in place.</summary>
        public bool IsGrabbed(string buff) => this.grabbed.Contains(buff);

        /// <summary>A skill (GrantedEffects id) with which this boss seizes the player.</summary>
        public bool IsGrab(string boss, string skill) => this.grabs.Contains($"{boss}|{skill}");

        /// <summary>A skill (GrantedEffects id) the files mark as impossible to dodge-roll, evade or block.</summary>
        public bool IsUnavoidable(string skill) => this.unavoidable.Contains(skill);

        /// <summary>Whether a state of the boss counts a progress, and its top value when the files give one.</summary>
        public (bool Counts, int? Max) Progress(string boss, string state) =>
            this.progress.TryGetValue($"{boss}|{state}", out var max) ? (true, max) : (false, null);

        /// <summary>
        ///     The element a state value of the boss stands for ("fire", "cold", "lightning", "chaos", "physical"),
        ///     <see cref="UnknownElement"/> for an element state the files do not spell out, null for any other state.
        /// </summary>
        public string? Element(string boss, string state, long value) =>
            this.elements.GetValueOrDefault($"{boss}|{state}={value.ToString(CultureInfo.InvariantCulture)}") ?? this.elements.GetValueOrDefault($"{boss}|{state}");

        /// <summary>What entities of this path are in a boss's arena; empty for most.</summary>
        public IReadOnlyList<BossPlaceRule> PlaceRules(string path) =>
            this.places.TryGetValue(path, out var rules) ? rules : Array.Empty<BossPlaceRule>();

        /// <summary>A buff's name and description as the game writes them, in the language or else in English; null when the catalog has neither.</summary>
        public (string Name, string Description)? BuffText(string buff, string language)
        {
            foreach (var code in new[] { language, "en" })
            {
                if (this.texts.TryGetValue(code, out var table) && table.TryGetValue(buff, out var text))
                {
                    return text;
                }
            }

            return null;
        }

        /// <summary>One line of a census kind; false for a kind this catalog does not know.</summary>
        private bool AddCensus(string[] c)
        {
            switch (c[0])
            {
                case "daemon":
                    this.daemons.Add($"{c[1]}|{c[2]}");
                    this.daemonMonsters.Add(c[2]);
                    this.daemonSkills[$"{c[1]}|{c[2]}"] = c.Length > 3 ? c[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : Array.Empty<string>();
                    return true;
                case "arena":
                    this.arenaStates[$"{c[1]}|{c[2]}"] = c.Length > 3 && c[3].Length > 0 ? c[3] : "change";
                    return true;
                case "empowered":
                    this.empowering.Add($"{c[1]}|{c[2]}");
                    return true;
                case "consume":
                    this.feeders.Add($"{c[1]}|{c[2]}");
                    return true;
                case "shelter":
                    this.shelters.Add(c[2]);
                    return true;
                case "zone":
                    this.zones.Add(c[2]);
                    return true;
                case "stack":
                    this.stacks.Add(c[1]);
                    return true;
                case "grabbed":
                    this.grabbed.Add(c[2]);
                    return true;
                case "grab":
                    this.grabs.Add($"{c[1]}|{c[2]}");
                    return true;
                case "unavoidable":
                    this.unavoidable.Add(c[2]);
                    return true;
                case "progress":
                    this.progress[$"{c[1]}|{c[2]}"] = c.Length > 3 && int.TryParse(c[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var max) && max > 0 ? max : null;
                    return true;
                case "element" when c.Length > 3:
                    this.elements[$"{c[1]}|{c[2]}"] = c[3];
                    return true;
                case "refuge" when c.Length > 3:
                    this.AddPlace(c[2], new BossPlaceRule(BossPlaceKind.Refuge, c[1], c[3]));
                    return true;
                case "clone" when c.Length > 3:
                    this.AddPlace(c[2], new BossPlaceRule(BossPlaceKind.Clone, c[1], c[3]));
                    return true;
                case "cover":
                    this.AddPlace(c[2], new BossPlaceRule(BossPlaceKind.Cover, c[1], null));
                    return true;
                case "use":
                    this.AddPlace(c[2], new BossPlaceRule(BossPlaceKind.Use, c[1], null));
                    return true;
                case "target":
                    this.AddPlace(c[2], new BossPlaceRule(BossPlaceKind.Break, c[1], null));
                    return true;
                case "text" when c.Length > 4:
                    if (!this.texts.TryGetValue(c[1], out var table))
                    {
                        this.texts[c[1]] = table = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
                    }

                    table[c[2]] = (BossTexts.Plain(c[3]), BossTexts.Plain(c[4]));
                    return true;
                default:
                    return false;
            }
        }

        private void AddPlace(string path, BossPlaceRule rule)
        {
            if (!this.places.TryGetValue(path, out var rules))
            {
                this.places[path] = rules = new List<BossPlaceRule>();
            }

            if (!rules.Contains(rule))
            {
                rules.Add(rule);
            }
        }
    }
}
