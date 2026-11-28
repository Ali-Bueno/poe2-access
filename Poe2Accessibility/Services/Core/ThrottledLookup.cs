namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>A lookup that costs a memory walk, repeated at most once per period; between two, the last answer stands. Pure.</summary>
    public sealed class ThrottledLookup<T>
    {
        private readonly int periodMs;
        private long lookedUpAt = long.MinValue / 2;
        private T value = default!;

        public ThrottledLookup(int periodMs) => this.periodMs = periodMs;

        /// <summary>The last answer, looked up again when the period has passed; <paramref name="state"/> keeps the lookup a static lambda.</summary>
        public T Get<TState>(TState state, Func<TState, T> lookup, long now)
        {
            if (now - this.lookedUpAt >= this.periodMs)
            {
                this.lookedUpAt = now;
                this.value = lookup(state);
            }

            return this.value;
        }
    }
}
