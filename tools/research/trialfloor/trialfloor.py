"""Decode the Trial of the Sekhemas floor from the map's columns container (structural start), with self-checks.
trialfloor.py [COLUMNS_CONTAINER]  (default: the one measured 2026-11-12)."""
import struct
import sys
from mem import P

p = P()
cols_c = int(sys.argv[1], 16) if len(sys.argv) > 1 else 0x5F3D56B6430
ui_cols = [p.children(c) for c in p.children(cols_c)]
sizes = [len(c) for c in ui_cols]
print('UI column sizes', sizes)

# chain: columns container -> parent x3 -> +0x3A0
a = cols_c
for _ in range(3):
    a = p.parent(a)
owner = a
floor = p.q(owner + 0x3A0)
print(f'owner 0x{owner:X} (+0x3A0) -> floor 0x{floor:X} vt 0x{p.q(floor):X}')


def table_name(t):
    return p.wstr(t + 8) if t else None


def fk(d, off):
    row, tab = struct.unpack_from('<QQ', d, off)
    if not row:
        return None
    rid = p.wcstr(p.q(row) or 0, 80)
    return f'{rid}' + ('' if tab and table_name(tab).endswith(('SanctumRooms.dat', 'SanctumPersistentEffects.dat'))
                       else f' [BAD TABLE {table_name(tab)}]')


errors = []


def links(base):
    b, e, c = p.vec(base)
    n = (e - b) // 0x20
    out = []
    for ci in range(n):
        rb, re_, rc = struct.unpack('<QQQ', p.read(b + ci * 0x20, 0x18))
        nr = (re_ - rb) // 0x38
        col = []
        for ri in range(nr):
            lb, le, lc = struct.unpack('<QQQ', p.read(rb + ri * 0x38, 0x18))
            if not (lb <= le <= lc) or le - lb > 8:
                errors.append(f'bad link vec [{ci}][{ri}]')
                col.append([])
                continue
            col.append(list(p.read(lb, le - lb)) if le > lb else [])
        out.append(col)
    return out


def rooms(base):
    b, e, c = p.vec(base)
    n = (e - b) // 0x40
    out = []
    for i in range(n):
        d = p.read(b + i * 0x40, 0x40)
        out.append(dict(col=d[0], row=d[1], encounter=fk(d, 0x8), reward=fk(d, 0x18), effect=fk(d, 0x28),
                        b38=d[0x38], visited=d[0x39]))
    return out


for name, lo, ro, tail in (('A (+0x1B0, floor start?)', 0x1B0, 0x1C8, 0x1E0), ('B (+0x1F8, current)', 0x1F8, 0x210, 0x228)):
    print(f'=== block {name}')
    L = links(floor + lo)
    if [len(c) for c in L] != sizes:
        errors.append(f'{name}: link column sizes {[len(c) for c in L]} != UI {sizes}')
    for ci, col in enumerate(L):
        for ri, t in enumerate(col):
            if ci + 1 < len(L):
                if not t or any(x >= len(L[ci + 1]) for x in t) or t != sorted(set(t)):
                    errors.append(f'{name}: [{ci}][{ri}] links {t} invalid')
            elif t:
                errors.append(f'{name}: last column has links {t}')
        if ci > 0:
            incoming = {x for t in L[ci - 1] for x in t}
            if incoming != set(range(len(col))):
                errors.append(f'{name}: column {ci} rooms without incoming link: {set(range(len(col))) - incoming}')
    print(' links:', ' | '.join(f'c{ci}: ' + ' '.join(f'{ri}->{t}' for ri, t in enumerate(col)) for ci, col in enumerate(L)))
    for r in rooms(floor + ro):
        print('  room', r)
    q = p.read(floor + tail, 0x18)
    print(f'  tail +0x{tail:X}: {q.hex(" ")}')
print(f'path bytes +0x230: {list(p.read(floor + 0x230, 8))}  +0x238 byte {p.read(floor + 0x238, 1)[0]}')
print(f'+0x190 {p.q(floor + 0x190)}  +0x240 {p.q(floor + 0x240)}  +0x198 {p.q(floor + 0x198)}  +0x1A0 {p.q(floor + 0x1A0)}  +0x248 {p.q(floor + 0x248)}')
print('ERRORS' if errors else 'self-check OK', errors)
