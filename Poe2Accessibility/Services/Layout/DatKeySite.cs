namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     A dat key a game struct keeps at a fixed offset (a passive node's PassiveSkills row, a world-map pin's MapPins row):
    ///     read where measured; while no struct has read there, a struct whose key does not read is searched nearby for a key
    ///     whose file names the table (<see cref="DatKey"/>), and the place is taken once two structs agree on it.
    /// </summary>
    public sealed class DatKeySite
    {
        // A struct whose key does not read is searched at most this often until one has read.
        private const int SearchEveryMs = 1000;

        // The whole struct is searched less often: it costs a key read per place.
        private const int WideEveryMs = 10_000;

        private readonly string part;
        private readonly int measured;
        private readonly string table;
        private readonly object gate = new();
        private int offset;
        private bool settled;
        private int generation = -1;
        private (int Offset, IntPtr Owner)? candidate;
        private long nextSearchAt;
        private long nextWideAt;

        public DatKeySite(string part, int measured, string table)
        {
            this.part = part;
            this.measured = measured;
            this.table = table;
            this.offset = measured;
        }

        /// <summary>Gets where the key is read now.</summary>
        public int Offset
        {
            get
            {
                this.Fresh();
                lock (this.gate)
                {
                    return this.offset;
                }
            }
        }

        /// <summary>The row of the key that <paramref name="owner"/> keeps, only when its file is the site's table.</summary>
        public IntPtr? Row(HostMemoryReader reader, UiElementReader elements, IntPtr owner)
        {
            var at = this.Offset;
            if (DatKey.Row(reader, elements, owner + at, this.table) is { } row)
            {
                this.Settle(at);
                return row;
            }

            (int Offset, IntPtr Owner)? known;
            lock (this.gate)
            {
                known = this.settled ? null : this.candidate;
            }

            // A place one struct was found to hold its key at is taken once another reads its own key there.
            if (known is { } place && place.Owner != owner && DatKey.Row(reader, elements, owner + place.Offset, this.table) is { } agreed)
            {
                this.Settle(place.Offset);
                return agreed;
            }

            lock (this.gate)
            {
                if (this.settled || owner == IntPtr.Zero || Environment.TickCount64 < this.nextSearchAt)
                {
                    return null;
                }

                this.nextSearchAt = Environment.TickCount64 + SearchEveryMs;
            }

            if ((DatKeySearch.Site(at, place => DatKey.Row(reader, elements, owner + place, this.table) != null) ?? this.SiteFar(reader, elements, owner, at)) is not { } found)
            {
                return null;
            }

            lock (this.gate)
            {
                // One struct proves nothing about the others: a second must read its key there too.
                if (this.candidate is not { } before || before.Offset != found || before.Owner == owner)
                {
                    this.candidate = (found, owner);
                    return null;
                }
            }

            this.Settle(found);
            return DatKey.Row(reader, elements, owner + found, this.table);
        }

        /// <summary>The key anywhere in the struct (<see cref="DatKeySearch.SiteWide"/>), asked for no more often than <see cref="WideEveryMs"/>.</summary>
        private int? SiteFar(HostMemoryReader reader, UiElementReader elements, IntPtr owner, int near)
        {
            lock (this.gate)
            {
                if (Environment.TickCount64 < this.nextWideAt)
                {
                    return null;
                }

                this.nextWideAt = Environment.TickCount64 + WideEveryMs;
            }

            return DatKeySearch.SiteWide(near, place => DatKey.Row(reader, elements, owner + place, this.table)?.ToInt64() ?? 0);
        }

        private void Settle(int at)
        {
            lock (this.gate)
            {
                if (this.settled && at == this.offset)
                {
                    return;
                }

                (this.offset, this.settled, this.candidate) = (at, true, null);
            }

            if (at == this.measured)
            {
                CompatibilityReport.Current?.Found(this.part);
            }
            else
            {
                CompatibilityReport.Current?.Moved(this.part, this.measured, at);
            }
        }

        /// <summary>A new game process (the report reset) judges the site again from where it was measured.</summary>
        private void Fresh()
        {
            var now = CompatibilityReport.Current?.Generation ?? 0;
            lock (this.gate)
            {
                if (now != this.generation)
                {
                    (this.generation, this.offset, this.settled, this.candidate, this.nextWideAt) = (now, this.measured, false, null, 0);
                }
            }
        }
    }
}
