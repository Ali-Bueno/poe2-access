namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Which shrine an object is before it is used: every shrine is one object file, and its kind is the row of the Shrines
    ///     table the server sent (<see cref="GameLayout.ShrineComponent"/>). The row's Id is believed only when the catalog made from
    ///     the game's files knows it (<see cref="ShrineCatalog"/>).
    /// </summary>
    public static class ShrineRow
    {
        /// <summary>Longer than any Id of the table ("ChaosDamageOverTimeLesser").</summary>
        private const int MaxIdChars = 64;

        /// <summary>The Id at the start of the shrine's row; empty when the pointers do not lead to text.</summary>
        public static string IdOf(HostMemoryReader memory, UiElementReader elements, IntPtr component) =>
            memory.TryRead<IntPtr>(component + GameLayout.ShrineComponent.RowOffset, out var row) && memory.IsValidAddress(row) &&
            memory.TryRead<IntPtr>(row, out var text) && memory.IsValidAddress(text) &&
            elements.ReadUtf16BufferAt(text, MaxIdChars) is { Length: > 0 } id
                ? id
                : string.Empty;
    }
}
