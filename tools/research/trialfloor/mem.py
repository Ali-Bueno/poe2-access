"""Read-only process memory helper (PROCESS_VM_READ | PROCESS_QUERY_INFORMATION only)."""
import ctypes as C
from ctypes import wintypes as W
import struct
import subprocess

k32 = C.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, C.c_void_p, C.c_void_p, C.c_size_t, C.POINTER(C.c_size_t)]


class MBI(C.Structure):
    _fields_ = [('BaseAddress', C.c_void_p), ('AllocationBase', C.c_void_p), ('AllocationProtect', W.DWORD),
                ('PartitionId', W.WORD), ('RegionSize', C.c_size_t), ('State', W.DWORD), ('Protect', W.DWORD),
                ('Type', W.DWORD)]


k32.VirtualQueryEx.argtypes = [W.HANDLE, C.c_void_p, C.POINTER(MBI), C.c_size_t]
k32.VirtualQueryEx.restype = C.c_size_t


def find_pid():
    out = subprocess.run(['tasklist', '/FI', 'IMAGENAME eq PathOfExileSteam.exe', '/FO', 'CSV', '/NH'],
                         capture_output=True, text=True).stdout
    for line in out.splitlines():
        parts = [p.strip('"') for p in line.split('","')]
        if len(parts) > 1 and parts[0].lower().startswith('pathofexile'):
            return int(parts[1])
    raise SystemExit('game not running')


class P:
    def __init__(self, pid=None):
        self.pid = pid or find_pid()
        self.h = k32.OpenProcess(0x0010 | 0x0400, False, self.pid)
        if not self.h:
            raise C.WinError(C.get_last_error())

    def read(self, addr, size):
        if addr <= 0 or addr > 0x7FFFFFFFFFFF or size <= 0:
            return None
        buf = C.create_string_buffer(size)
        done = C.c_size_t()
        if not k32.ReadProcessMemory(self.h, C.c_void_p(addr), buf, size, C.byref(done)):
            return None
        return buf.raw[:done.value]

    def q(self, addr):
        b = self.read(addr, 8)
        return struct.unpack('<Q', b)[0] if b and len(b) == 8 else None

    def i32(self, addr):
        b = self.read(addr, 4)
        return struct.unpack('<i', b)[0] if b else None

    def u32(self, addr):
        b = self.read(addr, 4)
        return struct.unpack('<I', b)[0] if b else None

    def vec(self, addr):
        b = self.read(addr, 24)
        if not b:
            return None
        return struct.unpack('<QQQ', b)

    def readable(self, addr):
        if addr < 0x10000 or addr > 0x7FFFFFFFFFFF:
            return False
        m = MBI()
        if not k32.VirtualQueryEx(self.h, C.c_void_p(addr), C.byref(m), C.sizeof(m)):
            return False
        return m.State == 0x1000 and (m.Protect & 0xEE) != 0 and not (m.Protect & 0x100)

    def wstr(self, addr):
        raw = self.read(addr, 0x20)
        if not raw:
            return None
        length, cap = struct.unpack_from('<QQ', raw, 0x10)
        if length > 4096 or cap < length:
            return None
        if cap < 8:
            data = raw[:length * 2]
        else:
            data = self.read(struct.unpack_from('<Q', raw)[0], length * 2)
            if data is None:
                return None
        return data.decode('utf-16-le', errors='replace')

    def wcstr(self, addr, maxc=256):
        """Null-terminated UTF-16 string at addr."""
        b = self.read(addr, maxc * 2)
        if not b:
            return None
        s = b.decode('utf-16-le', errors='replace')
        i = s.find('\0')
        return s if i < 0 else s[:i]

    # UI element helpers (GameHelper2 UiElementBaseOffset): children +0x10, parent +0xB8, flags +0x168
    def children(self, el):
        v = self.vec(el + 0x10)
        if not v:
            return []
        b, e, _ = v
        n = (e - b) // 8
        if n < 0 or n > 4096:
            return []
        data = self.read(b, n * 8) if n else b''
        return list(struct.unpack(f'<{n}Q', data)) if data else []

    def parent(self, el):
        return self.q(el + 0xB8)
