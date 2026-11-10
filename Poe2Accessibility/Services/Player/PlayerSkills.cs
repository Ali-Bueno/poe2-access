namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The granted effect Ids of the skills the followed player's Actor lists ("KillingPalmPlayer"): Parry is granted by
    ///     a buckler, a targe or two swords (KeywordPopups "Parry") and shows as "ParryPlayer"; the skills that cull come
    ///     from <see cref="CullSkills"/>.
    /// </summary>
    public sealed class PlayerSkills
    {
        /// <summary>GrantedEffects Id of the Parry skill (row 433, dat-strings 2026-10-28).</summary>
        public const string Parry = "ParryPlayer";

        /// <summary>Gear changes now and then: the skills are read again this often.</summary>
        private const int PollMs = 2000;

        /// <summary>A guard against a misread vector; a player lists a few dozen.</summary>
        private const int MaxSkills = 256;

        private const int MaxIdChars = 64;

        private const int MaxNameChars = 128;

        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly PluginLog log;
        private string logged = string.Empty;
        private long lastPoll = long.MinValue / 2;
        private List<string> ids = new();

        public PlayerSkills(HostMemoryReader memory, UiElementReader elements, PluginLog log)
        {
            this.memory = memory;
            this.elements = elements;
            this.log = log;
        }

        /// <summary>
        ///     A skill's localized name ("Saeta básica") from its GrantedEffectsPerLevel row, each key checked against its
        ///     table; "" when none. The name the game reads is said whenever it is text; the game files' name of the same
        ///     GrantedEffects Id (<see cref="GemCatalog"/>) stands in when it is not, and for every skill once the game's read
        ///     is judged moved (<see cref="SkillNameCheck"/>).
        /// </summary>
        public static string DisplayedName(HostMemoryReader memory, UiElementReader elements, IntPtr levelRow)
        {
            if (DatKey.Row(memory, elements, levelRow + GameLayout.SkillSlot.EffectKeyOffset, GameLayout.SkillSlot.EffectTable) is not { } effect)
            {
                return string.Empty;
            }

            var fromFiles = EffectId(memory, elements, effect) is { } id ? GemCatalog.Shared.SkillName(id, Localization.Language) : null;
            return SkillNameCheck.Choose(GameName(memory, elements, effect), fromFiles, NameCheck.Source);
        }

        /// <summary>The displayed name the game's own rows give a granted effect: its ActiveSkills row's; "" when the chain does not read.</summary>
        private static string GameName(HostMemoryReader memory, UiElementReader elements, IntPtr effect) =>
            DatKey.Row(memory, elements, effect + GameLayout.SkillSlot.SkillKeyOffset, GameLayout.SkillSlot.SkillTable) is { } skill &&
            memory.TryRead<IntPtr>(skill + GameLayout.SkillSlot.DisplayedNameOffset, out var name)
                ? elements.ReadUtf16BufferAt(name, MaxNameChars) ?? string.Empty
                : string.Empty;

        private static string? EffectId(HostMemoryReader memory, UiElementReader elements, IntPtr effect) =>
            memory.TryRead<IntPtr>(effect + GameLayout.SkillSlot.EffectIdOffset, out var id) && elements.ReadUtf16BufferAt(id, MaxIdChars) is { Length: > 0 } text ? text : null;

        /// <summary>Whose names are said, judged on all the followed player's skills at each poll; one for the plugin, as the offsets are.</summary>
        private static readonly SkillNameCheck NameCheck = new();
        private static readonly HashSet<string> OwnNamesLogged = new(StringComparer.Ordinal);
        private static int nameGeneration = -1;

        /// <summary>Whether the followed player has the skill now.</summary>
        public bool Has(string id) => this.Ids().Contains(id);

        /// <summary>The followed player's skills now; read at most every <see cref="PollMs"/>.</summary>
        public IReadOnlyList<string> Ids()
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return this.ids;
            }

            this.lastPoll = now;
            this.ids = Seats.Followed.TryGetComponent<Actor>(out var actor) ? this.SkillIds(actor.Address) : new List<string>();
            var joined = string.Join(", ", this.ids);
            if (joined != this.logged)
            {
                this.logged = joined;
                this.log.Info($"Player skills: {joined}");
            }

            return this.ids;
        }

        /// <summary>The granted effect Id of each skill the actor lists, through its level row, each key checked against its table.</summary>
        private List<string> SkillIds(IntPtr actor)
        {
            var ids = new List<string>();
            var names = new List<(string Id, string FromGame)>();
            if (!this.memory.TryRead<IntPtr>(actor + GameLayout.ActorComponent.SkillsOffset, out var first) ||
                !this.memory.TryRead<IntPtr>(actor + GameLayout.ActorComponent.SkillsOffset + IntPtr.Size, out var last) ||
                first == IntPtr.Zero || last.ToInt64() <= first.ToInt64())
            {
                return ids;
            }

            var count = Math.Min(MaxSkills, (last.ToInt64() - first.ToInt64()) / GameLayout.ActorComponent.SkillEntrySize);
            for (var i = 0; i < count; i++)
            {
                if (this.memory.TryRead<IntPtr>(first + (int)(i * GameLayout.ActorComponent.SkillEntrySize), out var details) && details != IntPtr.Zero &&
                    SkillLevelKey.Row(this.memory, this.elements, details) is { } level &&
                    DatKey.Row(this.memory, this.elements, level + GameLayout.SkillSlot.EffectKeyOffset, GameLayout.SkillSlot.EffectTable) is { } effect &&
                    EffectId(this.memory, this.elements, effect) is { } text && !ids.Contains(text))
                {
                    ids.Add(text);
                    names.Add((text, GameName(this.memory, this.elements, effect)));
                }
            }

            this.JudgeNames(names);
            return ids;
        }

        /// <summary>
        ///     One reading of every skill's name beside the game files' (in whichever language the game runs in). They agree on
        ///     most: a skill that differs is the game's own display name, and the game's is said. They disagree on most, twice
        ///     running: the row offsets the game's read goes through moved, the files' names are said and the columns are
        ///     reported as any column nothing judged is.
        /// </summary>
        private void JudgeNames(List<(string Id, string FromGame)> names)
        {
            var catalog = GemCatalog.Shared;
            var known = names.Where(skill => catalog.SkillName(skill.Id, "en") != null).ToList();
            var differing = known.Where(skill => !catalog.IsSkillName(skill.Id, skill.FromGame)).ToList();
            if ((CompatibilityReport.Current?.Generation ?? 0) is var generation && generation != nameGeneration)
            {
                nameGeneration = generation; // a new game process: judged again
                NameCheck.Reset();
                OwnNamesLogged.Clear();
            }

            if (NameCheck.Observe(known.Count, differing.Count))
            {
                if (NameCheck.Source == SkillNameSource.Files)
                {
                    this.log.Warn($"Skill names: the game's read DISAGREES with the game files on {differing.Count} of {known.Count} skills (first {differing[0].Id}: '{differing[0].FromGame}'); the files' names are said");
                    CompatibilityReport.Current?.Missing(DatColumns.UnjudgedPart);
                }
                else
                {
                    this.log.Info($"Skill names: the game's read agrees with the game files on {known.Count - differing.Count} of {known.Count} skills; the game's names are said");
                }
            }

            if (NameCheck.Source == SkillNameSource.Game)
            {
                foreach (var (id, fromGame) in differing.Where(skill => OwnNamesLogged.Add(skill.Id)))
                {
                    this.log.Info($"Skill name: the game shows {id} as '{fromGame}', the game files as '{catalog.SkillName(id, "en")}'; the game's is said when it is text");
                }
            }
        }
    }
}
