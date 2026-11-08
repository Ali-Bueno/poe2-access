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

        /// <summary>An action type's name ("GroundSlam"), for the log; null when the files do not list it.</summary>
        public string? ActionName(ushort id) => this.actionTypes.TryGetValue(id, out var name) ? name : null;

        private static IEnumerable<string> ReadLines(string path)
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
