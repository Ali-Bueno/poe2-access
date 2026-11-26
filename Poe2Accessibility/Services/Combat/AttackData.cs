namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    /// <summary>
    ///     What the game's files say of attacks beyond their timing, from the regenerated catalogs: each clip's melee reach and
    ///     impact distance (<c>attack-reach.txt</c>), each monster skill's danger area (<c>telegraph-skills.txt</c>), and the
    ///     action type names (<c>action-types.txt</c>) that check and name a monster's current action. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class AttackData
    {
        public const string ReachFile = "attack-reach.txt";
        public const string TelegraphSkillsFile = "telegraph-skills.txt";
        public const string ActionTypesFile = "action-types.txt";

        private const char Comment = '#';
        private const char Field = '|';

        private readonly Dictionary<string, (int Reach, int Impact)> reaches = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AttackArea> areas = new(StringComparer.Ordinal);
        private readonly Dictionary<ushort, string> actionTypes = new();
        private readonly Dictionary<string, float?> blasts = new(StringComparer.Ordinal);

        /// <summary>The ids of every action type the files list (<see cref="AttackClock.ActionId"/>), for <see cref="AttackFieldCheck.ActionTypeKnown"/>.</summary>
        public IReadOnlySet<ushort> ActionTypeIds => this.actionIds;

        private readonly HashSet<ushort> actionIds = new();

        public int Clips => this.reaches.Count;

        public int Areas => this.areas.Count;

        public static AttackData Load(string catalogDirectory)
        {
            var data = new AttackData();
            data.AddReaches(ReadLines(Path.Join(catalogDirectory, ReachFile)));
            data.AddTelegraphSkills(ReadLines(Path.Join(catalogDirectory, TelegraphSkillsFile)));
            data.AddActionTypes(ReadLines(Path.Join(catalogDirectory, ActionTypesFile)));
            return data;
        }

        /// <summary>Lines "ao path|clip=reach/impact" (world units from the monster's centre, 0 for none).</summary>
        public void AddReaches(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                var split = line.LastIndexOf('=');
                var parts = split > 0 ? line[(split + 1)..].Split('/') : Array.Empty<string>();
                if (line.Length == 0 || line[0] == Comment || parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var reach) ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var impact) || reach < 0 || impact < 0)
                {
                    continue;
                }

                this.reaches[line[..split]] = (reach, impact);
            }
        }

        /// <summary>Lines "skill id|kind|area|visuals|objects"; only geometry and trigger areas are kept.</summary>
        public void AddTelegraphSkills(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                var columns = line.Split(Field);
                if (line.Length == 0 || line[0] == Comment || columns.Length < 3 || AttackAreas.Parse(columns[1], columns[2]) is not { } area)
                {
                    continue;
                }

                this.areas[columns[0]] = AttackAreas.InWorld(area);
            }
        }

        /// <summary>One ActionTypes.dat Id a line.</summary>
        public void AddActionTypes(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                var name = line.Trim();
                if (name.Length > 0 && name[0] != Comment)
                {
                    var id = AttackClock.ActionId(name);
                    this.actionIds.Add(id);
                    this.actionTypes.TryAdd(id, name);
                }
            }
        }

        /// <summary>The farthest a clip's blow reaches from its monster's centre: its damage patterns or its contact's place; null when the files do not say.</summary>
        public int? Reach(string animatedObjectPath, string clip) =>
            this.reaches.TryGetValue($"{animatedObjectPath}|{clip}", out var reach) && Math.Max(reach.Reach, reach.Impact) is > 0 and var farthest ? farthest : null;

        /// <summary>The danger area a skill's table describes, by GrantedEffects id; null when it describes none.</summary>
        public AttackArea? Area(string skillId) => this.areas.TryGetValue(skillId, out var area) ? area : null;

        /// <summary>
        ///     The radius (world units) of the widest circle a skill without an area of its own sets off: the circles of the
        ///     skills whose ids, past their kind's capitals, begin with its own (EASDoryaniCrabExplode sets off GADoryaniCrabExplode,
        ///     radius 55 cells, and GADoryaniCrabExplodeSmash, 64: docs/research/boss-mechanics.md, Doryani); null when it has an
        ///     area of its own, no kind prefix, or none such.
        /// </summary>
        public float? BlastRadius(string skillId)
        {
            if (this.blasts.TryGetValue(skillId, out var cached))
            {
                return cached;
            }

            float? radius = null;
            if (!this.areas.ContainsKey(skillId) && Stem(skillId) is { } stem)
            {
                foreach (var (id, area) in this.areas)
                {
                    if (area.Shape == AttackAreas.Circle && Stem(id) is { } other && other.StartsWith(stem, StringComparison.Ordinal))
                    {
                        radius = Math.Max(radius ?? 0f, area.Length);
                    }
                }
            }

            this.blasts[skillId] = radius;
            return radius;
        }

        /// <summary>
        ///     The widest area (by length or radius) among the skills a skill sets off (<see cref="BossMechanicsCatalog.ChildrenOf"/>),
        ///     for a skill that draws none of its own (Kulemak's mega beam: the trigger draws 2 cells, its beam 220); null when none draws one.
        /// </summary>
        public AttackArea? WidestOf(IEnumerable<string> skills) => this.WidestChildOf(skills)?.Area;

        /// <summary>The skill <see cref="WidestOf"/> chooses, with its area.</summary>
        public (string Skill, AttackArea Area)? WidestChildOf(IEnumerable<string> skills)
        {
            (string Skill, AttackArea Area)? widest = null;
            foreach (var skill in skills)
            {
                if (this.Area(skill) is { } area && (widest is not { } best || area.Length > best.Area.Length))
                {
                    widest = (skill, area);
                }
            }

            return widest;
        }

        /// <summary>A skill id past its kind's capitals (<see cref="SkillIds.Stem"/>).</summary>
        internal static string? Stem(string skillId) => SkillIds.Stem(skillId);

        /// <summary>An action type's name ("GroundSlam"), for the log; null when the files do not list it.</summary>
        public string? ActionName(ushort id) => this.actionTypes.TryGetValue(id, out var name) ? name : null;

        /// <summary>A data file's lines; none when it is missing or cannot be read.</summary>
        internal static IEnumerable<string> ReadLines(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }
    }
}
