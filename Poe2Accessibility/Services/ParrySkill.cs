namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Whether the followed player can parry: Parry is granted by a buckler, a targe or two swords (KeywordPopups
    ///     "Parry") and shows as the granted effect "ParryPlayer" among the skills the player's Actor lists.
    /// </summary>
    public sealed class ParrySkill
    {
        /// <summary>GrantedEffects Id of the Parry skill (row 433, dat-strings 2026-10-28).</summary>
        private const string ParryEffectId = "ParryPlayer";

        /// <summary>Gear changes now and then: the skills are read again this often.</summary>
        private const int PollMs = 2000;

        /// <summary>A guard against a misread vector; a player lists a few dozen.</summary>
        private const int MaxSkills = 256;

        private const int MaxIdChars = 64;

        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly PluginLog log;
        private string logged = string.Empty;
        private long lastPoll = long.MinValue / 2;
        private bool has;

        public ParrySkill(HostMemoryReader memory, UiElementReader elements, PluginLog log)
        {
            this.memory = memory;
            this.elements = elements;
            this.log = log;
        }

        /// <summary>Whether the followed player has Parry now; the skills are read at most every <see cref="PollMs"/>.</summary>
        public bool Has()
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return this.has;
            }

            this.lastPoll = now;
            var ids = Seats.Followed.TryGetComponent<Actor>(out var actor) ? this.SkillIds(actor.Address) : new List<string>();
            this.has = ids.Contains(ParryEffectId);
            var joined = string.Join(", ", ids);
            if (joined != this.logged)
            {
                this.logged = joined;
                this.log.Info($"Player skills: {joined}");
            }

            return this.has;
        }

        /// <summary>The granted effect Id of each skill the actor lists, through its level row, each key checked against its table.</summary>
        private List<string> SkillIds(IntPtr actor)
        {
            var ids = new List<string>();
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
                    DatKey.Row(this.memory, this.elements, details + GameLayout.SkillSlot.LevelKeyOffset, GameLayout.SkillSlot.LevelTable) is { } level &&
                    DatKey.Row(this.memory, this.elements, level + GameLayout.SkillSlot.EffectKeyOffset, GameLayout.SkillSlot.EffectTable) is { } effect &&
                    this.memory.TryRead<IntPtr>(effect + GameLayout.SkillSlot.EffectIdOffset, out var id) &&
                    this.elements.ReadUtf16BufferAt(id, MaxIdChars) is { Length: > 0 } text && !ids.Contains(text))
                {
                    ids.Add(text);
                }
            }

            return ids;
        }
    }
}
