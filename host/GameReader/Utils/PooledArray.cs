namespace GameReader.Utils
{
    using System;
    using System.Buffers;

    /// <summary>
    ///     Elements read from the game into an array of the shared pool. A ref struct on purpose: it cannot be kept in a
    ///     field or captured, so nothing reads the array after <see cref="Dispose"/> hands it to the next reader.
    /// </summary>
    /// <typeparam name="T">the element read.</typeparam>
    public readonly ref struct PooledArray<T>
        where T : unmanaged
    {
        private readonly T[]? array;
        private readonly int count;
        private readonly bool pooled;

        internal PooledArray(T[] array, int count, bool pooled)
        {
            this.array = array;
            this.count = count;
            this.pooled = pooled;
        }

        /// <summary>Gets how many elements were read: 0 when the read failed.</summary>
        public int Length => this.count;

        /// <summary>Gets the elements read: copy what must outlive this.</summary>
        public ReadOnlySpan<T> Span => this.array == null ? default : this.array.AsSpan(0, this.count);

        /// <summary>Hands the array back: <see cref="Span"/> must not be read after.</summary>
        public void Dispose()
        {
            if (this.pooled && this.array != null)
            {
                ArrayPool<T>.Shared.Return(this.array);
            }
        }
    }
}
