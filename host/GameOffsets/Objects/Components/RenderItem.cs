namespace GameOffsets.Objects.Components
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    ///     Native layout of the <c>RenderItem</c> component (present on item entities).
    ///     It carries the item's 2D inventory art (.dds) path.
    /// </summary>
    /// <remarks>
    ///     Recovered from the POE2Radar project. <c>+0x28</c> is a pointer to the UTF-16 .dds art
    ///     path; its basename is a stable, unambiguous price-lookup key for uniques (each unique
    ///     has its own icon, and it matches poe.ninja / poe2scout's IconUrl basename). NB: later
    ///     offsets list socketed-gem art, so the FIRST entry (this one) is the item's own art.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct RenderItemOffsets
    {
        [FieldOffset(0x0000)] public ComponentHeader Header;

        // → UTF-16 .dds art path (the item's own 2D inventory icon).
        [FieldOffset(0x0028)] public IntPtr ResourcePathPtr;
    }
}
