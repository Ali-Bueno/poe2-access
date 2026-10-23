"""Read-only memory peek into the running game for offset research (never writes; dev tool only).

Addresses are hex. A chain like `31A928F1010+988` reads the pointer at each `+offset` step.
"""
import argparse
import ctypes as C
from ctypes import wintypes as W
import json
import struct
import sys

kernel32 = C.WinDLL('kernel32', use_last_error=True)
kernel32.OpenProcess.restype = W.HANDLE
kernel32.ReadProcessMemory.argtypes = [W.HANDLE, C.c_void_p, C.c_void_p, C.c_size_t, C.POINTER(C.c_size_t)]
PROCESS_VM_READ = 0x0010
MAX_READ = 0x10000
WSTRING_INLINE_CHARS = 8  # MSVC std::wstring small-buffer capacity
MAX_WSTRING_CHARS = 4096


class Process:
    def __init__(self, pid):
        self.handle = kernel32.OpenProcess(PROCESS_VM_READ, False, pid)
        if not self.handle:
            raise C.WinError(C.get_last_error())

    def read(self, address, size):
        buffer, done = C.create_string_buffer(size), C.c_size_t()
        if not kernel32.ReadProcessMemory(self.handle, C.c_void_p(address), buffer, size, C.byref(done)):
            raise OSError(f'Cannot read {size} bytes at 0x{address:X}')
        return buffer.raw[:done.value]

    def pointer(self, address):
        return struct.unpack('<Q', self.read(address, 8))[0]

    def wstring(self, address):
        """MSVC std::wstring: inline buffer or pointer at +0, length at +0x10, capacity at +0x18."""
        raw = self.read(address, 0x20)
        length, capacity = struct.unpack_from('<QQ', raw, 0x10)
        if length > MAX_WSTRING_CHARS or capacity < length:
            return None
        data = raw[:length * 2] if capacity < WSTRING_INLINE_CHARS else self.read(struct.unpack_from('<Q', raw)[0], length * 2)
        return data.decode('utf-16-le', errors='replace')


def resolve(process, chain):
    """`base+off1+off2` → follows pointers for every offset except the last, which is added."""
    parts = chain.split('+')
    address = int(parts[0], 16)
    for index, offset in enumerate(parts[1:]):
        address += int(offset, 16)
        if index < len(parts) - 2:
            address = process.pointer(address)
    return address


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--pid', type=int, required=True)
    sub = parser.add_subparsers(dest='action', required=True)
    for name in ('ptr', 'wstr'):
        sub.add_parser(name).add_argument('chain')
    hexdump = sub.add_parser('hex')
    hexdump.add_argument('chain')
    hexdump.add_argument('size', type=lambda text: int(text, 16))
    find = sub.add_parser('find', help='Offsets inside [chain, chain+size) holding the given 8-byte value')
    find.add_argument('chain')
    find.add_argument('size', type=lambda text: int(text, 16))
    find.add_argument('value', type=lambda text: int(text, 16))
    args = parser.parse_args()
    process = Process(args.pid)
    address = resolve(process, args.chain)
    result = dict(address=f'0x{address:X}')
    try:
        if args.action == 'ptr':
            result['value'] = f'0x{process.pointer(address):X}'
        elif args.action == 'wstr':
            result['text'] = process.wstring(address)
        elif args.action == 'hex':
            data = process.read(address, min(args.size, MAX_READ))
            result['rows'] = [f'+{row:04X} ' + ' '.join(f'{value:016X}' for value in struct.unpack_from(f'<{len(data[row:row + 32]) // 8}Q', data, row))
                              for row in range(0, len(data), 32)]
        elif args.action == 'find':
            data = process.read(address, min(args.size, MAX_READ))
            result['offsets'] = [f'+0x{offset:X}' for offset in range(0, len(data) - 7, 8)
                                 if struct.unpack_from('<Q', data, offset)[0] == args.value]
        result['ok'] = True
    except OSError as error:
        result.update(ok=False, error=str(error))
    print(json.dumps(result, indent=1))
    return 0 if result['ok'] else 1


if __name__ == '__main__':
    sys.exit(main())
