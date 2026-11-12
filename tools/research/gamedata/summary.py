"""Summaries of mechanics.tsv: fights per class, coverage per content group, global BuffVisuals instruction texts."""
import sys, os, csv, collections, json, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dat import S, Dat
rows = [l.rstrip('\n').split('\t') for l in open(os.path.join(S, 'mechanics.tsv'), encoding='utf-8', newline='\n')][1:]
rows = [r for r in rows if len(r) >= 10]
fights = json.load(open(os.path.join(S, 'fights.json')))
cls = collections.defaultdict(set)
conf = collections.defaultdict(collections.Counter)
for r in rows:
    cls[r[3]].add(r[0])
    conf[r[3]][r[8]] += 1
mode = sys.argv[1] if len(sys.argv) > 1 else 'classes'
if mode == 'classes':
    for c, fs in sorted(cls.items(), key=lambda a: -len(a[1])):
        print('%-26s %3d fights %s | %s' % (c, len(fs), dict(conf[c]), '; '.join(sorted(fs))[:600]))
elif mode == 'content':
    code = lambda c: c.split()[0]
    by = collections.defaultdict(dict)
    for f in fights:
        by[f['content']][f['name']] = set()
    for r in rows:
        by[r[1]].setdefault(r[0], set()).add(code(r[3]))
    for g in sorted(by):
        withm = sorted((n, c) for n, c in by[g].items() if c - {'A', 'H', 'NP', 'U'})
        plain = sorted(n for n, c in by[g].items() if not (c - {'A', 'H', 'NP', 'U'}))
        print('## %s (%d fights)' % (g, len(by[g])))
        print('  with: ' + '; '.join('%s [%s]' % (n, ','.join(sorted(c))) for n, c in withm))
        print('  only adds/hide/noport/unavoidable or none: ' + '; '.join('%s [%s]' % (n, ','.join(sorted(by[g][n]))) for n in plain))
elif mode == 'bufftext':
    bv = Dat('buffvisuals')
    c = bv.byname['BuffDescription']
    out = []
    for r in range(bv.n):
        d = bv.val(r, c) or ''
        if re.search(r'\b(move|stand|stay|go|run|get|avoid|leave|enter|kill|destroy|break|escape|return|step|walk|hide|use)\b', d, re.I):
            out.append('%s\t%s' % (bv.id(r), d.replace('\n', ' ')))
    open(os.path.join(S, 'bufftext_instructions.tsv'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
    print(len(out))
    for o in out:
        print(o[:200])
