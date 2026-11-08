namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     Of several lists shown without a focus, the one whose highlighted index moves between valid rows: the list the
    ///     player is navigating. A list whose index never moved is never read. Pure.
    /// </summary>
    public sealed class MovingIndexWatch
    {
        private readonly Dictionary<(IntPtr Owner, IntPtr Rows), long> last = new();
        private (IntPtr Owner, IntPtr Rows)? moving;

        /// <param name="Owner">The element holding the index field.</param>
        /// <param name="Rows">The element whose children are the rows.</param>
        /// <param name="Index">The index field as read; anything outside 0..<paramref name="Count"/>-1 is no row.</param>
        public readonly record struct Reading(IntPtr Owner, IntPtr Rows, long Index, int Count)
        {
            public bool IsRow => this.Index >= 0 && this.Index < this.Count;
        }

        public void Reset()
        {
            this.last.Clear();
            this.moving = null;
        }

        /// <summary>One reading of every list; the moving list's highlighted row, or null while none has moved.</summary>
        public (IntPtr Owner, IntPtr Rows, int Row)? Observe(IReadOnlyList<Reading> readings)
        {
            foreach (var reading in readings)
            {
                var key = (reading.Owner, reading.Rows);
                if (reading.IsRow && this.last.TryGetValue(key, out var before) && before >= 0 && before != reading.Index)
                {
                    this.moving = key;
                }

                this.last[key] = reading.IsRow ? reading.Index : -1;
            }

            foreach (var reading in readings)
            {
                if (this.moving == (reading.Owner, reading.Rows) && reading.IsRow)
                {
                    return (reading.Owner, reading.Rows, (int)reading.Index);
                }
            }

            return null;
        }
    }
}
