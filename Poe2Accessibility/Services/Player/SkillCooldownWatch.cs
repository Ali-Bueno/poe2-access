namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     A skill whose cooldown runs out (<see cref="SkillCooldowns"/>): its name said and a bolt's click heard as it can be used
    ///     again. Read from the player's Actor: its skills by id and name, and its cooldown entries keyed by the same id.
    /// </summary>
    public sealed class SkillCooldownWatch
    {
        public const string SoundKey = "skill_ready";

        private const int PollMs = 100;

        /// <summary>Gear changes now and then: the skills' names are read again this often.</summary>
        private const int SkillsPollMs = 2000;

        /// <summary>Guards against a misread vector; a player lists a few dozen skills and a few cooldowns.</summary>
        private const int MaxSkills = 256;

        private const int MaxCooldowns = 64;

        private const int MsPerSecond = 1000;

        private const int MaxUses = 32;

        private const string EntriesPart = "cooldowns";
        private const int MaxUnreadEntries = 20;

        private readonly Func<Entity?> subject;
        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly AudioCueService? audio;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly SkillCooldowns cooldowns = new();
        private Dictionary<uint, string> names = new();
        private IntPtr namesActor;
        private long namesReadAt = long.MinValue / 2;
        private long lastPoll;
        private float[]? click;
        private bool entriesJudged;
        private readonly IdLinkCheck idLink = new();
        private readonly LossWitnesses entriesMissed = new();
        private long entriesArea;
        private HashSet<uint> skillIds = new();
        private int unreadEntries;

        /// <summary>Told whether the cooldown entries still read; only the player's watch has it.</summary>
        public CompatibilityReport? Report { get; set; }

        /// <summary><paramref name="audio"/> null: speech only (the partner's skills must not ring like the player's).</summary>
        public SkillCooldownWatch(Func<Entity?> subject, HostMemoryReader memory, UiElementReader elements, AudioCueService? audio, IAccessibilityOutputService output, PluginLog log)
        {
            this.subject = subject;
            this.memory = memory;
            this.elements = elements;
            this.audio = audio;
            this.output = output;
            this.log = log;
        }

        /// <summary>A new area or character: what is recharging now is remembered, not news.</summary>
        public void Reset()
        {
            this.cooldowns.Reset();
            this.namesReadAt = long.MinValue / 2;
        }

        /// <summary>Per frame; <paramref name="reading"/> false while dead, paused or in a menu (a skill ready meanwhile is said after).</summary>
        public void Tick(bool reading, AccessibilitySettings settings)
        {
            var now = Environment.TickCount64;
            if (!reading || !(settings.CooldownSpeech || settings.CooldownSounds) || now - this.lastPoll < PollMs ||
                this.subject() is not { } entity || !entity.TryGetComponent<Actor>(out var actor))
            {
                return;
            }

            this.lastPoll = now;
            var names = this.Names(actor.Address, now);
            var usable = names.Keys.ToDictionary(id => id, _ => true);
            foreach (var (id, uses, max, _) in this.Cooldowns(actor.Address).Where(entry => usable.ContainsKey(entry.Id)))
            {
                usable[id] = uses < max;
            }

            this.JudgeEntries(actor.Address);
            this.JudgeIds(actor.Address);
            var ready = this.cooldowns.Update(usable, now, settings.CooldownMinSeconds * MsPerSecond).Select(id => names[id]).Distinct().ToList();
            if (ready.Count == 0)
            {
                return;
            }

            this.log.Info($"Skill ready: {string.Join(", ", ready)}");
            if (settings.CooldownSounds)
            {
                this.audio?.Play(this.click ??= CueSynth.SkillReady(AudioCueService.SampleRate), 0f, 1f, 1f, SoundKey);
            }

            if (settings.CooldownSpeech)
            {
                this.output.Say(SpeechKind.Cooldowns, Announcements.Sentences(ready.Select(Announcements.SkillReady)));
            }
        }

        /// <summary>
        ///     Entries that exist and none of which reads: their inner fields moved, and skills would stop saying they are ready with
        ///     nothing else to tell. Nothing can move them back, as nothing in an entry has a shape of its own.
        /// </summary>
        private void JudgeEntries(IntPtr actor)
        {
            if (this.Report == null || this.entriesJudged ||
                (this.Vector(actor + GameLayout.ActorComponent.CooldownsOffset, GameLayout.ActorComponent.CooldownEntrySize, MaxCooldowns)?.Count ?? 0) == 0)
            {
                return;
            }

            if (this.Cooldowns(actor).Any())
            {
                this.entriesJudged = true;
                this.Report.Found(EntriesPart);
                return;
            }

            // Twenty readings in one area are one witness (an Actor being rebuilt, an entry of a kind not met before): a
            // second area must read none either.
            var area = AreaWitness();
            if (area != this.entriesArea)
            {
                (this.entriesArea, this.unreadEntries) = (area, 0);
            }

            if (++this.unreadEntries == MaxUnreadEntries && this.entriesMissed.Against(area))
            {
                this.entriesJudged = true;
                this.Report.Missing(EntriesPart);
            }
        }

        /// <summary>
        ///     A cooldown entry's skill id and the id in a skill's details have the game's code as their only check, each alone;
        ///     together they have one more (<see cref="IdLinkCheck"/>): some entry must name one of the actor's skills.
        /// </summary>
        private void JudgeIds(IntPtr actor)
        {
            if (this.Report == null || this.idLink.Proved || this.idLink.Lost)
            {
                return;
            }

            var area = AreaWitness();
            if (!this.idLink.Observe(area, this.Cooldowns(actor).Select(entry => entry.Id).ToList(), this.skillIds))
            {
                return;
            }

            // Said as the cooldowns: a skill whose id no entry names never says it is ready.
            if (this.idLink.Proved)
            {
                this.log.Info("Layout: skill ids where measured (a cooldown entry names a listed skill)");
            }
            else
            {
                this.log.Warn($"Layout: skill ids NOT FOUND: no cooldown entry names one of the {this.skillIds.Count} listed skills, in two areas");
                this.Report.Missing(EntriesPart);
            }
        }

        /// <summary>The area now, as a witness: its instance's address and hash (the address alone can be the next area's too).</summary>
        private static long AreaWitness()
        {
            var area = GameReader.Core.States.InGameStateObject.CurrentAreaInstance;
            return LossWitnesses.AreaKey(area.Address.ToInt64(), area.AreaHash);
        }

        /// <summary>A new game process: the fields are judged again.</summary>
        public void ResetLayout()
        {
            this.idLink.Reset();
            this.entriesMissed.Clear();
            (this.entriesJudged, this.unreadEntries, this.entriesArea) = (false, 0, 0);
        }

        /// <summary>Research (dev command "cooldowns"): each cooldown entry with its skill's name, and the named skills' ids.</summary>
        public string Diagnose()
        {
            if (this.subject() is not { } entity || !entity.TryGetComponent<Actor>(out var actor))
            {
                return "Cooldowns: no player actor";
            }

            var names = this.ReadNames(actor.Address);
            var entries = this.Cooldowns(actor.Address).Select(entry =>
                $"0x{entry.Id:X8} {names.GetValueOrDefault(entry.Id, "?")} {entry.Uses}/{entry.Max} recharging, " +
                $"{(this.memory.TryRead<int>(entry.Entry + GameLayout.ActorComponent.CooldownTimeOffset, out var ms) ? ms : -1)} ms at 0x{entry.Entry.ToInt64():X}");
            var skills = names.Select(skill => $"0x{skill.Key:X8} {skill.Value}");
            return $"Cooldowns at 0x{actor.Address.ToInt64():X}: {string.Join("; ", entries)} | skills: {string.Join("; ", skills)}";
        }

        private Dictionary<uint, string> Names(IntPtr actor, long now)
        {
            if (actor != this.namesActor || now - this.namesReadAt >= SkillsPollMs)
            {
                this.names = this.ReadNames(actor);
                this.namesActor = actor;
                this.namesReadAt = now;
            }

            return this.names;
        }

        /// <summary>Each listed skill with a displayed name, by id (Dodge Roll and the default attacks have none).</summary>
        private Dictionary<uint, string> ReadNames(IntPtr actor)
        {
            var names = new Dictionary<uint, string>();
            var ids = new HashSet<uint>();
            foreach (var details in this.Vector(actor + GameLayout.ActorComponent.SkillsOffset, GameLayout.ActorComponent.SkillEntrySize, MaxSkills) ?? new List<IntPtr>())
            {
                if (!this.memory.TryRead<IntPtr>(details, out var skill) || skill == IntPtr.Zero ||
                    !this.memory.TryRead<uint>(skill + GameLayout.SkillSlot.SkillIdOffset, out var id))
                {
                    continue;
                }

                ids.Add(id);
                if (SkillLevelKey.Row(this.memory, this.elements, skill) is { } level &&
                    PlayerSkills.DisplayedName(this.memory, this.elements, level) is { Length: > 0 } name)
                {
                    names.TryAdd(id, name);
                }
            }

            this.skillIds = ids;
            return names;
        }

        /// <summary>Each cooldown entry: its skill's id, the uses recharging, the uses the skill holds and where it lies.</summary>
        private IEnumerable<(uint Id, int Uses, int Max, IntPtr Entry)> Cooldowns(IntPtr actor)
        {
            foreach (var entry in this.Vector(actor + GameLayout.ActorComponent.CooldownsOffset, GameLayout.ActorComponent.CooldownEntrySize, MaxCooldowns) ?? new List<IntPtr>())
            {
                if (this.Vector(entry + GameLayout.ActorComponent.CooldownUsesOffset, GameLayout.ActorComponent.CooldownUseSize, MaxUses) is { } uses &&
                    this.memory.TryRead<uint>(entry + GameLayout.ActorComponent.CooldownSkillIdOffset, out var id) &&
                    this.memory.TryRead<int>(entry + GameLayout.ActorComponent.CooldownMaxUsesOffset, out var max) &&
                    max is > 0 and <= MaxUses)
                {
                    yield return (id, uses.Count, max, entry);
                }
            }
        }

        /// <summary>The element addresses of a std::vector (first, last) of fixed-size elements; null when it reads wrong.</summary>
        private List<IntPtr>? Vector(IntPtr vector, int elementSize, int maxElements)
        {
            if (!this.memory.TryRead<IntPtr>(vector, out var first) || !this.memory.TryRead<IntPtr>(vector + IntPtr.Size, out var last))
            {
                return null;
            }

            var bytes = last.ToInt64() - first.ToInt64();
            if (bytes == 0)
            {
                return new List<IntPtr>();
            }

            if (first == IntPtr.Zero || bytes < 0 || bytes % elementSize != 0 || bytes / elementSize > maxElements)
            {
                return null;
            }

            return Enumerable.Range(0, (int)(bytes / elementSize)).Select(i => first + (i * elementSize)).ToList();
        }
    }
}
