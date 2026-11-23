# Seeds the client generated per area (Client.txt) against the places the mod remembered there: the evidence of catalog/area-maps.json. Usage: py instances.py <game folder> (repository root as the working directory).
import json,collections,re,sys
R='./'
n=json.load(open(R+'Poe2Accessibility/catalog/area-names.json',encoding='utf-8'))['es']; c=json.load(open(R+'Poe2Accessibility/catalog/area-layouts.json'))
seeds=collections.defaultdict(set)
for l in open(sys.argv[1]+'/logs/Client.txt',encoding='utf-8',errors='replace'):
    m=re.search(r'Generating level (\d+) area "([^"]+)" with seed (\d+)',l)
    if m: seeds[n.get(m[2],m[2])].add(m[3])
d=collections.defaultdict(lambda: collections.defaultdict(lambda: collections.defaultdict(set)))
for f in ('dist/Poe2Access/app','host/Poe2Access/bin/Release/net10.0-windows/win-x64'):
    for l in open(R+f+'/Plugins/Poe2Accessibility/config/known-places.txt',encoding='utf-8'):
        if '\t' not in l: continue
        ch,r=l.rstrip('\n').split('\t',1); p=r.split('|')
        if len(p)==5 and p[1] in('exit','waypoint','checkpoint','boss_room'): d[p[0]][(p[1],p[2])][(int(p[3]),int(p[4]))].add(ch)
byname={}
for k,v in n.items():
    if k in c: byname.setdefault(v,k)
for a,pl in sorted(d.items(),key=lambda x:byname.get(x[0],'~')):
    if a not in byname or 'own' in byname[a]: continue
    chars={x for v in pl.values() for s in v.values() for x in s}
    print(byname[a],a,'graphs',c[byname[a]],'seeds',len(seeds[a]),'chars',len(chars),{(k[0][:4],k[1][:10]):[(q,len(s)) for q,s in sorted(v.items())][:6] for k,v in pl.items() if k[0]!='x' and len(v)<=6})
