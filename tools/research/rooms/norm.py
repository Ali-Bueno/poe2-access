# Label keys (RoomLabels.Key) and their counts: py norm.py census.json coverage.json <top N>
import re, json, sys
from collections import Counter, defaultdict
ROLE = set('entrance exit end mid st cnr cc cv sink source join overlay mission rare checkpoint special injector'.split())
def key(label):
    t = re.findall(r'[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+', label.replace('-', '_'))
    t = [w.lower() for w in t]
    t = [w for w in t if not re.fullmatch(r'\d+|v\d+|lvl\d*|[a-z]', w) and w not in ROLE]
    return ' '.join(t)
if __name__ == '__main__':
    R = json.load(open(sys.argv[1])); cov = {c[0]: c for c in json.load(open(sys.argv[2]))}
    lab = Counter(); m = Counter(); ex = defaultdict(set)
    for r in R:
        k = key(r['label'].strip())
        if not k: continue
        lab[k] += 1; ex[k].add(r['label'])
        if r['f'] in cov and cov[r['f']][2] in ('tiles', 'both'): m[k] += 1
    for k, n in lab.most_common(int(sys.argv[3])): print(n, m[k], repr(k), sorted(ex[k])[:4])
