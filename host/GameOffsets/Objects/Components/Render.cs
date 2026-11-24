namespace GameOffsets.Objects.Components
{
    using System.Runtime.InteropServices;
    using Natives;

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct RenderOffsets
    {
        [FieldOffset(0x0000)] public ComponentHeader Header;

        // Same as Positioned Component CurrentWorldPosition,
        // but this one contains Z axis; Z axis is where the HealthBar is.
        // If you want to use ground Z axis, swap current one with TerrainHeight.
        [FieldOffset(0x138)] public StdTuple3D<float> CurrentWorldPosition;

        // Changing this value will move the in-game healthbar up/down.
        // Not sure if it's really X,Y,Z or something else. They all move
        // healthbar up/down. This might be useless.
        [FieldOffset(0x144)] public StdTuple3D<float> CharactorModelBounds;

        // [FieldOffset(0x00A0)] public StdWString ClassName;

        // Radians, the same as Positioned's after its cell (+0x44C) and as the next float here. Found live 2026-11-24 (0.5.5f):
        // the Singing Caverns' Matiki and water door hold their room file's turns (docs/research/boss-mechanics.md, Diamora).
        [FieldOffset(0x188)] public float Rotation;
        [FieldOffset(0x1B0)] public float TerrainHeight;
    }
}
