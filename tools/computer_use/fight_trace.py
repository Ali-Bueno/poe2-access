"""Read-only fight recorder for the dodge-warning research (docs/research/dodge-timing.md, "Live protocol").

Every frame, for each living monster near the followed player: its animation id (Actor +0x8B0), its current action (Actor
+0x238: type id, target entity id, target grid place), its animation controller (Animated +0x350 -> AnimationController:
clip, position, speed, global speed) and its place; the player's life and place. Each clip met is written once with its
name and events. Lines are JSON with `t` (ms since start) and `wall` (UTC ms) so a screen recording can be lined up.
Never writes to the game, never sends input.

    py -B tools/computer_use/fight_trace.py --seconds 120 --out fight.jsonl [--range 1500] [--shots DIR --shot-ms 100]
"""
import argparse
import ctypes as C
import json
import math
import os
import re
import struct
import subprocess
import sys
import time
from ctypes import wintypes as W

EXE = r'D:\games\steam\steamapps\common\Path of Exile 2\PathOfExileSteam.exe'
# The host's "Game States" pattern (host/GameOffsets/StaticOffsetsPatterns.cs), operand at the 4 bytes after 48 39 2D.
GAME_STATES = rb'\x48\x39\x2D(....)\x0F\x85....\xB9....\xE8....\x48\x8B\xF8\x48\x89\x44\x24\x50'
STATE_STACK, AREA, LOCAL_PLAYER, AWAKE = 0x10, 0x290, 0x5D8, 0x718
DETAILS, COMPONENTS, ID, PATH, LOOKUP, NAME_MAP = 0x8, 0x10, 0x88, 0x8, 0x28, 0x28
LIFE_CURRENT, RENDER_POSITION, POSITIONED_GRID = 0x1E0, 0x138, 0x444
ANIMATION_ID, ACTION, ACTION_FLAGS = 0x8B0, 0x238, 0x2A0
ACTION_SKILL, ACTION_TYPE, ACTION_TARGET, ACTION_PLACE = 0xF8, 0x120, 0x148, 0x150
ANIMATED_OBJECT = 0x350
# ObjectMagicProperties: Details1 (+0xB0) + Rarity (+0x94), host/GameOffsets ModsAndObjectMagicProperties.cs, read the same way
# by the host's ObjectMagicProperties; 0 normal, 1 magic, 2 rare, 3 unique. +0x94 alone (the Mods component's Details0) read 0
# for Beira, a unique, on 2026-11-08: every trace before then has a wrong rarity column.
RARITY = 0xB0 + 0x94
AC_GLOBAL, AC_DATA, AC_CLIP, AC_POSITION, AC_SPEED, AC_START, AC_END, AC_GLOBAL_OFF = 0x178, 0x180, 0x190, 0x1A4, 0x1B0, 0x1B8, 0x1BC, 0x1D4
DATA_CLIP_EVENTS, AMD_PATH, AMD_TABLE, TABLE_CLIPS, CLIP_SIZE = 0x10, 0x8, 0x28, 0x38, 0x138
EVENT_KIND, EVENT_TIME, EVENT_NAME, EVENT_ID, EVENT_NEXT = 0x8, 0xC, 0x18, 0x38, 0x40

kernel32 = C.WinDLL('kernel32', use_last_error=True)
psapi = C.WinDLL('psapi', use_last_error=True)
kernel32.OpenProcess.restype = W.HANDLE
kernel32.ReadProcessMemory.argtypes = [W.HANDLE, C.c_void_p, C.c_void_p, C.c_size_t, C.POINTER(C.c_size_t)]
PROCESS_VM_READ, PROCESS_QUERY_LIMITED_INFORMATION = 0x0010, 0x1000


def event_id(name):
    """The game's id of an animation event name (exe 0x1D48E10 in 26476662): the part before '#'."""
    value = 0
    for byte in name.split('#')[0].encode('latin1'):
        value = ((value * 0x1000193) & 0xFFFFFFFF) ^ byte
    return (((value >> 16) ^ value) & 0xFFFF) | 0x8000


