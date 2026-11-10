namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     Where a container keeps the pointer to one of its layers, when it is not near where it was measured: the layer is
    ///     first found in the element tree (a child that carries the field's name or shape), then the slot that holds its
    ///     address is looked for through the whole container. Two facts that do not depend on each other: what the tree
    ///     says the element is, and that the container points at it. Pure.
    /// </summary>
    public static class LayerSlots
    {
        /// <summary>
        ///     The offset of the slot (8 bytes each, from the container's start) that holds one of <paramref name="marked"/>, the
        ///     elements of the tree that are what the field must hold; the nearest <paramref name="expected"/> when several hold
        ///     the same one. Null when no slot does, or when the slots hold two different ones (not chosen between).
        /// </summary>
        public static int? Find(IReadOnlyList<long> slots, IReadOnlySet<long> marked, int expected)
        {
            int? found = null;
            long held = 0;
            for (var index = 0; index < slots.Count; index++)
            {
                var offset = index * sizeof(long);
                if (slots[index] == 0 || !marked.Contains(slots[index]))
                {
                    continue;
                }

                if (found is { } known && slots[index] != held)
                {
                    return null;
                }

                if (found is not { } nearest || Math.Abs(offset - expected) < Math.Abs(nearest - expected))
                {
                    (found, held) = (offset, slots[index]);
                }
            }

            return found;
        }
    }
}
