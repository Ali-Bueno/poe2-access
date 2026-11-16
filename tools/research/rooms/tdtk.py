# Print a .tdt key area: strings by offset and the bytes after the table: py tdtk.py <file.tdt>...
import struct, sys, os
def strings(d):
    v, n = struct.unpack_from('<II', d, 0); s = d[8:8+2*n].decode('utf-16le'); return v, n, s
def at(s, off):
    if off < 0 or off >= len(s): return None
    return s[off:].split('\0')[0]
if __name__ == '__main__':
    for p in sys.argv[1:]:
        d = open(p, 'rb').read(); v, n, s = strings(d); o = 8+2*n
        offs = {}; i = 0
        for part in s.split('\0'): offs[i] = part.split('/')[-1]; i += len(part)+1
        print(os.path.basename(p)[-50:], v, offs)
        print('  ', d[o:o+48].hex(' ', 4))
