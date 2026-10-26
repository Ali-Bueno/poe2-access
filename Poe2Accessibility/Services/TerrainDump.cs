namespace Poe2Accessibility.Services
{
    using System;
    using System.IO;
    using System.Text;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
    using GameOffsets.Natives;

    /// <summary>
    ///     Research (dev command "terrain"): the area's four terrain grid layers around the player, one hex digit per
    ///     cell, to tell which layer blocks sight or projectiles (walkable is the first; the others are undecoded).
    /// </summary>
    public sealed class TerrainDump
    {
        /// <summary>Terrain struct inside the area instance (GameOffsets AreaInstanceOffsets.TerrainMetadata).</summary>
        private const int TerrainOffset = 0x8D0;

        /// <summary>Std vectors of the terrain struct: walkable, "landscape", layer 3, layer 4 (POE2Radar Poe2Offsets).</summary>
        private static readonly int[] LayerOffsets = { 0xD0, 0xE8, 0x100, 0x118 };

        private const int HalfWidth = 40;
        private const int HalfHeight = 20;
        private const int MaxLayerBytes = 64 * 1024 * 1024;

        private readonly HostMemoryReader memory;
        private readonly PluginLog log;
        private readonly string directory;

        public TerrainDump(HostMemoryReader memory, PluginLog log, string directory)
        {
            this.memory = memory;
            this.log = log;
            this.directory = directory;
        }

        public void Write()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var row = area.TerrainMetadata.BytesPerRow;
            var cellSize = area.WorldToGridConvertor;
            var centre = ((int)(player.WorldPosition.X / cellSize), (int)(player.WorldPosition.Y / cellSize));
            var text = new StringBuilder();
            text.AppendLine($"# terrain layers around the player at cell {centre}, {row} bytes per row; player = @");
            byte[]? walkable = null;
            foreach (var offset in LayerOffsets)
            {
                var address = area.Address + TerrainOffset + offset;
                if (!this.memory.TryRead<StdVector>(address, out var vector))
                {
                    continue;
                }

                var length = vector.Last.ToInt64() - vector.First.ToInt64();
                text.AppendLine($"## layer +0x{offset:X}: {length} bytes ({(row > 0 ? length / row : 0)} rows)");
                if (length <= 0 || length > MaxLayerBytes || row <= 0)
                {
                    continue;
                }

                var bytes = this.memory.ReadStdVector<byte>(vector, (int)length);
                walkable ??= bytes;
                text.AppendLine(Histogram(walkable, bytes));
                for (var y = centre.Item2 - HalfHeight; y <= centre.Item2 + HalfHeight; y++)
                {
                    for (var x = centre.Item1 - HalfWidth; x <= centre.Item1 + HalfWidth; x++)
                    {
                        var index = (y * row) + (x / 2);
                        text.Append(x == centre.Item1 && y == centre.Item2 ? '@'
                            : x < 0 || y < 0 || x >= row * 2 || index >= bytes.Length ? ' '
                            : "0123456789ABCDEF"[(bytes[index] >> ((x & 1) * 4)) & 0xF]);
                    }

                    text.AppendLine();
                }
            }

            Directory.CreateDirectory(this.directory);
            var path = Path.Join(this.directory, $"terrain-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, text.ToString());
            if (walkable != null)
            {
                // The whole walkable layer for offline route research: "<name>.walkable" with the row width and the
                // player's cell in the text dump's first line.
                File.WriteAllBytes(Path.ChangeExtension(path, ".walkable"), walkable);
            }
            this.log.Info($"Terrain layers written to {path}");
        }

        /// <summary>Cells per value of this layer over the whole area, split by whether the walkable layer blocks them.</summary>
        private static string Histogram(byte[] walkable, byte[] layer)
        {
            var open = new long[16];
            var blocked = new long[16];
            for (var i = 0; i < layer.Length && i < walkable.Length; i++)
            {
                for (var shift = 0; shift <= 4; shift += 4)
                {
                    var value = (layer[i] >> shift) & 0xF;
                    if (((walkable[i] >> shift) & 0xF) == 0)
                    {
                        blocked[value]++;
                    }
                    else
                    {
                        open[value]++;
                    }
                }
            }

            var text = new StringBuilder("value: on walkable / on blocked cells\n");
            for (var value = 0; value < 16; value++)
            {
                if (open[value] + blocked[value] > 0)
                {
                    text.Append($"  {value:X}: {open[value]} / {blocked[value]}\n");
                }
            }

            return text.ToString();
        }
    }
}