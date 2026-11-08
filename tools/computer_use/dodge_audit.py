"""Audit of the dodge warning against a fight trace (docs/research/dodge-timing.md, Live protocol steps 4-7). Read-only.

Swings come from fight_trace.py (a clip started or restarted; its contacts from the clip's events at the clip's rate); the
warnings from the plugin log's "Dodge warning" lines (VerboseLogging); a "Dodge merged" line (a warning another cue covered,
one cue for blows together) counts as warned but not as a cue. Prints recall (warned swings aimed at the player
within --screen units), false warnings (aimed elsewhere or far), and the timing error per timing source: against contact
minus the lead, and against the earliest a warning can come (max(contact - lead, wind-up start)). Clips without a contact
are timed by the next life drop (noisy in a pack: another monster's hit counts).

    py -B tools/computer_use/dodge_audit.py fight.jsonl [--start HH:MM:SS] [--end HH:MM:SS] [--rarity 2,3] [--detail] [--replay-merge]

--replay-merge applies the plugin's merge rule (DodgeCueMerge) to a log written before it, to compare the cues per second.
"""
import argparse, bisect, collections, datetime, json, math, re, statistics
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[2]
LOG = REPOSITORY / 'host/Poe2Access/bin/Release/net10.0-windows/win-x64/Plugins/Poe2Accessibility/logs/accessibility.log'
ACTIONS = REPOSITORY / 'Poe2Accessibility/catalog/action-types.txt'
WORLD_PER_GRID = 250 / 23
MERGE_WINDOW_MS, MERGE_RARE, MERGE_KEEP_MS = 250, 2, 2000  # Poe2Accessibility/Services/DodgeCueMerge.cs
WARN = re.compile(r'^(\S+ \S+) DEBUG Dodge (?:warning|merged): monster (\d+) (\S+)\|(\d+) animation (\S+) \((\w+), ([^,]+), gap (-?\d+), aim (\w+), reach (\S+), blow in (-?\d+|\?) ms(?:, (\d+) ms of flight)?, clip ([^)]*)\)(?: covered by monster (\d+))?')
GATE = re.compile(r'^(\S+ \S+) INFO Dodge gate: (\S+)\|(\d+) (\S+) -> ([^(]+) \(')


def action_names():
    names = {}
    for line in open(ACTIONS, encoding='utf-8'):
        line = line.strip()
        if line and not line.startswith('#'):
            h = 0
            for b in line.encode('latin1'):
                h = ((h * 0x1000193) & 0xFFFFFFFF) ^ b
            names[((h >> 16) ^ h) & 0xFFFF] = line
    return names


def wall_of(stamp):
    return int(datetime.datetime.strptime(stamp, '%Y-%m-%d %H:%M:%S.%f').timestamp() * 1000)


def rate(row):
    g = row.get('glob') or 0.0
    g = 1.0 if (g != 0 and row.get('glob_off')) else g
    r = g * (row.get('speed') or 0.0)
    return 1.0 if r <= 0.01 else r


