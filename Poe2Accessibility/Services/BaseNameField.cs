namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>
    ///     Where an item's base name is read (GameHelper's BaseOffsets: the Base component's name row pointer, the name in
    ///     the row), which a game update can move with nothing static to check it by (docs/compatibility.md). Checked on
    ///     inventory tooltips instead: a normal or magic item's title holds its base name ("Broquel de cuero", "Broquel de
    ///     cuero del zorro"). Two titles that hold the name prove it; two that do not start a search for the nearest
    ///     place whose name the title holds, taken once a second title agrees. Each item's title counts once (the
    ///     focused item is described again on every value poll). Pure.
    /// </summary>
    public sealed class BaseNameField
    {
        /// <summary>Titles that must agree before a place is believed (one could hold a short name by chance).</summary>
        public const int Agreeing = 2;

        /// <summary>Titles in a row that miss the name before a search; that miss every place before the name is given up.</summary>
        public const int MissesToSearch = 2;
        public const int MissesToGiveUp = 6;

        public const int RowRadius = 0x40;
        public const int NameRadius = 0x80;
        public const int Step = 8;

        /// <summary>A name this short could be held by a title by chance ("de").</summary>
        public const int MinNameLength = 3;

        private readonly (int Row, int Name) measured;
        private (int Row, int Name)? candidate;
        private (IntPtr Item, string Title)? last;
        private int agreed;
        private int missed;

        public BaseNameField(int measuredRow, int measuredName)
        {
            this.measured = (measuredRow, measuredName);
            this.Offsets = this.measured;
        }

        /// <summary>Where the name is read now: where measured, or where a search found it.</summary>
        public (int Row, int Name) Offsets { get; private set; }

        /// <summary>Settled: <see cref="Offsets"/> holds the verdict, or <see cref="Lost"/> is set.</summary>
        public bool Judged { get; private set; }

        /// <summary>No place holds the name the titles show: base names are not read (titles are used instead).</summary>
        public bool Lost { get; private set; }

        public bool Moved => this.Judged && !this.Lost && this.Offsets != this.measured;

        /// <summary>
        ///     One tooltip title of a normal or magic <paramref name="item"/>, with <paramref name="nameAt"/> reading its name
        ///     at a row pointer offset and a name offset (empty where none); true on the call that settles the field.
        /// </summary>
        public bool Observe(IntPtr item, string title, Func<int, int, string> nameAt)
        {
            if (this.Judged || string.IsNullOrWhiteSpace(title) || this.last == (item, title))
            {
                return false;
            }

            this.last = (item, title);
            var place = this.candidate ?? this.Offsets;
            if (Holds(title, nameAt(place.Row, place.Name)))
            {
                this.missed = 0;
                if (++this.agreed < Agreeing)
                {
                    return false;
                }

                (this.Offsets, this.Judged) = (place, true);
                return true;
            }

            (this.candidate, this.agreed) = (null, 0);
            if (++this.missed < MissesToSearch)
            {
                return false;
            }

            this.candidate = OffsetSearch.Outward(this.measured.Row, RowRadius, Step)
                .SelectMany(row => OffsetSearch.Outward(this.measured.Name, NameRadius, Step).Select(name => (Row: row, Name: name)))
                .Where(at => Holds(title, nameAt(at.Row, at.Name)))
                .Cast<(int Row, int Name)?>()
                .FirstOrDefault();
            if (this.candidate != null)
            {
                this.agreed = 1;
                return false;
            }

            if (this.missed >= MissesToGiveUp)
            {
                (this.Judged, this.Lost) = (true, true);
                return true;
            }

            return false;
        }

        /// <summary>A new game process: judged again from where it was measured.</summary>
        public void Reset()
        {
            (this.Offsets, this.candidate, this.last, this.agreed, this.missed, this.Judged, this.Lost) = (this.measured, null, null, 0, 0, false, false);
        }

        private static bool Holds(string title, string name) =>
            name.Length >= MinNameLength && name.Any(char.IsLetter) && title.Contains(name, StringComparison.OrdinalIgnoreCase); // a prefix can lower the name's first letter
    }
}
