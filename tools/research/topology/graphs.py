import re,glob,os,collections,sys,json
# Reads the graph files extracted under gf/ (docs/research/map-hints.md, 'The level graphs'): py graphs.py [area regex]
G='gf/metadata/terrain/gallows'
def parse(f):
    L=[l.strip('\r') for l in open(f,encoding='utf-8-sig',errors='replace').read().split('\n')]
    nn=int(L[3].split()[1]); ne=int(L[4].split()[1]); size=L[1].split()[1:]
    i=5
    while i<len(L) and not re.match(r'^-?\d+ -?\d+ \d+ ',L[i]): i+=1
    # may be Default%
    nodes=[];edges=[]
    for l in L[i:i+nn]:
        m=re.match(r'^(-?\d+) (-?\d+) (\d+) ((?:\d+ )*)"([^"]*)"(.*)$',l)
        if not m: return None
        tags=re.findall(r'"([^"]*)"',m.group(6))
        nodes.append((int(m.group(1)),int(m.group(2)),m.group(5),tags))
    for l in L[i+nn:i+nn+ne]:
        m=re.match(r'^(\d+) (\d+) (\d+) ((?:-?\d+ )*)',l)
        if not m: return None
        et=re.findall(r'"([^"]*)"',l)
        n=int(m.group(3)); pts=[int(v) for v in m.group(4).split()][:2*n]
        edges.append((int(m.group(1)),int(m.group(2)),et[0] if et else '',list(zip(pts[::2],pts[1::2]))))
    return size,nodes,edges
if __name__=='__main__':
    areas=collections.defaultdict(list); bad=0
    for f in glob.glob(G+'/**/*.?gr',recursive=True):
        f=f.replace(chr(92),'/'); 
        try:p=parse(f)
        except Exception as e:p=None
        if not p: bad+=1; print('BAD',f); continue
        a='/'.join(f.split('gallows/')[1].split('/')[:3 if 'interlude' in f else 2])
        areas[a].append((f,p))
    for a in sorted(areas):
        if len(sys.argv)>1 and not re.search(sys.argv[1],a): continue
        T=areas[a]; n=len(T)
        c=collections.Counter();names=collections.Counter();ets=collections.Counter()
        for f,(size,nodes,edges) in T:
            for nm in {re.sub(r'_?(FI|I)?$','',x[2]) for x in nodes if x[2]}: names[nm]+=1
            by=collections.defaultdict(list)
            for u,v,et,_ in edges: by[et].append((u,v))
            for et,es in by.items():
                ets[et]+=1
                adj=collections.defaultdict(set)
                for u,v in es: 
                    if u<len(nodes) and v<len(nodes): adj[u].add(v);adj[v].add(u)
                seen=set()
                for s in adj:
                    if s in seen: continue
                    comp=[];st=[s]
                    while st:
                        x=st.pop()
                        if x in seen: continue
                        seen.add(x);comp.append(x);st+=adj[x]
                    for nm in {nodes[x][2] for x in comp if nodes[x][2]}: c[(et,nm)]+=1
        print('\n##',a,n,'templates; names',dict(names.most_common(25)))
        for et,k in ets.most_common():
            row=[(nm,v) for (e,nm),v in c.items() if e==et]
            row.sort(key=lambda t:-t[1])
            if row: print('  ',re.sub('Metadata/Terrain/','',et),k,'; '.join(f'{nm}:{v}' for nm,v in row[:9]))
