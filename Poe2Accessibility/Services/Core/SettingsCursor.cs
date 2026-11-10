namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Where the settings menu stands (D2R submenus, nested: Sound holds a submenu per kind of sound): on an entry of
    ///     the list it is in, each submenu remembering the entry it was left on, every list wrapping. Pure.
    /// </summary>
    public sealed class SettingsCursor
    {
        private readonly Node root;
        private readonly List<int> path = new() { 0 };
        private readonly Dictionary<string, int> leftOn = new();

        public SettingsCursor(Node root)
        {
            this.root = root;
        }

        /// <summary>The entry indices from the top level down to the entry stood on.</summary>
        public IReadOnlyList<int> Path => this.path;

        /// <summary>Inside a submenu; at the top level only the main submenus are listed.</summary>
        public bool InGroup => this.path.Count > 1;

        /// <summary>The entry stood on opens a submenu.</summary>
        public bool OnSubmenu => this.Stood.IsMenu;

        /// <summary>One-based place in the list stood in, and that list's length ("3 of 12").</summary>
        public (int Position, int Count) Place => (this.path[^1] + 1, this.List.Entries.Count);

        private Node List => this.path.Take(this.path.Count - 1).Aggregate(this.root, (node, index) => node.Entries[index]);

        private Node Stood => this.List.Entries[this.path[^1]];

        /// <summary>Next / previous entry of the list stood in.</summary>
        public void Move(int step) => this.path[^1] = Wrap(this.path[^1] + step, this.List.Entries.Count);

        /// <summary>Opens the submenu stood on, at the entry it was left on; false on a setting.</summary>
        public bool Enter()
        {
            if (!this.OnSubmenu)
            {
                return false;
            }

            this.path.Add(this.leftOn.GetValueOrDefault(Key(this.path)));
            return true;
        }

        /// <summary>Back to the list above, on the submenu just left; false at the top level.</summary>
        public bool Back()
        {
            if (!this.InGroup)
            {
                return false;
            }

            this.leftOn[Key(this.path.Take(this.path.Count - 1))] = this.path[^1];
            this.path.RemoveAt(this.path.Count - 1);
            return true;
        }

        public static int Wrap(int index, int count) => ((index % count) + count) % count;

        private static string Key(IEnumerable<int> path) => string.Join('/', path);

        /// <summary>A menu entry: a submenu lists entries of its own, a setting none.</summary>
        public sealed record Node(IReadOnlyList<Node> Entries)
        {
            public static readonly Node Setting = new(Array.Empty<Node>());

            public bool IsMenu => this.Entries.Count > 0;
        }
    }
}
