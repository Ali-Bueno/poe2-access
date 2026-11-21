"""Audit of the boss attacks the plugin leaves unwarned (2026-11-21, patch 0.5.5f dumps).

For every census fight (census/cards/*.txt) and every damaging skill of its boss, forms, adds and helpers: who casts it,
which of the plugin's warning paths reach it (hit-times line for that monster + animation, the skill's own area in
telegraph-skills, the long-blast stem rule, a ServerEffect marker in telegraph-effects, a hazard buff), its size against
the arena room, its delays, the dodge-roll flag and big-damage stats. Writes audit/attacks.tsv (one row per fight, caster,
skill) and audit/gaps.tsv (the rows judged a gap, scored). Read-only on the dumps and the shipped catalog.

Usage: py -B -I gaps.py <dump folder with dat/ and schema_dl/> <folder of the dumped .arm rooms>
"""
import sys, os, re, collections

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import dat  # noqa: E402

DUMP, ARMS = sys.argv[1], sys.argv[2]
dat.S, dat.DAT = DUMP, os.path.join(DUMP, 'dat')
Dat, NULL = dat.Dat, dat.NULL

REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
CAT = os.path.join(REPO, 'Poe2Accessibility', 'catalog')
CARDS = os.path.join(os.path.dirname(HERE), 'census', 'cards')
BLAST_LEAD_MS = 4000  # BossMechanicsWatch.Blast: a blow at least this far ahead gets the countdown
CELL = 250 / 23  # world units per grid cell (docs/research/attack-telegraphs.md)
TILE = 23        # cells per room tile

mv = Dat('monstervarieties'); ge = Dat('grantedeffects'); act = Dat('activeskills'); anim = Dat('animation')
st = Dat('stats'); ss = Dat('grantedeffectstatsets'); ga = Dat('geometryattack'); gt = Dat('geometrytrigger')
gft = Dat('groundeffecttypes'); bd = Dat('buffdefinitions'); gfx = Dat('groundeffects'); ssm = Dat('summonedspecificmonsters')
MV_ID = {mv.id(r): r for r in range(mv.n)}
MV_LOW = {k.lower(): r for k, r in MV_ID.items()}
GE_ID = {ge.id(r): r for r in range(ge.n)}
GA_BY = {ga.i32(r, 0): r for r in range(ga.n)}
GT_BY = {gt.i32(r, 0): r for r in range(gt.n)}
GT_CHILD = {}  # GE row of a trigger skill -> the GE row it fires
SHAPES = {1: 'circle', 2: 'cone', 3: 'rectangle', 4: 'donut', 5: 'triangle'}
GFX_BY_VAR = collections.defaultdict(list)
for _r in range(gfx.n):
    GFX_BY_VAR[gfx.i32(_r, 0x10)].append(_r)

# ---------- the shipped catalog ----------
HIT = {}  # (monster path, animation row) -> 'ms' timed, '-' untimed (with kind)
for line in open(os.path.join(CAT, 'hit-times.txt'), encoding='utf-8'):
    if line.startswith('#') or '|' not in line:
        continue
    m = re.match(r'^([^|]+)\|(\d+)=([^;]*)(?:;(.*))?$', line.strip())
    if m:
        HIT[(m.group(1), int(m.group(2)))] = (m.group(3), m.group(4) or '')
AREA = {}  # skill id -> (kind, area text, visuals)
for line in open(os.path.join(CAT, 'telegraph-skills.txt'), encoding='utf-8'):
    c = line.rstrip('\n').split('|')
    if line.startswith('#') or len(c) < 3:
        continue
    AREA[c[0]] = (c[1], c[2], c[3] if len(c) > 3 else '')
TEFX = {}
for line in open(os.path.join(CAT, 'telegraph-effects.txt'), encoding='utf-8'):
    c = line.rstrip('\n').split('|')
    if not line.startswith('#') and len(c) >= 2:
        TEFX[c[0]] = (c[1], c[2] if len(c) > 2 else '')
HAZ = [l.strip() for l in open(os.path.join(CAT, 'hazards.txt'), encoding='utf-8') if l.strip() and not l.startswith('#')]
DAEMON, PART, UNAV = collections.defaultdict(set), collections.defaultdict(set), collections.defaultdict(set)
for line in open(os.path.join(CAT, 'boss-mechanics.txt'), encoding='utf-8'):
    c = line.rstrip('\n').split('|')
    if c[0] == 'daemon' and len(c) >= 3:
        DAEMON[c[1]].add(c[2])
    elif c[0] == 'part' and len(c) >= 3:
        PART[c[1]].add(c[2])


