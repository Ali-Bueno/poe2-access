# Load a tile dump (debug/tiles-*.txt) by sub-tile; cls() is the first study class from the tile path (Manor names).
import sys
def load(p):
    T = {}
    for l in open(p):
        if l.startswith('#') or not l.strip(): continue
        f = l.split()
        if len(f) != 6: continue
        x, y, path, ix, iy, r = f
        T[(int(x)//23, int(y)//23)] = (path, int(ix), int(iy), int(r))
    return T
def cls(path):
    p = path.split('/')[-1][:-4]
    if 'black_nofog' in path: return '.'
    if 'blank' in p: return ' '
    if 'chapel' in p: return 'C'
    if 'arenabottom' in p: return 'Ab'
    if 'arenatop' in p: return 'At'
    if 'arenaholeupper' in p: return 'Au'
    if 'arenaholemid' in p: return 'Am'
    if 'arena' in p: return 'A'
    if 'closeddoor' in p: return 'D'
    if 'window' in p: return 'w'
    if 'crest' in p: return 'c'
    if 'organ' in p: return 'O'
    if 'cell' in p: return 'L'
    if 'doorfeature' in p: return 'd'
    if 'portrait' in p: return 'P'
    if 'oven' in p: return 'V'
    if 'opendoor' in p: return 'o'
    if 'stairsdown' in p: return 'v'
    if 'areatransition' in p: return '^'
    if 'entrance' in p: return 'E'
    if 'fireplace' in p: return 'f'
    if 'skylight' in p: return 's'
    if 'excavation' in path: return 'x'
    if 'stairs' in path or 'ledge' in path: return '='
    return '#'
if __name__ == '__main__':
    T = load(sys.argv[1]); W = max(k[0] for k in T)+1; H = max(k[1] for k in T)+1
    print(W, H)
    for y in range(H):
        row = ''.join(cls(T[(x,y)][0])[-1] if (x,y) in T else '?' for x in range(W))
        if row.strip('.'): print('%3d %s' % (y, row))
