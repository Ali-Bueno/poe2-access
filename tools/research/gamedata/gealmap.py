import sys,os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dat import *
ga=Dat('geometryattack'); ge=Dat('grantedeffects'); eg=Dat('executegeal')
byid={}
for r in range(ge.n): byid.setdefault(ge.i32(r,ge.byname['SkillID'][1]),[]).append(ge.id(r))
gid={eg.id(r):r for r in range(eg.n)}
o=ga.byname['ExecuteGeal'][1]
n=0
with open(os.path.join(S,'out_gealmap.tsv'),'w',encoding='utf-8') as f:
    for r in range(ga.n):
        v=ga.u64(r,o)
        if v==NULL or v==0: continue
        n+=1
        sk=byid.get(ga.i32(r,0),['?'])
        er=v if v<eg.n else None
        if er is None: f.write('%s\t%d\tNOGEAL\n'%(','.join(sk),v)); continue
        x=eg.row(er)
        f.write('%s\t%d\t%s\t%s\t%s\t%s\n'%(','.join(sk),v,';'.join(x['MetadataIDs']),(x['Script'] or '').replace('\n',' '),(x['ScriptCommand'] or '').replace('\n',' '),' || '.join((x[k] or '').replace('\n',' ') for k in ('unk30_93','unk31_9B','unk32_A3','unk33_AB') if x[k])))
print(n,'geometry attacks with a GEAL')
