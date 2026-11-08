namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     Research (dev command "skills"): each skill the followed player's Actor lists with its id, name, granted effect,
    ///     skill types from the catalog and every non-zero int32 of its ActiveSkillDetails, so a counter kept per skill
    ///     (crossbow bolts loaded, Combo, Glory) shows as the field that moves between two dumps (docs/research/class-mechanics.md).
    /// </summary>
    public sealed class SkillDetailsDump
    {
        /// <summary>GameHelper's estimate of an ActiveSkillDetails: 0x100 bytes.</summary>
        public const int DetailsBytes = 0x100;

        private const int MaxSkills = 256;
        private const int MaxIdChars = 64;

        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;

        public SkillDetailsDump(HostMemoryReader memory, UiElementReader elements)
        {
            this.memory = memory;
            this.elements = elements;
        }

        /// <summary>The non-zero int32 fields of a block, "+0x08=3 +0x40=1234".</summary>
        public static string Fields(int[] values) =>
            string.Join(" ", values.Select((value, i) => (value, i)).Where(field => field.value != 0).Select(field => $"+0x{field.i * sizeof(int):X2}={field.value}"));

        public string Dump()
        {
            if (!Seats.Followed.TryGetComponent<Actor>(out var actor))
            {
                return "Skills: no player actor";
            }

            var lines = new List<string>();
            var skills = actor.Address + GameLayout.ActorComponent.SkillsOffset;
            if (!this.memory.TryRead<IntPtr>(skills, out var first) || !this.memory.TryRead<IntPtr>(skills + IntPtr.Size, out var last) || first == IntPtr.Zero)
            {
                return "Skills: vector unread";
            }

            var count = Math.Min(MaxSkills, (last.ToInt64() - first.ToInt64()) / GameLayout.ActorComponent.SkillEntrySize);
            for (var i = 0; i < count; i++)
            {
                if (!this.memory.TryRead<IntPtr>(first + (int)(i * GameLayout.ActorComponent.SkillEntrySize), out var details) || details == IntPtr.Zero)
                {
                    continue;
                }

                var level = DatKey.Row(this.memory, this.elements, details + GameLayout.SkillSlot.LevelKeyOffset, GameLayout.SkillSlot.LevelTable);
                var effect = level is { } row ? DatKey.Row(this.memory, this.elements, row + GameLayout.SkillSlot.EffectKeyOffset, GameLayout.SkillSlot.EffectTable) : null;
                var granted = effect is { } effectRow && this.memory.TryRead<IntPtr>(effectRow + GameLayout.SkillSlot.EffectIdOffset, out var idText)
                    ? this.elements.ReadUtf16BufferAt(idText, MaxIdChars) ?? string.Empty
                    : string.Empty;
                var name = level is { } named ? PlayerSkills.DisplayedName(this.memory, this.elements, named) : string.Empty;
                this.memory.TryRead<uint>(details + GameLayout.SkillSlot.SkillIdOffset, out var id);
                var types = string.Join(",", ClassMechanicCatalog.Current.SkillTypes(granted));
                var values = this.memory.ReadArray<int>(details, DetailsBytes / sizeof(int));
                lines.Add($"0x{id:X8} {name} [{granted}] ({types}) at 0x{details.ToInt64():X}: {Fields(values)}");
            }

            return $"Skills of actor 0x{actor.Address.ToInt64():X}:\n" + string.Join("\n", lines);
        }
    }
}
