// <copyright file="PeImage.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils.CodeReader
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>One section of a PE file: where it is loaded (RVA) and where its bytes are in the file.</summary>
    /// <param name="Name">The section's name.</param>
    /// <param name="Start">Its RVA.</param>
    /// <param name="VirtualSize">Its size once loaded.</param>
    /// <param name="RawStart">Where its bytes start in the file.</param>
    /// <param name="RawSize">Its bytes in the file.</param>
    public readonly record struct PeSection(string Name, long Start, long VirtualSize, long RawStart, long RawSize)
    {
        /// <summary>Gets the bytes both loaded and in the file.</summary>
        public long Size => Math.Min(this.VirtualSize, this.RawSize);

        /// <summary>Gets the RVA past its loaded bytes.</summary>
        public long End => this.Start + this.VirtualSize;

        /// <summary>Gets whether the RVA lies in it.</summary>
        public bool Holds(long rva) => rva >= this.Start && rva < this.End;
    }

    /// <summary>A PE file read as data, addressed by RVA as the loaded image is; nothing is run or mapped. Reads outside the file are empty.</summary>
    public sealed class PeImage
    {
        public const string CodeName = ".text";
        public const string ConstantsName = ".rdata";
        public const string DataName = ".data";

        private const int HeaderPointer = 0x3C;
        private const ushort Pe32Plus = 0x20B;
        private const int SectionCountAt = 6;
        private const int OptionalSizeAt = 20;
        private const int OptionalAt = 24;
        private const int ImageBaseAt = 24;
        private const int DirectoriesAt = 112;
        private const int DirectorySize = 8;
        private const int ExceptionDirectory = 3;
        private const int SectionSize = 40;
        private const int SectionNameSize = 8;
        private const int SectionVirtualSizeAt = 8;
        private const int SectionStartAt = 12;
        private const int SectionRawSizeAt = 16;
        private const int SectionRawAt = 20;

        private readonly byte[] file;

        private PeImage(byte[] file, IReadOnlyList<PeSection> sections, long imageBase, PeSection exceptions)
        {
            this.file = file;
            this.Sections = sections;
            this.ImageBase = imageBase;
            this.Exceptions = exceptions;
            this.Code = this.Section(CodeName);
            this.Constants = this.Section(ConstantsName);
            this.Data = this.Section(DataName);
        }

        /// <summary>Gets the address the image prefers, which absolute pointers in its data are relative to.</summary>
        public long ImageBase { get; }

        /// <summary>Gets every section.</summary>
        public IReadOnlyList<PeSection> Sections { get; }

        /// <summary>Gets the code.</summary>
        public PeSection Code { get; }

        /// <summary>Gets the read-only data: strings, vtables, unwind data.</summary>
        public PeSection Constants { get; }

        /// <summary>Gets the writable data.</summary>
        public PeSection Data { get; }

        /// <summary>Gets the function table (<c>.pdata</c>'s rows), as an RVA and size.</summary>
        public PeSection Exceptions { get; }

        /// <summary>The image, or null when the file is not a PE32+ with a code section.</summary>
        public static PeImage? Read(byte[] file)
        {
            var sections = SectionsOf(file);
            if (sections.Count == 0)
            {
                return null;
            }

            var optional = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(HeaderPointer)) + OptionalAt;
            var directory = optional + DirectoriesAt + (ExceptionDirectory * DirectorySize);
            if (directory + DirectorySize > file.Length || BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(optional)) != Pe32Plus)
            {
                return null;
            }

            var imageBase = BinaryPrimitives.ReadInt64LittleEndian(file.AsSpan(optional + ImageBaseAt));
            var exceptions = new PeSection(
                ".pdata", BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(directory)), BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(directory + sizeof(int))), 0, 0);
            var image = new PeImage(file, sections, imageBase, exceptions);
            return image.Code.Size > 0 ? image : null;
        }

        /// <summary>The sections of a PE file; empty when it is not one.</summary>
        public static IReadOnlyList<PeSection> SectionsOf(ReadOnlySpan<byte> file)
        {
            var sections = new List<PeSection>();
            if (file.Length < HeaderPointer + sizeof(int))
            {
                return sections;
            }

            var header = BinaryPrimitives.ReadInt32LittleEndian(file[HeaderPointer..]);
            if (header < 0 || header + OptionalAt > file.Length)
            {
                return sections;
            }

            var table = header + OptionalAt + BinaryPrimitives.ReadUInt16LittleEndian(file[(header + OptionalSizeAt)..]);
            int count = BinaryPrimitives.ReadUInt16LittleEndian(file[(header + SectionCountAt)..]);
            for (var index = 0; index < count && table + ((index + 1) * SectionSize) <= file.Length; index++)
            {
                var row = file.Slice(table + (index * SectionSize), SectionSize);
                var name = Encoding.ASCII.GetString(row[..SectionNameSize]).TrimEnd('\0');
                sections.Add(new PeSection(
                    name,
                    BinaryPrimitives.ReadUInt32LittleEndian(row[SectionStartAt..]),
                    BinaryPrimitives.ReadUInt32LittleEndian(row[SectionVirtualSizeAt..]),
                    BinaryPrimitives.ReadUInt32LittleEndian(row[SectionRawAt..]),
                    BinaryPrimitives.ReadUInt32LittleEndian(row[SectionRawSizeAt..])));
            }

            return sections;
        }

        /// <summary>The named section, or an empty one.</summary>
        public PeSection Section(string name)
        {
            foreach (var section in this.Sections)
            {
                if (section.Name == name)
                {
                    return section;
                }
            }

            return default;
        }

        /// <summary>Up to <paramref name="length"/> bytes at an RVA, fewer where the section's file bytes end.</summary>
        public ReadOnlySpan<byte> Bytes(long rva, long length)
        {
            foreach (var section in this.Sections)
            {
                if (section.Holds(rva))
                {
                    var into = rva - section.Start;
                    var available = Math.Min(section.Size, this.file.Length - section.RawStart) - into;
                    return available <= 0 ? ReadOnlySpan<byte>.Empty
                        : this.file.AsSpan((int)(section.RawStart + into), (int)Math.Min(length, available));
                }
            }

            return ReadOnlySpan<byte>.Empty;
        }

        /// <summary>The byte at an RVA, or 0.</summary>
        public byte Byte(long rva) => this.Bytes(rva, sizeof(byte)) is { Length: sizeof(byte) } bytes ? bytes[0] : (byte)0;

        /// <summary>The dword at an RVA, or 0.</summary>
        public uint UInt32(long rva) => this.Bytes(rva, sizeof(uint)) is { Length: sizeof(uint) } bytes ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : 0;

        /// <summary>The RVA an absolute pointer at <paramref name="rva"/> points at, or -1.</summary>
        public long Pointer(long rva) =>
            this.Bytes(rva, sizeof(long)) is { Length: sizeof(long) } bytes ? BinaryPrimitives.ReadInt64LittleEndian(bytes) - this.ImageBase : -1;

        /// <summary>Gets whether the RVA is code.</summary>
        public bool IsCode(long rva) => rva >= this.Code.Start && rva < this.Code.Start + this.Code.Size;
    }
}
