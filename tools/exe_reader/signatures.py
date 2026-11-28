"""Makes the byte signatures of catalog/code-fields.txt from the game's exe, read as a file: nothing is run or attached.

A signature is a run of whole instructions with every displacement, immediate and branch operand wildcarded, the field's
own operand marked with ^. It is kept only when it hits once in .text and has at least 12 fixed bytes
(docs/research/exe-layouts.md, "Signatures"); two from different functions must agree for the mod to believe them.

  sites   <value> [size]     every instruction whose displacement is <value> (hex), grouped by function, to pick from
  make    <rva> [...]        a signature for the instruction at each <rva> (hex), its displacement the operand
  check   [code-fields.txt]  each catalog line read against this exe: hits per signature and the value read
  show    <rva> [before after]  the instructions around <rva>

Run: py -B -I tools/exe_reader/signatures.py <command> ...   (needs capstone and numpy)
The exe is common.EXE, or the file POE2_EXE names: run `check` on every archived build before a line is kept.
"""
import os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import PE, EXE, np
import capstone

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
MIN_FIXED = 12          # fixed bytes a signature needs: fewer are unique by accident
MAX_BYTES = 48          # a longer one crosses more code than a patch leaves alone
MAX_BEFORE = 3          # instructions before the field's own that a signature may start at
CATALOG = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Poe2Accessibility", "catalog", "code-fields.txt")

pe = PE(os.environ.get("POE2_EXE") or EXE)
img = pe.img
TEXT = pe.sec(".text")
T0, T1 = TEXT["va"], TEXT["va"] + TEXT["vsize"]
text = img[T0:T1]
F = pe.funcs()
FB, FE = F[:, 0].astype(np.int64), F[:, 1].astype(np.int64)
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
md.detail = True


LEAF_REACH = 0x400      # how far a leaf (no .pdata row) is searched for its padding on either side


def func_of(a):
    """The .pdata function holding a; else the leaf around it: from the 16-aligned byte after int3 padding (or the end of
    the .pdata function before it) to the next such padding. Leaf getters have no .pdata row and were skipped."""
    i = int(np.searchsorted(FB, a, side="right")) - 1
    if i >= 0 and FB[i] <= a < FE[i]:
        return int(FB[i]), int(FE[i])
    if not T0 <= a < T1:
        return None
    floor = max(T0, int(FE[i]) if i >= 0 else T0, a - LEAF_REACH)
    ceil = min(T1, int(FB[i + 1]) if i + 1 < len(FB) else T1, a + LEAF_REACH)
    start = next((p for p in range(a - a % 16, floor - 1, -16) if p == floor or img[p - 1] == 0xCC), None)
    end = next((p for p in range(a - a % 16 + 16, ceil, 16) if img[p - 1] == 0xCC), ceil)
    if start is None:
        return None
    while end > a and img[end - 1] == 0xCC:
        end -= 1
    return (start, end) if start <= a < end else None


def decode(f):
    return list(md.disasm(img[f[0]:f[1]], f[0]))


def line(x, mark="  "):
    return f"   {mark} {x.address:#9x}  {x.bytes.hex(' '):<32} {x.mnemonic} {x.op_str}"


def operands(x):
    """(offset, size) of the displacement and of the immediate or branch operand in the instruction; None for each it lacks.

    Capstone's encoding is checked against the bytes: after some prefixes it points at the wrong place.
    """
    disp = imm = None
    if x.disp_size and x.disp_offset:
        raw = int.from_bytes(x.bytes[x.disp_offset:x.disp_offset + x.disp_size], "little", signed=True)
        if raw == x.disp or (x.disp_size == 4 and "rip" in x.op_str):
            disp = (x.disp_offset, x.disp_size)
    if x.imm_size and x.imm_offset:
        imm = (x.imm_offset, x.imm_size)
    return disp, imm


def tokens(x, operand_at=None):
    out = [f"{b:02X}" for b in x.bytes]
    disp, imm = operands(x)
    for place in (disp, imm):
        if place:
            for i in range(place[0], place[0] + place[1]):
                out[i] = "??"
    if operand_at is not None:
        out.insert(operand_at, "^")
    return out


def hits(sig):
    rx = b"".join(b"." if t == "??" else re.escape(bytes([int(t, 16)])) for t in sig if t != "^")
    return [m.start() + T0 for m in re.finditer(b"(?s)(?=" + rx + b")", text)]