def own_area(skill):
    """The plugin keeps only geometry and trigger lines that parse to a shape (AttackData.AddTelegraphSkills)."""
    a = AREA.get(skill)
    return a if a and a[0] in ('geometry', 'trigger') and ('shape=' in a[1] or a[0] == 'trigger') and a[1] else None


def stem(skill):
    """AttackData.Stem: the id from the last capital before its first lower-case letter; None without a kind prefix."""
    i = 0
    while i < len(skill) and not skill[i].islower():
        i += 1
    return skill[i - 1:] if 2 <= i < len(skill) else None


def blast_radius(skill):
    """AttackData.BlastRadius: widest circle among skills whose stem extends this one's, when it has no own area."""
    if own_area(skill) or not stem(skill):
        return None
    best = None
    s = stem(skill)
    for k, (kind, area, _) in AREA.items():
        if kind in ('geometry', 'trigger') and 'shape=circle' in area and stem(k) and stem(k).startswith(s):
            m = re.search(r'length=(\d+)', area)
            if m:
                best = max(best or 0, int(m.group(1)))
    return best


def hazard(buff):
    return any(buff == h or buff.startswith(h + '_') for h in HAZ)


# ---------- skills ----------
for _r in range(gt.n):
    k = gt.key(_r, 4)
    if k is not None:
        GT_CHILD.setdefault(gt.i32(_r, 0), k)

MELEE_ROW = next(i for i in range(anim.n) if anim.id(i) == 'Melee')
BIG = re.compile(r'float:.*maximum|final_at_centre|^active_skill_damage_\+%_final$|hit_damage_\+%_final$|base_multiplier|more_damage|instant_kill|%_of_.*_life|maximum_life_%|generic_skill_trigger_skills_with_id|active_skill_base_area_of_effect_radius', re.I)
DUR = re.compile(r'duration|_ms$|delay', re.I)


ssl = Dat('grantedeffectstatsetsperlevel')
SSL_LAST = {}
for _r in range(ssl.n):
    SSL_LAST[ssl.key(_r, 0)] = _r


def heavy(big):
    """A big-damage hint: a slam stronger at its centre, an attack multiplier of 250 % or more, a spell range of 1.9x or more."""
    m = re.search(r'base_multiplier=(\d+)', big)
    f = [float(v) for v in re.findall(r'float:[a-z_]*=([0-9.]+)', big)]
    return 'final_at_centre' in big or (m and int(m.group(1)) >= 25000) or any(v >= 1.9 for v in f)


def stats_of(row):
    """Constant stats and implicit flags of every stat set, plus the last level row's float stats and BaseMultiplier."""
    g = ge.row(row)
    out, imp = {}, set()
    for k in [g['StatSet']] + (g['AdditionalStatSets'] or []):
        if k is None:
            continue
        r = ss.row(k)
        for s, v in zip(r['ConstantStats'], r['ConstantStatsValues']):
            out[st.id(s)] = v
        imp |= {st.id(s) for s in r['ImplicitStats']}
        if k in SSL_LAST:
            p = ssl.row(SSL_LAST[k])
            for s, v in zip(p['FloatStats'] or [], p['FloatStatsValues'] or []):
                if 'maximum' in st.id(s) and 'damage' in st.id(s):
                    out['float:' + st.id(s)] = round(v, 2)
            if p['BaseMultiplier'] not in (None, 0, 10000):
                out['base_multiplier'] = p['BaseMultiplier']
    return out, imp


