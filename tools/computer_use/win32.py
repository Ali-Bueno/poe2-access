"""Small typed bindings to Windows' existing desktop APIs; no packages or driver."""
import ctypes as C
from ctypes import wintypes as W
from pathlib import Path

user32 = C.WinDLL('user32', use_last_error=True)
gdi32 = C.WinDLL('gdi32', use_last_error=True)
kernel32 = C.WinDLL('kernel32', use_last_error=True)


def bind(dll, name, result, *args):
    function = getattr(dll, name)
    function.restype, function.argtypes = result, args
    return function


class MouseInput(C.Structure):
    _fields_ = [('dx', W.LONG), ('dy', W.LONG), ('data', W.DWORD),
                ('flags', W.DWORD), ('time', W.DWORD), ('extra', C.c_size_t)]


class KeyInput(C.Structure):
    _fields_ = [('vk', W.WORD), ('scan', W.WORD), ('flags', W.DWORD),
                ('time', W.DWORD), ('extra', C.c_size_t)]


class HardwareInput(C.Structure):
    _fields_ = [('message', W.DWORD), ('low', W.WORD), ('high', W.WORD)]


class InputData(C.Union):
    _fields_ = [('mouse', MouseInput), ('key', KeyInput), ('hardware', HardwareInput)]


class Input(C.Structure):
    _fields_ = [('type', W.DWORD), ('data', InputData)]


class BitmapInfo(C.Structure):
    # BITMAPINFOHEADER, BI_RGB needs no palette for a 32-bit bitmap.
    _fields_ = [('size', W.DWORD), ('width', W.LONG), ('height', W.LONG),
                ('planes', W.WORD), ('bits', W.WORD), ('compression', W.DWORD),
                ('image_size', W.DWORD), ('x_ppm', W.LONG), ('y_ppm', W.LONG),
                ('used', W.DWORD), ('important', W.DWORD)]


GAME_EXECUTABLES = {'pathofexilesteam.exe', 'pathofexile.exe'}  # Steam and standalone clients
GAME_TITLE = 'Path of Exile 2'

EnumProc = C.WINFUNCTYPE(W.BOOL, W.HWND, W.LPARAM)
enum_windows = bind(user32, 'EnumWindows', W.BOOL, EnumProc, W.LPARAM)
visible = bind(user32, 'IsWindowVisible', W.BOOL, W.HWND)
iconic = bind(user32, 'IsIconic', W.BOOL, W.HWND)
window_pid = bind(user32, 'GetWindowThreadProcessId', W.DWORD, W.HWND, C.POINTER(W.DWORD))
window_text = bind(user32, 'GetWindowTextW', C.c_int, W.HWND, W.LPWSTR, C.c_int)
foreground = bind(user32, 'GetForegroundWindow', W.HWND)
set_foreground = bind(user32, 'SetForegroundWindow', W.BOOL, W.HWND)
show_window = bind(user32, 'ShowWindow', W.BOOL, W.HWND, C.c_int)
client_rect = bind(user32, 'GetClientRect', W.BOOL, W.HWND, C.POINTER(W.RECT))
client_to_screen = bind(user32, 'ClientToScreen', W.BOOL, W.HWND, C.POINTER(W.POINT))
map_key = bind(user32, 'MapVirtualKeyW', W.UINT, W.UINT, W.UINT)
key_state = bind(user32, 'GetAsyncKeyState', W.SHORT, C.c_int)
send_input = bind(user32, 'SendInput', W.UINT, W.UINT, C.POINTER(Input), C.c_int)
cursor_position = bind(user32, 'SetCursorPos', W.BOOL, C.c_int, C.c_int)
get_dc = bind(user32, 'GetDC', W.HDC, W.HWND)
release_dc = bind(user32, 'ReleaseDC', C.c_int, W.HWND, W.HDC)
compatible_dc = bind(gdi32, 'CreateCompatibleDC', W.HDC, W.HDC)
compatible_bitmap = bind(gdi32, 'CreateCompatibleBitmap', W.HBITMAP, W.HDC, C.c_int, C.c_int)
select_object = bind(gdi32, 'SelectObject', W.HANDLE, W.HDC, W.HANDLE)
delete_object = bind(gdi32, 'DeleteObject', W.BOOL, W.HANDLE)
delete_dc = bind(gdi32, 'DeleteDC', W.BOOL, W.HDC)
bit_blt = bind(gdi32, 'BitBlt', W.BOOL, W.HDC, C.c_int, C.c_int, C.c_int, C.c_int,
               W.HDC, C.c_int, C.c_int, W.DWORD)
get_bits = bind(gdi32, 'GetDIBits', C.c_int, W.HDC, W.HBITMAP, W.UINT, W.UINT,
                W.LPVOID, C.POINTER(BitmapInfo), W.UINT)
open_process = bind(kernel32, 'OpenProcess', W.HANDLE, W.DWORD, W.BOOL, W.DWORD)
query_image = bind(kernel32, 'QueryFullProcessImageNameW', W.BOOL, W.HANDLE, W.DWORD,
                   W.LPWSTR, C.POINTER(W.DWORD))
close_handle = bind(kernel32, 'CloseHandle', W.BOOL, W.HANDLE)

# DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2: client and screenshot pixels agree.
bind(user32, 'SetProcessDpiAwarenessContext', W.BOOL, W.HANDLE)(C.c_void_p(-4))


def executable(pid):
    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
    handle = open_process(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
    if not handle:
        raise C.WinError(C.get_last_error())
    try:
        text, length = C.create_unicode_buffer(32768), W.DWORD(32768)
        if not query_image(handle, 0, text, C.byref(length)):
            raise C.WinError(C.get_last_error())
        return text.value
    finally:
        close_handle(handle)


def describe(hwnd):
    pid = W.DWORD()
    window_pid(hwnd, C.byref(pid))
    title = C.create_unicode_buffer(512)
    window_text(hwnd, title, len(title))
    rect, origin = W.RECT(), W.POINT()
    if not client_rect(hwnd, C.byref(rect)) or not client_to_screen(hwnd, C.byref(origin)):
        raise RuntimeError('Window geometry is unavailable')
    return dict(hwnd=hwnd, pid=pid.value, title=title.value, exe=executable(pid.value),
                x=origin.x, y=origin.y, width=rect.right, height=rect.bottom,
                foreground=foreground() == hwnd, minimized=bool(iconic(hwnd)))


def game_windows(pid=None):
    found = []

    @EnumProc
    def visit(hwnd, _):
        if not visible(hwnd):
            return True
        actual = W.DWORD()
        window_pid(hwnd, C.byref(actual))
        if pid is not None and actual.value != pid:
            return True
        try:
            info = describe(hwnd)
            if Path(info['exe']).name.lower() in GAME_EXECUTABLES and info['title'] == GAME_TITLE:
                found.append(info)
        except (OSError, RuntimeError):  # a window can disappear during enumeration
            pass
        return True

    if not enum_windows(visit, 0):
        raise C.WinError(C.get_last_error())
    return found


def target(pid):
    windows = game_windows(pid)
    if len(windows) != 1:
        raise RuntimeError(f'Expected one visible Path of Exile 2 window for PID {pid}; found {len(windows)}')
    return windows[0]


def require_foreground(info):
    actual = describe(info['hwnd'])
    if actual['pid'] != info['pid'] or actual['exe'] != info['exe'] or not actual['foreground'] or actual['minimized']:
        raise RuntimeError('Target changed or Path of Exile 2 lost focus; action stopped')
    return actual
