# Coverage of readable labelled rooms across all .arm: tile-structure matchable vs entity-anchor matchable.
import sys, os, glob, re, json; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arm
from collections import Counter, defaultdict
GEN = re.compile(re.search(r"GEN = re.compile\(r'(.*)', re.I\)", open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'labels.py')).read()).group(1), re.I)
ENT = re.compile(r'"(Metadata/(?:Chests|NPC|MiscellaneousObjects/(?!Doodad)|Terrain/[^"]*/Objects|QuestObjects|Shrines|Effects/[^"]*Transition)[^"]*)"')
stat = Counter(); per = defaultdict(Counter); lab = Counter(); out = []
for p in sorted(glob.glob(sys.argv[1] + '/*.arm')):
    try: r = arm.parse(p)
    except Exception: continue
    l = r['label'].strip()
    if GEN.match(l): continue
    S = r['strings']; k1 = 0; tagged = 0; big = 0
    for row in r['grid']:
        for e in row:
            if e[0] != 'k': continue
            t = e[1]
            if t[22]: tagged += 1
            if t[0]*t[1] == 1 and (any(t[2:6]) or t[22]): k1 += 1
            elif t[0]*t[1] > 1: big += 1
    ents = [m.group(1) for line in r['rest'] for m in [ENT.search(line)] if m and 'Doodad' not in line.split('"')[-2:][0]]
    ents = [x for x in ents if 'Monsters' not in x]
    tile_ok = k1 >= 12
    ent_ok = len(ents) >= 2
    f = os.path.basename(p)[len('metadata_terrain_'):-4]
    kind = 'both' if tile_ok and ent_ok else 'tiles' if tile_ok else 'entities' if ent_ok else 'neither'
    stat[kind] += 1; lab[l.lower()] += 1
    top = f.split('_')[0] + ('_' + f.split('_')[1] if f.startswith('gallows') else '')
    per[top][kind] += 1
    out.append((f, l, kind, k1, tagged, big, len(ents)))
print('readable rooms', sum(stat.values()), dict(stat))
for t, c in sorted(per.items(), key=lambda x: -sum(x[1].values()))[:14]: print(' ', t, dict(c))
json.dump(out, open(sys.argv[2], 'w'))
