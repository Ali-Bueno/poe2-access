namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    ///     What the game's files say of boss fights (<c>boss-mechanics.txt</c>, tools/Poe2UiExtract boss-mechanics;
    ///     docs/research/boss-mechanics.md): which monsters are bosses, the states that hide them or carry their phase or
    ///     enrage, the states in which they hide to be found, the monsters each brings or turns into, the unhurtable and enrage buffs, the player's movement rules and
    ///     the voice lines that announce a fight's turn. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed partial class BossMechanicsCatalog
    {
        public const string FileName = "boss-mechanics.txt";

        private const char Field = '|';
        private const char Comment = '#';

        private readonly HashSet<string> bosses = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> hides = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> phaseStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> enrageStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> seekStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> seekers = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> adds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> walls = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> forms = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> withForms = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> unhurtable = new(StringComparer.Ordinal);
        private readonly HashSet<string> enrages = new(StringComparer.Ordinal);
        private readonly Dictionary<string, StatusAilment> moveRules = new(StringComparer.Ordinal);
        private readonly HashSet<string> lines = new(StringComparer.Ordinal);

        /// <summary>The catalog the plugin loaded; empty until then (tests build their own).</summary>
        public static BossMechanicsCatalog Shared { get; set; } = new();

        public int Bosses => this.bosses.Count;

        public int Lines => this.lines.Count;

        public int MoveRules => this.moveRules.Count;

        public static BossMechanicsCatalog Load(string catalogDirectory)
        {
            var catalog = new BossMechanicsCatalog();
            try
            {
                var path = Path.Join(catalogDirectory, FileName);
                if (File.Exists(path))
                {
                    catalog.Add(File.ReadLines(path));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // No catalog: no boss help beyond the generic flags.
            }

            return catalog;
        }

        /// <summary>Catalog lines (formats in the file's header); unknown kinds and short lines are skipped.</summary>
        public void Add(IEnumerable<string> catalogLines)
        {
            foreach (var line in catalogLines)
            {
                var c = line.Split(Field);
                if (line.Length == 0 || line[0] == Comment || c.Length < 3)
                {
                    continue;
                }

                switch (c[0])
                {
                    case "boss":
                        this.bosses.Add(c[1]);
                        break;
                    case "hide":
                        this.hides.Add($"{c[1]}|{c[2]}");
                        break;
                    case "phase":
                        this.phaseStates.Add($"{c[1]}|{c[2]}");
                        break;
                    case "enrage":
                        this.enrageStates.Add($"{c[1]}|{c[2]}");
                        break;
                    case "seek":
                        this.seekStates.Add($"{c[1]}|{c[2]}");
                        this.seekers.Add(c[1]);
                        break;
                    case "add":
                        this.adds.Add($"{c[1]}|{c[2]}");
                        break;
                    case "wall":
                        this.walls.Add($"{c[1]}|{c[2]}");
                        break;
                    case "form":
                        this.forms.Add($"{c[1]}|{c[2]}");
                        this.withForms.Add(c[1]);
                        break;
                    case "buff" when c[2] == "invulnerable":
                        this.unhurtable.Add(c[1]);
                        break;
                    case "buff" when c[2] == "enrage":
                        this.enrages.Add(c[1]);
                        break;
                    case "move":
                        this.moveRules[c[1]] = c[2] == "stop" ? StatusAilment.StopMoving : StatusAilment.KeepMoving;
                        break;
                    case "line" when c.Length >= 4 && LineKey(c[2]) is { Length: > 0 } key:
                        this.lines.Add(key);
                        break;
                    default:
                        this.AddCensus(c);
                        break;
                }
            }
        }

        /// <summary>A monster's id from an entity path ("…/BaronBossCorruptedWolfForm@15" → without the level).</summary>
        public static string MonsterId(string entityPath)
        {
            var at = entityPath.IndexOf('@');
            return at > 0 ? entityPath[..at] : entityPath;
        }

        /// <summary>A monster with the boss life bar.</summary>
        public bool IsBoss(string monster) => this.bosses.Contains(monster);

        /// <summary>The boss's script draws nothing in this state value (bb_visible=0, emerge_hide=1).</summary>
        public bool Hides(string monster, string state, long value) => this.hides.Contains($"{monster}|{state}={value.ToString(CultureInfo.InvariantCulture)}");

        public bool IsPhaseState(string monster, string state) => this.phaseStates.Contains($"{monster}|{state}");

        public bool IsEnrageState(string monster, string state) => this.enrageStates.Contains($"{monster}|{state}");

        /// <summary>A state in which the boss hides until the player comes near it (Balbala's "hiding").</summary>
        public bool IsSeekState(string monster, string state) => this.seekStates.Contains($"{monster}|{state}");

        /// <summary>A boss whose script hides it to be found: out of play, it waits somewhere in its arena.</summary>
        public bool HidesToBeFound(string monster) => this.seekers.Contains(monster);

        /// <summary>A monster the boss brings into its fight (its script preloads it, no boss bar).</summary>
        public bool IsAdd(string boss, string monster) => this.adds.Contains($"{boss}|{monster}");

        /// <summary>An add of the boss that only stands in the way: a wall of the cage it shuts the player in (Kabala's Bone Wall).</summary>
        public bool IsWall(string boss, string monster) => this.walls.Contains($"{boss}|{monster}");

        /// <summary>Another boss this one turns into (its script preloads it, with a boss bar).</summary>
        public bool IsForm(string boss, string other) => this.forms.Contains($"{boss}|{other}");

        public bool HasForms(string boss) => this.withForms.Contains(boss);

        /// <summary>A buff (definition id) that makes its bearer unhurtable without hiding it.</summary>
        public bool IsUnhurtable(string buff) => this.unhurtable.Contains(buff);

        public bool IsEnrage(string buff) => this.enrages.Contains(buff);

        /// <summary>The rule a buff's description gives the player ("Stop moving or you will be imprisoned"); null for none.</summary>
        public StatusAilment? MoveRule(string buff) => this.moveRules.TryGetValue(buff, out var rule) ? rule : null;

        /// <summary>A chat line ("Speaker: text") that is a boss's voice line announcing a fight's turn, in any language the files ship.</summary>
        public bool IsMechanicLine(string chatLine)
        {
            var colon = chatLine.IndexOf(':');
            return this.lines.Count > 0 && LineKey(colon >= 0 ? chatLine[(colon + 1)..] : chatLine) is { Length: > 0 } key && this.lines.Contains(key);
        }

        /// <summary>A line's letters and digits in lower case, markup apart ("<i>{Rith}!</i>" → "rith"): the chat and the files agree on it.</summary>
        public static string LineKey(string text)
        {
            var key = new StringBuilder(text.Length);
            var inTag = false;
            foreach (var ch in text)
            {
                if (ch == '<')
                {
                    inTag = true;
                }
                else if (ch == '>')
                {
                    inTag = false;
                }
                else if (!inTag && char.IsLetterOrDigit(ch))
                {
                    key.Append(char.ToLowerInvariant(ch));
                }
            }

            return key.ToString();
        }
    }
}
