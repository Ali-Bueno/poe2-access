import struct, os, json
S = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DAT = os.path.join(S, 'dat')
_schema = None
NULL = 0xFEFEFEFEFEFEFEFE


def schema():
    global _schema
    if _schema is None:
        s = json.load(open(os.path.join(S, 'schema_dl', 'schema.min.json'), encoding='utf-8'))
        _schema = {}
        for t in s['tables']:
            if t['validFor'] & 2:
                _schema[t['name'].lower()] = t
    return _schema


def size(col):
    if col['array']:
        return 16
    t = col['type']
    if t == 'i32' and col.get('interval'):
        return 8
    return {'bool': 1, 'i16': 2, 'u16': 2, 'i32': 4, 'u32': 4, 'f32': 4, 'string': 8, 'foreignrow': 16, 'row': 8, 'enumrow': 4}[t]


class Dat:
    _cache = {}

    def __new__(cls, name):
        k = name.lower()
        if k in cls._cache:
            return cls._cache[k]
        o = super().__new__(cls)
        o._init(k)
        cls._cache[k] = o
        return o

    def _init(self, name):
        self.name = name
        d = open(os.path.join(DAT, 'data_balance_' + name + '.datc64'), 'rb').read()
        self.d = d
        self.n = struct.unpack_from('<I', d, 0)[0]
        m = d.find(b'\xbb' * 8, 4)
        self.rs = (m - 4) // self.n if self.n else 0
        self.var = m
        self.cols = []
        t = schema().get(name)
        off = 0
        if t:
            for i, c in enumerate(t['columns']):
                nm = c['name'] or ('unk%d_%X' % (i, off))
                self.cols.append((nm, off, c))
                off += size(c)
        self.schema_size = off
        self.byname = {c[0]: c for c in self.cols}
        self.idx = None

    def ok(self):
        return self.schema_size == self.rs

    def i32(self, r, o): return struct.unpack_from('<i', self.d, 4 + r * self.rs + o)[0]
    def u32(self, r, o): return struct.unpack_from('<I', self.d, 4 + r * self.rs + o)[0]
    def f32(self, r, o): return struct.unpack_from('<f', self.d, 4 + r * self.rs + o)[0]
    def i16(self, r, o): return struct.unpack_from('<h', self.d, 4 + r * self.rs + o)[0]
    def u64(self, r, o): return struct.unpack_from('<Q', self.d, 4 + r * self.rs + o)[0]
    def u8(self, r, o): return self.d[4 + r * self.rs + o]

    def strat(self, v):
        if v < 0 or self.var + v >= len(self.d):
            return None
        a = self.var + v
        b = a
        while b + 1 < len(self.d) and (self.d[b] != 0 or self.d[b + 1] != 0):
            b += 2
        return self.d[a:b].decode('utf-16le', 'replace')

    def str(self, r, o): return self.strat(self.u64(r, o))

    def key(self, r, o):
        v = self.u64(r, o)
        return None if v == NULL or v > 10 ** 8 else v

    def lst(self, r, o, esz, fn):
        cnt = self.u64(r, o)
        off = self.u64(r, o + 8)
        if cnt > 100000 or self.var + off + cnt * esz > len(self.d):
            return ['?BAD']
        out = []
        for i in range(cnt):
            a = self.var + off + i * esz
            out.append(fn(a))
        return out

    def val(self, r, col):
        nm, o, c = col
        t = c['type']
        if c['array']:
            if t == 'foreignrow':
                return self.lst(r, o, 16, lambda a: (lambda v: None if v == NULL else v)(struct.unpack_from('<Q', self.d, a)[0]))
            if t == 'row':
                return self.lst(r, o, 8, lambda a: (lambda v: None if v == NULL else v)(struct.unpack_from('<Q', self.d, a)[0]))
            if t == 'i32' or t == 'enumrow':
                return self.lst(r, o, 4, lambda a: struct.unpack_from('<i', self.d, a)[0])
            if t == 'f32':
                return self.lst(r, o, 4, lambda a: round(struct.unpack_from('<f', self.d, a)[0], 4))
            if t == 'string':
                return self.lst(r, o, 8, lambda a: self.strat(struct.unpack_from('<Q', self.d, a)[0]))
            cnt = self.u64(r, o)
            return '[%d of ?]' % cnt
        if t == 'bool': return self.u8(r, o)
        if t == 'i16': return self.i16(r, o)
        if t == 'u16': return struct.unpack_from('<H', self.d, 4 + r * self.rs + o)[0]
        if t == 'i32':
            if c.get('interval'):
                return (self.i32(r, o), self.i32(r, o + 4))
            return self.i32(r, o)
        if t == 'u32': return self.u32(r, o)
        if t == 'enumrow': return self.i32(r, o)
        if t == 'f32': return round(self.f32(r, o), 4)
        if t == 'string': return self.str(r, o)
        if t == 'foreignrow': return self.key(r, o)
        if t == 'row':
            v = self.u64(r, o)
            return None if v == NULL else v

    def row(self, r):
        return {c[0]: self.val(r, c) for c in self.cols}

    def ref(self, col):
        c = self.byname[col][2]
        return (c.get('references') or {}).get('table')

    def id(self, r):
        if r is None or r >= self.n:
            return None
        if self.cols and self.cols[0][2]['type'] == 'string':
            return self.str(r, 0)
        return '#%d' % r

    def find(self, idv):
        if self.idx is None:
            self.idx = {}
            for r in range(self.n):
                self.idx.setdefault(self.id(r), r)
        return self.idx.get(idv)

    def where(self, col, pred):
        c = self.byname[col]
        return [r for r in range(self.n) if pred(self.val(r, c))]


def show(t, r, skip_empty=True, resolve=True):
    d = Dat(t)
    out = []
    for nm, o, c in d.cols:
        v = d.val(r, (nm, o, c))
        if skip_empty and v in (None, 0, [], '', 0.0):
            continue
        ref = (c.get('references') or {}).get('table')
        if resolve and ref and v is not None:
            try:
                rt = Dat(ref)
                if isinstance(v, list):
                    v = [rt.id(x) if isinstance(x, int) else x for x in v]
                elif isinstance(v, int):
                    v = rt.id(v)
            except FileNotFoundError:
                pass
        out.append('  %s(+%X)=%s' % (nm, o, v))
    return '\n'.join(out)
