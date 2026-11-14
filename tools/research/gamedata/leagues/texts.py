# texts.py strings <regex> [-v]   ClientStrings and ClientStrings2 rows whose Id (or, with -v, English text) matches: Id | en || es
# texts.py buffs <regex>           BuffDefinitions rows whose Id matches: row Id | name en | name es | count? | description
import re
import sys

from h import Dat


def strings(pat, by_text):
    for t in ('clientstrings', 'clientstrings2'):
        en, es = Dat(t), Dat('spanish_' + t)
        for r in range(en.n):
            key, text = en.str(r, 0) or '', en.str(r, 8) or ''
            if pat.search(text if by_text else key):
                sp = (es.str(r, 8) or '') if r < es.n else ''
                print('%s | %s || %s' % (key, text.replace('\n', ' / ')[:140], sp.replace('\n', ' / ')[:140]))


def buffs(pat):
    en, es = Dat('buffdefinitions'), Dat('spanish_buffdefinitions')
    name, desc, count = en.byname['Name'], en.byname['Description'], en.byname['ShowCount']
    for r in range(en.n):
        key = en.str(r, 0) or ''
        if pat.search(key):
            print(r, key, '|', en.val(r, name), '|', es.val(r, name), '| count' if en.val(r, count) else '|', '|',
                  (en.val(r, desc) or '')[:120].replace('\n', ' / '))


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    kind, pat = sys.argv[1], re.compile(sys.argv[2], re.I)
    strings(pat, '-v' in sys.argv) if kind == 'strings' else buffs(pat)
