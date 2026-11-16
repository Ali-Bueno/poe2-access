# Match each labelled .arm room's slot pattern against a live tile dump under 8 symmetries.
import sys, glob, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arm, dump
from collections import Counter
TAGC = {'DoorClosed':'D','Window':'w','WindowPagan':'w','Crest':'c','organ':'O','Cell':'L','door_thinwall':'d',
        'CountPortrait':'P','Oven':'V','DoorOpen':'o','TransitionDown':'v','transition':'^','Entrance':'E',
        'Fireplace':'f','skylight':'s','Chapel':'C','ArenaBottom':'Ab','ArenaTop':'At','ArenaHoleUpper':'Au',
        'pagan':'#','railing':'='}
def slotcls(t, S, blackmode):
    tag = S[t[22]-1] if t[22] else ''
    if tag: return TAGC.get(tag, None)
    E = [S[i-1].lower() for i in t[2:6] if i]; G = [S[i-1].lower() for i in t[14:18] if i]
    if any('wildcard.et' in e for e in E): return None
    if t[0]*t[1] > 1: return None          # untagged big tile: unknown (arenaholemid etc.)
    if any('excavation' in s for s in E+G): return 'x'
    if any('stairs' in e or 'ledge' in e for e in E): return '='
    if any(not e.endswith('rooms.et') for e in E): return '#'
    if any('floor' in g for g in G): return ' '
    if all('black_inside_wall' in g for g in G): return blackmode
    return None
OKD = {'D', '#', ' '}
SOFT = {'D', 'f', 'w', 'c'}
def ok(c, d):
    if c in SOFT: return d == c or d in OKD
    return c == d
def pattern(r, blackmode):
    S = r['strings']; P = {}
    for y, row in enumerate(r['grid']):
        for x, e in enumerate(row):
            if e[0] != 'k': continue
            t = e[1]; c = slotcls(t, S, blackmode)
            if c is None or c == 'B': continue
            for dy in range(t[1]):
                for dx in range(t[0]): P[(x+dx, y+dy)] = c
    return P
def syms(P, W, H):
    out = []
    for k in range(8):
        Q = {}
        for (x, y), c in P.items():
            if k & 4: x, y = y, x
            w, h = (H, W) if k & 4 else (W, H)
            if k & 1: x = w-1-x
            if k & 2: y = h-1-y
            Q[(x, y)] = c
        out.append(Q)
    return out
def run(dumpfile, armglob, blackmode='.', maxmis=0.1, show=3):
    T = dump.load(dumpfile); D = {k: dump.cls(v[0]) for k, v in T.items()}
    freq = Counter(D.values())
    byc = {}
    for k, c in D.items(): byc.setdefault(c, []).append(k)
    res = []
    for p in sorted(glob.glob(armglob)):
        r = arm.parse(p)
        if not r['label']: continue
        P = pattern(r, blackmode)
        if not P: continue
        hits = []
        for k, Q in enumerate(syms(P, r['W'], r['H'])):
            ax, ay = min(Q, key=lambda q: (Q[q] in SOFT, freq.get(Q[q], 0), q)); ac = Q[(ax, ay)]
            lim = int(len(Q)*maxmis)
            for (dx, dy) in byc.get(ac, []):
                ox, oy = dx-ax, dy-ay; mis = 0
                for (x, y), c in Q.items():
                    if not ok(c, D.get((ox+x, oy+y))):
                        mis += 1
                        if mis > lim: break
                if mis <= lim: hits.append((mis, k, ox, oy))
        hits.sort()
        res.append((p.split('rooms_')[-1], r['label'], len(P), hits))
        print('%-45s %-14s cells=%3d hits=%d best=%s' % (p.split('rooms_')[-1], r['label'], len(P), len(hits), hits[:show]))
    return res
if __name__ == '__main__':
    run(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else '.', float(sys.argv[4]) if len(sys.argv) > 4 else 0.1)
