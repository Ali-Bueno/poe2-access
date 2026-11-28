namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     An Atlas node element's own fields (<see cref="GameLayout.AtlasNode"/>), each found again when a game update moves
    ///     it: its three dat keys by the table each names (<see cref="DatKeySite"/>), and its grid and status, which sit
    ///     right after the keys and have nothing of their own to be told by, by as far as the keys moved.
    ///     <see cref="AtlasState.Plausible"/> then judges what they read.
    /// </summary>
    public static class AtlasNodeFields
    {
        public static readonly DatKeySite MapsKey = new("Atlas node map key", GameLayout.AtlasNode.EndgameMapsKeyOffset, GameLayout.AtlasNode.EndgameMapsTable);

        public static readonly DatKeySite AreaKey = new("Atlas node area key", GameLayout.AtlasNode.AreaKeyOffset, GameLayout.WorldMap.AreaTable);

        public static readonly DatKeySite AtlasKey = new("Atlas node atlas key", GameLayout.AtlasNode.AtlasKeyOffset, GameLayout.AtlasNode.AtlasTable);

        /// <summary>
        ///     How far the node's fields moved: as far as its keys, when those a node has proved agree; 0 when none is proved
        ///     or two say otherwise, one found where it was among them (nothing is guessed between them).
        /// </summary>
        public static int Shift => DatKeySearch.SharedMove(MapsKey.ProvedMove, AreaKey.ProvedMove, AtlasKey.ProvedMove);

        /// <summary>
        ///     Whether an element holds a node's key where the keys are now, with no search: map_control's selected field is
        ///     something else on the act maps, and a search would take that thing's keys for a node's.
        /// </summary>
        public static bool Reads(HostMemoryReader reader, UiElementReader elements, IntPtr element) =>
            AtlasKey.RowAt(reader, elements, element) != null || MapsKey.RowAt(reader, elements, element) != null;

        /// <summary>
        ///     As <see cref="Reads"/>, looking for the keys when they are not where they were: only for an element that sits
        ///     where a node does, as the search takes any key to the Atlas's tables.
        /// </summary>
        public static bool Finds(HostMemoryReader reader, UiElementReader elements, IntPtr element) =>
            AtlasKey.Row(reader, elements, element) != null || MapsKey.Row(reader, elements, element) != null;

        /// <summary>Nodes of a scan each key is looked for on: a key is null on some (a node with no EndgameMapAtlas row).</summary>
        public const int NodesTaught = 8;

        /// <summary>Lets each key be found again on an element known to be a node: a child of the Atlas's canvas, of the nodes' class.</summary>
        public static void Learn(HostMemoryReader reader, UiElementReader elements, IntPtr node)
        {
            _ = MapsKey.Row(reader, elements, node);
            _ = AreaKey.Row(reader, elements, node);
            _ = AtlasKey.Row(reader, elements, node);
        }

        /// <summary>
        ///     Where a field after the keys is now: where the game's code put it (catalog/code-fields.txt), else as far from
        ///     where measured as the keys moved. Never both: the code's place already has the move in it.
        /// </summary>
        public static int Placed(int now, int measured, int shift) => now != measured ? now : measured + shift;

        /// <summary>No two fields of a node are this far apart: one read holds its grid and its status.</summary>
        private const int MostApart = 0x100;

        /// <summary>A node's grid cell and status byte, read at once; null when unreadable.</summary>
        public static (int X, int Y, byte Status)? Read(HostMemoryReader reader, IntPtr node, int shift)
        {
            var grid = Placed(GameLayout.AtlasNode.GridOffset, GameLayout.AtlasNode.GridMeasured, shift);
            var status = Placed(GameLayout.AtlasNode.StatusOffset, GameLayout.AtlasNode.StatusMeasured, shift);
            var first = Math.Min(grid, status);
            var length = Math.Max(grid + GameLayout.AtlasNode.GridBytes, status + 1) - first;
            if (first < 0 || length > MostApart)
            {
                return null;
            }

            var bytes = reader.ReadBytes(node + first, length);
            return bytes.Length == length
                ? (BitConverter.ToInt32(bytes, grid - first), BitConverter.ToInt32(bytes, grid - first + sizeof(int)), bytes[status - first])
                : null;
        }
    }
}