def pct(values, q):
    if not values:
        return None
    s = sorted(values)
    return s[min(len(s) - 1, max(0, int(math.ceil(q * len(s))) - 1))]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('trace')
    ap.add_argument('--log', default=LOG)
    ap.add_argument('--lead', type=int, default=500)
    ap.add_argument('--screen', type=float, default=700, help='attacker within this many world units counts as on screen')
    ap.add_argument('--rarity', default='0,1,2,3')
    ap.add_argument('--detail', action='store_true')
    ap.add_argument('--start', help='HH:MM:SS local, inclusive')
    ap.add_argument('--end', help='HH:MM:SS local')
    ap.add_argument('--replay-merge', action='store_true', help='merge the warnings of a log written before the merge rule')
    a = ap.parse_args()
    rarities = {int(x) for x in a.rarity.split(',')}
    actions = action_names()

    def clock(hms, first):
        if not hms:
            return None
        day = datetime.datetime.fromtimestamp(first / 1000).strftime('%Y-%m-%d')
        return wall_of(day + ' ' + hms + '.000')
    rows, clipinfo, lifes = collections.defaultdict(list), {}, []
    pending = None
    first_wall = last_wall = None
    for line in open(a.trace, encoding='utf-8'):
        try:
            j = json.loads(line)
        except ValueError:
            continue
        if 'clip_info' in j:
            pending = j['clip_info']
            continue
        if 'life' in j:
            lifes.append((j['wall'], j['life'], j['at'], j['player']))  # the player's id changes with the area
            continue
        if 'id' in j:
            first_wall = first_wall or j['wall']
            last_wall = j['wall']
            if pending is not None:
                clipinfo[(j['monster'].split('@')[0], j.get('clip'))] = pending['events']
                pending = None
            rows[j['id']].append(j)
    life_walls = [w for w, *_ in lifes]
    t0, t1 = clock(a.start, first_wall), clock(a.end, first_wall)
    t0, t1 = t0 or 0, t1 or 10**15

    def player_at(wall):
        i = bisect.bisect_right(life_walls, wall) - 1
        return lifes[max(0, i)][2] if lifes else [0, 0]

    def player_id_at(wall):
        i = bisect.bisect_right(life_walls, wall) - 1
        return lifes[max(0, i)][3] if lifes else None

    def hit_after(wall, within=450):
        """First life drop in (wall-50, wall+within]."""
        i = bisect.bisect_right(life_walls, wall - 50)
        prev = lifes[i - 1][1] if i > 0 else None
        while i < len(lifes) and lifes[i][0] <= wall + within:
            if prev is not None and lifes[i][1] < prev - 1:
                return lifes[i][0]
            prev = lifes[i][1]
            i += 1
        return None

    # Swings: consecutive rows of one monster in one clip with a rising position.
    swings = []
    for mid, rs in rows.items():
        cur = None
        for r in rs:
            if r.get('clip_index') is None or r.get('pos') is None or r.get('clip') is None:
                continue  # a restart reads no clip for one frame (the Devourer's spit_01, 2026-11-08): not a swing of its own
            if cur is None or r['clip_index'] != cur['clip_index'] or r['pos'] < cur['last_pos'] - 0.01:
                cur = dict(id=mid, monster=r['monster'].split('@')[0], clip=r.get('clip'), clip_index=r['clip_index'], anim=r.get('anim'),
                           rows=[], last_pos=r['pos'], rarity=r.get('rarity'))
                swings.append(cur)
            cur['rows'].append(r)
            cur['last_pos'] = r['pos']
    for s in swings:
        r0 = s['rows'][0]
        rt = statistics.median(rate(r) for r in s['rows'])
        s['rate'] = rt
        s['start'] = r0['wall'] - r0['pos'] / rt * 1000
        events = clipinfo.get((s['monster'], s['clip'])) or []
        s['contacts'] = [s['start'] + t / rt * 1000 for name, t in events if name == 'contact']
        acts = [r for r in s['rows'] if r.get('action')]
        s['action'] = actions.get(acts[0]['action'], acts[0]['action']) if acts else None
        s['target'] = acts[0].get('target') if acts else None
        s['place'] = acts[0].get('place') if acts else None
        s['at'] = r0['at']
        pa = player_at(r0['wall'])
        s['player'] = player_id_at(r0['wall'])
        s['dist'] = math.hypot(r0['at'][0] - pa[0], r0['at'][1] - pa[1])
        if s['place']:
            px, py = s['place'][0] * WORLD_PER_GRID, s['place'][1] * WORLD_PER_GRID
            s['place_dist'] = math.hypot(px - pa[0], py - pa[1])
        else:
            s['place_dist'] = None
        s['end_wall'] = s['rows'][-1]['wall']

    by_monster = collections.defaultdict(list)
    for s in swings:
        by_monster[s['id']].append(s)

    warns = []
    lo, hi = (first_wall or 0) - 2000, (last_wall or 0) + 2000
    gates = collections.defaultdict(set)
    for line in open(a.log, encoding='utf-8', errors='replace'):
        m = WARN.match(line)
        if m:
            w = wall_of(m.group(1))
            if lo <= w <= hi:
                warns.append(dict(wall=w, id=int(m.group(2)), monster=m.group(3).split('/')[-1], anim=int(m.group(4)), name=m.group(5),
                                  threat=m.group(6), reason=m.group(7), gap=int(m.group(8)), aim=m.group(9), reach=m.group(10),
                                  blow_in=None if m.group(11) == '?' else int(m.group(11)), flight=int(m.group(12) or 0), clip=m.group(13),
                                  merged=m.group(14) is not None))
            continue
        g = GATE.match(line)
        if g and lo <= wall_of(g.group(1)) <= hi:
            gates[(g.group(2).split('/')[-1], int(g.group(3)))].add(g.group(5).strip())

    # Match each warning to its swing and blow.
    for w in warns:
        cands = [s for s in by_monster.get(w['id'], []) if s['start'] - 150 <= w['wall'] <= s['end_wall'] + 400]
        cands.sort(key=lambda s: abs(w['wall'] - s['start']))
        w['swing'] = None
        for s in cands:
            nxt = [c for c in s['contacts'] if c >= w['wall'] - 250]
            w['swing'] = s
            # A clip of several contacts (Beira's ice_spear_combo_01: five) is warned again for a later one: the warning's
            # own estimate names which; the first contact ahead made those warnings look 450 ms late.
            # A warning at the wind-up ("blow in ? ms": no timing known) is for the swing's first contact.
            aimed_at = w['wall'] + (w['blow_in'] or 0) - w['flight']
            w['contact'] = min(nxt, key=lambda c: abs(c - aimed_at)) if nxt and (w['blow_in'] or 0) > 0 else (nxt[0] if nxt else None)
            s.setdefault('warns', []).append(w)
            break

    def ok_rarity(s):
        return (s.get('rarity') is None or s['rarity'] in rarities) and t0 <= s['start'] <= t1
    warns = [w for w in warns if t0 <= w['wall'] <= t1 and (w['swing'] is None or ok_rarity(w['swing']))]
    if a.replay_merge:
        replay_merge(warns)
    cue_rates(warns)
    covered = [w for w in warns if w['merged']]

    # Attacks at the player: a swing aimed at the player whose clip has a contact.
    threatening = {k for k, reasons in gates.items() if reasons - {'calm'}}
    threatening |= {(w['monster'].split('@')[0], w['anim']) for w in warns}
    def attack_clip(s):
        c = (s['clip'] or '').lower()
        return c and not any(w in c for w in ('idle', 'walk', 'run', 'death', 'hit_react', 'taunt', 'emerge', 'turn', 'flinch', 'stun', 'dodge'))
    at_player = [s for s in swings if ok_rarity(s) and attack_clip(s) and s['target'] == s['player'] and s['dist'] <= a.screen
                 and (s['end_wall'] - s['start']) > 150]
    attacks = [s for s in at_player if (s['monster'], s['anim']) in threatening]
    calm_hits = [s for s in at_player if (s['monster'], s['anim']) not in threatening]
    warned = [s for s in attacks if s.get('warns')]
    players = sorted({life[3] for life in lifes if t0 <= life[0] <= t1 and life[3] is not None})
    print(f'player {",".join(map(str, players))}; swings {len(swings)}; warnings {len(warns)}; attacks at the player on screen {len(attacks)} (+{len(calm_hits)} of kinds never judged threatening)')
    print(f'recall {len(warned)}/{len(attacks)} = {len(warned) / max(1, len(attacks)):.1%}')
    missed = collections.Counter((s['monster'], s['anim'], s['clip']) for s in attacks if not s.get('warns'))
    if missed:
        print('  missed:', dict(missed.most_common(12)))
    reasons_missed = collections.Counter()
    for s in attacks:
        if not s.get('warns'):
            reasons_missed[tuple(sorted(gates.get((s['monster'], s['anim']), {'?'})))] += 1
    if a.detail:
        for s_ in attacks:
            if not s_.get('warns'):
                print('   miss', datetime.datetime.fromtimestamp(s_['start'] / 1000).strftime('%H:%M:%S.%f')[:-3], s_['id'], s_['monster'], s_['clip'], 'dist', round(s_['dist']), 'rate', round(s_['rate'], 2), 'n', len(s_['rows']), 'dur', round(s_['end_wall'] - s_['start']))
    if reasons_missed:
        print('  gate reasons of missed kinds:', dict(reasons_missed.most_common(8)))
    if calm_hits:
        print('  never-threatening kinds aimed at the player:', dict(collections.Counter((s['monster'], s['anim'], s['clip'], s['action']) for s in calm_hits).most_common(8)))

    false, unmatched = [], []
    for w in warns:
        if w['merged']:
            continue  # no cue played: neither a false cue nor a timed one
        s = w['swing']
        if s is None:
            unmatched.append(w)
            continue
        aimed = s['target'] == s['player'] or (s['target'] in (0, None) and s['place_dist'] is not None and s['place_dist'] < 250)
        if not aimed or s['dist'] > a.screen + 400:
            false.append(w)
    cues = len(warns) - len(covered)
    print(f'false (aimed elsewhere / far): {len(false)}/{cues} cues = {len(false) / max(1, cues):.1%}; unmatched {len(unmatched)}; covered by another cue {len(covered)}')
    if false:
        print('  false:', dict(collections.Counter((w['monster'], w['name'], w['swing']['target'], round(w['swing']['dist'])) for w in false).most_common(10)))
    if unmatched:
        print('  unmatched:', dict(collections.Counter((w['monster'], w['name'], w['clip']) for w in unmatched).most_common(8)))

    def med(values):
        return statistics.median(values) if values else float('nan')

    # Timing: error = warning - (contact - lead), by reason.
    by_reason = collections.defaultdict(list)
    feasible = collections.defaultdict(list)
    fulllead = collections.defaultdict(list)
    est = collections.defaultdict(list)
    windup = collections.defaultdict(list)
    hitlag = []
    bylife = []
    for w in warns:
        if w['merged']:
            continue
        s, c = w['swing'], w.get('contact')
        if s is not None and c is None and (s['target'] == s['player']):
            h = hit_after(w['wall'] + 50, 1500)
            if h:
                bylife.append((w['reason'], w['wall'] - (h - 31 - a.lead), None if w['blow_in'] is None else w['wall'] + w['blow_in'] - (h - 31)))
        if s is None or c is None:
            continue
        c2 = c + w['flight']
        by_reason[w['reason']].append(w['wall'] - (c2 - a.lead))
        feasible[w['reason']].append(w['wall'] - max(c2 - a.lead, s['start']))
        if c2 - a.lead >= s['start'] + 50:
            fulllead[w['reason']].append(w['wall'] - (c2 - a.lead))
        if w['blow_in'] is not None:
            est[w['reason']].append(w['wall'] + w['blow_in'] - c2)
        windup[w['reason']].append(w['wall'] - s['start'])
        h = hit_after(c2)
        if h:
            hitlag.append(h - c2)
    for reason, errs in sorted(by_reason.items()):
        ab = [abs(e) for e in errs]
        print(f'timing {reason:15s} n={len(errs):3d} err median {statistics.median(errs):6.0f} p10 {pct(errs, .1):6.0f} p90 {pct(errs, .9):6.0f} |err| p90 {pct(ab, .9):5.0f}'
              f'  est-vs-real median {med(est[reason]):6.0f} | warn after wind-up start median {statistics.median(windup[reason]):5.0f}'
              f' | vs max(contact-lead, wind-up) median {statistics.median(feasible[reason]):5.0f} |p90| {pct([abs(x) for x in feasible[reason]], .9):5.0f}')
    for reason, errs in sorted(fulllead.items()):
        print(f'timing {reason:15s} blows at least the lead after the wind-up: n={len(errs):3d} err median {statistics.median(errs):5.0f} p10 {pct(errs, .1):5.0f} p90 {pct(errs, .9):5.0f} |err| p90 {pct([abs(e) for e in errs], .9):5.0f}')
    allerr = [abs(e) for v in by_reason.values() for e in v]
    allf = [abs(e) for v in feasible.values() for e in v]
    if allerr:
        print(f'timing all n={len(allerr)} |err| p50 {pct(allerr, .5):.0f} p90 {pct(allerr, .9):.0f}; vs feasible |err| p50 {pct(allf, .5):.0f} p90 {pct(allf, .9):.0f}')
    if bylife:
        for reason in sorted({r for r, _, _ in bylife}):
            errs = [e for r, e, _ in bylife if r == reason]
            ests = [e for r, _, e in bylife if r == reason and e is not None]
            print(f'timing by life drop (no contact event) {reason:15s} n={len(errs):3d} err median {statistics.median(errs):6.0f} p10 {pct(errs, .1):6.0f} p90 {pct(errs, .9):6.0f} |err| p90 {pct([abs(e) for e in errs], .9):5.0f} est-vs-real median {med(ests):6.0f}')
    if hitlag:
        print(f'life drop after contact: n={len(hitlag)} median {statistics.median(hitlag):.0f} p10 {pct(hitlag, .1):.0f} p90 {pct(hitlag, .9):.0f}')
    if a.detail:
        for w in warns:
            s = w['swing']
            c = w.get('contact')
            print(' ', datetime.datetime.fromtimestamp(w['wall'] / 1000).strftime('%H:%M:%S.%f')[:-3], w['monster'], w['name'], w['reason'], 'blow_in', w['blow_in'],
                  'real', None if c is None else round(c + w['flight'] - w['wall']), 'tgt', s and s['target'], 'dist', s and round(s['dist']), 'rate', s and round(s['rate'], 2), 'clip', w['clip'])


