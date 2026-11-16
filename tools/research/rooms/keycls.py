# Class of a live tile from its decoded .tdt key (tag, edges, grounds), the generic counterpart of dump.cls.
import struct, os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import match
# A folder of extracted .tdt files named by their path with '/' as '_' (set ROOMS_TDT_DIR).
B = os.environ.get('ROOMS_TDT_DIR', 'b')
def key(path):
    f = os.path.join(B, path.replace('/', '_'))
    if not os.path.exists(f): return None
    d = open(f, 'rb').read(); v, n = struct.unpack_from('<II', d, 0); s = d[8:8+2*n].decode('utf-16le'); o = 8+2*n
    st = lambda off: None if off < 0 or off >= len(s) else s[off:].split('\0')[0]
    parent = st(struct.unpack_from('<i', d, o)[0])
    o2 = o + 5 if parent and parent.lower().endswith('.tdt') else o + 8
    tag = st(struct.unpack_from('<i', d, o2)[0]); E = [st(x) for x in struct.unpack_from('<4i', d, o2+4)]
    W, H = d[o2+20], d[o2+21]; G = [st(x) for x in struct.unpack_from('<4i', d, o2+22)]
    return dict(parent=parent, tag=tag or '', E=E, W=W, H=H, G=G)
def cls(path, blackmode='.'):
    if 'black_nofog' in path: return blackmode
    k = key(path)
    if k is None: return '?'
    if k['tag']: return match.TAGC.get(k['tag'], 'T:' + k['tag'])
    E = [e.lower() for e in k['E'] if e]; G = [g.lower() for g in k['G'] if g]
    if k['W'] * k['H'] > 1: return None
    if any('excavation' in x for x in E+G): return 'x'
    if any('stairs' in e or 'ledge' in e for e in E): return '='
    if any('wildcard.et' in e for e in E): return None
    if not E and not any('black_inside_wall' in g for g in G): return ' '
    if any(not e.endswith('rooms.et') for e in E): return '#'
    if any('floor' in g for g in G): return ' '
    if G and all('black_inside_wall' in g for g in G): return blackmode
    return None
