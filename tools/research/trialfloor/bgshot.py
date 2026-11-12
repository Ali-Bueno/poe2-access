"""Capture a window's client area without bringing it to the front (PrintWindow, full content). Read-only."""
import ctypes as C
from ctypes import wintypes as W
import struct
import sys
import zlib

C.WinDLL('shcore').SetProcessDpiAwareness(2)  # real pixels: a scaled desktop reported two thirds of the window
user32, gdi32 = C.WinDLL('user32', use_last_error=True), C.WinDLL('gdi32', use_last_error=True)
PW_CLIENTONLY, PW_RENDERFULLCONTENT = 1, 2
user32.GetDC.restype = W.HDC
user32.GetDC.argtypes = [W.HWND]
gdi32.CreateCompatibleDC.restype = W.HDC
gdi32.CreateCompatibleDC.argtypes = [W.HDC]
gdi32.CreateCompatibleBitmap.restype = W.HBITMAP
gdi32.CreateCompatibleBitmap.argtypes = [W.HDC, C.c_int, C.c_int]
gdi32.SelectObject.restype = W.HGDIOBJ
gdi32.SelectObject.argtypes = [W.HDC, W.HGDIOBJ]
user32.PrintWindow.argtypes = [W.HWND, W.HDC, W.UINT]
gdi32.GetDIBits.argtypes = [W.HDC, W.HBITMAP, W.UINT, W.UINT, C.c_void_p, C.c_void_p, W.UINT]
gdi32.DeleteObject.argtypes = [W.HGDIOBJ]
gdi32.DeleteDC.argtypes = [W.HDC]
user32.ReleaseDC.argtypes = [W.HWND, W.HDC]


def window_of(pid):
    found = []

    @C.WINFUNCTYPE(W.BOOL, W.HWND, W.LPARAM)
    def each(hwnd, _):
        owner = W.DWORD()
        user32.GetWindowThreadProcessId(hwnd, C.byref(owner))
        if owner.value == pid and user32.IsWindowVisible(hwnd):
            rect = W.RECT()
            user32.GetClientRect(hwnd, C.byref(rect))
            found.append((rect.right * rect.bottom, hwnd, rect.right, rect.bottom))
        return True

    user32.EnumWindows(each, 0)
    return max(found)[1:] if found else None


def chunk(kind, payload):
    return struct.pack('!I', len(payload)) + kind + payload + struct.pack('!I', zlib.crc32(kind + payload))


def main():
    pid, target = int(sys.argv[1]), sys.argv[2]
    crop = [int(part) for part in sys.argv[3].split(',')] if len(sys.argv) > 3 else None
    hwnd, width, height = window_of(pid)
    screen = user32.GetDC(None)
    memory = gdi32.CreateCompatibleDC(screen)
    bitmap = gdi32.CreateCompatibleBitmap(screen, width, height)
    gdi32.SelectObject(memory, bitmap)
    done = user32.PrintWindow(hwnd, memory, PW_CLIENTONLY | PW_RENDERFULLCONTENT)
    header = struct.pack('<IiiHHIIiiII', 40, width, -height, 1, 32, 0, 0, 0, 0, 0, 0)
    pixels = C.create_string_buffer(width * height * 4)
    gdi32.GetDIBits(memory, bitmap, 0, height, pixels, header, 0)
    gdi32.DeleteObject(bitmap)
    gdi32.DeleteDC(memory)
    user32.ReleaseDC(None, screen)
    left, top, right, bottom = crop or (0, 0, width, height)
    raw = pixels.raw
    rows = bytearray()
    for y in range(top, bottom):
        line = raw[(y * width + left) * 4:(y * width + right) * 4]
        rgb = bytearray((right - left) * 3)
        rgb[0::3], rgb[1::3], rgb[2::3] = line[2::4], line[1::4], line[0::4]
        rows += b'\0' + rgb
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('!IIBBBBB', right - left, bottom - top, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(bytes(rows), 6)) + chunk(b'IEND', b'')
    with open(target, 'wb') as out:
        out.write(png)
    nonblack = sum(1 for value in raw[::997] if value > 16)
    print(f'printed={done} window={width}x{height} saved={right - left}x{bottom - top} nonblack_samples={nonblack}')


main()
