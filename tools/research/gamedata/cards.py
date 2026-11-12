"""Boss cards and mechanics.tsv for every boss-bar monster (PoE2 0.5.5f). Output: deepdig/cards/*.txt, deepdig/mechanics.tsv.
Read-only on game data already dumped under deepdig (dat/, ot/, armall/ -> arm_index.json)."""
import sys, os, re, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dat import Dat, NULL, S
import scripts as sc

OUT = os.path.join(S, 'cards')
os.makedirs(OUT, exist_ok=True)
sc.mark_generic()

mv = Dat('monstervarieties'); ge = Dat('grantedeffects'); act = Dat('activeskills'); anim = Dat('animation')
st = Dat('stats'); ss = Dat('grantedeffectstatsets'); ga = Dat('geometryattack'); gt = Dat('geometrytrigger')
eg = Dat('executegeal'); so = Dat('spawnobject'); mo = Dat('miscobjects'); md = Dat('movedaemon'); tb = Dat('triggerbeam')
ssm = Dat('summonedspecificmonsters'); gfx = Dat('groundeffects'); gft = Dat('groundeffecttypes'); bd = Dat('buffdefinitions')
bv = Dat('buffvisuals'); nta = Dat('npctextaudio'); wa = Dat('worldareas'); topo = Dat('topologies'); mb = Dat('miscbeams')

C = lambda t, n: t.byname[n]
MV_ID = {mv.id(r).lower(): r for r in range(mv.n)}
MV_SCRIPTS = {(mv.str(r, mv.byname['BaseMonsterTypeIndex'][1]) or '').lower() for r in range(mv.n)}
OT_PATH = {}
for _l in open(os.path.join(S, 'all.txt'), encoding='utf-8'):
    _l = _l.strip()
    if _l.endswith('.ot'):
        OT_PATH[_l.replace('/', '_')] = _l[:-3]
OT_FILES = sorted(OT_PATH)
GE_ID = {ge.id(r): r for r in range(ge.n)}
BUFF_ID = {bd.id(r): r for r in range(bd.n)}
GA_BY = {ga.i32(r, 0): r for r in range(ga.n)}
GT_BY = {gt.i32(r, 0): r for r in range(gt.n)}
GT_BY_GE = {}
for r in range(gt.n):
    k = gt.key(r, 4)
    if k is not None:
        GT_BY_GE.setdefault(k, []).append(r)
SO_BY = {so.i32(r, 0): r for r in range(so.n)}
MD_BY = {md.i32(r, 0): r for r in range(md.n)}
TB_BY = {tb.i32(r, 0): r for r in range(tb.n)}
EG_BY = {eg.id(r): r for r in range(eg.n)}
SSM_BY = {ssm.i32(r, 0): r for r in range(ssm.n)}
GFX_BY_VAR = collections.defaultdict(list)
for r in range(gfx.n):
    GFX_BY_VAR[gfx.i32(r, 0x10)].append(r)
LINE_ID = {nta.id(r): r for r in range(nta.n)}
BV_DESC = {bv.id(r): (bv.val(r, C(bv, 'BuffDescription')) or '') for r in range(bv.n)}

FLAGS = ('base_skill_cannot_be_avoided_by_dodge_roll_or_evaded_or_blocked', 'base_skill_can_be_avoided_by_dodge_roll',
         'cannot_be_blocked', 'base_skill_can_be_blocked', 'skill_can_be_active_blocked_from_all_directions', 'base_deal_no_damage',
         'skill_cannot_be_stunned', 'is_area_damage')
SHAPES = {1: 'circle', 2: 'cone', 3: 'rectangle', 4: 'donut', 5: 'triangle'}
HARM_STAT = re.compile(r'damage_taken_per_minute|_taken_\+%|movement_velocity_\+%|cannot_|reduced_|less_|_-%|life_regeneration_rate_per_minute_%_|degen|reservation', re.I)


def stats_of(skill_row):
    g = ge.row(skill_row)
    out, imp = {}, set()
    for k in [g['StatSet']] + g['AdditionalStatSets']:
        if k is None:
            continue
        r = ss.row(k)
        for s, v in zip(r['ConstantStats'], r['ConstantStatsValues']):
            out[st.id(s)] = v
        imp |= {st.id(s) for s in r['ImplicitStats']}
    return out, imp


def geal_text(r):
    x = eg.row(r)
    return {'id': x['Id'], 'objects': x['MetadataIDs'], 'self': (x['Script'] or '').replace('\n', ' '),
            'each': ' || '.join((x[k] or '').replace('\n', ' ') for k in ('ScriptCommand', 'unk30_93', 'unk31_9B', 'unk32_A3', 'unk33_AB') if x[k]),
            'radius': x['unk5_24']}


BUFF_CALL = re.compile(r'\b(\w*Buff\w*)\(\s*([A-Za-z_][\w]*)')


def buffs_in_text(t):
    out = set()
    for fn, a in BUFF_CALL.findall(t or ''):
        if a in BUFF_ID:
            out.add(a)
    return out


