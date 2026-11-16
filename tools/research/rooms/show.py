# Print rooms' grids by tag, edge or ground: py show.py <this folder> <file.arm>...
import sys, glob; sys.path.insert(0, sys.argv[1])
import arm
for p in sys.argv[2:]:
    r = arm.parse(p); S = r['strings']
    print(p.split('rooms_')[-1], r['label'], r['W'], 'x', r['H'], 'strings:', list(enumerate(S,1)))
    for row in r['grid']:
        out = []
        for e in row:
            if e[0] != 'k': out.append(e[0]*3); continue
            t = e[1]
            if t[22]: c = S[t[22]-1][:3]
            elif any(t[2:6]): c = '#' + ''.join(str(x) for x in t[2:6] if x)[:2]
            else:
                g = set(t[14:18]); c = 'g' + ''.join(str(x) for x in sorted(g))[:2]
            if t[0]*t[1] > 1: c = c[:1] + '%d%d' % (t[0], t[1])
            out.append(c.ljust(3)[:3])
        print(' '.join(out))
    print(r['hdr'], r['roomkey'])
