# Cut a corner of a tile dump and the catalog's rooms and tiles it needs, as a test fixture.
# py cut.py <tiles dump> <rooms.json> <out tiles> <out rooms> <set> <x0> <x1> <y0> <y1> <label>...
import json, sys
dump, catalog, out_tiles, out_rooms, room_set = sys.argv[1:6]
X0, X1, Y0, Y1 = (int(v) for v in sys.argv[6:10])
labels = set(sys.argv[10:])
lines = [l.rstrip('\n') for l in open(dump)]
cut = [lines[0]] + [l for l in lines[1:] if len(l.split()) == 6 and l.split()[0].isdigit() and X0 <= int(l.split()[0]) // 23 < X1 and Y0 <= int(l.split()[1]) // 23 < Y1]
open(out_tiles, 'w', newline='\n').write('\n'.join(cut) + '\n')
c = json.load(open(catalog))
rooms = [r for r in c['rooms'] if r['set'] == room_set and r['label'] in labels]
paths = {l.split()[2] for l in cut[1:]}
used = sorted({r['runs'][i + 3] for r in rooms for i in range(0, len(r['runs']), 4)} | set(c['softAccepts']))
new = {old: i for i, old in enumerate(used)}
fams = sorted({f for r in rooms for f in r['families']})
fnew = {old: i for i, old in enumerate(fams)}
tiles = {}
for d, m in c['tiles'].items():
    for f, k in m.items():
        if 'metadata/terrain/' + d + '/' + f in paths and k in new:
            tiles.setdefault(d, {})[f] = new[k]
for r in rooms:
    r['runs'] = [v if i % 4 != 3 else new[v] for i, v in enumerate(r['runs'])]
    r['families'] = [fnew[f] for f in r['families']]
o = dict(classes=[c['classes'][k] for k in used], soft=[new[k] for k in c['soft'] if k in new], softAccepts=[new[k] for k in c['softAccepts']],
         families=[c['families'][f] for f in fams], tiles=tiles, rooms=rooms)
open(out_rooms, 'w', newline='\n').write(json.dumps(o, separators=(',', ':')))
print(len(cut), 'lines;', len(rooms), 'rooms;', sum(len(m) for m in tiles.values()), 'tiles;', len(used), 'classes')
