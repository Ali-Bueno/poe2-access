import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dat import *

ge = Dat('grantedeffects')
st = Dat('stats')
ss = Dat('grantedeffectstatsets')
ssl = Dat('grantedeffectstatsetsperlevel')
act = Dat('activeskills')

# active skill type -> table keyed by GrantedEffects.SkillID
def table_for(kind):
    k = kind or ''
    if k.startswith('geometry_trigger'): return 'geometrytrigger'
    if k.startswith('geometry_projectiles'): return 'geometryprojectiles'
    if k.startswith('geometry_mortars'): return 'monstermortar'
    if k.startswith('geometry_'): return 'geometryattack'
    if k.startswith('spawn_object'): return 'spawnobject'
    if k == 'move_daemon': return 'movedaemon'
    if k == 'trigger_beam': return 'triggerbeam'
    if k.startswith('execute_geal'): return 'executegeal'
    if k.startswith('effect_driven'): return 'effectdrivenskill'
    if k == 'single_ground_laser': return 'singlegroundlaser'
    if k.startswith('suicide_explosion'): return 'suicideexplosion'
    if k == 'table_charge': return 'tablecharge'
    if k.startswith('monster_mortar'): return 'monstermortar'
    if k == 'monster_projectile_spell': return 'monsterprojectilespell'
    if k.startswith('monster_projectile_weapon'): return 'monsterprojectileattack'
    return None

# meanings measured by the repo's extractor (TelegraphColumns.cs)
NAMES = {'geometryattack': {0x24: 'Range', 0x28: 'Shape(1circ2cone3rect4donut5tri)', 0x30: 'Length', 0x34: 'Width', 0x71: 'Angle', 0x7A: 'Delay'},
         'geometrytrigger': {0x24: 'Count', 0x28: 'Delay', 0x34: 'Range', 0x38: 'Radius', 0x3C: 'Angle'}}


def sid(k): return st.id(k) if k is not None else None


def statset(k):
    r = ss.row(k)
    out = ['   statset %s' % ss.id(k)]
    cs = list(zip([sid(x) for x in r['ConstantStats']], r['ConstantStatsValues']))
    if cs: out.append('    const: ' + ', '.join('%s=%s' % p for p in cs))
    if r['ImplicitStats']: out.append('    implicit: ' + ', '.join(sid(x) for x in r['ImplicitStats']))
    rows = [i for i in range(ssl.n) if ssl.key(i, 0) == k]
    for pick in rows[-1:]:
        p = ssl.row(pick)
        add = list(zip([sid(x) for x in p['AdditionalStats']], p['AdditionalStatsValues']))
        fl = list(zip([sid(x) for x in p['FloatStats']], p['FloatStatsValues']))
        flags = [sid(x) for x in p['AdditionalFlags']]
        if add: out.append('    level %s: %s' % (p['GemLevel'], ', '.join('%s=%s' % a for a in add)))
        if fl: out.append('    float: ' + ', '.join('%s=%s' % a for a in fl))
        if flags: out.append('    flags: ' + ', '.join(flags))
        others = [ge.id(x) for x in p['GrantedEffects'] if x is not None and ge.id(x) != ss.id(k)]
        if others: out.append('    grants: ' + ', '.join(others))
    return '\n'.join(out)


def skill(name):
    r = ge.find(name)
    if r is None:
        print('no skill', name); return
    row = ge.row(r)
    kind = act.id(row['ActiveSkill'])
    print('== %s (row %d) SkillID=%s ActiveSkill=%s Animation=%s' % (name, r, row['SkillID'], kind, Dat('animation').id(row['Animation'])))
    print(statset(row['StatSet']))
    for k in row['AdditionalStatSets']:
        print(statset(k))
    t = table_for(kind)
    if not t: return
    d = Dat(t)
    for i in range(d.n):
        hit = (d.id(i) == str(row['SkillID'])) if t == 'executegeal' else d.i32(i, 0) == row['SkillID']
        if t == 'geometrytrigger' and d.key(i, 4) == r: hit = True
        if hit:
            print('  [%s row %d]' % (t, i))
            txt = show(t, i)
            for o, nm in NAMES.get(t, {}).items():
                txt = txt.replace('(+%X)=' % o, '(+%X %s)=' % (o, nm))
            print(txt)


if __name__ == '__main__':
    for n in sys.argv[1:]:
        skill(n)