def skill(row):
    g = ge.row(row)
    kind = act.id(g['ActiveSkill']) or ''
    sid = g['SkillID']
    stats, imp = stats_of(row)
    allst = set(stats) | imp
    # a melee skill with no animation of its own plays the action Melee (Animation row 1), which hit-times times
    a = g['Animation'] if g['Animation'] is not None else (MELEE_ROW if kind == 'melee' else None)
    info = {'id': ge.id(row), 'kind': kind, 'anim': a, 'shape': '', 'len': 0, 'width': 0, 'angle': 0, 'range': 0,
            'delay': 0, 'count': 1, 'spread': 0, 'radius': 0, 'buffs': set(), 'child': None, 'channel': False,
            'nodmg': 'base_deal_no_damage' in allst,
            'roll': 'base_skill_can_be_avoided_by_dodge_roll' in allst,
            'unav': 'base_skill_cannot_be_avoided_by_dodge_roll_or_evaded_or_blocked' in allst,
            'big': ','.join('%s=%s' % (k, v) for k, v in stats.items() if BIG.search(k))[:160],
            'dur': ','.join('%s=%s' % (k, v) for k, v in stats.items() if DUR.search(k) and v)[:120], 'summon': ''}
    if kind.startswith('geometry_trigger') and sid in GT_BY:
        r = GT_BY[sid]
        info.update(shape='trigger', count=gt.i32(r, 0x24), delay=gt.i32(r, 0x28), spread=gt.i32(r, 0x34), radius=gt.i32(r, 0x38))
        k = gt.key(r, 4)
        if k is not None:
            info['child'] = k
    elif kind.startswith('geometry_') and not kind.startswith('geometry_projectiles') and sid in GA_BY:
        r = GA_BY[sid]
        info.update(shape=SHAPES.get(ga.i32(r, 0x28), '?'), len=ga.i32(r, 0x30), width=ga.i32(r, 0x34), angle=ga.i32(r, 0x71),
                    range=ga.i32(r, 0x24), delay=ga.i32(r, 0x7A))
        b = ga.key(r, 0x7F)
        if b is not None and b < bd.n:
            info['buffs'].add(bd.id(b))
        k = ga.key(r, 0x122)
        if k is not None and k < gft.n:
            for c in ('BuffDefinition1', 'BuffDefinition2'):
                v = gft.val(k, gft.byname[c])
                if v is not None:
                    info['buffs'].add(bd.id(v))
        info['channel'] = ga.key(r, 0xC8) is not None
    var = stats.get('ground_fire_art_variation')
    if var is not None:
        for r in GFX_BY_VAR.get(var, []):
            b = gfx.val(r, gfx.byname['BuffVisual2'])
            if b is not None:
                info['buffs'].add(bd.id(b))
    am = stats.get('alternate_minion')
    if am is not None:
        for r in range(ssm.n):
            if ssm.i32(r, 0) == am and ssm.row(r)['MonsterVarietiesKey'] is not None:
                info['summon'] = mv.id(ssm.row(r)['MonsterVarietiesKey'])
    return info


def skills_of(paths):
    """Every skill of these monster variants, plus what their triggers fire (followed to the end)."""
    rows = []
    for p in paths:
        r = MV_ID.get(p)
        if r is None:
            continue
        for k in mv.row(r)['GrantedEffects']:
            if k is not None and k not in rows:
                rows.append(k)
    out, seen, parent = [], set(), {}
    todo = list(rows)
    while todo:
        k = todo.pop(0)
        if k in seen:
            continue
        seen.add(k)
        x = skill(k)
        x['parent'] = parent.get(k)
        out.append(x)
        if x['child'] is not None and x['child'] not in seen:
            parent[x['child']] = x['id']
            todo.append(x['child'])
    return out


# ---------- fights from the census cards ----------
def card(fn):
    t = open(os.path.join(CARDS, fn), encoding='utf-8').read()
    f = {'card': fn, 'name': t.split('\n', 1)[0][2:], 'content': '', 'monsters': [], 'adds': [], 'daemons': [], 'rooms': {}}
    m = re.search(r'^content: (.*)$', t, re.M)
    f['content'] = m.group(1) if m else ''
    m = re.search(r'^monsters: (.*)$', t, re.M)
    f['monsters'] = [x.strip() for x in m.group(1).split(',')] if m else []
    for m in re.finditer(r'^- (\S+) "([^"]*)" bossbar=(\S+) skills=(\S+)', t, re.M):
        f['adds'].append((m.group(1), m.group(2), m.group(3) in ('True', '1'), m.group(4)))
    m = re.search(r'^daemons: (.*)$', t, re.M)
    f['daemons'] = [x.strip() for x in m.group(1).split(',')] if m else []
    room = None
    for line in t.split('\n'):
        if line.startswith('room '):
            room = line[5:].strip()
            f['rooms'][room] = []
        elif room and line.startswith('  sector '):
            m = re.match(r"\s+sector \['([^']*)', (\d+), (\d+), (\d+), (\d+)", line)
            if m:
                f['rooms'][room].append((m.group(1), int(m.group(2)), int(m.group(3)), int(m.group(4)), int(m.group(5))))
        elif not line.startswith('  '):
            room = None
    return f


