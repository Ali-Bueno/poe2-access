namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using Entity = GameReader.RemoteObjects.States.InGameStateObjects.Entity;

    /// <summary>
    ///     Where an entity stands (world): by its Render component or, for one the game does not draw (a boss's cover, a
    ///     marker), by the cell its Positioned component holds, at the offset <see cref="PositionedGridSearch"/> finds live.
    /// </summary>
    public sealed class EntityPlaces
    {
        /// <summary>A search that found nothing (too few entities around) is tried again this much later (ms).</summary>
        private const int RetryMs = 2000;

        /// <summary>An entity stands in the middle of its cell (a trader at world 3864 in cell 355 of 10.87: 355.5 cells).</summary>
        private const float HalfCell = 0.5f;

        /// <summary>Entities read per search: enough to rule out a chance match, few enough for one frame.</summary>
        private const int MaxSamples = 16;

        private readonly HostMemoryReader memory;
        private readonly PluginLog log;
        private int? offset;
        private long lastSearch = long.MinValue / 2;

        public EntityPlaces(HostMemoryReader memory, PluginLog log)
        {
            this.memory = memory;
            this.log = log;
        }

        /// <summary>The offset found, for the log; null until a search succeeds.</summary>
        public int? CellOffset => this.offset;

        /// <summary>Looks for the cell's offset where it is not known yet, wherever enough entities stand (a town), ahead of the fight that needs it.</summary>
        public void Learn() => this.Offset();

        /// <summary>A new game process: the layout may be another build's.</summary>
        public void Reset() => this.offset = null;

        /// <summary>The entity's place (world), or null when it has neither component or its cell cannot be read.</summary>
        public Vector2? Of(Entity entity)
        {
            if (entity.TryGetComponent<Render>(out var render))
            {
                return new Vector2(render.WorldPosition.X, render.WorldPosition.Y);
            }

            if (!entity.TryGetComponent<Positioned>(out var positioned) || this.Offset() is not { } at)
            {
                return null;
            }

            Span<byte> cell = stackalloc byte[2 * sizeof(int)];
            if (!this.memory.TryReadBytes(positioned.Address + at, cell))
            {
                return null;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var (x, y) = (BitConverter.ToInt32(cell), BitConverter.ToInt32(cell[sizeof(int)..]));
            var bytesPerRow = area.TerrainMetadata.BytesPerRow;
            var inGrid = bytesPerRow > 0 && x > 0 && y > 0 && x < bytesPerRow * 2 && y < area.GridWalkableData.Length / bytesPerRow;
            return inGrid ? new Vector2(x + HalfCell, y + HalfCell) * area.WorldToGridConvertor : null;
        }

        /// <summary>
        ///     Research: for a few drawn entities, where in Positioned an integer or a float holds their cell or their world
        ///     place, each axis apart, so a search that finds nothing shows what the component really keeps.
        /// </summary>
        public IEnumerable<string> Evidence()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var cell = area.WorldToGridConvertor;
            var shown = 0;
            foreach (var entity in area.AwakeEntities.Values)
            {
                if (shown >= 6 || cell <= 0 || !entity.IsValid || !entity.TryGetComponent<Render>(out var render) || !entity.TryGetComponent<Positioned>(out var positioned) ||
                    this.memory.ReadBytes(positioned.Address, PositionedGridSearch.SearchBytes) is not { Length: PositionedGridSearch.SearchBytes } bytes)
                {
                    continue;
                }

                shown++;
                var (wx, wy) = (render.WorldPosition.X, render.WorldPosition.Y);
                var (gx, gy) = (wx / cell, wy / cell);
                var hits = new List<string>();
                for (var at = 0; at + sizeof(int) <= bytes.Length; at++)
                {
                    var (whole, real) = (BitConverter.ToInt32(bytes, at), BitConverter.ToSingle(bytes, at));
                    foreach (var (name, want, slack) in new[] { ("gx", gx, 1.5f), ("gy", gy, 1.5f), ("wx", wx, cell), ("wy", wy, cell) })
                    {
                        if (MathF.Abs(whole - want) <= slack)
                        {
                            hits.Add($"+0x{at:X} int {name}={whole}");
                        }

                        if (float.IsFinite(real) && MathF.Abs(real - want) <= slack)
                        {
                            hits.Add($"+0x{at:X} float {name}={real:0.#}");
                        }
                    }
                }

                yield return $"{entity.Id} {entity.Path} world {wx:0},{wy:0} cell {gx:0.#},{gy:0.#}: {string.Join("; ", hits)}";
            }

            yield return $"cell size {cell:0.###}, {shown} entities shown";
        }

        private int? Offset()
        {
            var now = Environment.TickCount64;
            if (this.offset != null || now - this.lastSearch < RetryMs)
            {
                return this.offset;
            }

            this.lastSearch = now;
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var cell = area.WorldToGridConvertor;
            var samples = new List<(byte[] Bytes, int X, int Y)>();
            foreach (var entity in area.AwakeEntities.Values)
            {
                if (samples.Count >= MaxSamples)
                {
                    break;
                }

                if (cell > 0 && entity.IsValid && entity.TryGetComponent<Render>(out var render) && entity.TryGetComponent<Positioned>(out var positioned) &&
                    this.memory.ReadBytes(positioned.Address, PositionedGridSearch.SearchBytes) is { Length: PositionedGridSearch.SearchBytes } bytes)
                {
                    samples.Add((bytes, (int)MathF.Floor(render.WorldPosition.X / cell), (int)MathF.Floor(render.WorldPosition.Y / cell)));
                }
            }

            this.offset = PositionedGridSearch.Find(samples);
            if (this.offset is { } found)
            {
                this.log.Info($"Positioned cell: found at +0x{found:X} from {samples.Count} entities");
            }

            return this.offset;
        }
    }
}
