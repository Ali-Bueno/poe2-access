namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using GameOffsets.Objects.UiElement;

    /// <summary>
    ///     Cheap, validated reads of single UI elements for live use (focus tracking), as opposed to the
    ///     research-oriented UiTreeReader that probes whole trees. Offsets come from GameLayout.
    /// </summary>
    public sealed partial class UiElementReader
    {
        private const int MaxChildrenPerNode = 4096;
        private const int MaxTextLength = 1024;

        /// <summary>The longest text read whole on request (the terms of use hold about 60 000 characters).</summary>
        private const int MaxLongTextLength = 1 << 18;
        private const int MaxParentDepth = 64;

        /// <summary>
        ///     Option sliders run 0..1000 at most (2026-10). A slider resting at its minimum proves nothing about where its
        ///     fields are: next to it, zeros followed by a positive int read as a slider at its minimum too.
        /// </summary>
        private const int MaxSliderMagnitude = 1000000;

        /// <summary>Warning red is a strong red channel with little green and blue (the price red is E4,00,00).</summary>
        private const uint MinWarningRed = 0xC0;
        private const uint MaxWarningGreenBlue = 0x40;

        /// <summary>
        ///     Grey text: a heard dialogue topic is FF606060, FF909090 while focused; new topics are beige FFE3D7A6 and
        ///     white FFFFFFF9 focused (Una, 2026-10-24).
        /// </summary>
        private const uint MaxGrey = 0xA0;

        private const uint MinGrey = 0x20;

        private const int GreyTolerance = 4;

        /// <summary>Host reads are all-or-nothing; objects are read in page-sized chunks so a short tail is tolerated.</summary>
        private const int ChunkBytes = 0x100;

        /// <summary>How far a combo subclass's fields are looked for from the group's (the chat's channel list: 0xC8).</summary>
        private const int SubclassFieldRadius = 0x100;

        /// <summary>More rows than an options section holds (Gráficos' sections have 12 at most, 2026-10-25).</summary>
        private const int MaxSectionRows = 256;

        /// <summary>A combo box holds its parts and a label or two (four children seen).</summary>
        private const int MaxComboBoxChildren = 8;

        /// <summary>Control names of a combo box's parts (gateway and language lists, the chat's channel list; 0.5.5e).</summary>
        private static readonly string[] ComboBoxParts = { "button", "scrollbar", "textField" };

        private static readonly int BaseSize = Unsafe.SizeOf<UiElementBaseOffset>();

        /// <summary>Enough bytes to cover the text fields of the label subclass (0x538 + 32 as measured; they can move).</summary>
        private static int TextBlockSize => GameLayout.Element.TextCopyOffset + WStringDecoder.Size;

        /// <summary>The base struct, and the control name wherever it lies now.</summary>
        private static int ElementBlockSize => Math.Max(Math.Max(GameReader.Utils.HostStructs.Size<UiElementBaseOffset>(), GameLayout.Element.NameOffset + WStringDecoder.Size),
            Math.Max(GameLayout.Image.TextureOffset, GameLayout.Image.AltTextureOffset) + IntPtr.Size);

        private readonly HostMemoryReader reader;

        /// <summary>The 32 bytes of a std::wstring read on their own (entity names, per listing), one array per thread.</summary>
        [ThreadStatic]
        private static byte[]? wideStringScratch;

        /// <summary>Combo field shift per element class (vtable), once proven on one of its controls.</summary>
        private readonly Dictionary<IntPtr, int> comboBoxShifts = new();

        public UiElementReader(HostMemoryReader reader)
        {
            this.reader = reader;
        }

        /// <summary>Where slider and combo box fields are after a game update; as measured when null.</summary>
        public ControlFields? Controls { get; set; }

        /// <param name="Texture">Texture resource of image elements (GameLayout.Image), read with the base block.</param>
        /// <param name="Block">The bytes read from the element, for the text fields when they fit (<see cref="TryReadText(in ElementInfo, out string, out string)"/>).</param>
        public readonly record struct ElementInfo(IntPtr Address, UiElementBaseOffset Raw, string Name, IntPtr Texture = default, IntPtr AltTexture = default, byte[]? Block = null)
        {
            public bool IsVisibleFlag => UiElementBaseFuncs.IsVisibleChecker(this.Raw.Flags);

            public bool HasFlagBit(int bit) => (this.Raw.Flags & (1u << bit)) != 0;
        }

        /// <summary>Reads the base struct plus the control name; false unless the self pointer confirms an element.</summary>
        public bool TryReadElement(IntPtr address, out ElementInfo info)
        {
            info = default;
            // One read covers the text fields too (the screen walk asks for them on every node); an element too small
            // for that, at the end of its heap region, is read as before.
            var block = this.reader.ReadBytes(address, Math.Max(ElementBlockSize, TextBlockSize));
            if (block.Length == 0)
            {
                block = this.reader.ReadBytes(address, ElementBlockSize);
            }

            return block.Length > 0 && this.Describe(address, block, out info);
        }

        /// <summary>
        ///     The same read into a block of <paramref name="blocks"/>, for a walk that is done with each element before it
        ///     reads many more: the caller gives <see cref="ElementInfo.Block"/> back to the pool once nothing reads the
        ///     element's bytes any more (its text), and must not keep the info past that. False leaves nothing to give back.
        /// </summary>
        public bool TryReadElement(IntPtr address, BlockPool blocks, out ElementInfo info)
        {
            info = default;
            var block = blocks.Rent(Math.Max(ElementBlockSize, TextBlockSize));
            if (!this.reader.TryReadBytes(address, block))
            {
                // Too small for the text fields: the shorter read, whose length says so, in an array of its own.
                blocks.Return(block);
                block = this.reader.ReadBytes(address, ElementBlockSize);
                return block.Length > 0 && this.Describe(address, block, out info);
            }

            if (this.Describe(address, block, out info))
            {
                return true;
            }

            blocks.Return(block);
            return false;
        }

        private bool Describe(IntPtr address, byte[] block, out ElementInfo info)
        {
            info = default;
            if (!GameReader.Utils.HostStructs.TryRead<UiElementBaseOffset>(block, out var raw) || (raw.Self != address && raw.Self != IntPtr.Zero))
            {
                return false;
            }

            var name = WStringDecoder.TryDecode(this.reader, block, GameLayout.Element.NameOffset, MaxTextLength, allowEmpty: true) ?? string.Empty;
            var texture = block.Length >= GameLayout.Image.TextureOffset + IntPtr.Size
                ? MemoryMarshal.Read<IntPtr>(block.AsSpan(GameLayout.Image.TextureOffset))
                : IntPtr.Zero;
            var altTexture = block.Length >= GameLayout.Image.AltTextureOffset + IntPtr.Size
                ? MemoryMarshal.Read<IntPtr>(block.AsSpan(GameLayout.Image.AltTextureOffset))
                : IntPtr.Zero;
            info = new ElementInfo(address, raw, name, texture, altTexture, block);
            return true;
        }

        /// <summary>Strict test used on untrusted pointers: real elements always store their own address in Self.</summary>
        public bool IsElement(IntPtr address) =>
            this.reader.IsValidAddress(address) &&
            this.reader.TryRead<UiElementBaseOffset>(address, out var raw) &&
            raw.Self == address;

        public IntPtr[] ReadChildren(in ElementInfo info) =>
            this.reader.ReadStdVector<IntPtr>(info.Raw.ChildrensPtr, MaxChildrenPerNode);

        /// <summary>The same children in the caller's <paramref name="buffer"/> (see <see cref="HostMemoryReader.ReadStdVector{T}(GameOffsets.Natives.StdVector, int, ref T[])"/>): how many.</summary>
        public int ReadChildren(in ElementInfo info, ref IntPtr[] buffer) =>
            this.reader.ReadStdVector(info.Raw.ChildrensPtr, MaxChildrenPerNode, ref buffer);

        /// <summary>The first element with this control name at or under <paramref name="root"/>, breadth first, <paramref name="maxDepth"/> levels down.</summary>
        public ElementInfo? FindNamed(IntPtr root, string name, int maxDepth)
        {
            var queue = new Queue<(IntPtr Address, int Depth)>();
            queue.Enqueue((root, 0));
            while (queue.Count > 0)
            {
                var (address, depth) = queue.Dequeue();
                if (!this.TryReadElement(address, out var info))
                {
                    continue;
                }

                if (info.Name == name)
                {
                    return info;
                }

                if (depth < maxDepth)
                {
                    foreach (var child in this.ReadChildren(info))
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            return null;
        }
    }
}
