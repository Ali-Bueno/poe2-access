# Parse every .arm; write rooms/census.json: path, label, W, H, slot counts, tags
import sys, os, glob, json, re; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arm
from collections import Counter
out = []; err = Counter(); vers = Counter()
for p in sorted(glob.glob(sys.argv[1] + '/*.arm')):
    first = open(p, encoding='utf-8-sig').readline().strip().lstrip('﻿'); vers[first] += 1
    try: r = arm.parse(p)
    except Exception as e: err[type(e).__name__] += 1; continue
    S = r['strings']; c = Counter(); tags = Counter(); big = 0
    for row in r['grid']:
        for e in row:
            c[e[0]] += 1
            if e[0] == 'k':
                t = e[1]
                if t[22] and t[22] <= len(S): tags[S[t[22]-1]] += 1
                if t[0]*t[1] > 1: big += 1
    name = os.path.basename(p)[len('metadata_terrain_'):-4]
    out.append(dict(f=name, label=r['label'], W=r['W'], H=r['H'], k=c['k'], s=c['s'], n=c['n'], fl=c['f'], o=c['o'], big=big, tags=dict(tags), hdr=r['hdr'][:2]))
json.dump(out, open(sys.argv[2], 'w'))
print(len(out), 'parsed; errors', dict(err)); print(vers.most_common(5))
