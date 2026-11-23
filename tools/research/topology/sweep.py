# Every area's graph edges that join named nodes in all its graphs, with the tile edge and .tdt folders that carry them: py sweep.py [area regex]; files under gf/ as for graphs.py, plus every .tdt.
import graphs,glob,re,collections,os,struct,pickle,sys
G='gf/metadata/terrain/gallows'
def strs(p):
    d=open(p,'rb').read()
    try: v,n=struct.unpack_from('<II',d,0); return [x for x in d[8:8+2*n].decode('utf-16le').split('\0') if x]
    except Exception: return []
if not os.path.exists('tdt.pk'):
    T={}
    for f in glob.glob('gf/metadata/terrain/**/*.tdt',recursive=True):
        k=f.replace(chr(92),'/')[3:].lower(); T[k]=[s.lower() for s in strs(f)]
    pickle.dump(T,open('tdt.pk','wb'))
T=pickle.load(open('tdt.pk','rb'))
def low(s): return s.lower()
def rooms(dirs):
    out={}
    for d in dirs:
        for f in glob.glob(d+'/**/*.arm',recursive=True):
            t=open(f,encoding='utf-8-sig',errors='replace').read().split('\n')
            try: n=int(t[1]); st=[x.strip().strip('"').lower() for x in t[2:2+n]]
            except Exception: continue
            out[f]={s for s in st if s.endswith('.et')}
    return out
def main():
    areas=collections.defaultdict(list)
    for f in glob.glob(G+'/**/*.?gr',recursive=True):
        f=f.replace(chr(92),'/'); p=graphs.parse(f)
        if p: areas['/'.join(f.split('gallows/')[1].split('/')[:3 if 'interlude' in f else 2])].append(p)
    for a in sorted(areas):
        if sys.argv[1:] and not re.search(sys.argv[1],a): continue
        if 'town' in a: continue
        Ts=[p for p in areas[a]]; N=len(Ts)
        # edges joining (connected comp) >=2 named nodes in all graphs having that edge
        d=f'{G}/{a}'
        tst=set()
        for x in glob.glob(d+'/*.tst')+glob.glob(d+'/*.gft'):
            tst|={s.lower() for s in re.findall(r'"([^"]+\.tdt)"',open(x,encoding='utf-8-sig',errors='replace').read())}
        R=rooms([d])
        cnt=collections.Counter(); joined=collections.defaultdict(collections.Counter)
        for size,nodes,edges in Ts:
            by=collections.defaultdict(list)
            for u,v,et,_ in edges: by[et.lower()].append((u,v))
            for et,es in by.items():
                cnt[et]+=1
                adj=collections.defaultdict(set)
                for u,v in es:
                    if u<len(nodes) and v<len(nodes): adj[u].add(v);adj[v].add(u)
                seen=set();best=set()
                for s in adj:
                    if s in seen: continue
                    comp=set();st=[s]
                    while st:
                        x=st.pop()
                        if x in comp: continue
                        comp.add(x);st+=adj[x]
                    seen|=comp
                    for nm in {nodes[x][2] for x in comp if nodes[x][2]}: joined[et][nm]+=1
        for et,k in cnt.items():
            if 'void' in et or 'black' in et or et.endswith('dungeon/rooms.et') or 'deepwater' in et: continue
            always=[n for n,v in joined[et].items() if v>=k]
            if k<max(1,0.6*N) or not always: continue
            # tile edges
            if any(et in T.get(t,()) for t in tst): te={et}
            else:
                W=[r for r in R.values() if et in r]; O=[r for r in R.values() if et not in r]
                te={x for r in W for x in r if x!=et and sum(x in q for q in W)>=.6*len(W) and sum(x in q for q in O)<=.15*max(1,len(O))} if W else set()
            tiles=collections.Counter('/'.join(t.replace('metadata/terrain/','').split('/')[:-1]) for t in tst if any(e in T.get(t,()) for e in te))
            print(a,f'[{k}/{N}]',et.replace('metadata/terrain/',''),'| joins',','.join(always[:8]),'| tile edges',[e.replace('metadata/terrain/','') for e in te][:3],'| tiles',dict(tiles.most_common(4)))
main()
