"""Trial of Chaos rooms (G3_10): per room .arm, its objective objects and their distances (cells) from where the player
arrives, the exit lever, and the escort statue's path length. Usage: rooms.py <dump folder> [room name filter]; the folder
holds the rooms dumped with `EXTS=.arm dumper <gameDir> <out> metadata/terrain/gallows/act3/3_10/rooms/`.
What it showed: docs/research/trial-of-chaos.md section 3."""
import os, sys, math, glob
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'trial-rooms'))
from armlinks import parse, short

GOALS = {'cores': 'SoulCorePickup', 'altar': 'BloodCircle', 'follow': 'StatuePathMarker', 'timer': 'SurviveSpawnerMarker',
         'idols': 'Marker_Pin_Goal_01.ao', 'clear': None}


def main(folder, pat=''):
    for f in sorted(glob.glob(os.path.join(folder, '*3_10_rooms_encounter_*.arm'))):
        name = os.path.basename(f).split('_rooms_')[1][:-4]
        if pat not in name:
            continue
        objs, links, _ = parse(f)
        kind = name.split('_')[1]
        arrive = ([o for o in objs if short(o['ot']) == 'EncounterTelepadMarker']
                  or [o for o in objs if 'LiftMovementController_Entrance' in o['ot']]
                  or [o for o in objs if short(o['ot']) == 'PlayerRespawnPosition'])[0]
        dist = lambda o: int(math.hypot(o['x'] - arrive['x'], o['y'] - arrive['y']))
        goal = GOALS[kind]
        goals = [o for o in objs if goal and (short(o['ot']) == goal or (o['ao'] or '').endswith(goal))]
        exits = [objs[b] for a, b, lab, ln in links
                 if 'UpDisabledUntilComplete' in objs[a]['ot'] and short(objs[b]['ot']) == 'LiftButton']
        line = '%s arrive=%s goals(%s)=%s exit_lever=%s levers=%d' % (
            name, short(arrive['ot']), goal, sorted(dist(o) for o in goals), [dist(o) for o in exits],
            sum(short(o['ot']) == 'LiftButton' for o in objs))
        statue = [o for o in objs if short(o['ot']) == 'EncounterStatue']
        if statue:
            nxt = {a: b for a, b, lab, ln in links if lab == 'next'}
            cur, length, legs = statue[0]['i'], 0.0, 0
            while cur in nxt and legs < 50:
                b = nxt[cur]
                length += math.hypot(objs[b]['x'] - objs[cur]['x'], objs[b]['y'] - objs[cur]['y'])
                cur, legs = b, legs + 1
            line += ' statue_at=%d path=%d (%d legs)' % (dist(statue[0]), length, legs)
        print(line)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else '')