def room_tiles(fn):
    p = os.path.join(ARMS, fn)
    if not os.path.exists(p):
        return None
    for line in open(p, encoding='utf-8-sig', errors='replace'):
        m = re.match(r'^k (\d+) (\d+) ', line)
        if m:
            return int(m.group(1)), int(m.group(2))
    return None


ARENA_SECTOR = re.compile(r'boss|arena|hide_teleports|no_portals|disable_instant', re.I)
MIN_SIDE = 60  # cells: smaller boss/arena sectors are lifts, spawn pads or doorways (the Arbiter's lift_arena is 52 x 58)


def arena_half(f):
    """Half the smaller side (cells) of the arena: the largest boss/arena/no-teleport sector of at least MIN_SIDE a side
    (they cover the fight: Doryani's boss_env and hide_teleports are both 162 cells, his 24 lights stand 81-84 from the
    centre), else the room's tiles; a campaign (gallows) room before a map's."""
    best = None
    for rm, secs in f['rooms'].items():
        boxes = [(s[3] - s[1], s[4] - s[2]) for s in secs if ARENA_SECTOR.search(s[0]) and min(s[3] - s[1], s[4] - s[2]) >= MIN_SIDE]
        if boxes:
            w, h = max(boxes, key=lambda b: b[0] * b[1])
            half, src = min(w, h) / 2, 'sector'
        else:
            tl = room_tiles(rm)
            if not tl:
                continue
            half, src = min(tl) * TILE / 2, 'room'
        pref = (0 if 'gallows' in rm and 'bossrush' not in rm else 1, 0 if src == 'sector' else 1)
        if best is None or pref < best[0]:
            best = (pref, half, rm, src)
    return (best[1], best[2], best[3]) if best else (None, '', '')


def reach_cells(x):
    if x['shape'] == 'trigger':
        return max(x['spread'] + x['radius'], x['radius'])
    return x['len']


def words(name):
    """A word list of DodgeThreats.cs, read from the plugin's source so this stays its rule."""
    t = open(os.path.join(REPO, 'Poe2Accessibility', 'Services', 'Combat', 'DodgeThreats.cs'), encoding='utf-8').read()
    m = re.search(r'string\[\] %s\s*=\s*\{(.*?)\}' % name, t, re.S)
    return re.findall(r'"([^"]+)"', m.group(1))


CALM, MELEE_W, RANGED_W = words('Calm'), words('MeleeWords'), words('RangedWords')


def threat_by_name(a):
    """DodgeThreats.Of: the action's name says calm, ranged or melee; None when it says no harm."""
    n = anim.id(a) if a is not None and a > 0 else None
    if not n or any(w in n for w in CALM):
        return None
    return 'ranged' if any(w in n for w in RANGED_W) else 'melee' if any(w in n for w in MELEE_W) else None


def timing(paths, a):
    """What the dodge cue does for this monster + action (BossWatch.WatchAttack): ('timed', ms), ('wind-up', kind) when
    no blow time is known, ('silent', why) when neither the hit-times line nor the action's name gives a harmful kind."""
    best = None
    for p in paths:
        h = HIT.get((p, a))
        if h is None:
            continue
        kinds = h[1].split(',')
        kind = 'calm' if 'calm' in kinds else 'melee' if 'melee' in kinds else 'ranged' if 'ranged' in kinds else threat_by_name(a)
        if kind in (None, 'calm'):
            best = best or ('silent', 'calm line' if kind == 'calm' else 'line, harmless name ' + (anim.id(a) or ''))
            continue
        if h[0] != '-':
            return ('timed', h[0].split('/')[0])
        best = ('wind-up', kind)
    if best:
        return best
    k = threat_by_name(a)
    return ('wind-up', k + ' by name') if k else ('silent', 'no line, harmless name ' + (anim.id(a) or '?'))


