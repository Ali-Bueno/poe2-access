namespace GameOffsets.Objects.Components
{
    using System;
    using System.Runtime.InteropServices;
    using GameOffsets.Natives;

    /// <summary>
    ///     The action an actor performs now (<see cref="ActorOffset.CurrentActionPtr"/>): its type, skill and target. Read by
    ///     the game's GEAL GetTarget, GetTargetLocation, IsCurrentAction and GetCurrentActionStat on five builds (25244054-26476662),
    ///     checked live on a player's actions 2026-11-08; provenance and checks in docs/research/dodge-timing.md.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ActorActionOffsets
    {
        [FieldOffset(0x000)] public IntPtr VTablePtr;

        // The skill instance acting (its stat sets; one of the actor's active skills).
        [FieldOffset(0x0F8)] public IntPtr SkillPtr;

        // The game's 16-bit hash of the ActionTypes.dat Id ("GroundSlam", "DodgeRoll"), as that row keeps at +0x08.
        [FieldOffset(0x120)] public ushort ActionTypeId;

        // The target entity, null for none.
        [FieldOffset(0x148)] public IntPtr TargetEntityPtr;

        // The target place, grid cells (Positioned +0x444 counts the same way).
        [FieldOffset(0x150)] public StdTuple2D<int> TargetGridPosition;

        // Which of the skill's stat sets the action's stats come from.
        [FieldOffset(0x19C)] public byte StatSetIndex;
    }
}
