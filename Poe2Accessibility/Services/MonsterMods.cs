namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using GameHelper.RemoteObjects.Components;
    using GameOffsets.Objects.Components;

    /// <summary>
    ///     A monster's modifier names as the game shows them under a magic or rare monster's name (D2R reads a
    ///     champion's or unique's enchantments with its name): the implicit and explicit mods of its
    ///     ObjectMagicProperties component (GameHelper's layout), each named by its Mods.dat row's Name in the
    ///     client language. Mods without a name (technical ones such as "MonsterNecromancerRaisable") are left out.
    /// </summary>
    public sealed class MonsterMods
    {
        private const int MaxMods = 16;

        private static readonly int ModsAt = (int)Marshal.OffsetOf<ObjectMagicPropertiesOffsets>(nameof(ObjectMagicPropertiesOffsets.Details1)) +
                                             (int)Marshal.OffsetOf<ModsAndObjectMagicProperties>(nameof(ModsAndObjectMagicProperties.Mods));

        private static readonly int VectorBytes = Marshal.SizeOf<GameOffsets.Natives.StdVector>();
        private static readonly int EntryBytes = Marshal.SizeOf<ModArrayStruct>();
        private static readonly int RowAt = (int)Marshal.OffsetOf<ModArrayStruct>(nameof(ModArrayStruct.ModsPtr));

        /// <summary>The first branch of a gendered name: "&lt;if:MS&gt;{de Rabia}&lt;elif:FS&gt;{…}" → "de Rabia".</summary>
        private static readonly Regex FirstBranch = new(@"^<if:[^>]*>\{([^}]*)\}", RegexOptions.Compiled);

        private readonly HostMemoryReader memory;
        private readonly UiElementReader reader;

        /// <summary>Mods.dat rows are static: row address → spoken name ("" for unnamed rows).</summary>
        private readonly Dictionary<IntPtr, string> names = new();

        public MonsterMods(HostMemoryReader memory, UiElementReader reader)
        {
            this.memory = memory;
            this.reader = reader;
        }

        /// <summary>The named mods of a monster, implicit first, in the game's order; empty when none or unreadable.</summary>
        public IReadOnlyList<string> Of(ObjectMagicProperties magic)
        {
            var found = new List<string>();
            for (var list = 0; list < 2; list++)
            {
                var vector = magic.Address + ModsAt + (list * VectorBytes);
                if (!this.memory.TryRead<IntPtr>(vector, out var first) || !this.memory.TryRead<IntPtr>(vector + 8, out var last) ||
                    first == IntPtr.Zero || last.ToInt64() <= first.ToInt64())
                {
                    continue;
                }

                var count = Math.Min((last.ToInt64() - first.ToInt64()) / EntryBytes, MaxMods);
                for (var i = 0; i < count; i++)
                {
                    if (this.memory.TryRead<IntPtr>(first + (int)(i * EntryBytes) + RowAt, out var row) && this.Name(row) is { Length: > 0 } name &&
                        !found.Contains(name))
                    {
                        found.Add(name);
                    }
                }
            }

            return found;
        }

        /// <summary>Spoken form of a Mods.dat Name: the first gender branch, other tags and braces dropped.</summary>
        internal static string Spoken(string raw)
        {
            var branch = FirstBranch.Match(raw);
            return branch.Success ? branch.Groups[1].Value.Trim() : ControllerGlyphs.Render(raw);
        }

        private string Name(IntPtr row)
        {
            if (row == IntPtr.Zero || !this.memory.IsValidAddress(row))
            {
                return string.Empty;
            }

            if (!this.names.TryGetValue(row, out var name))
            {
                name = this.memory.TryRead<IntPtr>(row + GameLayout.ModsDat.NameOffset, out var text) && this.memory.IsValidAddress(text) &&
                       this.reader.ReadUtf16BufferAt(text, 120) is { Length: > 0 } raw && !raw.Contains('\0')
                    ? Spoken(raw)
                    : string.Empty;
                this.names[row] = name;
            }

            return name;
        }
    }
}
