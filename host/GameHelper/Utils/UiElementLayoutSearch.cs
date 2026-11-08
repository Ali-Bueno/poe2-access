// <copyright file="UiElementLayoutSearch.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Objects.UiElement;

    /// <summary>
    ///     Where a UI element keeps its base fields (<see cref="UiElementBaseOffset"/>): self, children, parent, the position
    ///     group (relative position, modifier, scale), the flags group (flags, scale index) and the size; negative when not found.
    /// </summary>
    public readonly record struct UiElementFields(int Self, int Children, int Parent, int Position, int Flags, int Size)
    {
        /// <summary>The offsets the host was written for.</summary>
        public static UiElementFields Measured { get; } = new(
            OffsetOf(nameof(UiElementBaseOffset.Self)),
            OffsetOf(nameof(UiElementBaseOffset.ChildrensPtr)),
            OffsetOf(nameof(UiElementBaseOffset.ParentPtr)),
            OffsetOf(nameof(UiElementBaseOffset.RelativePosition)),
            OffsetOf(nameof(UiElementBaseOffset.Flags)),
            OffsetOf(nameof(UiElementBaseOffset.UnscaledSize)));

        private static int OffsetOf(string field) => Marshal.OffsetOf<UiElementBaseOffset>(field).ToInt32();
    }

    /// <summary>
    ///     Finds the UI element base fields on the UI root by what they hold: the root's own address, children that point
    ///     back at it, and its size, which is the UI's base resolution (2560 x 1600). The position and flags groups have no
    ///     such mark: each is taken at the shift of the parent or of the size that fits a check (the root's scale is 1 and
    ///     its scale index the root's; the root is visible and its descendants are not all alike).
    /// </summary>
    public sealed class UiElementLayoutSearch
    {
        /// <summary>Bytes read of each element: every base field measured so far ends before 0x290.</summary>
        public const int BlockBytes = 0x400;

        /// <summary>The root's scale index (GameHelper: root 3, child of root 3, first child of that 2).</summary>
        public const int RootScaleIndex = 3;

        private const long MinPointer = 0x10000;
        private const long MaxPointer = 0x7FFFFFFFFFFF;
        private const int Pointer = 8;
        private const int MaxChildren = 1024;
        private const int ChildrenChecked = 8;
        private const int NodesWalked = 300;

        private static readonly int Modifier = Offset(nameof(UiElementBaseOffset.PositionModifier)) - Offset(nameof(UiElementBaseOffset.RelativePosition));
        private static readonly int Scale = Offset(nameof(UiElementBaseOffset.LocalScaleMultiplier)) - Offset(nameof(UiElementBaseOffset.RelativePosition));
        private static readonly int ScaleIndex = Offset(nameof(UiElementBaseOffset.ScaleIndex)) - Offset(nameof(UiElementBaseOffset.Flags));

        private readonly IAreaMemory memory;
        private readonly Dictionary<long, byte[]> blocks = new();

        /// <summary>Initializes a new instance of the <see cref="UiElementLayoutSearch"/> class.</summary>
        public UiElementLayoutSearch(IAreaMemory memory)
        {
            this.memory = memory;
        }

        /// <summary>The fields on the UI root at <paramref name="root"/>, nearest <paramref name="near"/>.</summary>
        public UiElementFields Find(long root, UiElementFields near)
        {
            var block = this.Block(root);
            if (block.Length == 0)
            {
                return new UiElementFields(-1, -1, -1, -1, -1, -1);
            }

            var self = Nearest(near.Self, Pointer, at => Long(block, at) == root);
            var parent = -1;
            var children = self < 0 ? -1 : Nearest(near.Children, Pointer, at => (parent = Nearest(near.Parent, Pointer, p => p != at && this.ChildrenPointBack(block, root, at, self, p))) >= 0);
            var size = Nearest(near.Size, sizeof(float), at => at + (2 * sizeof(float)) <= block.Length &&
                BitConverter.ToSingle(block, at) == (float)UiElementBaseFuncs.BaseResolution.X && BitConverter.ToSingle(block, at + sizeof(float)) == (float)UiElementBaseFuncs.BaseResolution.Y);
            var shifts = new[] { 0, parent < 0 ? 0 : parent - near.Parent, size < 0 ? 0 : size - near.Size }.Distinct().ToList();
            var position = shifts.Select(shift => near.Position + shift).FirstOrDefault(at => IsRootPosition(block, at), -1);
            var flags = children < 0 ? -1 : shifts.Select(shift => near.Flags + shift).FirstOrDefault(at => this.IsVisibleFlag(block, root, children, self, at), -1);
            return new UiElementFields(self, children, parent, position, flags, size);
        }

        private static int Offset(string field) => Marshal.OffsetOf<UiElementBaseOffset>(field).ToInt32();

        private static int Nearest(int start, int step, Func<int, bool> fits)
        {
            for (var distance = 0; distance <= BlockBytes; distance += step)
            {
                if (start + distance < BlockBytes && fits(start + distance))
                {
                    return start + distance;
                }

                if (distance > 0 && start - distance >= 0 && fits(start - distance))
                {
                    return start - distance;
                }
            }

            return -1;
        }

        private static bool IsPointer(long value) => value > MinPointer && value < MaxPointer;

        private static long Long(byte[] bytes, int at) => at >= 0 && at + Pointer <= bytes.Length ? BitConverter.ToInt64(bytes, at) : 0;

        /// <summary>The root stands at the origin with no modifier and a scale of 1.</summary>
        private static bool IsRootPosition(byte[] root, int at) =>
            at >= 0 && at + Scale + sizeof(float) <= root.Length && BitConverter.ToSingle(root, at + Scale) == 1f &&
            Long(root, at) == 0 && Long(root, at + Modifier) == 0;

        private byte[] Block(long address)
        {
            if (!IsPointer(address))
            {
                return Array.Empty<byte>();
            }

            if (!this.blocks.TryGetValue(address, out var block))
            {
                block = this.memory.Read(address, BlockBytes);
                this.blocks[address] = block;
            }

            return block;
        }

        private long[] Children(byte[] element, int at)
        {
            var (first, last) = (Long(element, at), Long(element, at + Pointer));
            var bytes = last - first;
            if (!IsPointer(first) || bytes <= 0 || bytes % Pointer != 0 || bytes / Pointer > MaxChildren || Long(element, at + (2 * Pointer)) < last)
            {
                return Array.Empty<long>();
            }

            var pointers = this.memory.Read(first, (int)bytes);
            return pointers.Length < bytes ? Array.Empty<long>() : Enumerable.Range(0, (int)(bytes / Pointer)).Select(index => Long(pointers, index * Pointer)).ToArray();
        }

        /// <summary>The vector at <paramref name="at"/> holds elements (each its own self) whose parent field points at the root.</summary>
        private bool ChildrenPointBack(byte[] root, long address, int at, int self, int parent)
        {
            var children = this.Children(root, at);
            return children.Length > 0 && children.Take(ChildrenChecked).All(child =>
                this.Block(child) is { Length: > 0 } body && Long(body, self) == child && Long(body, parent) == address);
        }

        /// <summary>The root carries the visible bit and its scale index; of the elements under it some do and some do not.</summary>
        private bool IsVisibleFlag(byte[] root, long address, int children, int self, int at)
        {
            if (at < 0 || at + ScaleIndex >= root.Length || !UiElementBaseFuncs.IsVisibleChecker(BitConverter.ToUInt32(root, at)) || root[at + ScaleIndex] != RootScaleIndex)
            {
                return false;
            }

            var (shown, hidden) = (false, false);
            var queue = new Queue<long>(this.Children(root, children));
            for (var walked = 0; walked < NodesWalked && queue.Count > 0 && !(shown && hidden); walked++)
            {
                var node = queue.Dequeue();
                var body = this.Block(node);
                if (body.Length == 0 || Long(body, self) != node)
                {
                    continue;
                }

                var visible = UiElementBaseFuncs.IsVisibleChecker(BitConverter.ToUInt32(body, at));
                (shown, hidden) = (shown || visible, hidden || !visible);
                foreach (var child in this.Children(body, children))
                {
                    queue.Enqueue(child);
                }
            }

            return shown && hidden;
        }
    }
}
