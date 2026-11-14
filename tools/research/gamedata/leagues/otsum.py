# otsum.py <regex>... [-f]: one compact card per dumped .ot whose file name matches: extends, components (c: = client
# block), states, key fields (icon, texts, radius…) and every state/event handler on one line (effects shortened). -f keeps
# handlers whole. The .ot files are expected in $POE_DUMP/allot/ as dumped by ../dumper (path with / turned into _).
import os
import re
import sys

from h import OT

KEYS = re.compile(r'^\s*(icon|title_text|body_text|display_text|object_name|text|radius|duration|timer|max_|limit|range|'
                  r'monster_variety|buff|team|is_hidden|does_not_count|cannot_|base_)\w*\s*=.*', re.I)


def card(fn, full=False):
    t = open(os.path.join(OT, fn), encoding='utf-8').read().replace('\ufeff', '')
    out = ['== %s  extends %s' % (fn.replace('metadata_', '', 1), ','.join(re.findall(r'extends\s+"([^"]+)"', t)))]
    comps = [('c:' if m.group(1) else '') + m.group(2) for m in re.finditer(r'^(\t?)([A-Za-z_]\w*)\s*\n\s*\{', t, re.M)]
    out.append('  comps: ' + ' '.join(dict.fromkeys(comps)))
    states = []
    for m in re.finditer(r'define_(shared_|client_)?state\s*=\s*"([^"]*)"', t):
        states += [s.strip() for s in m.group(2).replace('\n', ';').split(';') if s.strip()]
    if states:
        out.append('  states: ' + ' '.join(states))
    out += ['  ' + line.strip()[:150] for line in t.splitlines() if KEYS.match(line)]
    for m in re.finditer(r'((?:on|create)_(?:or_create_)?state_\w+|on_\w+)\s*=\s*\{', t):
        i, depth = m.end(), 1
        while i < len(t) and depth:
            depth += {'{': 1, '}': -1}.get(t[i], 0)
            i += 1
        body = re.sub(r'\s+', ' ', t[m.end():i - 1]).strip()
        body = re.sub(r"'?Metadata/Effects/[^' ,)]*'?", 'E', body)
        body = re.sub(r"'?Metadata/[^' ,)]*/([^/' ,)]+)'?", r'M:\1', body)
        out.append('  %s: %s' % (m.group(1), body if full else body[:220]))
    return '\n'.join(out)


if __name__ == '__main__':
    full = '-f' in sys.argv
    pats = [a for a in sys.argv[1:] if a != '-f']
    for name in sorted(os.listdir(OT)):
        if any(re.search(p, name) for p in pats):
            print(card(name, full))
