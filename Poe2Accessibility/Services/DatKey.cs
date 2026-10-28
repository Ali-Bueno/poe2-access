namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>A dat foreign key in game memory, (row, file) (<see cref="GameLayout.SkillSlot"/>).</summary>
    public static class DatKey
    {
        /// <summary>The key's row, only when its file is the expected table.</summary>
        public static IntPtr? Row(HostMemoryReader reader, UiElementReader elements, IntPtr key, string table) =>
            reader.TryRead<IntPtr>(key, out var row) && row != IntPtr.Zero &&
            reader.TryRead<IntPtr>(key + GameLayout.SkillSlot.KeyFileOffset, out var file) && file != IntPtr.Zero &&
            string.Equals(elements.ReadWStringStructAt(file + GameLayout.SkillSlot.FileNameOffset), table, StringComparison.OrdinalIgnoreCase)
                ? row
                : null;
    }
}
