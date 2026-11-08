namespace GameOffsets.Objects.Components
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    ///     Native layout of the <c>Base</c> component (present on item entities).
    ///     It carries the item's BASE TYPE, including the rendered, localized display
    ///     name (e.g. "Greater Orb of Augmentation") and the internal BaseItemTypes row.
    /// </summary>
    /// <remarks>
    ///     Recovered from the POE2Radar project and validated live on 2026-06-20 against a
    ///     dropped Greater Orb of Augmentation:
    ///     <list type="bullet">
    ///         <item><c>+0x10</c> → a row whose <c>+0x30</c> is a pointer to the UTF-16 display name.</item>
    ///         <item><c>+0x18</c> → the BaseItemTypes row (<c>+0x00</c> internal id e.g.
    ///         "CurrencyAddModToMagic2", <c>+0x08</c> .dds art, <c>+0x10</c> .ao).</item>
    ///     </list>
    ///     The display name is the price-lookup key for NON-unique items (currency / runes /
    ///     essences / …) whose shared .dds art can't disambiguate across tiers.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct BaseOffsets
    {
        [FieldOffset(0x0000)] public ComponentHeader Header;

        // → name row; (name row + DisplayNameOffset) is a pointer to the UTF-16 display name.
        [FieldOffset(0x0010)] public IntPtr DisplayNameRowPtr;

        // → BaseItemTypes row; (row + 0x00) is a pointer to the internal (metadata) base-type id.
        [FieldOffset(0x0018)] public IntPtr BaseItemTypesRowPtr;

        /// <summary>Offset within <see cref="DisplayNameRowPtr" />'s row of the UTF-16 display-name pointer.</summary>
        public const int DisplayNameOffset = 0x30;
    }
}