class Memory:
    def __init__(self, pid):
        self.handle = kernel32.OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
        if not self.handle:
            raise C.WinError(C.get_last_error())

    def read(self, address, size):
        buffer, done = C.create_string_buffer(size), C.c_size_t()
        if not address or not kernel32.ReadProcessMemory(self.handle, C.c_void_p(address), buffer, size, C.byref(done)):
            return None
        return buffer.raw[:done.value] if done.value == size else None

    def unpack(self, fmt, address):
        raw = self.read(address, struct.calcsize(fmt))
        return struct.unpack(fmt, raw) if raw else None

    def q(self, address):
        value = self.unpack('<Q', address)
        return value[0] if value else 0

    def one(self, fmt, address):
        value = self.unpack(fmt, address)
        return value[0] if value else None

    def text(self, address, wide):
        """An MSVC std::string or std::wstring: inline below 16 bytes of capacity, else a pointer."""
        raw = self.read(address, 0x20)
        if not raw:
            return None
        length, capacity = struct.unpack_from('<QQ', raw, 0x10)
        unit = 2 if wide else 1
        if length > 512 or capacity < length:
            return None
        data = raw[:length * unit] if capacity * unit < 16 else self.read(struct.unpack_from('<Q', raw)[0], length * unit)
        return data.decode('utf-16-le' if wide else 'latin1', 'replace') if data is not None else None


def game_pid():
    out = subprocess.check_output(['tasklist', '/FI', 'IMAGENAME eq PathOfExileSteam.exe', '/FO', 'CSV', '/NH'], text=True)
    for line in out.splitlines():
        parts = line.strip('"').split('","')
        if len(parts) > 1 and parts[0].lower() == 'pathofexilesteam.exe':
            return int(parts[1])
    raise SystemExit('The game is not running.')


def module_base(memory):
    modules, needed = (W.HMODULE * 1)(), W.DWORD()
    psapi.EnumProcessModulesEx(memory.handle, modules, C.sizeof(modules), C.byref(needed), 3)
    return modules[0]


def game_states_rva():
    import pefile
    data = open(EXE, 'rb').read()
    text = [section for section in pefile.PE(data=data, fast_load=True).sections if section.Name.startswith(b'.text')][0]
    raw = data[text.PointerToRawData:text.PointerToRawData + text.SizeOfRawData]
    hits = list(re.finditer(GAME_STATES, raw, re.S))
    if len(hits) != 1:
        raise SystemExit(f'Game States pattern hit {len(hits)} times: update GAME_STATES from the host.')
    return text.VirtualAddress + hits[0].start() + 7 + struct.unpack('<i', hits[0].group(1))[0]


