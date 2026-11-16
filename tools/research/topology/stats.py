from topo import *
import networkx as nx
for a in STAMPS+['20261108-133607:L']:
    s,L=(a[:-2],True) if a.endswith(':L') else (a,False)
    r,G,ded=analyse(s,largest=L)
    byedge={id(x['edge']):x for x in ded}
    fork=lambda n: G.nodes[n]['kind']!='E' and G.degree(n)>=3
    nook=lambda n,d: (id(d) in byedge and n not in byedge[id(d)]['small'] and byedge[id(d)]['area']<P['nook'])
    forks=[n for n in G if fork(n)]
    real=[n for n in forks if sum(1 for u,v,k,d in G.edges(n,keys=True,data=True) if not nook(n,d))>=3]
    # path walk
    H2=nx.Graph()
    for u,v,k,d in G.edges(keys=True,data=True): H2.add_edge(u,v,w=d['length']*2)
    # longest shortest path from any node (diameter-ish) 
    best=(0,None)
    for st in list(H2)[:60]:
        dist,paths=nx.single_source_dijkstra(H2,st,weight='w'); far=max(dist,key=dist.get)
        if dist[far]>best[0]: best=(dist[far],paths[far])
    path=best[1]; mins=best[0]/37/60
    pf=[n for n in path[1:-1] if fork(n)]; pr=[n for n in pf if n in real]
    print(s,'L' if L else '', 'forks',len(forks),'real(>=3 non-nook)',len(real),'J-only',sum(1 for n in forks if G.nodes[n]['kind']=='J'),
      'diam path %.0f cells=%.2f min: forks %d (%.1f/min) real %d (%.1f/min)'%(best[0],mins,len(pf),len(pf)/mins,len(pr),len(pr)/mins),
      'rooms on path',sum(1 for n in path if G.nodes[n]['kind']=='R'))
