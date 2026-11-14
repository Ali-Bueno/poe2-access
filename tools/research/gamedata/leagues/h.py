# Shared setup for the league research scripts: points dat.py at a dump folder (env POE_DUMP) holding dat/, allot/ and
# schema_dl/schema.min.json, and gives spanish_<table> the English table's column layout.
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import dat  # noqa: E402

U = os.environ.get('POE_DUMP')
if not U:
    sys.exit('Set POE_DUMP to the dump folder (dat/, allot/, schema_dl/).')
dat.S = U
dat.DAT = os.path.join(U, 'dat')
OT = os.path.join(U, 'allot')
from dat import Dat, show  # noqa: E402,F401

_orig_init = Dat._init


def _init(self, name):
    _orig_init(self, name)
    if name.startswith('spanish_') and not self.cols:
        en = Dat(name[len('spanish_'):])
        self.cols, self.byname, self.schema_size = en.cols, en.byname, en.schema_size


Dat._init = _init
