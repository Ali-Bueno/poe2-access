"""Topology prototype: skeleton graph, rooms, forks, dead ends from a walkable grid."""
import sys, time, math, json, numpy as np
from scipy import ndimage as ndi
from skimage.morphology import skeletonize
from PIL import Image
import networkx as nx
from load import load

P = dict(f=2,             # coarsen factor
         floor=12,        # spur floor (cells)
         spur_k=1.5,      # spur if length < spur_k*halfwidth + floor
         room_r=24,       # EDT radius (cells) of a room core
         room_min=1200, room_peak=40,   # min room core area (cells^2)
         merge_k=1.0,     # collapse edge between nodes if len < merge_k*width + merge_floor
         merge_floor=6,
         hole=2500,       # fill enclosed obstacles smaller than this (cells^2) before skeletonizing
         dead_frac=0.5,
         nook=math.pi*60**2, small=3*math.pi*60**2)
N8 = np.ones((3, 3), int)
WALK = 37.0  # cells / s


def nbcount(sk):
    return ndi.convolve(sk.astype(np.uint8), N8, mode='constant') - sk


def segments(sk, nodelab):
    nb = nbcount(sk)
    S = sk & (nodelab == 0)
    sl, ns = ndi.label(S, N8)
    dil = ndi.grey_dilation(nodelab, size=3)
    ys, xs = np.nonzero(S)
    sid = sl[ys, xs]; nd = dil[ys, xs]
    m = nd > 0
    touch = {}
    if m.any():
        for s, n in np.unique(np.stack([sid[m], nd[m]], 1), axis=0):
            touch.setdefault(int(s), set()).add(int(n))
    fe = (nb[ys, xs] == 1) & (nd == 0)
    free = {}
    for s, y, x in zip(sid[fe], ys[fe], xs[fe]):
        free.setdefault(int(s), []).append((int(y), int(x)))
    cnt = np.bincount(sid, minlength=ns+1)
    return sl, ns, touch, free, cnt


def junction_labels(sk):
    J = sk & (nbcount(sk) >= 3)
    return ndi.label(J, N8)


def prune(sk, edt, f, P, log):
    fl = P['floor']/f
    for it in range(15):
        lab, n = junction_labels(sk)
        sl, ns, touch, free, cnt = segments(sk, lab)
        jr = ndi.maximum(edt, lab, index=np.arange(n+1)) if n else np.zeros(1)
        rem = np.zeros(ns+1, bool)
        for s, t in touch.items():
            if len(t) == 1 and s in free:
                if cnt[s] < P['spur_k']*jr[next(iter(t))] + fl: rem[s] = True
        # isolated tiny stubs (no junction at all)
        for s in range(1, ns+1):
            if s not in touch and cnt[s] < fl: rem[s] = True
        if not rem.any(): break
        sk = skeletonize(sk & ~rem[sl])
        log.append(int(rem.sum()))
    return sk


