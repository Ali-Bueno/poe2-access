"""Parse Sanctum .arm rooms: ordered object list (index = 0-based position in the object block),
per-object variables, and the trailing link list `<from> <to> "<label>"`."""
import os, re, sys, json
from collections import Counter
L = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ARM = os.path.join(L, 'arm')
NUMTOK = re.compile(r'^-?[0-9.]+(e-?\d+)?$')
LINK = re.compile(r'^(\d+) (\d+) "([^"]*)"$')


def short(p):
    return p.rsplit('/', 1)[-1]


def parse(fn):
    lines = [l.rstrip('\r\n').lstrip('\ufeff') for l in open(fn, encoding='utf-8', errors='replace')]
    objs, links = [], []
    start = None
    for i, l in enumerate(lines):
        q = re.findall(r'"([^"]*)"', l)
        toks = l.split(' ')
        if len(q) >= 2 and NUMTOK.match(toks[0]) and any(s.startswith('Metadata/') for s in q) and len(toks) > 8:
            start = i
            break
    if start is None:
        return objs, links, None
    i = start
    while i < len(lines) and lines[i] != '-1':
        l = lines[i]
        q = re.findall(r'"([^"]*)"', l)
        toks = l.split(' ')
        tail = l[l.rfind('"') + 1:].strip().split(' ')
        nvar = int(tail[0]) if tail and tail[0].isdigit() else None
        vars_ = tail[1:] if nvar else []
        flag = toks[2]
        if flag == '1':
            wx, wy = float(toks[3]), float(toks[4])
        else:
            wx = wy = None
        objs.append({'i': len(objs), 'line': i + 1, 'x': float(toks[0]), 'y': float(toks[1]), 'wx': wx, 'wy': wy,
                     'ao': q[0] if len(q) > 1 else None, 'ot': q[-1], 'vars': vars_})
        i += 1
    end = i
    for j in range(end + 1, len(lines)):
        m = LINK.match(lines[j])
        if m:
            links.append((int(m.group(1)), int(m.group(2)), m.group(3), j + 1))
    return objs, links, start + 1


def room_name(f):
    return f.replace('metadata_terrain_gallows_leagues_sanctum_', '').replace('.arm', '')


if __name__ == '__main__':
    pat = sys.argv[1] if len(sys.argv) > 1 else 'gauntlet'
    out = {}
    for f in sorted(os.listdir(ARM)):
        if not f.endswith('.arm') or pat not in f:
            continue
        objs, links, s = parse(os.path.join(ARM, f))
        out[room_name(f)] = {'objs': objs, 'links': links, 'objstart': s}
    json.dump(out, open(os.path.join(L, 'misc', 'arm_%s.json' % pat), 'w'), indent=0)
    for k, v in out.items():
        objs = v['objs']
        bad = [(a, b) for a, b, lab, ln in v['links'] if a >= len(objs) or b >= len(objs)]
        kinds = Counter(short(objs[a]['ot']) + '->' + short(objs[b]['ot']) + ':' + lab for a, b, lab, ln in v['links'] if a < len(objs) and b < len(objs))
        print(k, 'objs', len(objs), 'links', len(v['links']), 'bad', bad, dict(kinds))