fights = [card(fn) for fn in sorted(os.listdir(CARDS)) if fn.endswith('.txt')]
cols = ['fight', 'content', 'caster_role', 'caster', 'skill', 'kind', 'parent', 'anim', 'dodge_cue', 'blow_ms', 'word', 'blast_cells',
        'marker_ao', 'hazard_buff', 'shape', 'size_cells', 'width', 'angle', 'count', 'delay_ms', 'arena_half_cells', 'size_vs_arena',
        'dodge_roll', 'unavoidable', 'channel', 'big_damage', 'durations', 'buffs', 'gap', 'classes', 'score', 'arena_room']
rows = []
for f in fights:
    half, room, src = arena_half(f)
    bosses = f['monsters']
    helpers = set(f['daemons'])
    parts = set()
    for b in bosses:
        helpers |= DAEMON.get(b, set())
        parts |= PART.get(b, set())
    casters = [('boss', bosses, f['name'])]
    for a, nm, bar, sk in f['adds']:
        if sk == '-' and a not in helpers:
            continue
        if '/NPC/' in a and a not in helpers:
            continue  # an ally sharing the boss's skill family (Sekhema Asala's beam barrage hits the boss, not the player)
        role = 'daemon' if a in helpers else 'part' if a in parts else 'form' if bar else 'add'
        casters.append((role, [a], nm))
    for d in sorted(helpers - {a for a, *_ in f['adds']}):
        casters.append(('daemon', [d], ''))
    for role, paths, nm in casters:
        sks = skills_of(paths)
        byid = {x['id']: x for x in sks}
        animated = [x for x in sks if x['anim'] is not None]
        for x in sks:
            if x['nodmg'] or x['kind'] in ('move_daemon', 'spawn_object', 'summon_monster', 'summon_specific_monsters') or x['summon']:
                continue
            # the wind-up the plugin sees: the skill's own animation, else a trigger parent's, else an animated skill whose stem it extends
            par = None
            if x['anim'] is None:
                p = x['parent']
                while p and byid.get(p) and byid[p]['anim'] is None:
                    p = byid[p]['parent']
                if p and byid.get(p):
                    par = byid[p]
                elif stem(x['id']):
                    cands = [y for y in animated if stem(y['id']) and stem(x['id']).startswith(stem(y['id'])) and y['id'] != x['id']]
                    par = max(cands, key=lambda y: len(stem(y['id']))) if cands else None
            src_sk = x if x['anim'] is not None else par
            a = src_sk['anim'] if src_sk else None
            tm = timing(paths, a) if a is not None else (('clip?', '') if x['anim'] is None else ('none', ''))
            area = own_area(x['id'])
            word = 'own' if (area and x['anim'] is not None) else ''
            if not word and par is not None and own_area(par['id']):
                word = 'parent-own'
            br = blast_radius(src_sk['id']) if src_sk else None
            if not word and br and x['shape'] == 'circle' and tm[0] == 'timed' and int(tm[1]) >= BLAST_LEAD_MS:
                word = 'blast'
            vis = (AREA.get(x['id']) or ('', '', ''))[2]
            marker = ','.join(sorted({TEFX[v][0] for v in vis.split(',') if v in TEFX})) if vis else ''
            # an effect-driven skill's area is the EffectDrivenEvent block of its .ao (telegraph-effects), which the plugin does not read
            efx = [sh for v in vis.split(',') if v in TEFX for sh in TEFX[v][1].split(',') if sh]
            if efx and not x['shape']:
                x['shape'] = 'ao:' + efx[0].split(':')[0]
                x['len'] = max(int(float(sh.split(':')[2] or 0)) for sh in efx)
                x['delay'] = max(int(float(sh.split(':')[1] or 0)) for sh in efx)
                marker = (marker + ',ao-shapes').strip(',')
            hz = ','.join(sorted(b for b in x['buffs'] if hazard(b)))
            size = reach_cells(x)
            ratio = round(size / half, 2) if half and size else ''
            beam = bool(re.search(r'beam|laser|lazer|ray\b', x['id'], re.I)) or x['kind'] in ('trigger_beam', 'single_ground_laser') or x['channel']
            proj = x['kind'].startswith(('monster_projectile', 'geometry_projectiles', 'monster_mortar', 'geometry_mortar'))
            watched = role in ('boss', 'form', 'part')
            # gap reasons, each tagged with its generic class (the report's list)
            why, cls = [], []

            def gap(c, text):
                why.append(text)
                cls.append(c)
            if role == 'daemon':
                gap('HELPER', 'helper: warned by distance, its blow not timed')
            elif role == 'add':
                gap('ADD', 'add: watched only if the server makes it rare/unique')
            if tm[0] == 'clip?':
                gap('EVENT', 'fired by an event of a clip not known from the data: timed only by that clip')
            elif tm[0] == 'silent' and size:
                gap('SILENT', 'dodge cue silent: ' + tm[1])
            elif tm[0] == 'wind-up':
                gap('UNTIMED', 'no blow time: cue at the wind-up (%s)' % tm[1])
            if not word and size and tm[0] != 'silent':
                gap('CHILD' if x['anim'] is None else 'NOAREA',
                    "child area never read (only the action skill's)" if x['anim'] is None else 'no counterplay area')
            elif word == 'parent-own' and par is not None and size > reach_cells(par) * 1.25:
                gap('CHILD', 'the action draws %s cells, this %s' % (reach_cells(par), size))
            if x['shape'] == 'trigger' and x['count'] * max(x['delay'], 1) >= 1000:
                gap('SEQUENCE', 'sequence %dx%dms' % (x['count'], x['delay']))
            if beam:
                gap('BEAM', 'beam/laser')
            if proj:
                gap('PROJECTILE', 'projectile/mortar lands later')
            if hz:
                gap('GROUND', 'lasting ground: alarm only once inside')
            if marker and 'ao-shapes' in marker:
                gap('AOSHAPE', 'effect .ao shapes in telegraph-effects (unread)')
            if tm[0] == 'timed' and int(tm[1] or 0) >= BLAST_LEAD_MS and word != 'blast':
                gap('LONG', 'long wind-up %sms, no countdown' % tm[1])
            score = 0
            if ratio != '':
                score += min(ratio, 1.5) * 4
            score += 3 if role == 'daemon' else 2 if role == 'add' else 0
            score += 3 if tm[0] == 'silent' else 1 if tm[0] in ('wind-up', 'clip?') else 0
            score += 1.5 if (not word and size) else 0
            score += 2 if x['unav'] else 0 if x['roll'] else 1
            score += 1.5 if heavy(x['big']) else 0
            score += 1.5 if beam else 0
            score += 1 if proj else 0
            score += 1 if 'SEQUENCE' in cls else 0
            score += 1 if 'LONG' in cls else 0
            covered = not cls or (set(cls) <= {'GROUND'} and word)
            rows.append([f['name'], f['content'], role, paths[0], x['id'], x['kind'], par['id'] if par else '', a if a is not None else '',
                         tm[0], tm[1] if tm[0] == 'timed' else '', word, br or '', marker, hz, x['shape'], size, x['width'], x['angle'],
                         x['count'], x['delay'], half or '', ratio, int(x['roll']), int(x['unav']), int(x['channel']), x['big'], x['dur'],
                         ','.join(sorted(x['buffs'])), '' if covered else '; '.join(why), '' if covered else ','.join(dict.fromkeys(cls)),
                         round(score, 1), room])