def replay_merge(warns):
    """DodgeCueMerge on warnings logged one by one: a frame's warnings (stamps within 2 ms) rarer then nearer first."""
    played = []
    ordered = sorted(warns, key=lambda w: w['wall'])
    i = 0
    while i < len(ordered):
        j = i
        while j < len(ordered) and ordered[j]['wall'] - ordered[i]['wall'] <= 2:
            j += 1
        now = ordered[i]['wall']
        played = [c for c in played if now - c['issued'] <= MERGE_KEEP_MS]
        frame = []
        for w in ordered[i:j]:
            s = w['swing']
            rank = min(3, max(0, (s.get('rarity') or 0) if s else 0))
            frame.append(dict(w=w, rank=rank, dist=s['dist'] if s else 9e9, blow=w['wall'] + w['blow_in'] if (w['blow_in'] or 0) > 0 else None, issued=w['wall']))
        for c in sorted(frame, key=lambda c: (-c['rank'], c['dist'])):
            cover = next((p for p in played if (c['rank'] < MERGE_RARE or p['rank'] >= MERGE_RARE) and (
                abs(p['blow'] - c['blow']) <= MERGE_WINDOW_MS if p['blow'] is not None and c['blow'] is not None else abs(p['issued'] - c['issued']) <= MERGE_WINDOW_MS)), None)
            if cover is None:
                played.append(c)
            c['w']['merged'] = cover is not None
        i = j


def cue_rates(warns):
    """Cues per second over the seconds that had any warning: all warnings (before merging) against the cues played."""
    def rates(walls):
        per = collections.Counter(int(w // 1000) for w in walls)
        if not per:
            return 'none'
        counts = list(per.values())
        return f'{sum(counts) / len(counts):.2f}/s over {len(counts)} s, max {max(counts)}, seconds with 3+ {sum(c >= 3 for c in counts)}'
    print(f'cues per second: warnings {rates([w["wall"] for w in warns])}; played {rates([w["wall"] for w in warns if not w["merged"]])}')


main()
