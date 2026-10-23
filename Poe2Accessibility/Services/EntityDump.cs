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
        private static readonly string[] ProbedComponents = { "AreaTransition", "NPC" };

        private static readonly System.Reflection.FieldInfo? ComponentMap =
            typeof(GameHelper.RemoteObjects.States.InGameStateObjects.Entity).GetField("componentAddresses", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private readonly UiElementReader reader;
        private readonly HostMemoryReader memory;
        private readonly PluginLog log;
        private readonly string directory;

        public EntityDump(UiElementReader reader, HostMemoryReader memory, PluginLog log, string directory)
        {
            this.reader = reader;
            this.memory = memory;
            this.log = log;
            this.directory = directory;
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
                var targetable = entity.TryGetComponent<Targetable>(out var target) ? (target.IsTargetable ? "targetable" : "untargetable") : "-";
                var name = this.reader.ReadWStringStructAt(row.Render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty;
                var flags = target == null ? string.Empty : $" target[30..80]={Convert.ToHexString(this.memory.ReadBytes(target.Address + 0x30, 0x50))}";
                sb.AppendLine($"{row.Distance,6:F0} ({row.Dx,6:F0},{row.Dy,6:F0}) id={entity.Id} @0x{entity.Address.ToInt64():X} {entity.EntityType}/{entity.EntitySubtype} icon=\"{icon}\" {life} {reaction} {targetable}{flags} {entity.Path} name=\"{name}\"");
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