def analyse(stamp, P=P, png=None, largest=False):
    T = {}; t0 = time.time()
    g, (px, py) = load(stamp)
    f = P['f']
    H, W = (g.shape[0]//f)*f, (g.shape[1]//f)*f
    m = (g[:H, :W] > 0).reshape(H//f, f, W//f, f).mean((1, 3)) >= 0.5
    cy, cx = min(py//f, m.shape[0]-1), min(px//f, m.shape[1]-1)
    lab, _ = ndi.label(m, N8)
    if lab[cy, cx] == 0:
        _, ind0 = ndi.distance_transform_edt(lab == 0, return_indices=True)
        cy, cx = ind0[0][cy, cx], ind0[1][cy, cx]
    if largest:
        sz = np.bincount(lab.ravel()); sz[0] = 0; big = sz.argmax()
        cy, cx = [int(a[0]) for a in np.nonzero(lab == big)]
        cy, cx = np.nonzero(lab == big); k = len(cy)//2; cy, cx = int(cy[k]), int(cx[k])
    comp = lab == lab[cy, cx]
    ys, xs = np.nonzero(comp)
    y0 = max(ys.min()-2, 0); x0 = max(xs.min()-2, 0)
    comp = comp[y0:ys.max()+3, x0:xs.max()+3]; pyc, pxc = cy-y0, cx-x0
    T['load+coarsen'] = time.time()-t0; t0 = time.time()
    comp_raw = comp
    if P['hole'] > 0:
        bl, bn = ndi.label(~comp)          # 4-conn background
        bs = np.bincount(bl.ravel(), minlength=bn+1)
        border = np.unique(np.concatenate([bl[0], bl[-1], bl[:, 0], bl[:, -1]]))
        fill = bs < P['hole']/(f*f); fill[0] = False; fill[border] = False
        comp = comp | fill[bl]
    edt = ndi.distance_transform_edt(comp)
    T['edt'] = time.time()-t0; t0 = time.time()
    sk = skeletonize(comp)
    T['skeleton'] = time.time()-t0; t0 = time.time()
    nsk0 = int(sk.sum()); log = []
    sk = prune(sk, edt, f, P, log)
    T['prune'] = time.time()-t0; t0 = time.time()

    # rooms: EDT cores
    rl, rn = ndi.label(edt >= P['room_r']/f, N8)
    rs = np.bincount(rl.ravel()); keep = rs >= P['room_min']/(f*f); keep[0] = False
    pk = ndi.maximum(edt, rl, np.arange(rn+1))*f; keep &= pk >= P['room_peak']
    core = keep[rl]; rl, rn = ndi.label(core, N8)
    roomnode = ndi.grey_dilation(rl, size=3) * sk
    jl, jn = junction_labels(sk & (roomnode == 0))
    nodelab = np.where(roomnode > 0, roomnode, np.where(jl > 0, jl + rn, 0))
    sl, ns, touch, free, cnt = segments(sk, nodelab)
    nids = np.unique(nodelab[nodelab > 0])
    G0 = nx.MultiGraph()
    cm = ndi.center_of_mass(sk, nodelab, nids); mr = ndi.maximum(edt, nodelab, nids)
    for nid, c, r in zip(nids, cm, mr):
        G0.add_node(int(nid), kind='R' if nid <= rn else 'J', pos=c, r=float(r))
    segw = ndi.mean(edt, sl, np.arange(1, ns+1))*2 if ns else []
    eid = 10_000_000
    for s in range(1, ns+1):
        nodes = sorted(touch.get(s, ()))
        for p in free.get(s, []):
            eid += 1; G0.add_node(eid, kind='E', pos=p, r=float(edt[p])); nodes.append(eid)
        a = dict(seg={s}, length=float(cnt[s]), w=float(segw[s-1]))
        if len(nodes) >= 2:
            G0.add_edge(nodes[0], nodes[1], **a)
            for x in nodes[2:]: G0.add_edge(nodes[0], x, **dict(a, length=a['length']/2))
        elif len(nodes) == 1 and cnt[s] > 3 and s in touch:
            G0.add_edge(nodes[0], nodes[0], **a)
    T['graph'] = time.time()-t0; t0 = time.time()
    if P.get('debug'):
        from collections import Counter
        print('G0', ns, len(nids), Counter((d['kind'], G0.degree(n)) for n, d in G0.nodes(data=True)), Counter(len(touch.get(s, ())) for s in range(1, ns+1)))

    # collapse short edges (union-find)
    par = {n: n for n in G0}
    def find(x):
        while par[x] != x: par[x] = par[par[x]]; x = par[x]
        return x
    collapsed = []
    for u, v, k, d in sorted(G0.edges(keys=True, data=True), key=lambda e: e[3]['length']):
        if G0.nodes[u]['kind'] == 'E' or G0.nodes[v]['kind'] == 'E': continue
        if d['length']*f < P['merge_k']*d['w']*f + P['merge_floor']:
            ru, rv = find(u), find(v)
            if ru != rv:
                if G0.nodes[ru]['kind'] == 'R' and G0.nodes[rv]['kind'] == 'R': continue
                if G0.nodes[rv]['kind'] == 'R': ru, rv = rv, ru
                par[rv] = ru
            collapsed.append(d['seg'])
    members = {}
    for n in G0: members.setdefault(find(n), set()).add(n)
    G = nx.MultiGraph()
    for r, ms in members.items():
        kinds = [G0.nodes[x]['kind'] for x in ms]
        G.add_node(r, kind='R' if 'R' in kinds else G0.nodes[r]['kind'], members=ms,
                   pos=G0.nodes[r]['pos'], r=max(G0.nodes[x]['r'] for x in ms), segs=set())
    for u, v, k, d in G0.edges(keys=True, data=True):
        ru, rv = find(u), find(v)
        if ru == rv:
            G.nodes[ru]['segs'] |= d['seg']
            continue
        G.add_edge(ru, rv, **d)
    T['collapse'] = time.time()-t0; t0 = time.time()

    # areas via nearest skeleton pixel
    _, ind = ndi.distance_transform_edt(~sk, return_indices=True)
    own_n = nodelab[ind[0], ind[1]]; own_s = sl[ind[0], ind[1]]
    seg_area = np.bincount(own_s[comp & (own_n == 0)], minlength=ns+1)*f*f
    nar = np.bincount(own_n[comp & (own_n > 0)], minlength=int(nodelab.max())+2)*f*f
    for n, d in G.nodes(data=True):
        d['area'] = int(sum(nar[x] for x in d['members'] if x < 10_000_000) + sum(seg_area[s] for s in d['segs']))
    for *_, d in G.edges(keys=True, data=True): d['area'] = int(sum(seg_area[s] for s in d['seg']))
    total = sum(d['area'] for _, d in G.nodes(data=True)) + sum(d['area'] for *_, d in G.edges(data=True))
    pnode_raw = own_n[pyc, pxc]; pseg = own_s[pyc, pxc]
    pnode = None
    for n, d in G.nodes(data=True):
        if pnode_raw in d['members'] or (pseg in d['segs']): pnode = n
    pedge = None
    for u, v, k, d in G.edges(keys=True, data=True):
        if pseg in d['seg']: pedge = (u, v)

    S = nx.Graph(); mult = {}
    for u, v, k, d in G.edges(keys=True, data=True):
        S.add_edge(u, v); mult[frozenset((u, v))] = mult.get(frozenset((u, v)), 0)+1
    S.add_nodes_from(G.nodes)
    deads = []
    for u, v in nx.bridges(S):
        if mult[frozenset((u, v))] != 1: continue
        d = list(G[u][v].values())[0]
        S2 = S.copy(); S2.remove_edge(u, v)
        cu = nx.node_connected_component(S2, u); cv = nx.node_connected_component(S2, v)
        def area(c):
            a = sum(G.nodes[n]['area'] for n in c)
            return a + sum(dd['area'] for x, y, k, dd in G.edges(keys=True, data=True) if x in c and y in c)
        au, av = area(cu), area(cv)
        small, A, end = (cu, au, u) if au <= av else (cv, av, v)
        A += d['area']
        sub = nx.Graph(); sub.add_nodes_from(small)
        for x, y, k, dd in G.edges(keys=True, data=True):
            if x in small and y in small: sub.add_edge(x, y, w=dd['length'])
        L = d['length'] + max(nx.single_source_dijkstra_path_length(sub, end, weight='w').values())
        hp = (pnode in small) or (pedge is not None and pedge[0] in small and pedge[1] in small)
        deads.append(dict(area=A, length=L*f, small=small, edge=d, has_player=hp, end=end))
    ded = [a for a in deads if a['area'] < P['dead_frac']*total and not a['has_player']]
    for a in ded: a['nested'] = any(b is not a and a['small'] < b['small'] for b in ded)
    top = [a for a in ded if not a['nested']]
    T['areas+bridges'] = time.time()-t0
    cls = lambda A: 'nook' if A < P['nook'] else ('small' if A < P['small'] else 'large')
    fork = lambda n: G.nodes[n]['kind'] != 'E' and G.degree(n) >= 3
    skl = sum(d['length'] for *_, d in G.edges(keys=True, data=True))*f
    nforks = sum(1 for n in G if fork(n) and G.nodes[n]['kind'] == 'J')
    res = dict(stamp=stamp, shape=g.shape, comp_cells=int(comp.sum())*f*f, skel_px_raw=nsk0, skel_px=int(sk.sum()),
               prune_rounds=log, rooms=sum(1 for n in G if G.nodes[n]['kind'] == 'R'), junctions=nforks,
               room_forks=sum(1 for n in G if G.nodes[n]['kind'] == 'R' and G.degree(n) >= 3),
               endpoints=sum(1 for n in G if G.nodes[n]['kind'] == 'E'), edges=G.number_of_edges(),
               dead_all={c: sum(1 for a in ded if cls(a['area']) == c) for c in ('nook', 'small', 'large')},
               dead_top={c: sum(1 for a in top if cls(a['area']) == c) for c in ('nook', 'small', 'large')},
               skel_len_cells=skl, forks_per_min_density=(nforks+sum(1 for n in G if G.nodes[n]['kind'] == 'R' and G.degree(n) >= 3))/skl*WALK*60)
    # forks met walking from player to farthest node
    H2 = nx.Graph()
    for u, v, k, d in G.edges(keys=True, data=True): H2.add_edge(u, v, w=d['length']*f)
    st = pnode if pnode in H2 else (pedge[0] if pedge else None)
    if st in H2:
        dist, paths = nx.single_source_dijkstra(H2, st, weight='w')
        far = max(dist, key=dist.get); path = paths[far]
        nf = sum(1 for n in path[1:-1] if fork(n))
        res.update(path_len_cells=dist[far], path_forks=nf, path_minutes=dist[far]/WALK/60,
                   forks_per_min_path=nf/(dist[far]/WALK/60))
        # per-edge-walk stats: time between consecutive forks along path
        gaps = []; acc = 0
        for a, b in zip(path, path[1:]):
            acc += H2[a][b]['w']
            if fork(b): gaps.append(acc/WALK); acc = 0
        res['median_gap_s'] = float(np.median(gaps)) if gaps else None
    res['times'] = T; res['times']['total'] = sum(T.values())
    if png: render(png, f, comp, sk, nodelab, core, G, ded, sl, ind, pyc, pxc, own_n, own_s)
    return res, G, ded


def ds(mask, size, thr=0):
    im = Image.fromarray(mask.astype(np.uint8)*255).resize(size, Image.BOX)
    return np.asarray(im) > thr


def render(path, f, comp, sk, nodelab, core, G, ded, sl, ind, pyc, pxc, own_n, own_s):
    h, w = comp.shape
    sc = min(2.0, 1600/max(h, w)); size = (max(1, int(w*sc)), max(1, int(h*sc)))
    img = np.zeros((size[1], size[0], 3), np.uint8)
    wk = ds(comp, size, 60); img[wk] = (75, 75, 75)
    dn = set(); dsg = set()
    for a in ded:
        for n in a['small']:
            dn |= G.nodes[n]['members']; dsg |= G.nodes[n]['segs']
        dsg |= a['edge']['seg']
        for x, y, k, d in G.edges(keys=True, data=True):
            if x in a['small'] and y in a['small']: dsg |= d['seg']
    dm = (np.isin(own_n, list(dn)) & (own_n > 0)) | (np.isin(own_s, list(dsg)) & (own_n == 0) & (own_s > 0))
    dm &= comp
    img[ds(dm, size, 60) & wk] = (40, 55, 130)
    rm = ndi.binary_dilation(core, N8) & ~core
    img[ds(rm, size, 0)] = (60, 200, 80)
    img[ds(sk if sc >= 0.9 else ndi.binary_dilation(sk, N8), size, 0)] = (255, 255, 255)
    img[ds(sk & dm, size, 0)] = (90, 170, 255)
    jm = np.zeros_like(comp); em = np.zeros_like(comp)
    for n, d in G.nodes(data=True):
        if d['kind'] == 'J' and G.degree(n) >= 3:
            y, x = d['pos']; jm[int(round(y)), int(round(x))] = True
        if d['kind'] == 'E':
            y, x = d['pos']; em[int(y), int(x)] = True
    img[ds(ndi.binary_dilation(jm, N8, iterations=4), size, 0)] = (255, 40, 40)
    img[ds(ndi.binary_dilation(em, N8, iterations=2), size, 0)] = (255, 160, 0)
    pm = np.zeros_like(comp); pm[pyc, pxc] = True
    img[ds(ndi.binary_dilation(pm, N8, iterations=6), size, 0)] = (255, 255, 0)
    Image.fromarray(img).save(path)


STAMPS = ["20261108-124237", "20261108-115707", "20261108-133607", "20261108-134540"]
if __name__ == "__main__":
    for a in (sys.argv[1:] or STAMPS):
        s, L = (a[:-2], True) if a.endswith(':L') else (a, False)
        r, G, ded = analyse(s, png=f"topo-{s}{'-L' if L else ''}.png", largest=L)
        t = r.pop('times')
        print(json.dumps({k: (round(v, 2) if isinstance(v, float) else v) for k, v in r.items()}, default=str))
        print({k: round(v, 2) for k, v in t.items()})
