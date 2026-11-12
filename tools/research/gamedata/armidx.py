"""Index every dumped .arm room: placed objects, named points, sectors. Writes deepdig/arm_index.json."""
import os, re, json
S = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
D = os.path.join(S, 'armall')
NUM = r'-?[0-9.]+(?:e-?\d+)?'
OBJ = re.compile(r'^(' + NUM + r') (' + NUM + r') .*?"([^"]*)"(?: "([^"]*)")? ?\S*$')
SECTOR = re.compile(r'^"([^"]+)" (\d+) (\d+) (\d+) (\d+) "([^"]*)"')
POINT = re.compile(r'^(' + NUM + r') (' + NUM + r') (' + NUM + r') "([^"]*)"$')


def parse(fn):
    objs, points, sectors = [], [], []
    for line in open(fn, encoding='utf-8', errors='replace'):
        line = line.strip().lstrip('﻿')
        m = SECTOR.match(line)
        if m:
            sectors.append([m.group(1), int(m.group(2)), int(m.group(3)), int(m.group(4)), int(m.group(5)), m.group(6)])
            continue
        m = POINT.match(line)
        if m:
            name = m.group(4)
            if name.startswith('Metadata/'):
                objs.append([float(m.group(1)), float(m.group(2)), name])
            elif name:
                points.append([float(m.group(1)), float(m.group(2)), name])
            continue
        if '"Metadata/' in line and line[:1] in '-0123456789':
            q = re.findall(r'"([^"]*)"', line)
            nums = line.split(' ')
            try:
                x, y = float(nums[0]), float(nums[1])
            except ValueError:
                continue
            path = None
            for s in reversed(q):
                if s.startswith('Metadata/') and not s.lower().endswith('.ao'):
                    path = s
                    break
            if path is None:
                path = q[-1] if q else ''
            objs.append([x, y, path])
    return objs, points, sectors


def main():
    idx = {}
    for f in os.listdir(D):
        o, p, s = parse(os.path.join(D, f))
        idx[f] = {'o': o, 'p': p, 's': s}
    json.dump(idx, open(os.path.join(S, 'arm_index.json'), 'w'))
    print(len(idx), sum(len(v['o']) for v in idx.values()))


if __name__ == '__main__':
    main()
