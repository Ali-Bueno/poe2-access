"""The 28 'Find the Exit' rooms (SanctumRoomTypes Gauntlet; Maze/Puzzle have no SanctumRooms rows): counts and exit markers."""
import json, os, math, itertools
from collections import defaultdict, Counter
L = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
d = json.load(open(os.path.join(L, 'misc', 'arm_gauntlet.json')))
sh = lambda p: p.rsplit('/', 1)[-1]
dist = lambda a, b: math.hypot(a['x'] - b['x'], a['y'] - b['y'])
FLOOR = {'1': 'Caverns', '2': 'Ruins', '3': 'Depths', '4': 'Abyss'}
HAZ = {'GroundSpikeSpawner': 'spike', 'LaserCrossBeamSpawner': 'laser', 'BulletHellSpawner': 'bullethell', 'ProxMine@0': 'mine',
       'QuicksandPit@0': 'quicksand', 'PressurePlate_DartLauncher@0': 'dartplate', 'ProjectilePortalSpawner': 'projportal',
       'SanctumSpawnerFloor3_TrapBeetles': 'beetles', 'TimeRewindSpawner': 'rewind'}
hdr = ['room', 'levers', 'multi_use', 'doors', 'doors_start_open', 'doors_2_levers', 'indicators', 'plates(gas/dart)', 'ambush_levers',
       'lever_other_targets', 'blocker', 'goal_cell', 'goal_to_blocker', 'telepad_to_goal', 'hazards', 'single_lever_door_dist']
rows = ['\t'.join(hdr)]
for room, v in d.items():
    o = v['objs']; inn = defaultdict(list); out = defaultdict(list)
    for a, b, lab, ln in v['links']: inn[b].append(a); out[a].append((b, lab))
    by = defaultdict(list)
    for x in o: by[sh(x['ot'])].append(x)
    lv, dr, ind, pl = by['SanctumGenericLever'], by['SanctumLogicDoor'], by['SanctumLogicIndicator'], by['SanctumGenericPressurePlate']
    mu = sum('multi_use=true' in x['vars'] for x in lv)
    start_open = sum(all('start_active=true' in o[a]['vars'] for a in inn[x['i']]) for x in dr)
    two = sum(len(set(f for a in inn[x['i']] for f in inn[a])) > 1 for x in dr)
    gas = sum(any('gas_trap=true' in s for s in x['vars']) for x in pl); dart = sum(any('dart_trap=true' in s for s in x['vars']) for x in pl)
    amb = sum(any(sh(o[b]['ot']) == 'SanctumGenericAmbushTrigger' for b, lab in out[x['i']]) for x in lv)
    other = Counter(sh(o[b]['ot']) + ':' + lab for x in lv for b, lab in out[x['i']] if sh(o[b]['ot']) not in ('SanctumLogicIndicator', 'SanctumGenericAmbushTrigger'))
    g = by['SanctumProxGoal'][0]; bl = by['SanctumAirlockBlocker']
    fn = os.path.join(L, 'arm', 'metadata_terrain_gallows_leagues_sanctum_' + room + '.arm')
    tele = [l.split(' ') for l in open(fn, encoding='utf-8') if l.rstrip().endswith('"telepad"')][0]
    tp = {'x': float(tele[0]), 'y': float(tele[1])}
    hz = Counter(HAZ[sh(x['ot'])] for x in o if sh(x['ot']) in HAZ)
    lvl = room.split('_')[1]; num = room[-2:]
    sld = round(dist(lv[0], dr[0])) if len(lv) == 1 and len(dr) == 1 else ''
    rows.append('\t'.join(map(str, ['%s_Gauntlet_%s' % (FLOOR[lvl], num), len(lv), mu, len(dr), start_open, two, len(ind), '%d(%d/%d)' % (len(pl), gas, dart), amb,
                                     ','.join('%s x%d' % kv for kv in other.items()) or '-', len(bl), '(%d,%d)' % (g['x'], g['y']),
                                     round(dist(g, bl[0])) if bl else '-', round(dist(g, tp)), ' '.join('%s%d' % kv for kv in sorted(hz.items())), sld])))
open(os.path.join(L, 'misc', 'find_the_exit_rooms.tsv'), 'w').write('\n'.join(rows) + '\n')
print('\n'.join(rows))
