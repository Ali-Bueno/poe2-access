"""Client-only version of the colour rule: a lever's colour = .ao variant of the indicator(s) within 15 cells of it;
a door's indicators = the two indicators nearest the door; the door is opened by the levers whose colour is among its
indicators' variants. Compared with the real wiring of the .arm links."""
import json, os, math
from collections import defaultdict, Counter
L = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
d = json.load(open(os.path.join(L, 'misc', 'arm_gauntlet.json')))
sh = lambda p: p.rsplit('/', 1)[-1]
dist = lambda a, b: math.hypot(a['x'] - b['x'], a['y'] - b['y'])
C = Counter()
for room, v in d.items():
    o = v['objs']; inn = defaultdict(list)
    for a, b, lab, ln in v['links']: inn[b].append(a)
    inds = [x for x in o if sh(x['ot']) == 'SanctumLogicIndicator']
    doors = [x for x in o if sh(x['ot']) == 'SanctumLogicDoor']
    levers = [x for x in o if sh(x['ot']) == 'SanctumGenericLever']
    col = {lv['i']: set(sh(i['ao']) for i in inds if dist(i, lv) <= 15) for lv in levers}
    for lv in levers: C['lever colours within 15 = %d' % len(col[lv['i']])] += 1
    for dr in doors:
        near = sorted(inds, key=lambda i: dist(i, dr))[:2]
        variants = set(sh(i['ao']) for i in near)
        guess = set(l for l in col if col[l] & variants)
        real = set(f for a in inn[dr['i']] for f in inn[a])
        C['door rule %s' % (guess == real)] += 1
        if guess != real: print(room, dr['i'], guess, real)
print(dict(C))
