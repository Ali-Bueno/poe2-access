"""Bounded keyboard and mouse inputs; no game-memory writes. The controller lives in pad.py."""
import ctypes as C
import time
import win32 as w

# Virtual-key / KEYEVENTF / MOUSEEVENTF constants from winuser.h.
VK = dict(ctrl=0x11, shift=0x10, alt=0x12, enter=0x0D, escape=0x1B, space=0x20,
          tab=0x09, backspace=0x08, up=0x26, down=0x28, left=0x25, right=0x27,
          f1=0x70)
VK.update({f'f{number}': VK['f1'] + number - 1 for number in range(2, 25)})  # VK_F1..24
VK_PAUSE, KEY_UP, SCANCODE, EXTENDED = 0x13, 0x02, 0x08, 0x01
MODIFIER_KEYS = {VK['ctrl'], VK['shift'], VK['alt'], 0x5B, 0x5C}  # VK_LWIN / VK_RWIN
MAX_HOLD_SECONDS = 2.0  # small observable steps, never unattended navigation
POLL_SECONDS = 0.01  # stop promptly when Pause is pressed or focus changes
MAX_SCROLL_NOTCHES = 8  # keep each scroll small enough to inspect its result
MAX_TYPED_CHARACTERS = 64  # names, e-mail addresses and passwords; never prose
UNICODE = 0x0004  # KEYEVENTF_UNICODE


def keys_from_text(text):
    values = []
    for name in text.lower().split('+'):
        if name in VK:
            values.append(VK[name])
        elif len(name) == 1 and name.isascii() and name.isalnum():
            values.append(ord(name.upper()))
        else:
            raise ValueError(f'Unsupported key: {name}')
    if not 1 <= len(values) <= 3 or len(set(values)) != len(values):
        raise ValueError('Use one to three different keys')
    return values


def send(event):
    if w.send_input(1, C.byref(event), C.sizeof(event)) != 1:
        raise RuntimeError(f'SendInput failed ({C.get_last_error()}); check foreground and process integrity levels')


def key_event(vk, release=False):
    scan = w.map_key(vk, 0)  # MAPVK_VK_TO_VSC
    if not scan:
        raise ValueError('Key has no scan code')
    flags = SCANCODE | (KEY_UP if release else 0) | (EXTENDED if vk in {VK['up'], VK['down'], VK['left'], VK['right']} else 0)
    return w.Input(type=1, data=w.InputData(key=w.KeyInput(scan=scan, flags=flags)))


def hold(info, down, up, seconds):
    if not 0 < seconds <= MAX_HOLD_SECONDS:
        raise ValueError(f'Hold must be within (0, {MAX_HOLD_SECONDS}] seconds')
    w.require_foreground(info)
    # Flush the previous Pause transition; a currently held Pause also aborts.
    if w.key_state(VK_PAUSE) & 0x8000:
        raise RuntimeError('Pause fail-safe is active')
    pressed = []
    try:
        for press, release in zip(down, up):
            send(press)
            pressed.append(release)
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            w.require_foreground(info)
            if w.key_state(VK_PAUSE) & 0x8001:
                raise RuntimeError('Stopped by Pause fail-safe')
            time.sleep(min(POLL_SECONDS, max(0, deadline - time.monotonic())))
    finally:
        failures = []
        for release in reversed(pressed):
            try:
                send(release)
            except RuntimeError as error:
                failures.append(str(error))
        if failures:
            raise RuntimeError('Key/button release failed: ' + '; '.join(failures))


def press_keys(info, text, seconds):
    keys = keys_from_text(text)
    checked = set(keys) | MODIFIER_KEYS
    if any(w.key_state(vk) & 0x8000 for vk in checked):
        raise RuntimeError('A requested key or modifier is already physically held')
    hold(info, [key_event(vk) for vk in keys], [key_event(vk, True) for vk in keys], seconds)


def type_text(info, text):
    """Types characters as Unicode key events, so the result does not depend on the keyboard layout."""
    if not 0 < len(text) <= MAX_TYPED_CHARACTERS:
        raise ValueError(f'Type one to {MAX_TYPED_CHARACTERS} characters')
    if any(w.key_state(vk) & 0x8000 for vk in MODIFIER_KEYS):
        raise RuntimeError('A modifier is already held')
    for character in text:
        w.require_foreground(info)
        for release in (False, True):
            event = w.KeyInput(scan=ord(character), flags=UNICODE | (KEY_UP if release else 0))
            send(w.Input(type=1, data=w.InputData(key=event)))
        time.sleep(POLL_SECONDS)


def tap_alt():
    """Windows only lets the process that sent the last input event change the foreground window."""
    send(key_event(VK['alt']))
    send(key_event(VK['alt'], True))


def move_mouse(info, x, y):
    info = w.require_foreground(info)
    if w.key_state(VK_PAUSE) & 0x8000:
        raise RuntimeError('Pause fail-safe is active')
    if not (0 <= x < info['width'] and 0 <= y < info['height']):
        raise ValueError('Position is outside the inspected game client')
    if not w.cursor_position(info['x'] + x, info['y'] + y):
        raise C.WinError(C.get_last_error())


def click(info, x, y, button):
    info = w.require_foreground(info)
    if not (0 <= x < info['width'] and 0 <= y < info['height']):
        raise ValueError('Click is outside the inspected game client')
    vk, down, up = {'left': (1, 0x02, 0x04), 'right': (2, 0x08, 0x10)}[button]
    if any(w.key_state(key) & 0x8000 for key in {vk} | MODIFIER_KEYS):
        raise RuntimeError('Mouse button or modifier is already held')
    move_mouse(info, x, y)
    hold(info, [w.Input(type=0, data=w.InputData(mouse=w.MouseInput(flags=down)))],
         [w.Input(type=0, data=w.InputData(mouse=w.MouseInput(flags=up)))], 0.05)


def scroll(info, x, y, steps):
    if not -MAX_SCROLL_NOTCHES <= steps <= MAX_SCROLL_NOTCHES or steps == 0:
        raise ValueError('Scroll must be one to eight notches in either direction')
    if any(w.key_state(key) & 0x8000 for key in MODIFIER_KEYS):
        raise RuntimeError('A modifier is already held')
    move_mouse(info, x, y)
    w.require_foreground(info)
    WHEEL_DELTA, MOUSEEVENTF_WHEEL = 120, 0x0800
    send(w.Input(type=0, data=w.InputData(mouse=w.MouseInput(data=C.c_uint32(steps * WHEEL_DELTA).value, flags=MOUSEEVENTF_WHEEL))))
