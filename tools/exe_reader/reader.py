"""Reads struct offsets out of the game's exe, as a data file: nothing is run, attached or changed.

No fixed addresses, no byte signatures: only strings the game needs for itself (component names, its GEAL
registration table), .pdata and rules about code shape. Why, how far it goes and what is unproven:
docs/research/exe-layouts.md. A development tool; the mod does not use it at run time.

Run after each game patch and keep the output (docs/research/evidence/): "want" is 0.5.5f, so a WRONG line is
either a field that moved or a rule the patch broke, and only a live check tells which.

Run: py -B -I tools/exe_reader/reader.py [path to PathOfExileSteam.exe]   (needs capstone and numpy)
"""
import sys, os, struct, time, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import PE, np
import capstone
from capstone import x86_const as X

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
# A rule a patch broke leaves its fields out: the report goes on, and those lines read None.
RULE_FAILED = (StopIteration, KeyError, IndexError, TypeError, ValueError, AttributeError)
t0 = time.time()
pe = PE(sys.argv[1]) if len(sys.argv) > 1 else PE()
img, IB = pe.img, pe.imagebase
TEXT, RDATA, DATA = pe.sec(".text"), pe.sec(".rdata"), pe.sec(".data")
T0, T1 = TEXT["va"], TEXT["va"] + TEXT["vsize"]
R0, R1 = RDATA["va"], RDATA["va"] + RDATA["vsize"]
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
md.detail = True

# ---- one pass over .text: call targets and RIP-relative lea targets ------------------------------
t = pe.np[T0:T1]
e8 = np.nonzero(t == 0xE8)[0]
e8 = e8[e8 + 5 < len(t)]
rel = (t[e8 + 1].astype(np.int64) | t[e8 + 2].astype(np.int64) << 8 | t[e8 + 3].astype(np.int64) << 16
       | t[e8 + 4].astype(np.int64) << 24)
rel = np.where(rel >= 1 << 31, rel - (1 << 32), rel)
tgt = e8 + T0 + 5 + rel
tgt = tgt[(tgt >= T0) & (tgt < T1)]
call_t, call_n = np.unique(tgt, return_counts=True)

def callers(f):
    i = int(np.searchsorted(call_t, f))
    return int(call_n[i]) if i < len(call_t) and call_t[i] == f else 0

i8d = np.nonzero(t == 0x8D)[0]
i8d = i8d[(i8d > 0) & (i8d + 6 < len(t))]
i8d = i8d[((t[i8d - 1] & 0xFB) == 0x48) & ((t[i8d + 1] & 0xC7) == 0x05)]
rel = (t[i8d + 2].astype(np.int64) | t[i8d + 3].astype(np.int64) << 8 | t[i8d + 4].astype(np.int64) << 16
       | t[i8d + 5].astype(np.int64) << 24)
rel = np.where(rel >= 1 << 31, rel - (1 << 32), rel)
lea_t = i8d + T0 + 6 + rel
order = np.argsort(lea_t, kind="stable")
lea_t, lea_s = lea_t[order], (i8d + T0 - 1)[order]

def lea_sites(a):
    lo, hi = np.searchsorted(lea_t, a), np.searchsorted(lea_t, a, side="right")
    return [int(x) for x in lea_s[lo:hi]]

F = pe.funcs()
FB = F[:, 0].astype(np.int64)
FE = F[:, 1].astype(np.int64)

def func_of(a):
    i = int(np.searchsorted(FB, a, side="right")) - 1
    return (int(FB[i]), int(FE[i])) if i >= 0 and FB[i] <= a < FE[i] else None

def body(f, limit=0x2000):
    """Instructions of the function at f. Leaf functions have no .pdata: stop at ret or int3."""
    r = func_of(f)
    if r and r[0] == f:
        return list(md.disasm(img[r[0]:r[1]], r[0]))
    out = []
    for i in md.disasm(img[f:f + limit], f):
        if i.mnemonic == "int3":
            break
        out.append(i)
        if i.mnemonic == "ret":
            break
    return out

