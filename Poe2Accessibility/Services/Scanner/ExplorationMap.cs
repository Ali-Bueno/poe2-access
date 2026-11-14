namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     Where the player has been in this area, for the scanner's unexplored-areas category (D2R NF2). PoE2 loads
    ///     the whole walkable grid with the area, so "unexplored" is ground never seen: blocks of BlockCells² grid
    ///     cells count as seen once the player comes within <see cref="SeenRadiusCells"/>. The frontier is the
    ///     walkable, reachable, unseen blocks next to seen walkable ones, grouped by connected run and by direction
    ///     from the player; each group is offered at its block nearest the player. Pure: the caller passes the grid
    ///     (two cells per byte, low nibble = even x, 0 = blocked) and positions in grid cells.
    /// </summary>
    public sealed partial class ExplorationMap
    {
        public const int BlockCells = 8;

        /// <summary>A scanner entry's kind for a pocket (<see cref="FrontierPlace"/>).</summary>
        public const string PocketKind = "pocket";

        /// <summary>A scanner entry's kind for unseen ground that soon ends (<see cref="UnseenSize.Small"/>).</summary>
        public const string SmallKind = "small_unexplored";

        /// <summary>Where a scanner entry of the unexplored category stands among them by its kind: ground that goes on, then what soon ends, then nooks.</summary>
        public static int Rank(string kind) => kind == PocketKind ? 2 : kind == SmallKind ? 1 : 0;


        /// <summary>Ground this near the player counts as seen (about half the screen's height).</summary>
        public const float SeenRadiusCells = 60f;

        /// <summary>
        ///     A radar ray is marked unexplored when it leaves the seen ground within this many cells (LeAccess: twice the
        ///     reveal radius). Farther, the edge of the strip a walk revealed is met by the ray pointing back along it.
        /// </summary>
        public const float MarkReachCells = 2 * SeenRadiusCells;

        /// <summary>A block with fewer walkable cells is wall (a corridor three cells wide crossing it still counts).</summary>
        private const int MinWalkableCells = 6;

        /// <summary>Smaller frontier groups before a pocket are not worth a place of their own.</summary>
        private const int MinGroupBlocks = 2;

        /// <summary>Unseen ground smaller than this is a pocket: half the reveal disc, what one look from its edge shows whole.</summary>
        private const int PocketBlocks = (int)(Math.PI * SeenRadiusCells * SeenRadiusCells / (2 * BlockCells * BlockCells));

        /// <summary>
        ///     Unseen ground under two reveal discs soon ends, a room or a short branch; ten discs and more is a large part of
        ///     an area. Not measured against the game's areas: the first guess of 2026-11-12, when a walk to "unexplored ground"
        ///     kept ending at a wall (La ciudad perdida: 50 such entries asked for in 77 minutes, user's log).
        /// </summary>
        private const int SmallBlocks = 4 * PocketBlocks;
        private const int LargeBlocks = 20 * PocketBlocks;

        /// <summary>
        ///     Four times the largest floor known (a Trial of the Sekhemas floor: 64,080 sub-tiles of 23 x 23 cells, 2026-11-13): a kept
        ///     map claiming more is not one. Its file then holds at most 260 KB of bits.
        /// </summary>
        private const long MaxBlocks = 4L * 64_080 * 23 * 23 / (BlockCells * BlockCells);

        /// <summary>cos 45°: the edge of a ray's quarter.</summary>
        private static readonly float QuarterCos = MathF.Sqrt(0.5f);

        private bool[] seen = Array.Empty<bool>();
        private int blocksX;
        private int blocksY;

        /// <summary>
        ///     What the last <see cref="Frontier"/> found: the blocks the player can reach, the size of each one's unseen ground,
        ///     and the smallest unseen ground worth a mark: nooks only once nothing else is left.
        /// </summary>
        private (bool[] Reachable, int[] UnseenGround, int MarkFloor) ground = NoGround;

        private static readonly (bool[], int[], int) NoGround = (Array.Empty<bool>(), Array.Empty<int>(), 0);

        /// <summary>The buffers of this map's own walks, and those lent to its snapshots (one frontier at a time).</summary>
        private GridWalks walks = new();
        private GridWalks? lent;

        /// <summary>How many blocks have been seen: it grows while the player finds new ground.</summary>
        public int SeenBlocks { get; private set; }

        /// <summary>A new area: nothing seen, and the last area's walk buffers let go.</summary>
        public void Reset()
        {
            this.seen = Array.Empty<bool>();
            this.blocksX = this.blocksY = 0;
            this.ground = NoGround;
            this.SeenBlocks = 0;
            this.walks = new GridWalks();
            this.lent = null;
        }

        /// <summary>
        ///     A map sharing this one's seen blocks, for a <see cref="Frontier"/> worked out off the frame thread: on a large
        ///     floor it takes a tenth of a second (docs/research/performance.md). <see cref="Adopt"/> takes what it found.
        /// </summary>
        public ExplorationMap Snapshot() => new() { seen = this.seen, blocksX = this.blocksX, blocksY = this.blocksY, walks = this.lent ??= new GridWalks() };

        /// <summary>Takes the ground a snapshot's <see cref="Frontier"/> found; false when the map is another by now (a new area).</summary>
        public bool Adopt(ExplorationMap snapshot)
        {
            if (!ReferenceEquals(snapshot.seen, this.seen))
            {
                return false;
            }

            this.ground = snapshot.ground;
            return true;
        }

        /// <summary>The seen blocks as bytes, to be kept past the area and the host (<see cref="AreaMemory"/>): two ints, a bit a block.</summary>
        public byte[] Save()
        {
            var bytes = new byte[8 + ((this.seen.Length + 7) / 8)];
            BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), this.blocksX);
            BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), this.blocksY);
            for (var i = 0; i < this.seen.Length; i++)
            {
                if (this.seen[i])
                {
                    bytes[8 + (i >> 3)] |= (byte)(1 << (i & 7));
                }
            }

            return bytes;
        }

        /// <summary>Blocks kept by <see cref="Save"/>; false, and nothing changed, for bytes that cannot be such.</summary>
        public bool Load(byte[] bytes)
        {
            if (bytes.Length < 8)
            {
                return false;
            }

            var bx = BitConverter.ToInt32(bytes, 0);
            var by = BitConverter.ToInt32(bytes, 4);
            if (bx <= 0 || by <= 0 || (long)bx * by > MaxBlocks || bytes.Length != 8 + (((bx * by) + 7) / 8))
            {
                return false;
            }

            var seen = new bool[bx * by];
            for (var i = 0; i < seen.Length; i++)
            {
                seen[i] = (bytes[8 + (i >> 3)] & (1 << (i & 7))) != 0;
            }

            (this.blocksX, this.blocksY, this.seen) = (bx, by, seen);
            this.ground = NoGround;
            this.SeenBlocks = seen.Count(block => block);
            return true;
        }

        /// <summary>Marks the blocks around <paramref name="at"/> (grid cells) as seen.</summary>
        public void Visit(Vector2 at, int widthCells, int heightCells)
        {
            var bx = (widthCells + BlockCells - 1) / BlockCells;
            var by = (heightCells + BlockCells - 1) / BlockCells;
            if (bx <= 0 || by <= 0)
            {
                return;
            }

            if (bx != this.blocksX || by != this.blocksY)
            {
                (this.blocksX, this.blocksY, this.seen) = (bx, by, new bool[bx * by]);
                this.ground = NoGround;
                this.SeenBlocks = 0;
            }

            var radius = (int)MathF.Ceiling(SeenRadiusCells / BlockCells);
            var cx = (int)(at.X / BlockCells);
            var cy = (int)(at.Y / BlockCells);
            for (var y = Math.Max(0, cy - radius); y <= Math.Min(by - 1, cy + radius); y++)
            {
                for (var x = Math.Max(0, cx - radius); x <= Math.Min(bx - 1, cx + radius); x++)
                {
                    var centre = new Vector2((x + 0.5f) * BlockCells, (y + 0.5f) * BlockCells);
                    if (Vector2.Distance(centre, at) <= SeenRadiusCells && !this.seen[(y * bx) + x])
                    {
                        this.seen[(y * bx) + x] = true;
                        this.SeenBlocks++;
                    }
                }
            }
        }

        /// <summary>The ground at <paramref name="cell"/> (grid cells) has been seen.</summary>
        public bool IsSeen(Vector2 cell)
        {
            var x = (int)(cell.X / BlockCells);
            var y = (int)(cell.Y / BlockCells);
            return x >= 0 && y >= 0 && x < this.blocksX && y < this.blocksY && this.seen[(y * this.blocksX) + x];
        }
    }

    /// <summary>
    ///     How much unseen ground lies behind an unexplored place, what a look at the map tells a sighted player: a pocket is
    ///     what one look from its edge would show whole, a nook with no way on; small soon ends; large is a great part of an area.
    /// </summary>
    public enum UnseenSize
    {
        Pocket,
        Small,
        Medium,
        Large,
    }

    /// <summary>An unexplored place: where unseen ground begins, and how much of it there is.</summary>
    public readonly record struct FrontierPlace(Vector2 Cell, UnseenSize Size)
    {
        public bool Pocket => this.Size == UnseenSize.Pocket;
    }
}