def skill_info(sr, actmap):
    """Everything a skill row leads to."""
    g = ge.row(sr)
    kind = act.id(g['ActiveSkill']) or ''
    sid = g['SkillID']
    action = anim.id(g['Animation'])
    stats, imp = stats_of(sr)
    info = {'id': ge.id(sr), 'kind': kind, 'action': action, 'anims': actmap.get(action, []) if action else [],
            'flags': sorted(f for f in FLAGS if f in imp or stats.get(f)), 'geom': None, 'geal': [], 'summons': [], 'objects': [],
            'daemons': [], 'buffs': set(), 'triggers': [], 'beams': [], 'stats': stats}
    if kind.startswith('geometry_trigger') and sid in GT_BY:
        r = GT_BY[sid]
        trig = gt.key(r, 4)
        info['geom'] = 'trigger count=%d delay=%d spread=%d radius=%d angle=%d anchor=%d' % (
            gt.i32(r, 0x24), gt.i32(r, 0x28), gt.i32(r, 0x34), gt.i32(r, 0x38), gt.i32(r, 0x3C), gt.i32(r, 0x2C))
        if trig is not None:
            info['triggers'].append(ge.id(trig))
    elif kind.startswith('geometry_') and not kind.startswith('geometry_projectiles') and sid in GA_BY:
        r = GA_BY[sid]
        sh = ga.i32(r, 0x28)
        info['geom'] = '%s length=%d width=%d angle=%d range=%d delay=%d anchor=%d' % (
            SHAPES.get(sh, 'shape%d' % sh), ga.i32(r, 0x30), ga.i32(r, 0x34), ga.i32(r, 0x71), ga.i32(r, 0x24), ga.i32(r, 0x7A), ga.i32(r, 0x2C))
        k = ga.u64(r, 0x101)
        if k != NULL and k < eg.n:
            info['geal'].append(geal_text(k))
        b = ga.key(r, 0x7F)
        if b is not None and b < bd.n:
            info['buffs'].add(bd.id(b))
        gtk = ga.key(r, 0x122)
        if gtk is not None and gtk < gft.n:
            for c in ('BuffDefinition1', 'BuffDefinition2'):
                v = gft.val(gtk, C(gft, c))
                if v is not None:
                    info['buffs'].add(bd.id(v))
        chan = ga.key(r, 0xC8)
        if chan is not None:
            info['channel'] = Dat('geometrychannel').id(chan)
    if kind.startswith('execute_geal') and str(sid) in EG_BY:
        info['geal'].append(geal_text(EG_BY[str(sid)]))
    if kind.startswith('spawn_object') and sid in SO_BY:
        for k in so.row(SO_BY[sid])['MiscObjects']:
            if k is not None:
                info['objects'].append(mo.str(k, 8))
    if kind == 'move_daemon' and sid in MD_BY:
        k = md.key(MD_BY[sid], 0x18)
        if k is not None:
            info['daemons'].append(mv.id(k))
    if kind == 'trigger_beam' and sid in TB_BY:
        x = tb.row(TB_BY[sid])
        info['beams'] += [mb.id(b) for b in (x['unk1_4'] or []) + (x['unk2_14'] or []) if isinstance(b, int) and b < mb.n]
    am = stats.get('alternate_minion')
    if am is not None and am in SSM_BY:
        x = ssm.row(SSM_BY[am])
        if x['MonsterVarietiesKey'] is not None:
            info['summons'].append(mv.id(x['MonsterVarietiesKey']))
        if x['BuffDefinition'] is not None:
            info['buffs'].add(bd.id(x['BuffDefinition']))
    var = stats.get('ground_fire_art_variation')
    if var is not None:
        for r in GFX_BY_VAR.get(var, []):
            b = gfx.val(r, C(gfx, 'BuffVisual2'))
            if b is not None:
                info['buffs'].add(bd.id(b))
            info['ground'] = (gfx.val(r, C(gfx, 'AOFile')) or [''])[0]
    for gl in info['geal']:
        info['buffs'] |= buffs_in_text(gl['self'] + ' ' + gl['each'])
    return info


def script_info(path):
    """Own script chain facts (generic bases left out) plus full-chain hide/seek handlers."""
    own = sc.chain(path, own_only=True)
    full = sc.chain(path)
    res = {'chain': [s.path for s in own], 'states': [], 'handlers': [], 'preloads': [], 'sections': set(), 'fields': [], 'lines': [],
           'hides': set(), 'untarget': set(), 'seek': set(), 'phase': set(), 'brackets': set(), 'buffs': set(), 'calls': collections.Counter(), 'text': ''}
    for s in own:
        res['states'] += s.states
        res['preloads'] += s.preloads
        res['sections'] |= set(s.sections)
        res['fields'] += s.fields
        res['text'] += s.text
        for name, b in s.handlers:
            calls = s.calls(b)
            res['handlers'].append((name, b))
            for n, a in calls:
                res['calls'][n] += 1
                if n in ('PlayTextAudio', 'QueueTextAudio'):
                    res['lines'].append((name, a.split(',')[0].strip()))
                if n == 'TriggerBracket':
                    res['brackets'].add(a)
            res['buffs'] |= buffs_in_text(b)
            mg = re.match(r'on_(?:gain|lose)_(\w+)$', name)
            if mg and mg.group(1) in BUFF_ID:
                res['buffs'].add(mg.group(1))
    for p, v in res['preloads']:
        if p == 'preload_buff' and v in BUFF_ID:
            res['buffs'].add(v)
    for s in full:
        for name, b in s.handlers:
            m = sc.STATE_H.match(name)
            if not m:
                continue
            stv = (m.group(1), int(m.group(2)))
            if 'DisableRendering(' in b:
                res['hides'].add(stv)
            if 'DisableTargetable(' in b:
                res['untarget'].add(stv)
            if 'AddEffectPack(' in b and 'fade_by_player_dist' in b.lower() and stv[1] != 0:
                res['seek'].add(stv)
            if re.search(r'SetGlobalAudioParam\(\s*\w*Phase', b):
                res['phase'].add(stv[0])
    return res


def obj_info(path):
    """Components and states of an object/monster script chain (full chain, generic bases kept for components)."""
    full = sc.chain(path)
    secs, fields, states, hand = set(), [], [], []
    for s in full:
        secs |= set(s.sections)
        fields += s.fields
        if s.path.lower() not in sc.GENERIC:
            states += s.states
            hand += [(n, b) for n, b in s.handlers]
    f = collections.defaultdict(list)
    for k, v in fields:
        f[k].append(v.strip('"'))
    ext = set(e.lower() for s in full for e in s.extends)
    return {'found': bool(full), 'sections': secs, 'fields': dict(f), 'states': states, 'handlers': hand,
            'inanimate': 'metadata/utility/inanimateobject' in ext, 'chain': [s.path for s in full]}


def comp_summary(oi):
    keep = ['Life', 'TriggerableBlockage', 'InteractionAction', 'Interactable', 'ControlZone', 'Buffs', 'Transitionable', 'ProximityTrigger',
            'Targetable', 'MinimapIcon', 'DiesAfterTime', 'SectorEvents', 'Usable', 'UsableObject', 'Chest', 'Actor', 'Stats']
    c = [k for k in keep if k in oi['sections']]
    f = oi['fields']
    extra = []
    for k in ('start_blocked', 'blocking', 'object_size', 'transition_on_damage_taken', 'radius', 'attackable', 'server_only', 'icon'):
        if k in f:
            extra.append('%s=%s' % (k, f[k][0]))
    return ' '.join(c) + (' [' + ' '.join(extra) + ']' if extra else '')


# ---------- arena rooms ----------
ARM = json.load(open(os.path.join(S, 'arm_index.json')))
ARM_BY_OBJ = collections.defaultdict(set)
for fn, v in ARM.items():
    for o in v['o']:
        ARM_BY_OBJ[o[2].lower()].add(fn)
ARM_BY_TOP = collections.defaultdict(set)
for p, fns in ARM_BY_OBJ.items():
    ARM_BY_TOP['/'.join(p.split('/')[:3])] |= fns


def area_links():
    out = collections.defaultdict(list)
    for r in range(wa.n):
        x = wa.row(r)
        dirs = set()
        for k in x['Topologies']:
            p = topo.str(k, 8) or ''
            d = p.lower().rsplit('/graphs/', 1)[0]
            dirs.add(d.replace('/', '_'))
        for k in x['Bosses_MonsterVarietiesKeys']:
            out[k].append((x['Id'], x['Name'], x['Act'], x['IsMapArea'], dirs))
    return out


AREAS = area_links()


