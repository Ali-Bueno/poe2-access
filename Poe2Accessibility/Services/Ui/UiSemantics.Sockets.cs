namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     A piece of gear's sockets and the runes in them, for <see cref="UiSemantics"/> (docs/research/item-mods.md,
    ///     "Sockets and runes"): the tooltip draws the sockets as art only and shows a rune's lines among the modifiers
    ///     with nothing to tell them apart, so the sockets are read from memory and the runes' lines found by the files.
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The readings kept; past this many they are dropped together.</summary>
        private const int MaxSocketReadings = 256;

        /// <summary>Each piece's last reading, by entity and tooltip: the line and where it goes, and the tooltip's lines it takes.</summary>
        private readonly Dictionary<(IntPtr Entity, int Tooltip), (string? Line, int At, IReadOnlyList<int> Given)> socketReadings = new();

        /// <summary>
        ///     The tooltip with the item's sockets said: "2 engarces: Runa de hierro menor, vacío. Efecto: Daño físico
        ///     aumentado un 14%" where the runes' first line was (they leave their places), else before the item's first
        ///     modifier line, else at the end. A gem's sockets are its supports, said by the build's lines: left alone.
        /// </summary>
        private List<string> WithSockets(IntPtr entity, List<string> details)
        {
            var (line, at, given) = this.SocketReading(entity, details);
            if (line == null)
            {
                return details;
            }

            var said = details.Where((_, index) => !given.Contains(index)).ToList();
            said.Insert(Math.Min(at, said.Count), line);
            return said;
        }

        /// <summary>The tooltip's lines the item's runes give (none for an item without them), so the modifier check leaves them out.</summary>
        private IReadOnlyList<int> RuneLinesOf(IntPtr entity, IReadOnlyList<string> tooltip) => this.SocketReading(entity, tooltip).Given;

        private (string? Line, int At, IReadOnlyList<int> Given) SocketReading(IntPtr entity, IReadOnlyList<string> tooltip)
        {
            var none = ((string?)null, 0, (IReadOnlyList<int>)Array.Empty<int>());
            if (entity == IntPtr.Zero || this.inventory.IsGem(entity))
            {
                return none;
            }

            var key = (entity, tooltip.Aggregate(tooltip.Count, (hash, line) => HashCode.Combine(hash, line)));
            if (this.socketReadings.TryGetValue(key, out var known))
            {
                return known;
            }

            var socketed = this.inventory.SocketedItems(entity);
            var reading = none;
            if (socketed.Count > 0)
            {
                var runes = socketed.Where(item => item != IntPtr.Zero).Select(this.inventory.ItemBaseId).Where(path => path.Length > 0).ToList();
                var modLines = this.inventory.ItemMods(entity) is { } mods
                    ? ItemModLines.Render(mods, Localization.Language, ModStatCatalog.Shared, ModTextCatalog.Shared).Where(line => line.Certain).Select(line => line.Text).ToList()
                    : new List<string>();
                var given = SocketedRunes.LinesGiven(runes, tooltip, modLines, Localization.Language, ModStatCatalog.Shared, ModTextCatalog.Shared);
                var firstMod = tooltip.ToList().FindIndex(modLines.Contains) is >= 0 and var found ? found : tooltip.Count;
                var names = socketed.Select(item => item == IntPtr.Zero ? null : this.inventory.DescribeItemEntity(item) ?? Announcements.UnknownItem).ToList();
                reading = (Announcements.Sockets(names, given.Select(index => tooltip[index]).ToList()), given.Count > 0 ? given[0] : firstMod, given);
            }

            if (this.socketReadings.Count >= MaxSocketReadings)
            {
                this.socketReadings.Clear();
            }

            this.socketReadings[key] = reading;
            return reading;
        }
    }
}
