import re,glob,collections,sys
c=collections.Counter();per=collections.defaultdict(collections.Counter);hdr=collections.Counter()
for f in glob.glob('t/*.dgr'):
    L=open(f,encoding='utf-8',errors='replace').read().split('\n')
    try: n=int(L[4].split()[1]); 
    except: continue
    i=[k for k,l in enumerate(L) if l.startswith('Default%')][0]+2
    for l in L[i:i+n]:
        m=re.match(r'^-?\d+ -?\d+ \d+ (?:\d+ )*"([^"]*)"',l)
        nm=m.group(1) if m else '?'
        c[nm]+=1; per[f.split('metadata_terrain_gallows_')[-1].split('_graphs_')[0]][nm]+=1
print(len(c)); print(c.most_common(70))
import pickle;pickle.dump(per,open('per.pk','wb'))
for a in ['act1_1_15','act1_1_14']:
    print(a,dict(per[a]))
