namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Entity component fields a game update can move, found again from what they hold (docs/compatibility.md): the
    ///     Targetable in-play flag (<see cref="InPlayFlag"/>), which the interaction target beside it follows, and the
    ///     NPC quest marker (a MiscAnimated row whose id is NPCQuestMarker). Sampled every few seconds until both are known.
    /// </summary>
    public sealed class EntityFields
    {
        private const int PassMs = 2000;

        /// <summary>Bytes of a Targetable component read: the flags sit near its start (0.5.5e: +0x69, +0x6B).</summary>
        private const int TargetableBytes = 0x100;

        /// <summary>How far either side of where it was measured the flag is looked for (one byte at a time).</summary>
        private const int FlagRadius = 0x40;

        /// <summary>How far the quest marker's pointer is looked for (8-byte fields).</summary>
        private const int MarkerRadius = 0x80;

        private const int MaxMarkerChars = 32;

        /// <summary>Things surely in play, by path: the waypoint, the stash, checkpoints (the minimap's own icons).</summary>
        private static readonly string[] InPlayPaths = { "/Waypoint", "/Stash", "/Checkpoint" };

        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly int measuredFlag = GameLayout.TargetableComponent.IsTargetableOffset;
        private readonly int measuredTargeted = GameLayout.TargetableComponent.IsTargetedOffset;
        private readonly int measuredMarker = GameLayout.NpcComponent.MarkerRowOffset;
        private readonly List<byte[]> inPlay = new();
        private readonly Dictionary<string, List<byte[]>> variants = new(StringComparer.Ordinal);
        private readonly List<IntPtr> npcs = new();
        private int markerSearchedFor = -1;
        private bool flagKnown;
        private bool markerKnown;
        private long lastPass = long.MinValue / 2;

        public EntityFields(HostMemoryReader memory, UiElementReader elements)
        {
            this.memory = memory;
            this.elements = elements;
        }

        /// <summary>The in-play flag is believed until a game update visibly breaks it; then everything counts as in play.</summary>
        public static bool InPlayTrusted { get; private set; } = true;

        public CompatibilityReport? Report { get; set; }

        /// <summary>Whether this pass over the awake entities samples them (every <see cref="PassMs"/> until both fields are known).</summary>
        public bool StartPass()
        {
            var now = Environment.TickCount64;
            if ((this.flagKnown && this.markerKnown) || now - this.lastPass < PassMs)
            {
                return false;
            }

            this.lastPass = now;
            if (!this.flagKnown)
            {
                // Kept once judged, for the self-test.
                this.inPlay.Clear();
                this.variants.Clear();
            }

            this.npcs.Clear();
            return true;
        }

        public void Sample(Entity entity, Render render)
        {
            if (entity.EntityType == EntityTypes.NPC && entity.TryGetComponent<NPC>(out var npc))
            {
                this.npcs.Add(npc.Address);
            }

            if (this.flagKnown || !entity.TryGetComponent<Targetable>(out var target))
            {
                return;
            }

            var surelyInPlay = entity.EntityType == EntityTypes.Player ||
                               (entity.EntityType == EntityTypes.Monster && EntityCatalog.IsAliveEnemy(entity)) ||
                               (entity.Path is { } path && InPlayPaths.Any(part => path.Contains(part, StringComparison.Ordinal)));
            if (!surelyInPlay && entity.EntityType != EntityTypes.NPC)
            {
                return;
            }

            var block = this.memory.ReadBytes(target.Address, TargetableBytes);
            if (block.Length < TargetableBytes)
            {
                return;
            }

            if (surelyInPlay)
            {
                this.inPlay.Add(block);
            }
            else if (this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) is { Length: > 0 } name)
            {
                // Story variants of one NPC share its name; at most one of them is in play.
                (this.variants.TryGetValue(name, out var group) ? group : this.variants[name] = new List<byte[]>()).Add(block);
            }
        }

        /// <summary>After a sampled pass: judges what the pass gathered.</summary>
        public void EndPass()
        {
            this.JudgeFlag();
            this.JudgeMarker();
        }

        /// <summary>A new game process (perhaps a patched one): both are judged again from where they were measured.</summary>
        public void Reset()
        {
            GameLayout.TargetableComponent.IsTargetableOffset = this.measuredFlag;
            GameLayout.TargetableComponent.IsTargetedOffset = this.measuredTargeted;
            GameLayout.NpcComponent.MarkerRowOffset = this.measuredMarker;
            (this.flagKnown, this.markerKnown, InPlayTrusted, this.markerSearchedFor) = (false, false, true, -1);
        }

        /// <summary>Research (dev command "layout"): every other flag offset within reach that also fits, and the marker's state.</summary>
        public string SelfTest()
        {
            var groups = this.Groups();
            var flag = this.inPlay.Count < InPlayFlag.MinInPlay ? "in-play flag: too little in play to judge"
                : OffsetSearch.Outward(GameLayout.TargetableComponent.IsTargetableOffset, FlagRadius, 1).Where(at => InPlayFlag.Fits(this.inPlay, groups, at)).ToList() is var fits &&
                  fits.Count == 1 && fits[0] == GameLayout.TargetableComponent.IsTargetableOffset ? "in-play flag ok"
                : $"in-play flag FAIL (fits at {string.Join(", ", fits.Select(at => $"+0x{at:X}"))}, is +0x{GameLayout.TargetableComponent.IsTargetableOffset:X})";
            var ids = this.npcs.Select(npc => this.RowIdAt(npc, GameLayout.NpcComponent.MarkerRowOffset)).Where(id => id.Length > 0).Distinct().ToList();
            var marker = this.markerKnown ? "quest marker ok"
                : $"quest marker: no NPC shows one here ({this.npcs.Count} NPCs; rows at +0x{GameLayout.NpcComponent.MarkerRowOffset:X}: {(ids.Count == 0 ? "none" : string.Join(", ", ids))})";
            return $"{flag}; {marker}";
        }

        private List<IReadOnlyList<byte[]>> Groups() => this.variants.Values.Where(group => group.Count > 1).Select(group => (IReadOnlyList<byte[]>)group).ToList();

        private void JudgeFlag()
        {
            if (this.flagKnown || this.inPlay.Count < InPlayFlag.MinInPlay)
            {
                return;
            }

            var measured = GameLayout.TargetableComponent.IsTargetableOffset;
            if (InPlayFlag.Share(this.inPlay, measured) >= 0.5)
            {
                this.flagKnown = true;
                this.Report?.Found("targetable flag");
                return;
            }

            var groups = this.Groups();
            if (groups.Count == 0)
            {
                return; // nothing surely out of play here (a town has story variants): wait for one
            }

            this.flagKnown = true;
            if (OffsetSearch.First(measured, FlagRadius, 1, at => InPlayFlag.Fits(this.inPlay, groups, at)) is { } found)
            {
                GameLayout.TargetableComponent.IsTargetableOffset = found;
                GameLayout.TargetableComponent.IsTargetedOffset = this.measuredTargeted + (found - this.measuredFlag);
                this.Report?.Moved("targetable flag", this.measuredFlag, found);
            }
            else
            {
                InPlayTrusted = false;
                this.Report?.Missing("targetable flag");
            }
        }

        private void JudgeMarker()
        {
            if (this.markerKnown || this.npcs.Count == 0)
            {
                return;
            }

            if (this.npcs.Any(npc => this.HasMarkerAt(npc, GameLayout.NpcComponent.MarkerRowOffset)))
            {
                this.markerKnown = true;
                this.Report?.Found("quest marker");
                return;
            }

            // No NPC shows one where measured: searched once per set of NPCs (an area without quests shows none at all).
            if (this.npcs.Count == this.markerSearchedFor)
            {
                return;
            }

            this.markerSearchedFor = this.npcs.Count;
            if (OffsetSearch.First(this.measuredMarker, MarkerRadius, IntPtr.Size, at => this.npcs.Any(npc => this.HasMarkerAt(npc, at)), IntPtr.Size) is { } found)
            {
                GameLayout.NpcComponent.MarkerRowOffset = found;
                this.markerKnown = true;
                this.Report?.Moved("quest marker", this.measuredMarker, found);
            }
        }

        /// <summary>The NPC component holds, at the offset, a dat row whose id names the quest indicator.</summary>
        public bool HasMarkerAt(IntPtr npc, int offset) => this.RowIdAt(npc, offset).StartsWith(GameLayout.NpcComponent.QuestMarkerId, StringComparison.Ordinal);

        /// <summary>The id of the dat row the component points at there; empty when there is none.</summary>
        private string RowIdAt(IntPtr npc, int offset) =>
            this.memory.TryRead<IntPtr>(npc + offset, out var row) && row != IntPtr.Zero && this.memory.IsValidAddress(row) &&
            this.memory.TryRead<IntPtr>(row, out var id) && id != IntPtr.Zero && this.memory.IsValidAddress(id)
                ? this.elements.ReadUtf16BufferAt(id, MaxMarkerChars) ?? string.Empty
                : string.Empty;
    }
}