def ptr(a):
    return struct.unpack_from("<Q", img, a)[0] - IB

def mem(i):
    for o in i.operands:
        if o.type == X.X86_OP_MEM:
            return o.mem
    return None

def rip_target(i):
    m = mem(i)
    return i.address + i.size + m.disp if m and m.base == X.X86_REG_RIP else None

def direct_target(i):
    if i.operands and i.operands[0].type == X.X86_OP_IMM:
        return i.operands[0].imm
    return None

def is_code(a):
    return T0 <= a < T1

def find_string(s, wide=False):
    needle = (s.encode("utf-16le") + b"\0\0") if wide else (b"\0" + s.encode() + b"\0")
    p = img.find(needle, R0, R1)
    if p < 0:
        return None
    return p if wide else p + 1

# ---- anchors -------------------------------------------------------------------------------------
def component_getter(name):
    """A component's GetComponent<T>: holds the name string, is short, and has the most callers."""
    best = (0, None)
    a = find_string(name)
    for s in lea_sites(a):
        r = func_of(s)
        if r and r[1] - r[0] <= 0xA0 and callers(r[0]) > best[0]:
            best = (callers(r[0]), r[0])
    return best[1]

def pool(name):
    """name -> (pool, vtable): the static initialiser loads the name and the pool, then the vtable."""
    a = find_string(name)
    for s in lea_sites(a):
        r = func_of(s)
        if not r or r[1] - r[0] > 0x100:
            continue
        ins = body(r[0])
        rx = {}
        seq = [rip_target(i) for i in ins if i.mnemonic == "lea" and rip_target(i) is not None]
        if len(seq) >= 3 and seq[0] == a:
            p, vt = seq[1], seq[2]
            if pe.sec_of(p) == ".data" and pe.sec_of(vt) == ".rdata":
                return p, vt
    return None, None

def pool_size(name):
    p, vt = pool(name)
    if vt is None:
        return None
    for i in body(ptr(vt + 8))[:10]:
        if i.mnemonic == "cmp" and i.operands[0].type == X.X86_OP_REG and md.reg_name(i.operands[0].reg) == "rdx" \
                and i.operands[1].type == X.X86_OP_IMM:
            return i.operands[1].imm
    return None

def pool_ctor(name):
    p, vt = pool(name)
    alloc = ptr(vt + 8)
    for s in lea_sites(p):
        r = func_of(s)
        if not r:
            continue
        ins = body(r[0])
        seen = False
        for i in ins:
            d = direct_target(i) if i.mnemonic in ("call", "jmp") else None
            if d == alloc:
                seen = True
            elif seen and d and is_code(d) and not (r[0] <= d < r[1]):
                return d
    return None

