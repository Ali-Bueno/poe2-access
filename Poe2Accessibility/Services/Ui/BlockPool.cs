namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     Byte blocks of one exact size for a walk that reads thousands of elements and is done with each before the next
    ///     (the screen watcher's): a block is handed out again only after its reader gave it back, and one never given back
    ///     is simply lost to the collector. Not thread-safe: one walker owns it.
    /// </summary>
    public sealed class BlockPool
    {
        private readonly Stack<byte[]> free = new();
        private readonly int maxKept;

        /// <param name="maxKept">How many returned blocks are kept for the next reads.</param>
        public BlockPool(int maxKept)
        {
            this.maxKept = maxKept;
        }

        /// <summary>Gets how many blocks wait to be handed out.</summary>
        public int Kept => this.free.Count;

        /// <summary>A block of exactly <paramref name="size"/> bytes (code reads its length as what was read); its bytes are an earlier read's.</summary>
        public byte[] Rent(int size)
        {
            while (this.free.Count > 0)
            {
                var block = this.free.Pop();
                if (block.Length == size)
                {
                    return block;
                }
            }

            return new byte[size]; // the size changed (a field moved): the blocks of the old size were dropped above
        }

        /// <summary>Gives a block back once nothing reads it any more; null is ignored.</summary>
        public void Return(byte[]? block)
        {
            if (block != null && this.free.Count < this.maxKept)
            {
                this.free.Push(block);
            }
        }
    }
}
