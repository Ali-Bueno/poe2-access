# Readable labels (not generic words) and where they are: py labels.py census.json <top N>
import json, re, sys
from collections import Counter, defaultdict
R = json.load(open(sys.argv[1]))
GEN = re.compile(r'^(|tile_?overlay|overlay\d*|mission\w*|entrance\d*|exit\d*|checkpoint\w*|large\w*|(map_|no_|mini)?boss\w*|end\d*|start\d*|rare\d*|forced_rare|feature\d*|special\d*|sink|source|join|connect\w*|st|mid|cnr|corner|diagonal|\d+x\d+|block|empty|blank|insert\w*|void|core|hub|encounter\w*|anchor|ambush|unique_reward|currency_reward|landmark\d*|test\w*|default|fill\w*|filler|path\w*|straight|turn|cap|dead_?end|room\d*|area\d*|piece\d*|[a-z]?\d+|.*injector.*|.*_spawn|spawn\w*|portal\w*|transition\w*|edge\w*|lower|upper|left|right|north|south|east|west|centre|center|inner|outer|top|bottom)$', re.I)
lab = Counter(); where = defaultdict(set)
for r in R:
    l = r['label'].strip()
    if GEN.match(l): continue
    lab[l] += 1
    parts = r['f'].split('_'); where[l].add('_'.join(parts[:3]))
print('readable-candidate labels', len(lab), 'rooms', sum(lab.values()))
for l, n in lab.most_common(int(sys.argv[2])): print(n, l, sorted(where[l])[:3])