def geal_table():
    """{L"Name", factory} rows the game keeps in .data. Found from one name, walked both ways."""
    a = find_string("IsWalkable", wide=True)
    q = np.frombuffer(img, dtype="<u8", count=DATA["vsize"] // 8, offset=DATA["va"])
    hit = np.nonzero(q == np.uint64(a + IB))[0]
    p = DATA["va"] + int(hit[0]) * 8

    def row(x):
        s, f = ptr(x), ptr(x + 8)
        if not (R0 <= s < R1 and is_code(f)):
            return None
        e = s
        while img[e:e + 2] != b"\0\0" and e < s + 160:
            e += 2
        try:
            n = img[s:e].decode("utf-16le")
        except UnicodeDecodeError:
            return None
        return (n, f) if n and all(32 <= ord(c) < 127 for c in n) else None

    while row(p - 16):
        p -= 16
    out = {}
    while row(p):
        n, f = row(p)
        out[n] = f
        p += 16
    return out

def factory(name):
    """The registration table, else the name in the factory's own arity error (more than one table exists)."""
    if name in G:
        return G[name]
    a = img.find(("GEAL::`anonymous-namespace'::%s\0" % name).encode(), R0, R1)
    return func_of(lea_sites(a)[0])[0] if a >= 0 and lea_sites(a) else None

def node_slot2(factory):
    """A GEAL factory installs its node's vtable; slot 2 evaluates the node."""
    for i in body(factory):
        tg = rip_target(i) if i.mnemonic == "lea" else None
        if tg and R0 <= tg < R1 and is_code(ptr(tg)):
            return ptr(tg + 16)
    return None

def impl(factory):
    s = node_slot2(factory)
    ins = body(s)
    if sum(i.size for i in ins) < 40:
        for i in ins:
            if i.mnemonic in ("call", "jmp") and direct_target(i):
                return direct_target(i)
    return s

def after(ins, target):
    for k, i in enumerate(ins):
        if i.mnemonic in ("call", "jmp") and direct_target(i) == target:
            return ins[k + 1:]
    return []

def indirect_calls(ins):
    for k, i in enumerate(ins):
        m = mem(i) if i.mnemonic == "call" else None
        if m and m.base not in (0, X.X86_REG_RIP):
            yield k, m.disp

def virtual_getter(f):
    """lea rax,[rcx+D]; ret — no register choice at all."""
    ins = body(f)[:2]
    if len(ins) == 2 and ins[0].mnemonic == "lea" and ins[1].mnemonic == "ret":
        m = mem(ins[0])
        if m and md.reg_name(m.base) == "rcx":
            return m.disp
    return None

# ---- rules ---------------------------------------------------------------------------------------
res = {}
G = geal_table()
W = {n: component_getter(n) for n in ("Life", "Actor", "Buffs", "Stats")}
print("GEAL table rows:", len(G), "| getters:", {k: hex(v) for k, v in W.items()})

try:
    # Entity, from any getter: [rcx+D] is the details pointer, [rdi+D] then [rax+rcx*8] the component vector
    ins = body(W["Life"])
    res["Entity.Details"] = next(mem(i).disp for i in ins if i.mnemonic == "mov" and mem(i) and md.reg_name(mem(i).base) == "rcx")
    for a, b in zip(ins, ins[1:]):
        if mem(b) and mem(b).index and mem(a) and a.mnemonic == "mov" and md.reg_name(mem(b).base) == "rax":
            res["Entity.Components"] = mem(a).disp
    ins = body(impl(G["GetObjectId"]))
    res["Entity.Id"] = next(mem(i).disp for i in after(ins, direct_target(next(i for i in ins if i.mnemonic == "call"))) if mem(i))
except RULE_FAILED as error:
    print("rule failed: Entity, from any getter:", repr(error))

try:
    # Life: IsAlive compares the current health with zero
    ins = after(body(impl(G["IsAlive"])), W["Life"])
    cur = next(mem(i).disp for i in ins if i.mnemonic == "cmp" and mem(i))
    res["Life.Health.Current"] = cur
    res["Life.Health (GameHelper base)"] = cur - 0x30
except RULE_FAILED as error:
    print("rule failed: Life:", repr(error))

try:
    # Life's vitals: ordered sub-objects of the constructor, from Health's object on
    ctor = pool_ctor("Life")
    offs = sorted({mem(i).disp for i in body(ctor) if i.mnemonic == "lea" and mem(i) and mem(i).base != X.X86_REG_RIP
                   and mem(i).base != X.X86_REG_RSP and cur - 0x38 <= mem(i).disp < pool_size("Life")})
    offs = [o for o in offs if o < offs[0] + 0x1E0]
    for name, k in (("Mana", 1), ("EnergyShield", 2), ("Ward", 4), ("Divinity", 5)):
        res["Life.%s (GameHelper base)" % name] = offs[k] + 8
except RULE_FAILED as error:
    print("rule failed: Life's vitals:", repr(error))

try:
    # Terrain: IsWalkable calls the world's GetTerrain virtually, then the leaf that reads the whole layout
    ins = body(impl(G["IsWalkable"]))
    k, slot = next(indirect_calls(ins))
    walk = next(direct_target(i) for i in ins[k + 1:] if i.mnemonic == "call" and direct_target(i)
                and callers(direct_target(i)) > 0 and func_of(direct_target(i)) is None or i.mnemonic == "call" and direct_target(i) and len(body(direct_target(i))) > 12 and any(j.mnemonic == "imul" for j in body(direct_target(i))))
    wi = body(walk)
    im = [mem(i).disp for i in wi if i.mnemonic == "imul" and mem(i)]
    res["Terrain.TotalTiles"], res["Terrain.BytesPerRow"] = im[0], im[-1]
    res["Terrain.GridWalkable"] = next(mem(i).disp for i in wi if i.mnemonic == "mov" and mem(i) and mem(i).index and mem(i).disp > 0x40)
except RULE_FAILED as error:
    print("rule failed: Terrain:", repr(error))

try:
    # World vtable: statically, the vtables whose slot decodes as a virtual getter. At run time: [area+0].
    votes = collections.Counter()
    vts = np.unique(lea_t[(lea_t >= R0) & (lea_t < R1) & (lea_t % 8 == 0)])
    for vt in vts:
        vt = int(vt)
        if vt + slot + 8 >= R1 or not is_code(ptr(vt)) or not is_code(ptr(vt + slot)):
            continue
        if not all(is_code(ptr(vt + o)) for o in range(0, 100 * 8, 8)):   # a world class has 120+ virtuals
            continue
        d = virtual_getter(ptr(vt + slot))
        if d is not None and d >= 0x100:
            votes[d] += 1
            world_vt = vt
    res["Area.Terrain"] = votes.most_common(1)[0][0]
    print("terrain getter votes:", {hex(k): v for k, v in votes.items()}, "| slot", hex(slot))
except RULE_FAILED as error:
    print("rule failed: World vtable:", repr(error))

try:
    # Local players and server data: GetLocalPlayer / GetPlayers go through two more world virtuals
    def virtual_slot_of(name):
        ins = body(node_slot2(factory(name)))
        for i in ins:
            d = direct_target(i) if i.mnemonic == "call" else None
            if d and len(body(d)) < 24:
                for _, s in indirect_calls(body(d)):
                    return s
        return None

    s_local, s_players = virtual_slot_of("GetLocalPlayer"), virtual_slot_of("GetPlayers")
    ins = body(ptr(world_vt + s_local))
except RULE_FAILED as error:
    print("rule failed: Local players and server data:", repr(error))

try:
    # A rule that no longer fits leaves its field out; 25740136 compiled this one without the sub.
    head = next((mem(i).disp for i in ins if i.mnemonic == "sub" and mem(i) and mem(i).disp > 0x100), None)
    if head is not None:
        res["Area.LocalPlayer"] = head + 0x18
    ins = body(ptr(world_vt + s_players))
    k = next((k for k, _ in indirect_calls(ins)), None)
    server = None if k is None else next((mem(i).disp for i in ins[k + 1:] if i.mnemonic == "mov" and mem(i) and mem(i).disp > 0x100), None)
    if server is not None:
        res["Area.ServerData"] = server
except RULE_FAILED as error:
    print("rule failed: A rule that no longer fits leaves its field out; 25740136 compiled this one without the sub.:", repr(error))

try:
    # Actor: SetActorType resets the animation record: the same immediate twice, 0x18 apart
    ins = after(body(node_slot2(G["SetActorType"])), W["Actor"])
    st = [(mem(i).disp, i.operands[1].imm) for i in ins if i.mnemonic == "mov" and mem(i) and i.operands[0].type == X.X86_OP_MEM
          and i.operands[0].size == 4 and i.operands[1].type == X.X86_OP_IMM]
    res["Actor.AnimationId"] = next(d for d, v in st if (d + 0x18, v) in st)
except RULE_FAILED as error:
    print("rule failed: Actor:", repr(error))

try:
    # Actor: ForceSkillIntoCooldown: virtual sub-object getter, then the skill vector inside it
    ins = after(body(impl(G["ForceSkillIntoCooldown"])), W["Actor"])
    k, slot = next(indirect_calls(ins))
    inner = next(mem(i).disp for i in ins[k + 1:] if i.mnemonic == "mov" and mem(i) and md.reg_name(mem(i).base) == "rax")
    cctor = pool_ctor("ClientActor")
    cins = body(cctor)
    cvt = next(rip_target(i) for i in cins if i.mnemonic == "lea" and rip_target(i) and R0 <= rip_target(i) < R1)
    res["Actor.ActiveSkills"] = virtual_getter(ptr(cvt + slot)) + inner
    res["Actor.Cooldowns (ActiveSkills+0x18, assumed)"] = res["Actor.ActiveSkills"] + 0x18
except RULE_FAILED as error:
    print("rule failed: Actor:", repr(error))

try:
    # Actor: ClientActor's constructor stores `this` at X and zeroes the vector right after it
    for i in cins:
        if i.mnemonic == "mov" and mem(i) and i.operands[0].type == X.X86_OP_MEM and i.operands[1].type == X.X86_OP_REG \
                and i.operands[1].reg == mem(i).base:
            res["Actor.Deployed"] = mem(i).disp + 8
except RULE_FAILED as error:
    print("rule failed: Actor:", repr(error))

try:
    # Buffs: HasBuff walks the list: begin and end read in the callee
    ins = after(body(impl(G["HasBuff"])), W["Buffs"])
    f = next(direct_target(i) for i in ins if i.mnemonic == "call" and direct_target(i))
    res["Buffs.StatusEffects"] = min(mem(i).disp for i in body(f)[:12] if i.mnemonic == "mov" and mem(i) and md.reg_name(mem(i).base) == "rcx")
except RULE_FAILED as error:
    print("rule failed: Buffs:", repr(error))

try:
    # Stats: the most used displacement on the getter's result
    h = collections.Counter()
    for c in (e8[(e8 + T0 + 5 + ((t[e8 + 1].astype(np.int64) | t[e8 + 2].astype(np.int64) << 8 | t[e8 + 3].astype(np.int64) << 16
              | t[e8 + 4].astype(np.int64) << 24) + (1 << 31)) % (1 << 32) - (1 << 31)) == W["Stats"]][:400] + T0):
        c = int(c)
        for i in list(md.disasm(img[c + 5:c + 40], c + 5))[:2]:
            m = mem(i)
            if m and md.reg_name(m.base) == "rax":
                h[m.disp] += 1
    res["Stats (top field)"] = h.most_common(1)[0][0]
except RULE_FAILED as error:
    print("rule failed: Stats:", repr(error))

sizes = {n: pool_size(n) for n in ("Life", "Actor", "ClientActor", "Render", "Positioned", "Buffs", "Stats", "Targetable", "Player")}

truth = {"Entity.Details": 0x8, "Entity.Components": 0x10, "Entity.Id": 0x88, "Life.Health.Current": 0x1E0,
         "Life.Health (GameHelper base)": 0x1B0, "Life.Mana (GameHelper base)": 0x208,
         "Life.EnergyShield (GameHelper base)": 0x248, "Life.Ward (GameHelper base)": 0x2E8,
         "Life.Divinity (GameHelper base)": 0x338, "Terrain.TotalTiles": 0x18, "Terrain.GridWalkable": 0xD0,
         "Terrain.BytesPerRow": 0x130, "Area.Terrain": 0x8F8, "Area.LocalPlayer": 0x5D8, "Area.ServerData": 0x5B8,
         "Actor.AnimationId": 0x8B0, "Actor.ActiveSkills": 0xB08, "Actor.Cooldowns (ActiveSkills+0x18, assumed)": 0xB20,
         "Actor.Deployed": 0xC28, "Buffs.StatusEffects": 0x160, "Stats (top field)": 0x160}
ok = 0
for k, v in truth.items():
    got = res.get(k)
    good = got == v
    ok += good
    print("%-46s got %-8s want %-8s %s" % (k, hex(got) if got is not None else None, hex(v), "OK" if good else "WRONG"))
print("matched %d of %d" % (ok, len(truth)))
print("sizes:", {k: hex(v) if v else None for k, v in sizes.items()})
print("seconds: %.2f (Python, includes PE load and two passes over .text)" % (time.time() - t0))
