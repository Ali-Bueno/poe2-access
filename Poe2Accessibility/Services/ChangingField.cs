namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Finds a field with nothing static to check it by the way it was measured: dumps diffed while the value moved.
    ///     Of the shifts within reach, the field is the one that changes on one control as the player moves its value. The
    ///     expected shift is proved the first time it changes; until then, another shift that changed twice between valid
    ///     values while the expected one stayed put takes its place (the nearest, if several). Pure.
    /// </summary>
    public sealed class ChangingField
    {
        /// <summary>Changes another shift needs before it is taken for the field.</summary>
        public const int ChangesToMove = 2;

        private readonly Dictionary<int, int> changes = new();
        private IntPtr control;
        private Dictionary<int, long> last = new();

        /// <summary>The shift proved by a change; null until one is seen.</summary>
        public int? Proved { get; private set; }

        /// <summary>
        ///     One reading of <paramref name="values"/> (shift → value; null where unreadable) on <paramref name="owner"/>;
        ///     true when it settles the field (<see cref="Proved"/> set). Values that are not <paramref name="valid"/> never count.
        /// </summary>
        public bool Observe(IntPtr owner, int expected, IReadOnlyDictionary<int, long> values, Func<long, bool> valid)
        {
            if (this.Proved != null)
            {
                return false;
            }

            if (owner != this.control)
            {
                (this.control, this.last) = (owner, new Dictionary<int, long>(values));
                this.changes.Clear();
                return false;
            }

            var expectedChanged = false;
            foreach (var (shift, value) in values)
            {
                if (!this.last.TryGetValue(shift, out var before) || before == value)
                {
                    continue;
                }

                if (shift == expected)
                {
                    expectedChanged = true;
                }
                else if (valid(before) && valid(value))
                {
                    this.changes[shift] = this.changes.GetValueOrDefault(shift) + 1;
                }
            }

            this.last = new Dictionary<int, long>(values);
            if (expectedChanged && values.TryGetValue(expected, out var now) && valid(now))
            {
                this.Proved = expected;
                return true;
            }

            var moved = this.changes.Where(change => change.Value >= ChangesToMove).Select(change => change.Key)
                .OrderBy(shift => Math.Abs(shift - expected)).Cast<int?>().FirstOrDefault();
            if (moved != null)
            {
                this.Proved = moved;
                return true;
            }

            return false;
        }
    }
}
