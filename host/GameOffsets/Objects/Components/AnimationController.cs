namespace GameOffsets.Objects.Components
{
    using System;
    using System.Runtime.InteropServices;
    using GameOffsets.Natives;

    /// <summary>
    ///     The AnimationController component of an animated object (<see cref="AnimatedOffsets.AnimatedObjectPtr"/>): the clip
    ///     playing and how far it is. Read by the game's GEAL getters and its debug dump on five builds (25244054-26476662),
    ///     checked live 2026-11-08; provenance and checks in docs/research/dodge-timing.md.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct AnimationControllerOffsets
    {
        [FieldOffset(0x000)] public ComponentHeader Header;

        // GetGlobalAnimationSpeed, "Global Animation Speed Multiplier: "; taken as 1 while GlobalSpeedOff is set.
        [FieldOffset(0x178)] public float GlobalSpeed;

        // The clips and their events (AnimationDataOffsets); null without animation data.
        [FieldOffset(0x180)] public IntPtr AnimationDataPtr;

        // CurrentAnimationIndex: the clip's index in the animation data, -1 when none plays.
        [FieldOffset(0x190)] public int CurrentClip;

        // "Current Animation Position: ", seconds into the clip.
        [FieldOffset(0x1A4)] public float CurrentPosition;

        // GetCurrentAnimationSpeed, "Current Animation Speed: ".
        [FieldOffset(0x1B0)] public float CurrentSpeed;

        // GetCurrentAnimationLength is CurrentEnd - CurrentStart, seconds.
        [FieldOffset(0x1B8)] public float CurrentStart;
        [FieldOffset(0x1BC)] public float CurrentEnd;

        [FieldOffset(0x1D4)] public byte GlobalSpeedOff;
    }

    /// <summary>An animated object's animation data (AnimationController +0x180): the .amd and each clip's events.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct AnimationDataOffsets
    {
        // The .amd file (AmdFileOffsets).
        [FieldOffset(0x00)] public IntPtr AmdFilePtr;

        // Replaces the .amd's clip table when set.
        [FieldOffset(0x08)] public IntPtr ClipTablePtr;

        // One 0x18-byte StdVector of event pointers per clip index (event lookup, exe 0x1D47110 in 26476662).
        [FieldOffset(0x10)] public StdVector ClipEvents;
    }

    /// <summary>One event of a clip; events of one kind chain through <see cref="Next"/>, sorted by time.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct AnimationEventOffsets
    {
        [FieldOffset(0x00)] public IntPtr VTablePtr;

        // 0 for a named event (StandardEventType).
        [FieldOffset(0x08)] public uint Kind;

        // Seconds into the clip.
        [FieldOffset(0x0C)] public float Time;

        // Named events only: "contact", "damage", ...
        [FieldOffset(0x18)] public StdString Name;

        // The game's hash of Name (exe 0x1D48E10), so a reading checks itself.
        [FieldOffset(0x38)] public ushort Id;

        [FieldOffset(0x40)] public IntPtr Next;
    }

    /// <summary>An .amd file object: its path, and its clips (0x138-byte records starting with the clip's name).</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct AmdFileOffsets
    {
        [FieldOffset(0x08)] public StdWString Path;
        [FieldOffset(0x28)] public IntPtr ClipTablePtr;
    }

    /// <summary>The clip table of an .amd: a StdVector of 0x138-byte clips, a StdString name first.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct AmdClipTableOffsets
    {
        [FieldOffset(0x38)] public StdVector Clips;
    }
}
