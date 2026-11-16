# The matcher on key classes: py keymatch.py <tiles dump> "<folder>/*1_15_rooms_*.arm" [max mismatch share]
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dump, keycls, match
dump.cls = keycls.cls
match.run(sys.argv[1], sys.argv[2], 'B', float(sys.argv[3]) if len(sys.argv) > 3 else 0.0)
