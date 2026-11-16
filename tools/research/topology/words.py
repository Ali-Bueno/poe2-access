import re,glob,collections
cov=set('bridge stair stairs staircase stairway door doors doorway gate gates gateway gatehouse ruin ruins church chapel cathedral graveyard cemetery hut huts hovel shack cabin cottage house houses tower towers temple arena bossroom bossarena boss entrance dock docks dockedge pier jetty wharf waterfall waterfalls encampment camp courtyard alley alleyway ramp lake corridor tomb crypt mausoleum forge smithy foundry statue statues well boat ship shipwreck market bazaar stall stalls garden library'.split())
def words(p):
    n=p.rsplit('/',1)[-1].rsplit('.',1)[0]
    n=re.sub(r'([a-z])([A-Z])',r'\1_\2',n)
    return [w for w in re.split(r'[_\W\d]+',n.lower()) if w]
for a in ['act1_1_15','act1_1_14','act1_1_7','act1_1_13_1','act2_2_3','act2_2_12_1','act3_3_6_1','act3_3_12']:
    c=collections.Counter();tot=0
    for f in glob.glob(f't/metadata_terrain_gallows_{a}_tiles*.tst'):
        for l in open(f,encoding='utf-8',errors='replace'):
            m=re.search(r'"([^"]+\.tdt)"',l)
            if m and not l.lstrip().startswith('//'):
                tot+=1
                for w in set(words(m.group(1))):
                    if w not in cov and w not in('metadata','terrain','tdt'): c[w]+=1
    print(a,tot,c.most_common(45))
