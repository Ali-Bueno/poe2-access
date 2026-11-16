# Untagged multi-cell tiles named by their key's signature, on both sides.
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dump, keycls, match
def sig(w, h, E, G):
    n = lambda xs: sorted(x.lower().split('/')[-1] for x in xs if x)
    return 'big:%dx%d:%s:%s' % (min(w, h), max(w, h), ','.join(n(E)), ','.join(n(G)))
old_cls = keycls.cls
def cls(path, blackmode='.'):
    k = keycls.key(path) if 'black_nofog' not in path else None
    if k and not k['tag'] and k['W'] * k['H'] > 1: return sig(k['W'], k['H'], k['E'], k['G'])
    return old_cls(path, blackmode)
old_slot = match.slotcls
def slotcls(t, S, bm):
    if not t[22] and t[0]*t[1] > 1:
        return sig(t[0], t[1], [S[i-1] for i in t[2:6] if i], [S[i-1] for i in t[14:18] if i])
    return old_slot(t, S, bm)
dump.cls = cls; match.slotcls = slotcls
if __name__ == "__main__": match.run(sys.argv[1], sys.argv[2], "B", 0.0)