def rooms_for(fight):
    folder = fight['folder'].lower()
    objs = {o.lower() for o in fight['objpaths']}
    dirs = set()
    for a in fight['areas']:
        dirs |= a[4]
    score = collections.Counter()
    cand = set(ARM_BY_TOP.get(folder, set()))
    for o in objs:
        cand |= ARM_BY_OBJ.get(o, set())
    for fn in ARM:
        if any(fn.startswith(d) for d in dirs):
            cand.add(fn)
    for fn in cand:
        v = ARM[fn]
        s = 0
        inarea = any(fn.startswith(d) for d in dirs)
        for o in v['o']:
            p = o[2].lower()
            if p.startswith(folder + '/') and not p.endswith('.ao'):
                s += 3
            elif p in objs:
                s += 2
        if inarea and ('boss' in fn or any(pp[2].startswith('mapboss') or pp[2] == 'boss' for pp in v['p'])):
            s += 2
        if s >= 2:
            score[fn] = s
    return [f for f, _ in score.most_common(3)]


# ---------- content group ----------
def content_of(fight):
    for a in fight['areas']:
        i, act_ = a[0], a[2]
        if i.startswith(('G1_', 'G2_', 'G3_', 'G4_')):
            return 'Act %s' % i[1]
        if re.match(r'P\d_', i):
            return 'Interlude'
        if i.startswith('Sanctum'):
            return 'Sekhemas trial'
        if 'Ultimatum' in i or 'Trial' in i:
            return 'Chaos trial'
        if i.startswith('MapUberBoss'):
            return 'Pinnacle'
        if i.startswith('Expedition'):
            return 'League: Expedition'
        if i.startswith('Delirium'):
            return 'League: Delirium'
        if i.startswith('Abyss'):
            return 'League: Abyss'
        if i.startswith('Chayula') or 'Breach' in i:
            return 'League: Breach'
        if i.startswith('BossRush'):
            continue
        if i.startswith('Map'):
            return 'Map boss'
    for rm in fight['rooms']:
        m = re.search(r'gallows_act(\d)_', rm)
        if m:
            return 'Act %s' % m.group(1)
        if 'act4_interlude' in rm:
            return 'Interlude'
    p = ' '.join(fight['ids']).lower()
    for k, v in (('sanctum', 'Sekhemas trial'), ('ultimatum', 'Chaos trial'), ('trialmaster', 'Chaos trial'), ('expedition', 'League: Expedition'),
                 ('breach', 'League: Breach'), ('ritual', 'League: Ritual'), ('delirium', 'League: Delirium'), ('abyss', 'League: Abyss'),
                 ('incursion', 'League: Incursion'), ('azmeri', 'League: Azmeri'), ('delve', 'League: Delve'), ('heist', 'League: Heist'),
                 ('legion', 'League: Legion'), ('harvest', 'League: Harvest'), ('blight', 'League: Blight'), ('hellscape', 'League: Hellscape'),
                 ('essence', 'Essence'), ('strongbox', 'Strongbox'), ('atlasboss', 'Pinnacle'), ('atlasexile', 'Pinnacle'), ('uber', 'Pinnacle'),
                 ('pinnacle', 'Pinnacle'), ('bossrush', 'Boss rush')):
        if k in p:
            return v
    if re.search(r'map', p):
        return 'Map boss'
    return 'Other/unplaced'


# ---------- fights ----------
SUFFIX = re.compile(r'(_|CampaignSTANDALONE|STANDALONE|Standalone|MAP|Map|Invasion|Spring|Endgame)$')


def norm(script):
    p = script.rsplit('/', 1)
    name = p[-1]
    prev = None
    while prev != name:
        prev = name
        name = SUFFIX.sub('', name)
    return (p[0] + '/' + name).lower()


bb = C(mv, 'BossHealthBar')
bosses = [r for r in range(mv.n) if mv.val(r, bb) == 1]
parent = {r: r for r in bosses}


def find(a):
    while parent[a] != a:
        parent[a] = parent[parent[a]]
        a = parent[a]
    return a


def union(a, b):
    a, b = find(a), find(b)
    if a != b:
        parent[max(a, b)] = min(a, b)


bykey = {}
for r in bosses:
    k = norm(mv.str(r, C(mv, 'BaseMonsterTypeIndex')[1]) or mv.id(r))
    if k in bykey:
        union(r, bykey[k])
    else:
        bykey[k] = r
# forms: a boss preloading another boss-bar monster of the same top folder
for r in bosses:
    script = mv.str(r, C(mv, 'BaseMonsterTypeIndex')[1]) or ''
    si = sc.chain(script, own_only=True)
    top = '/'.join(script.split('/')[:3]).lower()
    for s in si:
        for p, v in s.preloads:
            if p == 'preload_monster_variety':
                o = MV_ID.get(v.lower())
                if o is not None and o in parent and v.lower().startswith(top + '/'):
                    union(r, o)

groups = collections.defaultdict(list)
for r in bosses:
    groups[find(r)].append(r)

HELPER = re.compile(r'invisible|daemon|helper|marker|controller|dummy|proxy', re.I)
rows_tsv = []
fights = []


def mrow(f, cls, do, ev, sig, place, conf, cat=''):
    rows_tsv.append([f['name'], f['content'], ','.join(f['ids']), cls, do, ev, sig, place, conf, cat])


STOP = re.compile(r'\b(stop moving|stand still|remain still|stay still|do not move|don\'t move)\b', re.I)
KEEP = re.compile(r'\b(keep moving|moving breaks|moving .{0,40}\bbreak|(move|run)\b.{0,40}\bto break\b)', re.I)

FAM = re.compile(r'^[A-Z0-9]+(?=[A-Z][a-z])')


def family(sid):
    s = FAM.sub('', sid)
    w = re.findall(r'[A-Z][a-z0-9]+', s)
    return ''.join(w[:2]) if len(w) >= 2 else None


fam_owners = collections.defaultdict(set)
for r in range(mv.n):
    for k in mv.row(r)['GrantedEffects']:
        if k is not None:
            fm = family(ge.id(k))
            if fm:
                fam_owners[fm].add('/'.join(((mv.str(r, C(mv, 'BaseMonsterTypeIndex')[1]) or mv.id(r))).split('/')[:3]).lower())
FAM_MV = collections.defaultdict(set)
for r in range(mv.n):
    if mv.val(r, bb) == 1:
        continue
    for k in mv.row(r)['GrantedEffects']:
        if k is not None:
            fm = family(ge.id(k))
            if fm and '/npc/' in mv.id(r).lower():
                FAM_MV[fm].add(r)


def snake(w):
    return re.sub(r'(?<=[a-z0-9])(?=[A-Z])', '_', w).lower()


ORPHAN = []  # GA rows with a GEAL that no skill uses
_used = {g['SkillID'] for g in (ge.row(r) for r in range(ge.n))}
for r in range(ga.n):
    k = ga.u64(r, 0x101)
    if k != NULL and k < eg.n and ga.i32(r, 0) not in _used:
        sh = ga.i32(r, 0x28)
        ORPHAN.append((ga.i32(r, 0), '%s length=%d width=%d anchor=%d' % (SHAPES.get(sh, sh), ga.i32(r, 0x30), ga.i32(r, 0x34), ga.i32(r, 0x2C)), geal_text(k)))
