"""Phrase tables for the mod's own speech in the game's other languages.

Every spoken string is a T(english, spanish) call in Poe2Accessibility/Announcements*.cs. Languages other than
English and Spanish look the English text up in catalog/lang/<code>.json; an interpolated string is looked up as its
composite format ("Life {0} percent"), the way Announcements.Phrase builds it at run time.

    py tools/localization/phrases.py check            # missing, stale and placeholder-mismatched entries per language
    py tools/localization/phrases.py missing fr       # JSON {english: spanish} still to translate for one language
    py tools/localization/phrases.py merge fr new.json  # adds translated entries, drops stale ones, writes sorted
    py tools/localization/phrases.py leaks            # interpolation holes that carry their own literal text
"""

import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SOURCES = sorted((REPO / "Poe2Accessibility").glob("Announcements*.cs"))
LANG_DIR = REPO / "Poe2Accessibility" / "catalog" / "lang"
LANGUAGES = ["pt", "fr", "de", "ru", "th", "ja", "ko", "zh-TW"]
ESCAPES = {"n": "\n", "t": "\t", "r": "\r", "0": "\0", "\\": "\\", '"': '"', "'": "'", "a": "\a", "b": "\b", "f": "\f", "v": "\v"}


class Lexer:
    """Just enough C# lexing to find T( calls and read their string-literal arguments."""

    def __init__(self, text):
        self.text = text

    def skip_trivia(self, i):
        text = self.text
        while i < len(text):
            if text[i].isspace():
                i += 1
            elif text.startswith("//", i):
                i = text.find("\n", i)
                i = len(text) if i < 0 else i
            elif text.startswith("/*", i):
                i = text.index("*/", i) + 2
            else:
                break
        return i

    def literal_end(self, i):
        """End of the string or char literal starting at i (prefixes included), else None."""
        text = self.text
        j = i
        interpolated = verbatim = False
        while j < len(text) and text[j] in "$@":
            interpolated |= text[j] == "$"
            verbatim |= text[j] == "@"
            j += 1
        if j >= len(text) or text[j] not in "\"'" or (j > i and text[j] == "'"):
            return None
        quote = text[j]
        j += 1
        while j < len(text):
            c = text[j]
            if verbatim and c == '"':
                if text.startswith('""', j):
                    j += 2
                    continue
                return j + 1
            if not verbatim and c == "\\":
                j += 2
                continue
            if c == quote:
                return j + 1
            if interpolated and c == "{":
                if text.startswith("{{", j):
                    j += 2
                    continue
                j = self.hole_end(j + 1)
                continue
            j += 1
        raise ValueError(f"unterminated literal at {i}")

    def hole_end(self, i):
        """Index just past the '}' closing an interpolation hole whose expression starts at i."""
        depth = 0
        text = self.text
        while i < len(text):
            end = self.literal_end(i) if text[i] in "$@\"'" else None
            if end:
                i = end
                continue
            c = text[i]
            if c in "([{":
                depth += 1
            elif c in ")]":
                depth -= 1
            elif c == "}":
                if depth == 0:
                    return i + 1
                depth -= 1
            i += 1
        raise ValueError("unterminated hole")

    def calls(self, start=0, end=None):
        """Positions just past every 'T(' in code between start and end, holes of interpolated strings included."""
        text = self.text
        end = len(text) if end is None else end
        i = start
        while i < end:
            if text.startswith("//", i) or text.startswith("/*", i):
                i = self.skip_trivia(i)
                continue
            lit = self.literal_end(i) if text[i] in "$@\"'" else None
            if lit:
                for hole_start, hole_stop in self.holes(i, lit):
                    yield from self.calls(hole_start, hole_stop)
                i = lit
                continue
            if text.startswith("T(", i) and (i == 0 or not (text[i - 1].isalnum() or text[i - 1] in "_.")):
                if not text[:i].rstrip().endswith(("string", "Phrase")):
                    yield i + 2
            i += 1

    def holes(self, start, stop):
        """(start, end) of each hole expression in the literal spanning start..stop."""
        text = self.text
        prefix_end = start
        while text[prefix_end] in "$@":
            prefix_end += 1
        if "$" not in text[start:prefix_end]:
            return
        j = prefix_end + 1
        while j < stop - 1:
            if text.startswith("{{", j):
                j += 2
            elif text[j] == "{":
                hole_stop = self.hole_end(j + 1)
                yield j + 1, hole_stop - 1
                j = hole_stop
            else:
                j += 1

    def arguments(self, i):
        """Raw source text of each argument of the call whose '(' ends just before i."""
        args, depth, begin = [], 0, i
        text = self.text
        while True:
            end = self.literal_end(i) if text[i] in "$@\"'" else None
            if end:
                i = end
                continue
            if text.startswith("//", i) or text.startswith("/*", i):
                i = self.skip_trivia(i)
                continue
            c = text[i]
            if c in "([{":
                depth += 1
            elif c in ")]}":
                if depth == 0:
                    args.append(text[begin:i].strip())
                    return args
                depth -= 1
            elif c == "," and depth == 0:
                args.append(text[begin:i].strip())
                begin = i + 1
            i += 1


def unescape(body, verbatim):
    if verbatim:
        return body.replace('""', '"')
    out, i = [], 0
    while i < len(body):
        c = body[i]
        if c != "\\":
            out.append(c)
            i += 1
            continue
        n = body[i + 1]
        if n == "u":
            out.append(chr(int(body[i + 2:i + 6], 16)))
            i += 6
        elif n == "x":
            m = re.match(r"[0-9a-fA-F]{1,4}", body[i + 2:])
            out.append(chr(int(m.group(0), 16)))
            i += 2 + len(m.group(0))
        else:
            out.append(ESCAPES[n])
            i += 2
    return "".join(out)


