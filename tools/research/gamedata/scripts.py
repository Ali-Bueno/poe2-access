"""Parsing of .ot / .act scripts dumped under deepdig/ot (path lowercased, '/' -> '_')."""
import os, re
S = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OT = os.path.join(S, 'ot')

EXTENDS = re.compile(r'^\s*extends\s+"([^"]+)"', re.M)
STATES = re.compile(r'define_\w*state\s*=\s*"([^"]*)"')
HANDLER = re.compile(r'\b(on_[A-Za-z0-9_]+|create_state_[A-Za-z0-9_]+|create_transitioned|create_untransitioned|on_transition)\s*=')
STATE_H = re.compile(r'^(?:on_or_create_state|on_state|create_state|on_exit_state)_(\w+?)_(\d+)$')
CALL = re.compile(r'\b([A-Z][A-Za-z0-9_]*)\s*\(\s*([^()]*)')
SECTION = re.compile(r'^[ \t]*([A-Za-z][A-Za-z0-9_]*)[ \t]*\r?\n[ \t]*\{', re.M)
PRELOAD = re.compile(r'\b(preload_[a-z_]+)\s*=\s*"([^"]+)"')
FIELD = re.compile(r'\b(start_blocked|blocking|object_size|transition_on_damage_taken|radius|attackable|targetable|server_only|icon|base_cannot_be_damaged|condition|team|on_triggered|force_targetable|basic_action|action|animated_object)\s*=\s*("[^"]*"|\{[^}]*\}|[^\s;]+)')

_cache = {}
GENERIC = set()  # filled by mark_generic()


def fname(path):
    p = path.lower()
    return os.path.join(OT, p.replace('/', '_'))


def read(path, ext='.ot'):
    p = path if path.lower().endswith(ext) else path + ext
    k = p.lower()
    if k in _cache:
        return _cache[k]
    fn = fname(p)
    t = None
    if os.path.exists(fn):
        t = open(fn, encoding='utf-8', errors='replace').read().lstrip('﻿')
    _cache[k] = t
    return t


def body(t, i):
    while i < len(t) and t[i] in ' \t\r\n':
        i += 1
    if i >= len(t):
        return ''
    if t[i] == '"':
        j = t.find('"', i + 1)
        return t[i + 1:j if j > 0 else len(t)]
    if t[i] != '{':
        j = t.find('\n', i)
        return t[i:j if j > 0 else len(t)]
    d = 0
    j = i
    while j < len(t):
        c = t[j]
        if c == '{':
            d += 1
        elif c == '}':
            d -= 1
            if d == 0:
                return t[i:j + 1]
        j += 1
    return t[i:]


def strip_comments(t):
    t = re.sub(r'/\*.*?\*/', '', t, flags=re.S)
    return re.sub(r'//[^\n]*', '', t)


class Script:
    def __init__(self, path, text):
        self.path = path
        t = strip_comments(text)
        self.text = t
        self.extends = [e for e in EXTENDS.findall(t) if e != 'nothing']
        self.states = []
        for m in STATES.findall(t):
            for s in m.split(';'):
                s = s.strip()
                if s:
                    self.states.append(s)
        self.handlers = []  # (name, body)
        for m in HANDLER.finditer(t):
            self.handlers.append((m.group(1), body(t, m.end())))
        self.sections = sorted(set(SECTION.findall(t)))
        self.preloads = PRELOAD.findall(t)
        self.fields = FIELD.findall(t)
        self.client = 'client' in self.sections

    def calls(self, b):
        return [(n, a.strip()) for n, a in CALL.findall(b)]


def load(path):
    t = read(path)
    if t is None:
        return None
    return Script(path, t)


def chain(path, own_only=False):
    out = []
    seen = set()
    st = [path]
    while st:
        p = st.pop(0)
        k = p.lower()
        if k in seen:
            continue
        seen.add(k)
        s = load(p)
        if s is None:
            continue
        if own_only and k in GENERIC:
            continue
        out.append(s)
        st += s.extends
    return out


def mark_generic(threshold=25):
    """Scripts extended by many others (Monster, BossBase, InanimateObject...) are not a fight's own."""
    import collections
    cnt = collections.Counter()
    for f in os.listdir(OT):
        if not f.endswith('.ot'):
            continue
        t = open(os.path.join(OT, f), encoding='utf-8', errors='replace').read()
        for e in EXTENDS.findall(t):
            cnt[e.lower()] += 1
    for k, v in cnt.items():
        if v >= threshold:
            GENERIC.add(k)
    return cnt


ACT_LINE = re.compile(r'^\s*(\w+)\s*=\s*"([^"]+)"', re.M)
STANCE = re.compile(r'^\s*stance\s+(\w+)', re.M)


def act_map(path):
    """action -> (stance, animation) from a .act file."""
    t = read(path, '.act')
    if t is None:
        return {}
    t = strip_comments(t)
    out = {}
    stance = 'default'
    depth = 0
    stance_depth = None
    for line in t.split('\n'):
        m = STANCE.match(line)
        if m:
            stance = m.group(1)
            stance_depth = depth
        depth += line.count('{') - line.count('}')
        if stance_depth is not None and depth <= stance_depth + 0 and '}' in line and stance != 'default':
            # closing of the stance block
            if depth <= stance_depth:
                stance = 'default'
                stance_depth = None
        m = ACT_LINE.match(line)
        if m:
            out.setdefault(m.group(1), []).append((stance, m.group(2)))
    return out
