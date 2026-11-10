using System;

/// <summary>
///     What the bundle index must show before any step trusts it: the container header LibBundle3 reads without a version
///     check, then files, bundles and one table every step needs. Pure.
/// </summary>
internal static class BundleIndexCheck
{
    /// <summary>The bytes of a bundle file's header that <see cref="HeaderProblem"/> reads.</summary>
    public const int HeaderBytes = 60;

    // A bundle file (the index is one): three sizes, then the head payload: the compressor, a 1, the two sizes again as
    // 64 bits, the block count, the block granularity, sixteen zero bytes and a size for each block.
    private const int SizesBytes = 12;
    private const int HeadFixedBytes = 48;
    private const int BlockGranularity = 262144;

    /// <summary>
    ///     Why the first bytes of a bundle file are not the container this reader's library knows (0.5.x, 2026-11-10: every
    ///     relation below holds on the game's index); null when they are. A new container would otherwise be decompressed as
    ///     the old one and read as garbage or fail deep inside the library.
    /// </summary>
    public static string? HeaderProblem(ReadOnlySpan<byte> head, long fileLength)
    {
        if (head.Length < HeaderBytes)
        {
            return $"the file is {fileLength} bytes, shorter than a bundle header";
        }

        uint uncompressed = BitConverter.ToUInt32(head), payload = BitConverter.ToUInt32(head[4..]), headPayload = BitConverter.ToUInt32(head[8..]);
        ulong uncompressed64 = BitConverter.ToUInt64(head[20..]), payload64 = BitConverter.ToUInt64(head[28..]);
        uint blocks = BitConverter.ToUInt32(head[36..]), granularity = BitConverter.ToUInt32(head[40..]);
        if (uncompressed != uncompressed64 || payload != payload64)
        {
            return "the header's sizes do not repeat as 64-bit sizes";
        }

        if (granularity != BlockGranularity || blocks != (uncompressed + (ulong)granularity - 1) / granularity)
        {
            return $"the header says {blocks} blocks of {granularity} bytes for {uncompressed} bytes";
        }

        if (headPayload != HeadFixedBytes + (sizeof(uint) * (ulong)blocks) || fileLength != SizesBytes + (long)headPayload + payload)
        {
            return $"the header's sizes ({headPayload} + {payload} bytes) do not add up to the file's {fileLength}";
        }

        return null;
    }

    /// <summary>The batch's exit code when the index could not be opened or does not look like the game's.</summary>
    public const int ExitCode = 2;

    /// <summary>The name the launcher lists in game-data-failed.txt for it.</summary>
    public const string Name = "bundle index";

    /// <summary>The cause, or null when the index looks right.</summary>
    public static string? Problem(int files, int bundles, bool statsTableFound, int unparsedPaths = 0)
    {
        if (files <= 0 || bundles <= 0)
        {
            return $"the index lists {files} files in {bundles} bundles";
        }

        if (unparsedPaths * 2 > files)
        {
            return $"{unparsedPaths} of {files} paths in the index did not parse";
        }

        return statsTableFound ? null : $"the index lists {files} files but not data/balance/stats.datc64 (nor data/stats.datc64)";
    }
}
