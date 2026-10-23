// Thin export-name shim: LibBundle3's P/Invoke targets "oo2core.dll" and calls
// OodleLZ_Decompress with the standard 14-parameter Oodle signature. ooz's kraken.cpp
// already implements the equivalent Ooz_Decompress (same semantics: the compressed
// stream's own header byte selects Kraken/Mermaid/Selkie/Leviathan/LZNA/Bitknit, so it
// works for any Oodle compressor, not just Kraken). This file just re-exports it under
// the name and exact parameter types LibBundle3 expects.
#include <cstdint>
#include <cstddef>

extern "C" int Ooz_Decompress(uint8_t const* src_buf, int src_len, uint8_t* dst, size_t dst_size,
    int fuzzSafe, int checkCRC, int verbosity, uint8_t* decBufBase, size_t decBufSize,
    void* fpCallback, void* cbUserData, void* decoderMem, size_t decoderMemSize, int threadPhase);

extern "C" __declspec(dllexport) int64_t OodleLZ_Decompress(
    const uint8_t* src, size_t srcLen, uint8_t* dst, size_t dstLen,
    int fuzzSafe, int checkCRC, int verbosity,
    uint8_t* decBufBase, size_t decBufSize,
    void* fpCallback, void* cbUserData,
    void* decoderMem, size_t decoderMemSize, int threadPhase)
{
    int result = Ooz_Decompress(src, (int)srcLen, dst, dstLen,
        fuzzSafe, checkCRC, verbosity, decBufBase, decBufSize,
        fpCallback, cbUserData, decoderMem, decoderMemSize, threadPhase);
    return (int64_t)result;
}

// LibBundle3.Oodle.Initialize() unconditionally asks how much scratch memory a decoder
// instance needs for a chunk of `outputSize` bytes, then allocates that much and passes
// it back in as decoderMem/decoderMemSize on every OodleLZ_Decompress call. Our
// Ooz_Decompress (kraken.cpp) manages its own scratch internally via Kraken_Create and
// ignores the caller-supplied decoder memory entirely, so the exact bound doesn't matter
// as long as it is a sane positive size — we derive it from the real requested chunk
// size (outputSize) instead of inventing a constant.
extern "C" __declspec(dllexport) int32_t OodleLZDecoder_MemorySizeNeeded(int compressor, int64_t outputSize)
{
    (void)compressor;
    return outputSize > 0 ? (int32_t)outputSize : 0;
}

// Compression-path entry points: Poe2UiExtract only reads bundles, and this shim only
// implements decompression, so these are unreachable in that flow (LibBundle3.Oodle
// only calls them when Settings.EnableCompressing is true). Exported anyway so the
// import resolves if ever touched, returning failure rather than crashing.
extern "C" __declspec(dllexport) int64_t OodleLZ_GetCompressScratchMemBound(int compressor, int level, int64_t bufferSize, void* options)
{
    (void)compressor; (void)level; (void)options;
    return bufferSize;
}

extern "C" __declspec(dllexport) int64_t OodleLZ_GetCompressedBufferSizeNeeded(int compressor, int64_t bufferSize)
{
    (void)compressor;
    return bufferSize;
}

extern "C" __declspec(dllexport) int64_t OodleLZ_Compress(int compressor, const uint8_t* buffer, int64_t bufferSize,
    uint8_t* output, int level, void* options, void* dictionaryBase, void* lrm, void* scratch, int64_t scratchSize)
{
    (void)compressor; (void)buffer; (void)bufferSize; (void)output; (void)level;
    (void)options; (void)dictionaryBase; (void)lrm; (void)scratch; (void)scratchSize;
    return 0; // compression not implemented by this shim
}