with open(os.path.join(HERE, 'attacks.tsv'), 'w', encoding='utf-8') as fo:
    fo.write('\t'.join(cols) + '\n')
    for r in rows:
        fo.write('\t'.join(str(c) for c in r) + '\n')
gi = cols.index('gap')
gaps = sorted((r for r in rows if r[gi]), key=lambda r: -r[cols.index('score')])
with open(os.path.join(HERE, 'gaps.tsv'), 'w', encoding='utf-8') as fo:
    fo.write('\t'.join(cols) + '\n')
    for r in gaps:
        fo.write('\t'.join(str(c) for c in r) + '\n')
print('fights', len(fights), 'rows', len(rows), 'gaps', len(gaps))
ci = cols.index('classes')
TAB, NL = chr(9), chr(10)
per = collections.defaultdict(lambda: [set(), set()])
for r in gaps:
    for c in r[ci].split(','):
        per[c][0].add(r[0])
        per[c][1].add(r[4])
with open(os.path.join(HERE, 'classes.tsv'), 'w', encoding='utf-8') as fo:
    fo.write('class' + TAB + 'fights' + TAB + 'skills' + NL)
    for c, (fs, ks) in sorted(per.items(), key=lambda kv: -len(kv[1][0])):
        fo.write(TAB.join((c, str(len(fs)), str(len(ks)))) + NL)
        print(c, len(fs), len(ks))