def make(rva):
    """Signatures for the instruction at rva: one starting at it, and one starting a few instructions before."""
    f = func_of(rva)
    if not f:
        return [], "no .pdata function"
    ins = decode(f)
    at = next((i for i, x in enumerate(ins) if x.address == rva), None)
    if at is None:
        return [], "not an instruction's start (decoded from the function's)"
    disp, _ = operands(ins[at])
    if not disp:
        return [], "the instruction has no displacement capstone and its bytes agree on"
    found = []
    for back in range(0, MAX_BEFORE + 1):
        if at - back < 0:
            break
        sig = []
        for i in range(at - back, len(ins)):
            sig += tokens(ins[i], disp[0] if i == at else None)
            size = sum(1 for t in sig if t != "^")
            fixed = sum(1 for t in sig if t not in ("^", "??"))
            if size > MAX_BYTES:
                break
            if i >= at and fixed >= MIN_FIXED and len(hits(sig)) == 1:
                while sig[-1] == "??" and i > at:   # a trailing operand adds nothing
                    sig.pop()
                if len(hits(sig)) == 1:
                    found.append((back, disp[1], " ".join(sig)))
                break
    return found, None


def cmd_sites(value, size=None):
    value = int(value, 16)
    by_func = {}
    for width in ((1, 4) if size is None else (int(size),)):
        if width == 1 and not 0 <= value < 0x80:
            continue
        needle = value.to_bytes(width, "little")
        for m in re.finditer(re.escape(needle), text):
            f = func_of(m.start() + T0)
            if f:
                by_func.setdefault(f, set()).add(m.start() + T0)
    shown = 0
    for f, places in sorted(by_func.items()):
        ins = decode(f)
        mine = [x for x in ins if any(x.address <= p < x.address + x.size for p in places) and operands(x)[0]
                and x.disp == value and "rsp" not in x.op_str and "rip" not in x.op_str]
        if mine:
            shown += 1
            print(f"function {f[0]:#x}..{f[1]:#x}")
            for x in mine:
                print(line(x))
    print(f"{shown} functions")


def cmd_make(rvas):
    for arg in rvas:
        rva = int(arg, 16)
        found, why = make(rva)
        f = func_of(rva)
        print(f"== {rva:#x}" + (f" in function {f[0]:#x}" if f else "") + (f": {why}" if why else ""))
        for back, size, sig in found:
            print(f"   size {size}, from {back} before: {sig}")
        if not found and not why:
            print("   none unique within the limits")


def cmd_check(path=CATALOG):
    bad = 0
    for row in open(path, encoding="utf-8"):
        cols = row.rstrip("\n").split("\t")
        if not row.strip() or row.startswith("#") or len(cols) < 5:
            continue
        name, target, want, size = cols[0], cols[1], int(cols[2], 16), int(cols[3])
        reads = []
        for sig in cols[4:]:
            toks = sig.split()
            found = hits(toks)
            if len(found) == 1:
                at = found[0] + toks.index("^")
                reads.append((int.from_bytes(img[at:at + size], "little", signed=size > 1), func_of(found[0])))
            else:
                reads.append((None, len(found)))
        values = {v for v, _ in reads if v is not None}
        funcs = {f for v, f in reads if v is not None}
        ok = len(values) == 1 and len([v for v, _ in reads if v is not None]) >= 2 and values == {want}
        bad += not ok
        note = "" if len(funcs) != 1 or len(reads) < 2 else " (one function)"
        print(f"{'ok ' if ok else 'BAD'} {name} -> {target} want {want:#x}: " +
              ", ".join(f"{v:#x}" if v is not None else f"{n} hits" for v, n in reads) + note)
    print(f"{bad} lines the mod would not believe")


def cmd_show(rva, before="6", after="8"):
    rva = int(rva, 16)
    f = func_of(rva)
    if not f:
        print("no .pdata function; bytes", img[rva:rva + 16].hex(" "))
        return
    ins = decode(f)
    at = next((i for i, x in enumerate(ins) if x.address <= rva < x.address + x.size), 0)
    print(f"function {f[0]:#x}..{f[1]:#x}")
    for x in ins[max(0, at - int(before)):at + int(after) + 1]:
        print(line(x, ">>" if x.address <= rva < x.address + x.size else "  "))


if __name__ == "__main__":
    command, args = (sys.argv[1] if len(sys.argv) > 1 else ""), sys.argv[2:]
    if command == "sites" and args:
        cmd_sites(*args[:2])
    elif command == "make" and args:
        cmd_make(args)
    elif command == "check":
        cmd_check(*args[:1])
    elif command == "show" and args:
        cmd_show(*args[:3])
    else:
        print(__doc__)
