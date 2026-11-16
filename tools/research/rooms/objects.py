# Object classes in all .arm files, and which appear as live entity paths in the debug dumps (docs/research/rooms.md).
# py objects.py <folder of extracted .arm> <debug folder> <out folder>  -> arms.json (labels, sizes, objects), classes.json
import sys, os, glob, re, json
from collections import Counter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from norm import key
ARM, DBG, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
OBJ = re.compile(r'^(\d+) (\d+) .*?"([^"]+\.ao)" "([^"]*)"(.*)$')
cls = Counter(); clsrooms = Counter(); out = []
for p in sorted(glob.glob(ARM + '/*.arm')):
    t = open(p, encoding='utf-8-sig', errors='replace').read().lstrip('\ufeff').split('\n')
    t = [l.rstrip('\r') for l in t]
    try:
        n = int(t[1]); strings = [s.strip('"') for s in t[2:2+n]]; label = t[2+n+2].strip().strip('"').strip()
        rk = t[2+n+4].split(); W, H = int(rk[1]), int(rk[2])
    except Exception: continue
    objs = []
    for l in t:
        m = OBJ.match(l)
        if m: objs.append((int(m[1]), int(m[2]), m[3], m[4], m[5].strip()))
    seen = set()
    for o in objs:
        c = re.sub(r'@\d+$', '', o[3]); cls[c] += 1
        if c not in seen: seen.add(c); clsrooms[c] += 1
    out.append(dict(f=os.path.basename(p)[len('metadata_terrain_'):-4], label=label, key=key(label), W=W, H=H,
                    strings=[s for s in strings if s.lower().endswith(('.et', '.gt'))][:6],
                    objs=[o for o in objs if 'Doodad' not in o[3]]))
json.dump(out, open(OUT + '/arms.json', 'w'))
live = Counter()
for f in glob.glob(DBG + '/entities-*.txt') + glob.glob(DBG + '/tiles-*.txt'):
    for l in open(f, encoding='utf-8', errors='replace'):
        for m in re.finditer(r'(Metadata/[A-Za-z0-9_/@.]+)', l):
            live[re.sub(r'@\d+$', '', m[1])] += 1
json.dump(dict(cls=cls, rooms=clsrooms, live=live), open(OUT + '/classes.json', 'w'))
print(len(out), 'arms;', len(cls), 'classes;', len(live), 'live paths')
