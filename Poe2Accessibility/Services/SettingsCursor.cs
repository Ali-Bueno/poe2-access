namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     Where the settings menu stands (D2R submenus): on a group of the top level or on an entry within one. Each
    ///     group remembers the entry it was left on, and every list wraps. Pure.
    /// </summary>
    public sealed class SettingsCursor
    {
        private readonly int[] sizes;
        private readonly int[] within;

        /// <param name="sizes">The number of entries of each group, in order.</param>
        public SettingsCursor(int[] sizes)
        {
            this.sizes = sizes;
            this.within = new int[sizes.Length];
        }

        public int Group { get; private set; }

        /// <summary>The entry stood on, or the one the group opens on while at the top level.</summary>
        public int Entry => this.within[this.Group];

        public bool InGroup { get; private set; }

        /// <summary>One-based place in the list stood in, and that list's length ("3 of 12").</summary>
        public (int Position, int Count) Place => this.InGroup
            ? (this.Entry + 1, this.sizes[this.Group])
            : (this.Group + 1, this.sizes.Length);

        /// <summary>Next / previous group, or entry within a group.</summary>
        public void Move(int step)
        {
            if (this.InGroup)
            {
                this.within[this.Group] = Wrap(this.Entry + step, this.sizes[this.Group]);
            }
            else
            {
                this.Group = Wrap(this.Group + step, this.sizes.Length);
            }
        }

        /// <summary>Opens the group stood on, at the entry it was left on.</summary>
        public void Enter() => this.InGroup = true;

        /// <summary>Back to the groups, on the one just left.</summary>
        public void Back() => this.InGroup = false;

        public static int Wrap(int index, int count) => ((index % count) + count) % count;
    }
}