for root, members in sorted(groups.items()):
    ids = [mv.id(r) for r in members]
    names = []
    for r in members:
        n = mv.val(r, C(mv, 'Name'))
        if n and n not in names:
            names.append(n)
    scripts_ = []
    for r in members:
        s = mv.str(r, C(mv, 'BaseMonsterTypeIndex')[1])
        if s and s not in scripts_:
            scripts_.append(s)
    folder = '/'.join(scripts_[0].split('/')[:3]) if scripts_ else ''
    f = {'name': names[0] if names else ids[0], 'names': names, 'ids': ids, 'scripts': scripts_, 'folder': folder, 'areas': [], 'rooms': [], 'objpaths': set()}
    for r in members:
        f['areas'] += AREAS.get(r, [])
    # skills of all variants
    skills = []
    for r in members:
        for k in mv.row(r)['GrantedEffects']:
            if k is not None and k not in skills:
                skills.append(k)
    actmap = {}
    for r in members:
        for a in mv.row(r)['ACTFiles']:
            for k, v in sc.act_map(a).items():
                actmap.setdefault(k, v)
    si = {'states': [], 'handlers': [], 'preloads': [], 'lines': [], 'hides': set(), 'untarget': set(), 'seek': set(), 'phase': set(),
          'brackets': set(), 'buffs': set(), 'sections': set(), 'text': '', 'chain': []}
    for s in scripts_:
        x = script_info(s)
        for k in si:
            if isinstance(si[k], set):
                si[k] |= x[k]
            elif isinstance(si[k], list):
                si[k] += [y for y in x[k] if y not in si[k]]
            else:
                si[k] += x[k]
    sk = [skill_info(k, actmap) for k in skills]
    # triggered skills (GT -> GE) followed one level
    for x in list(sk):
        for t in x['triggers']:
            if t in GE_ID and all(y['id'] != t for y in sk):
                y = skill_info(GE_ID[t], actmap)
                y['via'] = x['id']
                sk.append(y)
    fams = {family(ge.id(k)) for k in skills} - {None}
    fams = {x for x in fams if len({o for o in fam_owners[x] if not re.search(r'/(npc|invisiblefire)$', o)}) <= 4}
    participants = sorted({r for x in fams for r in FAM_MV.get(x, set())} - set(members))
    part_ids = [mv.id(r) for r in participants if (mv.val(r, C(mv, 'Name')) or '') not in ('', 'Invisible')]
    pre_mon = [v for p, v in si['preloads'] if p == 'preload_monster_variety']
    pre_obj = [v for p, v in si['preloads'] if p == 'preload_object']
    summons = sorted({m for x in sk for m in x['summons']})
    daemons = sorted({m for x in sk for m in x['daemons']})
    spawned = sorted({o for x in sk for o in x['objects']})
    geals = [(x['id'], g) for x in sk for g in x['geal']]
    adds = []
    for m in pre_mon + summons + part_ids:
        r = MV_ID.get(m.lower())
        if r is None or m in [a['id'] for a in adds] or r in members:
            continue
        x = mv.row(r)
        adds.append({'id': mv.id(r), 'name': x['Name'], 'bossbar': x['BossHealthBar'], 'skills': [ge.id(k) for k in x['GrantedEffects'] if k is not None],
                     'script': x['BaseMonsterTypeIndex'], 'row': r})
    # add skills (for daemons and adds acting on the fight)
    add_sk = {}
    for a in adds:
        am = sc.act_map((mv.row(a['row'])['ACTFiles'] or [''])[0]) if mv.row(a['row'])['ACTFiles'] else {}
        add_sk[a['id']] = [skill_info(GE_ID[s], am) for s in a['skills'] if s in GE_ID]
    for d in daemons:
        r = MV_ID.get(d.lower())
        if r is not None and d not in add_sk:
            add_sk[d] = [skill_info(k, {}) for k in mv.row(r)['GrantedEffects'] if k is not None]
    objs = {}
    for o in pre_obj + spawned + [p for _, g in geals for p in g['objects']]:
        if o not in objs and o.lower() not in MV_ID:
            objs[o] = obj_info(o)
    # every non-monster script in the boss script's own folder (its arena objects: Yama's YamaFutureClone, Viridi's seed)
    for s_ in scripts_:
        d_ = s_.rsplit('/', 1)[0].lower().replace('/', '_') + '_'
        if d_.count('_') < 3:
            continue
        for fn_ in OT_FILES:
            if fn_.startswith(d_) and fn_.endswith('.ot'):
                pth = OT_PATH.get(fn_)
                if pth and pth.lower() not in MV_SCRIPTS and pth not in objs and len(objs) < 120:
                    objs[pth] = obj_info(pth)
    f['objpaths'] = set(objs) | set(spawned)
    f['rooms'] = rooms_for(f)
    f['content'] = content_of(f)
    room_objs = collections.OrderedDict()
    for rm in f['rooms']:
        for o in ARM[rm]['o']:
            p = o[2]
            if p.lower().endswith('.ao') or re.search(r'/(Doodad|Doodad_LeanToPlayer|Arena_Blocker|PatrolPathNode\w*)$', p) or 'Doodads/' in p:
                continue
            room_objs.setdefault((rm, p), []).append((o[0], o[1]))
    for (rm, p) in room_objs:
        if p not in objs and p.lower() not in MV_ID and (p.lower().startswith(folder.lower() + '/') or '/Objects/' in p or 'QuestObjects' in p):
            objs[p] = obj_info(p)
    # linked buffs of the fight
    fb = set(si['buffs'])
    for x in sk:
        fb |= x['buffs']
    for xs in add_sk.values():
        for x in xs:
            fb |= x['buffs']
    for a in adds:
        fb |= script_info(a['script'])['buffs'] if a['script'] else set()
    for o, oi in objs.items():
        for n, b in oi['handlers']:
            fb |= buffs_in_text(b)
    stems = ({snake(s.split('/')[-1]) for s in scripts_} | {snake(folder.split('/')[-1])}) if folder else set()
    stems = {x for x in stems if len(x) >= 6}
    named = {b for b in BUFF_ID if any(re.search(r'(^|_)%s(_|$)' % re.escape(x), b) for x in stems)} - fb
    f['named_buffs'] = named
    fb = fb | named
    f['buffs'] = fb
    words = {w.lower() for a in adds for w in re.findall(r'[A-Za-z]+', a['name'] or '') if len(w) > 3}
    f['orphans'] = [(i, gm, g) for i, gm, g in ORPHAN if any(re.search(r'AddBuff\(\s*%s_' % w, g['each'], re.I) for w in words)]
    fights.append(f)

    # ---------------- card ----------------
    L = []
    L.append('# %s' % ' / '.join(names))
    L.append('content: %s' % f['content'])
    L.append('monsters: %s' % ', '.join(ids))
    L.append('scripts: %s' % ', '.join(scripts_))
    L.append('own chain: %s' % ', '.join(si['chain']))
    L.append('areas: %s' % '; '.join('%s %s act%s' % (a[0], a[1], a[2]) for a in f['areas']))
    L.append('ai: %s (not shipped)' % ', '.join(sorted({mv.val(r, C(mv, 'AISFile')) or '' for r in members})))
    L.append('\n## skills')
    for x in sk:
        a = ','.join('%s:%s' % (s_, n_) for s_, n_ in x['anims'])
        L.append('- %s [%s] action=%s anim=%s%s' % (x['id'], x['kind'], x['action'], a or '-', (' via ' + x['via']) if x.get('via') else ''))
        if x['geom']:
            L.append('    geom: %s' % x['geom'])
        if x['flags']:
            L.append('    flags: %s' % ' '.join(x['flags']))
        for g in x['geal']:
            L.append('    GEAL %s on %s: self{%s} each{%s} r=%s' % (g['id'], g['objects'] or 'any', g['self'], g['each'], g['radius']))
        for k in ('summons', 'objects', 'daemons', 'beams', 'triggers'):
            if x[k]:
                L.append('    %s: %s' % (k, ', '.join(x[k])))
        if x['buffs']:
            L.append('    buffs: %s' % ', '.join(sorted(x['buffs'])))
        if x.get('ground'):
            L.append('    ground: %s' % x['ground'])
    L.append('\n## own states and handlers')
    L.append('states: %s' % '; '.join(si['states']))
    for n, b in si['handlers']:
        bb_ = re.sub(r'\s+', ' ', b)[:300]
        L.append('- %s = %s' % (n, bb_))
    L.append('hides(DisableRendering): %s' % sorted(si['hides']))
    L.append('untargetable(DisableTargetable): %s' % sorted(si['untarget']))
    L.append('seek: %s  phase: %s  brackets: %s' % (sorted(si['seek']), sorted(si['phase']), sorted(si['brackets'])))
    L.append('\n## preloads')
    for p, v in si['preloads']:
        L.append('- %s %s' % (p, v))
    L.append('\n## adds and forms')
    for a in adds:
        oi = obj_info(a['script']) if a['script'] else {'sections': set(), 'fields': {}, 'inanimate': False, 'states': []}
        L.append('- %s "%s" bossbar=%s skills=%s comps=%s states=%s' % (a['id'], a['name'], a['bossbar'], ','.join(a['skills']) or '-',
                                                                     comp_summary(oi) if 'chain' in oi else '-', ';'.join(oi['states'])))
    if daemons:
        L.append('daemons: %s' % ', '.join(daemons))
    L.append('\n## objects (preloaded, spawned, acted on by GEAL, placed in room)')
    for o, oi in objs.items():
        if not oi['found']:
            L.append('- %s (no script)' % o)
            continue
        hs = '; '.join('%s{%s}' % (n, re.sub(r'\s+', ' ', b)[:120]) for n, b in oi['handlers'][:6])
        L.append('- %s: %s states=%s %s' % (o, comp_summary(oi), ';'.join(oi['states']), hs))
    L.append('\n## lines')
    for h, lid in si['lines']:
        r = LINE_ID.get(lid)
        t = (nta.val(r, C(nta, 'Text')) or '').replace('\n', ' ') if r is not None else '(random group)'
        L.append('- %s -> %s: %s' % (h, lid, t[:160]))
    L.append('\n## linked buffs')
    for b in sorted(fb):
        r = BUFF_ID[b]
        x = bd.row(r)
        vis = bd.val(r, C(bd, 'BuffVisual'))
        vd = BV_DESC.get(bv.id(vis), '') if vis is not None else ''
        L.append('- %s "%s": %s | stats=%s%s' % (b, x['Name'] or '', (x['Description'] or '').replace('\n', ' ')[:200],
                                                ','.join(st.id(s) for s in x['Stats'] if s is not None), (' | visual: ' + vd.replace('\n', ' ')) if vd else ''))
    L.append('\n## arena rooms')
    for rm in f['rooms']:
        L.append('room %s' % rm)
        for s_ in ARM[rm]['s']:
            L.append('  sector %s' % s_)
        for pnt in ARM[rm]['p'][:20]:
            L.append('  point %s' % pnt)
        for (r2, p), pos in room_objs.items():
            if r2 == rm:
                L.append('  obj %s x%d %s' % (p, len(pos), ' '.join('(%g,%g)' % q for q in pos[:10])))
    fn = re.sub(r'[^A-Za-z0-9]+', '_', f['name'])[:60] + '__' + re.sub(r'[^A-Za-z0-9]+', '_', ids[0].split('/')[-1])[:40]
    f['card'] = fn + '.txt'
    open(os.path.join(OUT, f['card']), 'w', encoding='utf-8').write('\n'.join(L) + '\n')

    # ---------------- mechanics ----------------
    bossp = ids[0]
    adds_ids = {a['id'].lower() for a in adds}
    # 1 gone/back
    for (stn, v) in sorted(si['hides'] | si['untarget']):
        kinds = []
        if (stn, v) in si['hides']:
            kinds.append('DisableRendering')
        if (stn, v) in si['untarget']:
            kinds.append('DisableTargetable')
        mrow(f, 'H gone/back', 'boss leaves play; say gone, then back', 'script chain handler state %s=%d calls %s' % (stn, v, '+'.join(kinds)),
             'state %s=%d on %s' % (stn, v, bossp), '', 'PROVEN', 'hide|%s|%s=%d' % (bossp, stn, v))
    for (stn, v) in sorted(si['seek']):
        mrow(f, 'S seek', 'boss hides; lead to its entity', 'handler state %s=%d AddEffectPack fade_by_player_dist' % (stn, v), 'state %s=%d' % (stn, v), bossp, 'PROVEN', 'seek|%s|%s' % (bossp, stn))
    for p in sorted(si['phase']):
        mrow(f, 'P phase', 'next phase', 'handler of state %s sets *Phase music param' % p, 'state %s rises' % p, '', 'PROVEN', 'phase|%s|%s' % (bossp, p))
    for a in adds:
        if a['bossbar']:
            mrow(f, 'P form', 'boss changes form', 'preload_monster_variety with boss bar', 'entity %s appears' % a['id'], a['id'], 'PROVEN', 'form|%s|%s' % (bossp, a['id']))
    for r in members:
        for s2 in scripts_:
            for s in sc.chain(s2, own_only=True):
                for p_, v_ in s.preloads:
                    if p_ == 'preload_monster_variety' and v_.lower() == mv.id(r).lower():
                        mrow(f, 'P form', 'boss changes form', '%s preloads %s (boss bar)' % (s.path, mv.id(r)), 'entity %s appears' % mv.id(r), mv.id(r), 'PROVEN', 'form|%s|%s' % (bossp, mv.id(r)))
    # 9 walls / adds
    for a in adds:
        if a['bossbar']:
            continue
        oi = obj_info(a['script']) if a['script'] else None
        blocking = oi and 'true' in oi['fields'].get('blocking', [])
        if oi and not a['skills'] and oi['inanimate'] and blocking:
            mrow(f, 'W wall', 'blocking wall add: find the way out or break nearest', '%s: no skills, extends InanimateObject, blocking=true' % a['id'], 'entities %s' % a['id'], a['id'], 'PROVEN', 'wall|%s|%s' % (bossp, a['id']))
        elif a['name'] and not HELPER.search(a['name']) and not HELPER.search(a['id'].split('/')[-1]):
            nos = not a['skills'] and oi and ('Life' in oi['sections'])
            if nos:
                mrow(f, 'OB break', 'object-like add with Life and no skills: break it', '%s: no skills, Life%s' % (a['id'], ', blocking' if blocking else ''), 'entity %s' % a['id'], a['id'], 'LIKELY', 'target|%s|%s' % (bossp, a['id']))
            else:
                mrow(f, 'A add', 'adds', 'preload_monster_variety/summon %s' % a['id'], 'entity %s' % a['id'], '', 'PROVEN', 'add|%s|%s' % (bossp, a['id']))
    # 3 invulnerable / enrage
    for b in sorted(fb):
        x = bd.row(BUFF_ID[b])
        sids = {st.id(s) for s in x['Stats'] if s is not None}
        if 'base_cannot_be_damaged' in sids and 'is_hidden_monster' not in sids:
            mrow(f, 'I invulnerable', 'boss cannot be damaged now', 'buff %s (base_cannot_be_damaged) linked to fight' % b, 'buff %s' % b, '', 'PROVEN', 'buff|%s|invulnerable' % b)
        if 'enrage' in b:
            mrow(f, 'I enrage', 'boss enraged', 'buff %s' % b, 'buff %s' % b, '', 'PROVEN', 'buff|%s|enrage' % b)
    for s_ in si['states']:
        n = s_.split('=')[0].strip()
        if n.split('_')[-1] in ('enrage', 'enraged'):
            mrow(f, 'I enrage', 'boss enraged', 'state %s' % n, 'state %s on boss' % n, '', 'PROVEN', 'enrage|%s|%s' % (bossp, n))
    # 7 / 8 / stacking / grab / tether / mark / shelter from buffs
    for b in sorted(fb):
        r = BUFF_ID[b]
        x = bd.row(r)
        d = (x['Description'] or '') + ' ' + (x['Name'] or '')
        vis = bd.val(r, C(bd, 'BuffVisual'))
        vd = BV_DESC.get(bv.id(vis), '') if vis is not None else ''
        sids = [st.id(s) for s in x['Stats'] if s is not None]
        txt = d + ' ' + vd
        if STOP.search(txt):
            mrow(f, 'M stop', 'stand still', 'buff %s text "%s"' % (b, txt.strip()[:120]), 'buff %s on player' % b, '', 'PROVEN', 'move|%s|stop' % b)
        if KEEP.search(txt):
            mrow(f, 'M keep', 'keep moving', 'buff %s text "%s"' % (b, txt.strip()[:120]), 'buff %s on player' % b, '', 'PROVEN', 'move|%s|keep' % b)
        if any('damage_taken_per_minute' in s for s in sids) or 'per_minute' in ' '.join(sids) and 'damage' in ' '.join(sids):
            mrow(f, 'G hazard', 'harmful ground/aura: move out', 'buff %s stats %s' % (b, ','.join(sids)), 'buff %s on player' % b, '', 'PROVEN', 'hazard|%s' % b)
        if x['ShowCount'] and re.search(r'stack|petrif|reach|maximum|turn(s|ed)? to stone|explode', txt, re.I):
            mrow(f, 'ST stacking', 'stacking debuff: avoid reaching the threshold', 'buff %s ShowCount=1 text "%s"' % (b, txt.strip()[:140]), 'buff %s count on player' % b, '', 'LIKELY', 'stack|%s' % b)
        if re.search(r'tether|chain|leash|link|binding|bound to', b + ' ' + txt, re.I):
            mrow(f, 'TE tether', 'tethered: move to break or stay close per text', 'buff %s text "%s"' % (b, txt.strip()[:140]), 'buff %s on player' % b, '', 'LIKELY', 'tether|%s' % b)
        if re.search(r'grab|\bprison\b|imprison(?!er)|cocoon|entomb|encase|trapped|restrain|\bheld\b|petrif|frozen solid|webbed', b.replace('_', ' ') + ' ' + txt, re.I):
            mrow(f, 'GR grab/prison', 'held: break out', 'buff %s text "%s"' % (b, txt.strip()[:140]), 'buff %s on player' % b, '', 'LIKELY', 'held|%s' % b)
        if re.search(r'\bmark(ed)?\b|target(ed)? by|follow', b.replace('_', ' ') + ' ' + txt, re.I):
            mrow(f, 'MK mark', 'marked: the attack follows you', 'buff %s text "%s"' % (b, txt.strip()[:140]), 'buff %s on player' % b, '', 'UNSURE', 'mark|%s' % b)
        if re.search(r'\b(sanctuar\w*|shelter\w*|shield(ed)?|protected|safe)\b', b.replace('_', ' ') + ' ' + txt, re.I) and not re.search(r'\byou (are|take|have|deal|cannot)\b', txt, re.I):
            mrow(f, 'SH shelter?', 'protective buff tied to the fight (check meaning)', 'buff %s text "%s" stats %s' % (b, txt.strip()[:120], ','.join(sids)), 'buff %s on player' % b, '', 'UNSURE', 'shelter|%s' % b)
        if re.search(r'\b(you are within|while (you are )?(standing )?(in|inside|within)|gaining significant)\b', txt, re.I) and re.search(r'regenerat|less damage|immune|protect|cannot be (hit|damaged)', txt, re.I):
            mrow(f, 'SH shelter', 'stand inside the place the buff names', 'buff %s text "%s"' % (b, txt.strip()[:140]), 'buff %s on player (present = sheltered)' % b, '', 'PROVEN' if b in si['buffs'] else 'LIKELY', 'shelter|%s|%s' % (bossp, b))
        if re.search(r'deal no damage while|can only be dealt to targets inside|only .{0,30}inside .{0,20}can be damaged|cannot deal damage', txt, re.I):
            mrow(f, 'DZ damage rule', 'damage works only in/out of a named zone: lead the player (or the boss) there', 'buff %s text "%s"' % (b, txt.strip()[:140]), 'buff %s on player' % b, '', 'PROVEN' if b in si['buffs'] else 'LIKELY', 'dmgzone|%s|%s' % (bossp, b))
        if re.search(r'\b(are empowering|empowered by|protected by|while .{0,40}(live|alive|remain|stand))', txt, re.I):
            mrow(f, 'EM empowered by others', 'kill or stop the source the buff text names', 'buff %s text "%s"' % (b, txt.strip()[:140]), 'buff %s on boss' % b, '', 'PROVEN' if b in si['buffs'] else 'LIKELY', 'empowered|%s|%s' % (bossp, b))
        if b in f.get('named_buffs', ()) and not (x['Description'] or vd):
            mrow(f, 'NB named buff, no text', 'buff named after the fight with no text: meaning from its id only', 'buff %s stats %s' % (b, ','.join(sids)), 'buff %s' % b, '', 'UNSURE', '')
        if vd and re.search(r'\b(move|stand|stay|go|run|get|avoid|leave|enter|kill|destroy|break)\b', vd, re.I):
            mrow(f, 'BT buff text', 'second buff text gives an instruction', 'BuffVisuals %s BuffDescription "%s"' % (bv.id(vis), vd.strip()[:160]), 'buff %s' % b, '', 'PROVEN', 'bufftext|%s|%s' % (b, vd.strip()[:80]))
    # GEALs: shelter / boss acts on arena object / adds empower boss
    byg = collections.OrderedDict()
    for sid_, g in geals:
        byg.setdefault(g['id'], [[], g])[0].append(sid_)
    for a in adds:
        for x in add_sk.get(a['id'], []):
            for g in x['geal']:
                byg.setdefault(g['id'], [[], g])[0].append('%s(%s)' % (x['id'], a['id'].split('/')[-1]))
    for i, gm, g in f['orphans']:
        byg.setdefault(g['id'], [[], g])[0].append('GA%d[no skill: %s]' % (i, gm))
    for gid, (sids, g) in byg.items():
        sid_ = ','.join(sids)
        each = g['each']
        tobj = [o for o in g['objects'] if o.lower() not in MV_ID]
        tmon = [o for o in g['objects'] if o.lower() in MV_ID]
        if not g['objects'] and re.search(r'AddBuff\(\s*(\w+)', each):
            bname = re.search(r'AddBuff\(\s*(\w+)', each).group(1)
            harmful = bname in BUFF_ID and any(HARM_STAT.search(st.id(s) or '') for s in bd.row(BUFF_ID[bname])['Stats'] if s is not None)
            bdesc = (bd.row(BUFF_ID[bname])['Description'] or '') if bname in BUFF_ID else ''
            if re.search(r'shield|shelter|sanctu|protect|safe', bname + ' ' + bdesc, re.I) and not harmful:
                mrow(f, 'SH shelter', 'stand inside the area that grants %s' % bname, 'skill %s GEAL %s each{%s}' % (sid_, g['id'], each[:120]), 'buff %s on player' % bname, '', 'PROVEN' if 'GA' not in sid_ else 'LIKELY', 'shelter|%s|%s' % (bossp, bname))
            else:
                mrow(f, 'GZ buff zone', 'area that puts %s on whoever stands in it: %s' % (bname, bdesc[:80]), 'skill %s GEAL %s each{%s}' % (sid_, g['id'], each[:120]), 'buff %s on player' % bname, '', 'PROVEN', 'zone|%s|%s' % (bossp, bname))
        if tobj:
            mrow(f, 'BR boss acts on object', 'boss skill changes an arena object (lure it there / expect it)', 'skill %s GEAL %s on %s: %s' % (sid_, g['id'], ';'.join(tobj), each[:140]),
                 'skill %s; object state change' % sid_, ';'.join(tobj), 'PROVEN', 'objhit|%s|%s|%s' % (bossp, sid_, ';'.join(tobj)))
        if tmon:
            boss_ref = re.search(r'\b(this\.|snake_boss|globals\.)|\bboss\.|_boss\.', g['self'] + ' ' + each)
            mine = [m for m in tmon if m.lower() in adds_ids or m.lower() in {i.lower() for i in ids}]
            if any(m.lower() in {i.lower() for i in ids} for m in tmon):
                mrow(f, 'PH phase-break', 'an outside skill changes the boss state (ends its phase)', 'skill %s GEAL %s on %s: %s' % (sid_, g['id'], ';'.join(tmon), each[:140]),
                     'state change on boss', ';'.join(tmon), 'PROVEN', 'bosshit|%s|%s' % (bossp, sid_))
            elif boss_ref:
                mrow(f, 'EM adds empower boss', 'kill these adds before the boss consumes them', 'skill %s GEAL %s on %s: self{%s} each{%s}' % (sid_, g['id'], ';'.join(tmon), g['self'][:80], each[:120]),
                     'entities %s; boss state change' % ';'.join(tmon), ';'.join(tmon), 'PROVEN', 'consume|%s|%s' % (bossp, ';'.join(tmon)))
    # cover: spawned blocking object + big area skill
    big = [x for x in sk if x['geom'] and re.search(r'length=(\d+)', x['geom']) and int(re.search(r'length=(\d+)', x['geom']).group(1)) >= 60]
    for x in sk:
        for o in x['objects']:
            oi = objs.get(o) or obj_info(o)
            fl = oi['fields']
            if 'true' in fl.get('start_blocked', []) or 'true' in fl.get('blocking', []):
                mrow(f, 'CV cover', 'blocking object spawned by the boss: stand behind it (from the boss) during the big attack',
                     'skill %s spawns %s (%s); big skills: %s' % (x['id'], o, comp_summary(oi), ','.join(y['id'] for y in big) or 'none'),
                     'entity %s appears' % o, o, 'LIKELY' if big else 'UNSURE', 'cover|%s|%s' % (bossp, o))
    # objects: break / use / arena
    for o, oi in objs.items():
        if not oi['found']:
            continue
        secs = oi['sections']
        fl = oi['fields']
        hit = 'Life' in secs or '1' in fl.get('transition_on_damage_taken', []) or 'true' in fl.get('attackable', [])
        if re.search(r'Transition|Portal|LootProxy|Waypoint|Checkpoint|Stash|/NPC/|Exit|Entrance|Elevator|Lift|Chest|Glyph|KillCounter|Converter|Pedestal|Crafting|Telepad|Summon(Ally|Alva)', o):
            continue
        if 'InteractionAction' in secs or 'Interactable' in secs or 'Usable' in secs:
            mrow(f, 'US use', 'object to use', '%s: %s' % (o, comp_summary(oi)), 'entity %s; its states %s' % (o, ';'.join(oi['states'])), o, 'LIKELY', 'use|%s|%s' % (bossp, o))
        elif hit and not ('true' in fl.get('server_only', []) and 'client' not in secs):
            mrow(f, 'OB break', 'object to hit/break', '%s: %s states=%s' % (o, comp_summary(oi), ';'.join(oi['states'])), 'entity %s; states %s' % (o, ';'.join(oi['states'])), o, 'LIKELY', 'target|%s|%s' % (bossp, o))
        if 'Buffs' in secs and 'ControlZone' not in secs and not hit:
            mrow(f, 'SH/area object?', 'object carrying buffs (aura area?)', '%s: %s' % (o, comp_summary(oi)), 'entity %s (if networked)' % o, o, 'UNSURE', '')
    # arena changes
    ar_words = re.compile(r'(^|_)(arena|room|burn\w*|dark\w*|flood\w*|lava\w*|collapse\w*|shrink\w*|sector\w*|fog|sandstorm|fill\w*|maze\w*|destruction|floorlights?|retreat\w*|platform\w*)(_|$)', re.I)
    seen = set()
    for n, b in si['handlers']:
        m = sc.STATE_H.match(n)
        stn = m.group(1) if m else n
        calls = re.findall(r'\b(TriggerBracket|SetEnvironment\w*|ChangeEnvironment\w*|ActivateSector\w*|EnableSector\w*|DisableSector\w*|SetSectorState\w*|Sector\w*)\(', b)
        if n in ('on_death', 'on_spawned_dead', 'on_construction_complete'):
            continue
        if calls or (m and ar_words.search(stn)):
            k = (stn, tuple(sorted(set(calls))))
            if k in seen:
                continue
            seen.add(k)
            mrow(f, 'AR arena change', 'arena changes (breaks/burns/darkens): say it, keep the player in the live part', 'handler %s calls %s' % (n, ','.join(sorted(set(calls))) or '(state name)'),
                 'state %s on %s' % (stn, bossp), '', 'PROVEN' if calls else 'UNSURE', 'arena|%s|%s' % (bossp, stn))
    handled = {sc.STATE_H.match(n).group(1) for n, _ in si['handlers'] if sc.STATE_H.match(n)}
    for s_ in si['states']:
        n = s_.split('=')[0].strip()
        if n not in handled and ar_words.search(n):
            mrow(f, 'AR arena change', 'arena changes (state with no client handler)', 'declared state %s' % n, 'state %s on %s' % (n, bossp), '', 'UNSURE', 'arena|%s|%s' % (bossp, n))
        if re.search(r'portal|teleport', n, re.I):
            mrow(f, 'NP no-teleport', 'no blink/teleport now', 'state %s' % n, 'state %s' % n, '', 'PROVEN', 'noport|%s|%s' % (bossp, n))
        if re.search(r'clone|chosen|real|fake|illusion|decoy|mirror', n, re.I):
            mrow(f, 'CL clones', 'find the real one', 'state %s' % n, 'state %s' % n, '', 'UNSURE', 'clone|%s|%s' % (bossp, n))
        if re.search(r'progress|collect|timetrial|count(?!er)', n, re.I):
            mrow(f, 'MG minigame/progress', 'progress counter of a task', 'state %s' % n, 'state %s' % n, '', 'UNSURE', 'progress|%s|%s' % (bossp, n))
        if re.search(r'element|infuse|infusion', n, re.I):
            mrow(f, 'EL element', 'next damage element', 'state %s' % n, 'state %s value' % n, '', 'LIKELY', 'element|%s|%s' % (bossp, n))
        if re.search(r'heal(?!th)|regen', n, re.I):
            mrow(f, 'HL heal', 'boss heals', 'state %s' % n, 'state %s' % n, '', 'UNSURE', 'heal|%s|%s' % (bossp, n))
    for o, oi in objs.items():
        for s_ in oi.get('states', []):
            n = s_.split('=')[0].strip()
            if re.search(r'clone|chosen|real|fake|illusion|decoy', n, re.I):
                mrow(f, 'CL clones', 'find the one the state marks', 'object %s state %s' % (o, n), 'state %s on %s' % (n, o), o, 'UNSURE', 'clone|%s|%s' % (o, n))
            if re.search(r'progress|collect|timetrial|readytoactivate', n, re.I):
                mrow(f, 'MG minigame/progress', 'task object', 'object %s state %s' % (o, n), 'state %s on %s' % (n, o), o, 'UNSURE', 'progress|%s|%s' % (o, n))
    for rm in f['rooms']:
        for s_ in ARM[rm]['s']:
            if re.search(r'teleport|portal', s_[0], re.I):
                mrow(f, 'NP no-teleport', 'no teleport in the arena sector', 'room %s sector %s' % (rm, s_[0]), 'player inside sector (room coords %s)' % s_[1:5], '', 'PROVEN', '')
                break
    # skills: heal / clones / grab / beams / unavoidable
    for x in sk:
        i = x['id']
        if re.search(r'heal|regen', i, re.I):
            mrow(f, 'HL heal', 'boss heals', 'skill %s [%s]' % (i, x['kind']), 'skill %s action %s' % (i, x['action']), '', 'LIKELY', 'heal|%s|%s' % (bossp, i))
        if re.search(r'Grab|Cocoon|Entomb|Encase|Devour|Swallow|Imprison(?!er)|Prison(?!er)', i):
            mrow(f, 'GR grab/prison', 'held by the boss', 'skill %s [%s] %s' % (i, x['kind'], x['geom'] or ''), 'skill %s action %s' % (i, x['action']), '', 'LIKELY', 'grab|%s|%s' % (bossp, i))
        if x['beams'] or x['kind'] == 'trigger_beam':
            mrow(f, 'TE beam link', 'a beam links two entities', 'skill %s beams %s' % (i, ','.join(x['beams'])), 'skill %s' % i, '', 'UNSURE', '')
        if 'base_skill_cannot_be_avoided_by_dodge_roll_or_evaded_or_blocked' in x['flags']:
            mrow(f, 'U unavoidable', 'cannot be dodged/blocked: leave the area or use the counterplay', 'skill %s flags; %s' % (i, x['geom'] or x['kind']), 'skill %s action %s anim %s' % (i, x['action'], ','.join(a for _, a in x['anims'])), '', 'PROVEN', 'unavoidable|%s|%s' % (bossp, i))
    # hidden attacks (not judged when hidden): stance animations + daemon skills
    if si['hides'] or si['untarget'] or si['seek']:
        st_anims = [(x['id'], s_, a) for x in sk for s_, a in x['anims'] if s_ != 'default' and x['geom']]
        for i, s_, a in st_anims:
            mrow(f, 'D hidden attack', 'attack possibly made while hidden/untargetable: not judged now', 'skill %s mapped in .act %s -> %s' % (i, s_, a),
                 'animation %s' % a, '', 'UNSURE', '')
    for d, xs in add_sk.items():
        if HELPER.search(d) or (MV_ID.get(d.lower()) is not None and (mv.val(MV_ID[d.lower()], C(mv, 'Name')) or '') in ('Invisible', '')):
            for x in xs:
                if x['geom']:
                    mrow(f, 'D daemon attack', 'area attack from an invisible helper (not the boss skill)', 'daemon %s skill %s %s' % (d, x['id'], x['geom']),
                         'entity %s' % d, d, 'PROVEN', 'daemon|%s|%s' % (bossp, d))

json.dump([{k: (sorted(v) if isinstance(v, set) else v) for k, v in f.items() if k in ('name', 'names', 'ids', 'content', 'card', 'rooms')} for f in fights],
          open(os.path.join(S, 'fights.json'), 'w'), indent=0)
with open(os.path.join(S, 'mechanics.tsv'), 'w', encoding='utf-8') as fo:
    fo.write('fight\tcontent\tmonsters\tclass\tdo\tevidence\tsignal\tplace\tconfidence\tcatalog\n')
    for r in rows_tsv:
        fo.write('\t'.join(re.sub(r'\s+', ' ', str(c)) for c in r) + '\n')
print('fights', len(fights), 'rows', len(rows_tsv))
print(collections.Counter(r[3] for r in rows_tsv).most_common())