def split_hole(expr):
    """(expression, ',alignment' or '', ':format' or '') of one hole's source text."""
    lexer = Lexer(expr)
    depth, i, comma, colon = 0, 0, None, None
    while i < len(expr):
        end = lexer.literal_end(i) if expr[i] in "$@\"'" else None
        if end:
            i = end
            continue
        c = expr[i]
        if c in "([{":
            depth += 1
        elif c in ")]}":
            depth -= 1
        elif depth == 0 and c == "," and comma is None and colon is None:
            comma = i
        elif depth == 0 and c == ":" and colon is None and not expr.startswith("::", i) and expr[i - 1] != ":":
            colon = i
            break
        i += 1
    stop = colon if colon is not None else len(expr)
    alignment = expr[comma:stop].replace(" ", "") if comma is not None else ""
    head = expr[:comma if comma is not None else stop]
    return head.strip(), alignment, expr[colon:] if colon is not None else ""


def phrase(arg):
    """(composite format, [hole expressions]) of a single string-literal argument, else None."""
    lexer = Lexer(arg)
    end = lexer.literal_end(0) if arg[:1] in "$@\"" else None
    if end != len(arg):
        return None
    prefix = 0
    while arg[prefix] in "$@":
        prefix += 1
    verbatim, interpolated = "@" in arg[:prefix], "$" in arg[:prefix]
    body = arg[prefix + 1:-1]
    if not interpolated:
        return unescape(body, verbatim), []
    parts, holes, j = [], [], 0
    sub = Lexer(body)
    while j < len(body):
        if body.startswith("{{", j) or body.startswith("}}", j):
            parts.append(body[j:j + 2])
            j += 2
        elif body[j] == "{":
            stop = sub.hole_end(j + 1)
            expr, alignment, fmt = split_hole(body[j + 1:stop - 1])
            parts.append("{%d%s%s}" % (len(holes), alignment, fmt))
            holes.append(expr)
            j = stop
        else:
            k = j
            while k < len(body) and body[k] not in "{}":
                k += 1 + (body[k] == "\\" and not verbatim)
            parts.append(unescape(body[j:k], verbatim).replace("{", "{{").replace("}", "}}"))
            j = k
    return "".join(parts), holes


def collect():
    """{english format: (spanish format, 'file:line', [hole expressions])} of every T call, plus unreadable calls."""
    found, problems = {}, []
    for path in SOURCES:
        text = path.read_text(encoding="utf-8")
        lexer = Lexer(text)
        for pos in lexer.calls():
            line = text.count("\n", 0, pos) + 1
            where = f"{path.name}:{line}"
            args = lexer.arguments(pos)
            if len(args) != 2:
                problems.append(f"{where}: T call with {len(args)} arguments")
                continue
            en, es = phrase(args[0]), phrase(args[1])
            if en is None or es is None:
                problems.append(f"{where}: argument is not a single string literal: {args[0][:60]}")
                continue
            found.setdefault(en[0], (es[0], where, en[1]))
    return found, problems


def placeholders(fmt):
    return sorted(set(re.findall(r"(?<!\{)\{(\d+)[^{}]*\}", fmt.replace("{{", "").replace("}}", ""))))


def load(lang):
    path = LANG_DIR / f"{lang}.json"
    return json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}


def save(lang, table):
    LANG_DIR.mkdir(parents=True, exist_ok=True)
    text = json.dumps(dict(sorted(table.items())), ensure_ascii=False, indent=1)
    (LANG_DIR / f"{lang}.json").write_text(text + "\n", encoding="utf-8")


def check():
    found, problems = collect()
    print(f"{len(found)} phrases")
    for problem in problems:
        print("unreadable:", problem)
    bad = bool(problems)
    for lang in LANGUAGES:
        table = load(lang)
        missing = [key for key in found if key not in table]
        stale = [key for key in table if key not in found]
        mismatched = [key for key, value in table.items() if key in found and placeholders(key) != placeholders(value)]
        print(f"{lang}: {len(table)} entries, {len(missing)} missing, {len(stale)} stale, {len(mismatched)} placeholder mismatches")
        for key in mismatched:
            print(f"  mismatch: {key!r} -> {table[key]!r}")
        bad |= bool(missing or stale or mismatched)
    return 1 if bad else 0


def leaks():
    found, _ = collect()
    for key, (_, where, holes) in found.items():
        for hole in holes:
            if '"' in hole and "T(" not in hole:
                print(f"{where}: {key!r} hole {hole!r}")


def main(argv):
    command = argv[1] if len(argv) > 1 else "check"
    if command == "check":
        return check()
    if command == "leaks":
        leaks()
        return 0
    if command == "missing":
        found, _ = collect()
        table = load(argv[2])
        missing = {key: es for key, (es, _, _) in found.items() if key not in table}
        sys.stdout.reconfigure(encoding="utf-8")
        print(json.dumps(missing, ensure_ascii=False, indent=1))
        return 0
    if command == "merge":
        found, _ = collect()
        table = load(argv[2])
        table.update(json.loads(Path(argv[3]).read_text(encoding="utf-8")))
        save(argv[2], {key: value for key, value in table.items() if key in found})
        return check()
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
