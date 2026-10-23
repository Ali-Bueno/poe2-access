"""Capture the foreground game's visible client area as a standard RGB PNG."""
import ctypes as C
from pathlib import Path
import struct
import zlib
import win32 as w

MAX_CAPTURE_DIMENSION = 8192  # allocation limit; permits up to an 8K game client


def png_chunk(kind, payload):
    return struct.pack('!I', len(payload)) + kind + payload + struct.pack('!I', zlib.crc32(kind + payload))


def encode_png(width, height, pixels):
    rgb = bytearray(width * height * 3)
    rgb[0::3], rgb[1::3], rgb[2::3] = pixels[2::4], pixels[1::4], pixels[0::4]
    stride = width * 3
    rows = b''.join(b'\0' + rgb[start:start + stride] for start in range(0, len(rgb), stride))
    header = struct.pack('!IIBBBBB', width, height, 8, 2, 0, 0, 0)
    return b'\x89PNG\r\n\x1a\n' + png_chunk(b'IHDR', header) + png_chunk(b'IDAT', zlib.compress(rows, 3)) + png_chunk(b'IEND', b'')


def downscale(width, height, pixels, factor):
    """Keep every factor-th pixel of every factor-th row (BGRA), so a review image costs less to inspect."""
    if factor == 1:
        return width, height, pixels
    source = memoryview(pixels).cast('I')
    rows = [source[row * width:(row + 1) * width:factor].tobytes() for row in range(0, height, factor)]
    return len(range(0, width, factor)), len(rows), b''.join(rows)


def screenshot(info, destination, factor=1):
    info = w.require_foreground(info)
    width, height = info['width'], info['height']
    if not (0 < width <= MAX_CAPTURE_DIMENSION and 0 < height <= MAX_CAPTURE_DIMENSION):
        raise RuntimeError('Unexpected client dimensions')
    source, memory, bitmap, previous = None, None, None, None
    try:
        source = w.get_dc(None)
        memory = w.compatible_dc(source)
        bitmap = w.compatible_bitmap(source, width, height)
        if not source or not memory or not bitmap:
            raise C.WinError(C.get_last_error())
        previous = w.select_object(memory, bitmap)
        if not previous:
            raise C.WinError(C.get_last_error())
        # SRCCOPY | CAPTUREBLT, from wingdi.h: copy visible pixels including overlays.
        if not w.bit_blt(memory, 0, 0, width, height, source, info['x'], info['y'], 0x00CC0020 | 0x40000000):
            raise C.WinError(C.get_last_error())
        w.select_object(memory, previous)
        previous = None
        header = w.BitmapInfo(size=C.sizeof(w.BitmapInfo), width=width, height=-height, planes=1, bits=32)
        pixels = C.create_string_buffer(width * height * 4)
        if w.get_bits(memory, bitmap, 0, height, pixels, C.byref(header), 0) != height:
            raise RuntimeError('Incomplete screen capture')
        w.require_foreground(info)
        path = Path(destination).resolve()
        path.parent.mkdir(parents=True, exist_ok=True)
        small_width, small_height, small = downscale(width, height, pixels.raw, factor)
        path.write_bytes(encode_png(small_width, small_height, small))
        return dict(path=str(path), width=small_width, height=small_height, scale=factor)
    finally:
        if previous and memory:
            w.select_object(memory, previous)
        if bitmap:
            w.delete_object(bitmap)
        if memory:
            w.delete_dc(memory)
        if source:
            w.release_dc(None, source)