class Game:
    def __init__(self):
        self.pid = game_pid()
        self.memory = Memory(self.pid)
        self.states = module_base(self.memory) + game_states_rva()
        self.components = {}
        self.clips = {}

    def area(self):
        states = self.memory.q(self.states)
        last = self.memory.q(states + STATE_STACK + 8)
        return self.memory.q(self.memory.q(last - 0x10) + AREA)

    def awake(self, area):
        """The awake entity map (an MSVC std::map): (id, entity) pairs."""
        head = self.memory.q(area + AWAKE)
        found, stack, seen = [], [self.memory.q(head + 8)], set()
        while stack and len(found) < 4000:
            node = stack.pop()
            if not node or node == head or node in seen or self.memory.one('<B', node + 0x19) != 0:
                continue
            seen.add(node)
            found.append((self.memory.one('<I', node + 0x20), self.memory.q(node + 0x28)))
            stack += [self.memory.q(node), self.memory.q(node + 0x10)]
        return found

    def path(self, entity):
        return self.memory.text(self.memory.q(entity + DETAILS) + PATH, True) or ''

    def lookup(self, entity):
        """Component name -> address, through the details' name map (as the host's Entity reads it)."""
        if entity in self.components:
            return self.components[entity]
        names = self.memory.q(self.memory.q(entity + DETAILS) + LOOKUP)
        first, last = self.memory.q(names + NAME_MAP), self.memory.q(names + NAME_MAP + 8)
        vector, found = self.memory.q(entity + COMPONENTS), {}
        raw = self.memory.read(first, last - first) if 0 < last - first <= 0x1000 else None
        for at in range(0, len(raw or b''), 16):
            name_pointer, index = struct.unpack_from('<Qi', raw, at)
            name = self.memory.read(name_pointer, 40)
            if name and 0 <= index < 200:
                found[name.split(b'\0')[0].decode('latin1')] = self.memory.q(vector + index * 8)
        self.components[entity] = found
        return found

    def clip(self, data, index):
        """A clip's name and events [(name, seconds)], read once; an event whose id is not its name's hash is left out."""
        key = (data, index)
        if key not in self.clips:
            memory = self.memory
            amd = memory.q(data)
            table = memory.q(data + 8) or memory.q(amd + AMD_TABLE)
            first, last = memory.q(table + TABLE_CLIPS), memory.q(table + TABLE_CLIPS + 8)
            name = memory.text(first + index * CLIP_SIZE, False) if 0 <= index < (last - first) // CLIP_SIZE else None
            events, rejected = [], 0
            groups, end = memory.q(data + DATA_CLIP_EVENTS), memory.q(data + DATA_CLIP_EVENTS + 8)
            if 0 <= index < (end - groups) // 0x18:
                begin, stop = memory.q(groups + index * 0x18), memory.q(groups + index * 0x18 + 8)
                for slot in range(min(64, max(0, (stop - begin) // 8))):
                    event, hops = memory.q(begin + slot * 8), 0
                    while event and hops < 64:
                        if memory.one('<I', event + EVENT_KIND) == 0:
                            label, ident = memory.text(event + EVENT_NAME, False), memory.one('<H', event + EVENT_ID)
                            if label and ident == event_id(label):
                                events.append((label, round(memory.one('<f', event + EVENT_TIME), 4)))
                            else:
                                rejected += 1
                        event, hops = memory.q(event + EVENT_NEXT), hops + 1
            self.clips[key] = dict(clip=name, amd=memory.text(amd + AMD_PATH, True), index=index, events=sorted(set(events), key=lambda e: e[1]), rejected=rejected)
            return self.clips[key], True
        return self.clips[key], False


def sample(game, out, reach, last):
    memory, area = game.memory, game.area()
    if last.get('area') != area:
        # A new area reuses entity addresses: nothing read before holds.
        game.components.clear()
        game.clips.clear()
        last.clear()
        last['area'] = area
    player = memory.q(area + LOCAL_PLAYER)
    if not player:
        return
    parts = game.lookup(player)
    here = memory.unpack('<3f', parts.get('Render', 0) + RENDER_POSITION) or (0, 0, 0)
    life = memory.one('<i', parts.get('Life', 0) + LIFE_CURRENT)
    stamp = dict(t=round((time.perf_counter() - START) * 1000), wall=int(time.time() * 1000))
    moved = math.hypot(here[0] - last.get('x', here[0]), here[1] - last.get('y', here[1])) > 25
    if last.get('life') != life or moved or 'x' not in last:
        # Also when the player walks: an audit measures distances from this place.
        last['life'], last['x'], last['y'] = life, here[0], here[1]
        out.write(json.dumps(dict(stamp, player=memory.one('<I', player + ID), life=life, at=[round(v) for v in here[:2]])) + '\n')
    owners = last.setdefault('owners', {})
    for entity_id, entity in game.awake(area):
        if owners.get(entity) != entity_id:
            # A new monster at a dead one's address (spawned larvae): its components are elsewhere.
            owners[entity] = entity_id
            game.components.pop(entity, None)
        path = game.path(entity)
        if not path.startswith('Metadata/Monsters'):
            continue
        parts = game.lookup(entity)
        actor, render = parts.get('Actor'), parts.get('Render')
        place = memory.unpack('<3f', render + RENDER_POSITION) if render else None
        if not actor or not place or math.hypot(place[0] - here[0], place[1] - here[1]) > reach:
            continue
        if 'Life' in parts and (memory.one('<i', parts['Life'] + LIFE_CURRENT) or 0) <= 0:
            continue
        row = dict(stamp, id=entity_id, monster=path.split('/')[-1], anim=memory.one('<i', actor + ANIMATION_ID), at=[round(v) for v in place[:2]])
        if 'ObjectMagicProperties' in parts:
            row['rarity'] = memory.one('<i', parts['ObjectMagicProperties'] + RARITY)
        action = memory.q(actor + ACTION)
        if action:
            target = memory.q(action + ACTION_TARGET)
            row.update(action=memory.one('<H', action + ACTION_TYPE), skill=hex(memory.q(action + ACTION_SKILL)), flags=memory.one('<B', actor + ACTION_FLAGS),
                       target=memory.one('<I', target + ID) if target else 0, place=memory.unpack('<2i', action + ACTION_PLACE))
        animated = memory.q(parts['Animated'] + ANIMATED_OBJECT) if 'Animated' in parts else 0
        controller = game.lookup(animated).get('AnimationController') if animated else None
        if controller:
            data, index = memory.q(controller + AC_DATA), memory.one('<i', controller + AC_CLIP)
            row.update(clip_index=index, pos=memory.one('<f', controller + AC_POSITION), speed=memory.one('<f', controller + AC_SPEED),
                       glob=memory.one('<f', controller + AC_GLOBAL), glob_off=memory.one('<B', controller + AC_GLOBAL_OFF),
                       end=memory.one('<f', controller + AC_END))
            if data and index is not None and index >= 0:
                info, new = game.clip(data, index)
                row['clip'] = info['clip']
                if new:
                    out.write(json.dumps(dict(stamp, clip_info=info)) + '\n')
        key = (row.get('anim'), row.get('action'), row.get('target'), row.get('clip_index'))
        previous = last.get(entity_id)
        busy = action or (row.get('clip') or '').find('idle') < 0
        if previous is None or previous[0] != key or (busy and stamp['t'] - previous[1] >= 33):
            last[entity_id] = (key, stamp['t'])
            out.write(json.dumps(row) + '\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--seconds', type=float, default=60)
    parser.add_argument('--out', required=True)
    parser.add_argument('--range', type=float, default=1500, help='world units around the player')
    parser.add_argument('--shots', help='folder for frame-timed screenshots (the game must be in front)')
    parser.add_argument('--shot-ms', type=int, default=100)
    args = parser.parse_args()
    game = Game()
    shots, next_shot = None, 0
    if args.shots:
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        import capture
        import win32
        shots = (capture, win32.target(game.pid))
    last = {}
    with open(args.out, 'w', encoding='utf-8') as out:
        out.write(json.dumps(dict(start_wall=int(time.time() * 1000), pid=game.pid)) + '\n')
        while time.perf_counter() - START < args.seconds:
            sample(game, out, args.range, last)
            now = (time.perf_counter() - START) * 1000
            if shots and now >= next_shot:
                next_shot = now + args.shot_ms
                try:
                    shot = shots[0].screenshot(shots[1], os.path.join(args.shots, f'{round(now):07d}.png'), 2)
                    out.write(json.dumps(dict(t=round(now), wall=int(time.time() * 1000), shot=shot['path'])) + '\n')
                except (OSError, RuntimeError) as error:
                    out.write(json.dumps(dict(t=round(now), shot_error=str(error))) + '\n')
            time.sleep(0.004)


START = time.perf_counter()
if __name__ == '__main__':
    main()
