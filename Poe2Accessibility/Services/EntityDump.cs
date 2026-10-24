namespace Poe2Accessibility.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Research: the awake entities around the player, nearest first (path, GameHelper type, minimap icon,
    ///     life, reaction, targetable, grid offset, name candidates from the Render component). Dev command
    ///     "entities"; feeds the scanner's categories and naming.
    /// </summary>
    public sealed class EntityDump
    {
        /// <summary>Render-component offsets probed for a display name (GameHelper notes a class name at +0xA0).</summary>
        /// <summary>Components whose pointers are followed to strings (destination area, quest state).</summary>
        private static readonly string[] ProbedComponents = { "AreaTransition", "NPC", "Chest", "TriggerableBlockage", "Transitionable", "Counter", "ProximityTrigger", "Timer" };

        private static readonly System.Reflection.FieldInfo? ComponentMap =
            typeof(GameHelper.RemoteObjects.States.InGameStateObjects.Entity).GetField("componentAddresses", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private readonly UiElementReader reader;
        private readonly HostMemoryReader memory;
        private readonly PluginLog log;
        private readonly string directory;
        private readonly MonsterMods mods;

        public EntityDump(UiElementReader reader, HostMemoryReader memory, PluginLog log, string directory)
        {
            this.reader = reader;
            this.memory = memory;
            this.log = log;
            this.directory = directory;
            this.mods = new MonsterMods(memory, reader);
        }

        public string Write(int maxEntities)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!area.Player.TryGetComponent<Render>(out var playerRender))
            {
                return string.Empty;
            }

            var origin = playerRender.GridPosition;
            var rows = area.AwakeEntities.Values
                .Where(entity => entity.IsValid && entity.TryGetComponent<Render>(out _))
                .Select(entity =>
                {
                    entity.TryGetComponent<Render>(out var render);
                    var dx = render!.GridPosition.X - origin.X;
                    var dy = render.GridPosition.Y - origin.Y;
                    return (Entity: entity, Render: render, Dx: dx, Dy: dy, Distance: Math.Sqrt((dx * dx) + (dy * dy)));
                })
                .OrderBy(row => row.Distance)
                .Take(maxEntities)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"# entities around the player at grid ({origin.X:F0},{origin.Y:F0}); dx/dy in grid units");
            foreach (var row in rows)
            {
                var entity = row.Entity;
                var icon = entity.TryGetComponent<MinimapIcon>(out var minimap) ? minimap.IconName ?? string.Empty : string.Empty;
                var life = entity.TryGetComponent<Life>(out var vitals) ? (vitals.IsAlive ? "alive" : "dead") : "-";
                var reaction = entity.TryGetComponent<Positioned>(out var positioned) ? (positioned.IsFriendly ? "friendly" : "hostile") : "-";
                var targetable = entity.TryGetComponent<Targetable>(out var target) ? (EntityCatalog.IsInPlay(this.memory, entity) ? "targetable" : "untargetable") : "-";
                var name = this.reader.ReadWStringStructAt(row.Render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty;
                var flags = target == null ? string.Empty : $" target[30..80]={Convert.ToHexString(this.memory.ReadBytes(target.Address + 0x30, 0x50))}";
                sb.AppendLine($"{row.Distance,6:F0} ({row.Dx,6:F0},{row.Dy,6:F0}) id={entity.Id} @0x{entity.Address.ToInt64():X} {entity.EntityType}/{entity.EntitySubtype} icon=\"{icon}\" {life} {reaction} {targetable}{flags} {entity.Path} name=\"{name}\" bounds=({row.Render.ModelBounds.X:F0},{row.Render.ModelBounds.Y:F0},{row.Render.ModelBounds.Z:F0})");
                if (ComponentMap?.GetValue(entity) is System.Collections.Concurrent.ConcurrentDictionary<string, IntPtr> components)
                {
                    sb.AppendLine($"         components: {string.Join(",", components.Keys.OrderBy(key => key))}");
                    foreach (var probed in ProbedComponents)
                    {
                        if (components.TryGetValue(probed, out var address) && address != IntPtr.Zero)
                        {
                            sb.AppendLine($"         {probed}@0x{address.ToInt64():X}: {this.Strings(address)}");
                        }
                    }

                    if (entity.TryGetComponent<WorldItem>(out var worldItem) && worldItem.ItemEntityAddress != IntPtr.Zero)
                    {
                        // The item a ground entity holds: how its name can be read (gems have no base display name).
                        var item = (GameHelper.RemoteObjects.States.InGameStateObjects.Item)Activator.CreateInstance(
                            typeof(GameHelper.RemoteObjects.States.InGameStateObjects.Item),
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new object[] { worldItem.ItemEntityAddress }, null)!;
                        var itemBase = item.TryGetComponent<Base>(out var baseComponent) ? $"\"{baseComponent.BaseItemName}\" ({baseComponent.InternalName})" : "-";
                        var itemComponents = ComponentMap.GetValue(item) is System.Collections.Concurrent.ConcurrentDictionary<string, IntPtr> held
                            ? string.Join(",", held.Select(pair => $"{pair.Key}@0x{pair.Value.ToInt64():X}").OrderBy(text => text))
                            : string.Empty;
                        sb.AppendLine($"         item @0x{worldItem.ItemEntityAddress.ToInt64():X} {item.Path} base={itemBase} components: {itemComponents}");
                    }

                    if (components.TryGetValue("ObjectMagicProperties", out var magic) && magic != IntPtr.Zero)
                    {
                        this.ModRows(sb, magic);
                        if (entity.TryGetComponent<ObjectMagicProperties>(out var properties))
                        {
                            sb.AppendLine($"         {properties.Rarity}, spoken mods: {string.Join(" | ", this.mods.Of(properties))}");
                        }
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("# tile names (TgtTilesLocations): count, nearest grid offset");
            foreach (var (name, spots) in area.TgtTilesLocations.OrderBy(pair => pair.Key))
            {
                var nearest = spots.OrderBy(spot => ((spot.X - origin.X) * (spot.X - origin.X)) + ((spot.Y - origin.Y) * (spot.Y - origin.Y))).First();
                sb.AppendLine($"{spots.Count,4} ({nearest.X - origin.X,6:F0},{nearest.Y - origin.Y,6:F0}) {name}");
            }

            Directory.CreateDirectory(this.directory);
            var path = Path.Combine(this.directory, $"entities-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, sb.ToString());
            this.log.Info($"Entities written to {path} ({rows.Count})");
            return path;
        }

        /// <summary>
        ///     Research (monster mod names): each Mods.dat row in the ObjectMagicProperties mod vectors (GameHelper's
        ///     layout: +0xB0 details, +0xA0 five 0x18 vectors of 0x40-byte entries, row pointer at +0x28) with every
        ///     byte offset of the row's first 0x200 bytes that points to readable UTF-16.
        /// </summary>
        private void ModRows(StringBuilder sb, IntPtr component)
        {
            for (var list = 0; list < 5; list++)
            {
                var vector = component + 0xB0 + 0xA0 + (list * 0x18);
                if (!this.memory.TryRead<IntPtr>(vector, out var first) || !this.memory.TryRead<IntPtr>(vector + 8, out var last) ||
                    first == IntPtr.Zero || last.ToInt64() <= first.ToInt64())
                {
                    continue;
                }

                for (var entry = first; entry.ToInt64() < last.ToInt64() && entry.ToInt64() < first.ToInt64() + (0x40 * 16); entry += 0x40)
                {
                    if (!this.memory.TryRead<IntPtr>(entry + 0x28, out var row) || !this.memory.IsValidAddress(row))
                    {
                        continue;
                    }

                    var texts = new System.Collections.Generic.List<string>();
                    for (var offset = 0; offset < 0x200; offset++)
                    {
                        if (this.memory.TryRead<IntPtr>(row + offset, out var pointer) && this.memory.IsValidAddress(pointer) &&
                            this.reader.ReadUtf16BufferAt(pointer, 80) is { Length: > 1 } text && !text.Any(char.IsControl))
                        {
                            texts.Add($"+{offset:X}=\"{text}\"");
                        }
                    }

                    sb.AppendLine($"         mod list {list} row 0x{row.ToInt64():X}: {string.Join(" ", texts)}");
                    if (!this.modTableDone)
                    {
                        this.modTableDone = true;
                        this.ModTable(sb, row);
                    }
                }
            }
        }

        private bool modTableDone;

        /// <summary>Research: the Mods.dat row size (distance to the next row whose +0 reads as an id) and rows with text at +0x62.</summary>
        private void ModTable(StringBuilder sb, IntPtr row)
        {
            string? Id(IntPtr at) => this.memory.TryRead<IntPtr>(at, out var pointer) && this.memory.IsValidAddress(pointer) &&
                                     this.reader.ReadUtf16BufferAt(pointer, 80) is { Length: > 3 } text &&
                                     text.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') && char.IsAsciiLetterUpper(text[0])
                ? text
                : null;

            var size = 0;
            for (var offset = 0x40; offset < 0x800 && size == 0; offset++)
            {
                if (Id(row + offset) != null)
                {
                    size = offset;
                }
            }

            sb.AppendLine($"         Mods.dat row size candidate: 0x{size:X}");
            if (size == 0)
            {
                return;
            }

            var start = row;
            while (Id(start - size) != null)
            {
                start -= size;
            }

            var shown = 0;
            var count = 0;
            for (var at = start; Id(at) is { } id; at += size, count++)
            {
                if (shown < 60 && id.StartsWith("Monster", StringComparison.Ordinal) && this.memory.TryRead<IntPtr>(at + 0x62, out var name) &&
                    this.reader.ReadUtf16BufferAt(name, 80) is { Length: > 0 } text)
                {
                    sb.AppendLine($"         row {count} {id} +62=\"{text}\" domain+5E={this.memory.ReadBytes(at + 0x5E, 4)[0]}");
                    shown++;
                }
            }

            sb.AppendLine($"         Mods.dat rows: {count} from 0x{start.ToInt64():X}");
        }

        /// <summary>Each qword of a component's first 0x80 bytes that leads to text: "+18->ptr->\"G1_2\"".</summary>
        private string Strings(IntPtr component)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (var offset = 0x10; offset < 0x80; offset += 8)
            {
                if (!this.memory.TryRead<IntPtr>(component + offset, out var pointer) || !this.memory.IsValidAddress(pointer))
                {
                    continue;
                }

                if (this.reader.ReadUtf16BufferAt(pointer, 64) is { Length: > 2 } direct && direct.All(c => c >= ' ' && c < 0x7F))
                {
                    parts.Add($"+{offset:X}=\"{direct}\"");
                }

                for (var inner = 0; inner < 0x20; inner += 8)
                {
                    if (this.memory.TryRead<IntPtr>(pointer + inner, out var row) && this.memory.IsValidAddress(row) &&
                        this.reader.ReadUtf16BufferAt(row, 64) is { Length: > 2 } text && text.All(c => c >= ' ' && c < 0x7F))
                    {
                        parts.Add($"+{offset:X}->+{inner:X}=\"{text}\"");
                    }
                }
            }

            return string.Join(" ", parts);
        }
    }
}
