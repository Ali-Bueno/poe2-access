"""Shared helpers: load the PE once as a flat image; read-only."""
import struct, mmap, os, pickle, bisect, sys, site
sys.path.append(site.getusersitepackages())  # -I hides user site; numpy/capstone live there
import numpy as np
EXE = r"D:\games\steam\steamapps\common\Path of Exile 2\PathOfExileSteam.exe"

class PE:
    def __init__(self, path=EXE):
        with open(path, "rb") as f:
            self.raw = f.read()
        raw = self.raw
        e = struct.unpack_from("<I", raw, 0x3C)[0]
        assert raw[e:e+4] == b"PE\0\0"
        self.machine, self.nsec, self.tstamp = struct.unpack_from("<HHI", raw, e+4)
        optsz = struct.unpack_from("<H", raw, e+20)[0]
        o = e+24
        self.magic = struct.unpack_from("<H", raw, o)[0]
        self.linker = (raw[o+2], raw[o+3])
        self.entry = struct.unpack_from("<I", raw, o+16)[0]
        self.imagebase = struct.unpack_from("<Q", raw, o+24)[0]
        self.size_image = struct.unpack_from("<I", raw, o+56)[0]
        self.dllchars = struct.unpack_from("<H", raw, o+70)[0]
        nrva = struct.unpack_from("<I", raw, o+108)[0]
        self.dirs = [struct.unpack_from("<II", raw, o+112+8*i) for i in range(nrva)]
        s = o+optsz
        self.sections = []
        for i in range(self.nsec):
            name = raw[s:s+8].rstrip(b"\0").decode("latin1")
            vs, va, rs, rp = struct.unpack_from("<IIII", raw, s+8)
            ch = struct.unpack_from("<I", raw, s+36)[0]
            self.sections.append(dict(name=name, vsize=vs, va=va, rsize=rs, rptr=rp, chars=ch))
            s += 40
        img = bytearray(self.size_image)
        hs = self.sections[0]["rptr"]
        img[:hs] = raw[:hs]
        for x in self.sections:
            n = min(x["rsize"], x["vsize"]) if x["vsize"] else x["rsize"]
            img[x["va"]:x["va"]+n] = raw[x["rptr"]:x["rptr"]+n]
        self.img = bytes(img)
        self.np = np.frombuffer(self.img, dtype=np.uint8)
    def sec(self, name):
        for x in self.sections:
            if x["name"] == name: return x
    def sec_of(self, rva):
        for x in self.sections:
            if x["va"] <= rva < x["va"]+max(x["vsize"], x["rsize"]): return x["name"]
    def u32(self, rva): return struct.unpack_from("<I", self.img, rva)[0]
    def i32(self, rva): return struct.unpack_from("<i", self.img, rva)[0]
    def u64(self, rva): return struct.unpack_from("<Q", self.img, rva)[0]
    def cstr(self, rva, maxn=512):
        e = self.img.find(b"\0", rva, rva+maxn)
        return self.img[rva:e if e >= 0 else rva+maxn]
    def funcs(self):
        """(begin,end,unwind) rows of .pdata, sorted."""
        va, sz = self.dirs[3]
        a = np.frombuffer(self.img, dtype="<u4", count=sz//4, offset=va).reshape(-1, 3)
        a = a[a[:, 0] != 0]
        return a[np.argsort(a[:, 0])]

def find_all(buf, needle, start=0, end=None):
    out = []; i = buf.find(needle, start, end if end is not None else len(buf))
    while i >= 0:
        out.append(i); i = buf.find(needle, i+1, end if end is not None else len(buf))
    return out

def entropy(b):
    if not len(b): return 0.0
    c = np.bincount(np.frombuffer(b, dtype=np.uint8), minlength=256).astype(float)
    p = c[c > 0]/c.sum()
    return float(-(p*np.log2(p)).sum())
