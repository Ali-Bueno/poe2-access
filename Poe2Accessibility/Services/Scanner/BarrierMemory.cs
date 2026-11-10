namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The live walkable grid is only true near the player. The client only knows the entities of its network
    ///     bubble (about 200 cells): a tutorial barrier past it reads walkable, closes as the player walks up and opens
    ///     again as they walk away, so the guide flipped between the long way through it and the way that stops short
    ///     (Riverbank, 2026-10-24). A cell that was walkable once and reads closed stays closed for routes until it reads
    ///     walkable within <see cref="TrustCells"/> of the player.
    /// </summary>
    internal sealed class BarrierMemory
    {
        /// <summary>Cells (each axis) around the player whose live state is believed, well inside the bubble.</summary>
        public const int TrustCells = 120;

        /// <summary>Per cell nibble: non-zero where the cell was ever walkable (the grid as loaded, then every sample).</summary>
        private byte[] everOpen = Array.Empty<byte>();

        /// <summary>Per cell nibble: 0xF where a once walkable cell read closed and has not read walkable near the player since.</summary>
        private byte[] closed = Array.Empty<byte>();
        private int closedCount;

        /// <summary>Cells remembered closed.</summary>
        public int ClosedCount => this.closedCount;

        /// <summary>A new area.</summary>
        public void Reset()
        {
            this.everOpen = Array.Empty<byte>();
            this.closed = Array.Empty<byte>();
            this.closedCount = 0;
        }

        /// <summary>One live sample, with the grid as the area loaded (to know a barrier closed before the first sample).</summary>
        public void Observe(byte[] live, byte[] loaded, int bytesPerRow, int playerX, int playerY)
        {
            if (bytesPerRow <= 0)
            {
                return;
            }

            if (this.everOpen.Length != live.Length)
            {
                this.everOpen = loaded.Length == live.Length ? (byte[])loaded.Clone() : new byte[live.Length];
                this.closed = new byte[live.Length];
                this.closedCount = 0;
            }

            // Closed is believed at any distance (the client blocks a cell only for an entity it knows); walkable only
            // near the player.
            for (var i = 0; i < live.Length; i++)
            {
                var now = Nibbles(live[i]);
                var shut = (byte)(Nibbles(this.everOpen[i] |= live[i]) & ~now & ~this.closed[i]);
                if (shut != 0)
                {
                    this.closed[i] |= shut;
                    this.closedCount += Count(shut);
                }

                var reopened = (byte)(now & this.closed[i]);
                for (var half = 0; reopened != 0 && half < 2; half++)
                {
                    var mask = (byte)(half == 0 ? 0x0F : 0xF0);
                    if ((reopened & mask) != 0 &&
                        Math.Abs(((i % bytesPerRow) * 2) + half - playerX) <= TrustCells &&
                        Math.Abs((i / bytesPerRow) - playerY) <= TrustCells)
                    {
                        this.closed[i] &= (byte)~mask;
                        this.closedCount--;
                    }
                }
            }
        }

        /// <summary>0x0F and/or 0xF0 for the walkable cells of a grid byte.</summary>
        private static byte Nibbles(byte value) => (byte)(((value & 0x0F) != 0 ? 0x0F : 0) | ((value & 0xF0) != 0 ? 0xF0 : 0));

        private static int Count(byte nibbles) => ((nibbles & 0x0F) != 0 ? 1 : 0) + ((nibbles & 0xF0) != 0 ? 1 : 0);

        /// <summary>Clears the remembered closed cells from a grid of the same area, in place.</summary>
        public void Apply(byte[] grid)
        {
            if (this.closedCount == 0 || grid.Length != this.closed.Length)
            {
                return;
            }

            for (var i = 0; i < grid.Length; i++)
            {
                grid[i] &= (byte)~this.closed[i];
            }
        }
    }
}
