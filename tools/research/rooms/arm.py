# Parse a room file (.arm): strings, label, size, grid of slots (docs/research/rooms.md).
import re
def parse(path):
    L = open(path, encoding='utf-8-sig').read().lstrip('﻿').split('\n')
    L = [l.rstrip('\r') for l in L]
    i = 0
    assert L[0].startswith('version'), L[0]
    n = int(L[1]); strings = [s.strip('"') for s in L[2:2+n]]; i = 2+n
    hdr = L[i:i+4]; label = hdr[2].strip('"')
    i += 4
    rk = L[i].split(); W, H = int(rk[1]), int(rk[2]); i += 1
    # skip to grid: first line starting with grid token after the '""' line following room key
    while not re.match(r'^[knsfo]( |$)', L[i]): i += 1
    toks = []
    rows = []
    while len(rows) < H:
        rows.append(L[i]); i += 1
    grid = []
    for r in rows:
        t = r.split(); j = 0; row = []
        while j < len(t):
            c = t[j]
            if c == 'k': row.append(('k', tuple(int(x) for x in t[j+1:j+25]))); j += 25
            elif c == 'f': row.append(('f', int(t[j+1]))); j += 2
            elif c in 'ns': row.append((c,)); j += 1
            elif c == 'o': row.append(('o',)); j += 1
            else: raise ValueError((path, c, j))
        grid.append(row)
    return dict(strings=strings, label=label, W=W, H=H, hdr=hdr, roomkey=rk, grid=grid, rest=L[i:])
