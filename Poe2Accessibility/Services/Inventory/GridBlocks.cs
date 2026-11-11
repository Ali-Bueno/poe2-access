namespace Poe2Accessibility.Services
{
    /// <summary>Blocks of cells in an inventory grid: each a top left cell and how many columns and rows it spans. Pure.</summary>
    public static class GridBlocks
    {
        /// <summary>Whether two blocks share a cell.</summary>
        public static bool Overlap(int column, int row, int columns, int rows, int otherColumn, int otherRow, int otherColumns, int otherRows) =>
            column < otherColumn + otherColumns && otherColumn < column + columns && row < otherRow + otherRows && otherRow < row + rows;
    }
}
