# Path classes against key classes on a tile dump: py keyval.py <tiles dump>
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dump, keycls
from collections import Counter
T = dump.load(sys.argv[1]); c = Counter(); ex = {}
for k, v in T.items():
    a = dump.cls(v[0]); b = keycls.cls(v[0]); c[(a, b)] += 1; ex.setdefault((a, b), v[0])
for (a, b), n in sorted(c.items(), key=lambda t: -t[1]): print(repr(a), repr(b), n, ex[(a, b)].split('/')[-1])
