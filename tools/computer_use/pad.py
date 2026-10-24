"""Virtual DualShock 4 (or Xbox 360) for PoE2 testing. `serve` plugs one pad in for the whole session; desktop.py sends it
bounded presses."""
import argparse
import json
import sys
import time
from multiprocessing.connection import Client, Listener

ADDRESS = r'\\.\pipe\poe2access-pad'
AUTHKEY = b'poe2access-pad'  # local named pipe; the key only keeps unrelated pipe clients out
MAX_HOLD_SECONDS = 2.0  # small observable steps, never unattended navigation
BUTTONS = dict(cross='CROSS', circle='CIRCLE', square='SQUARE', triangle='TRIANGLE',
               l1='SHOULDER_LEFT', r1='SHOULDER_RIGHT', l3='THUMB_LEFT', r3='THUMB_RIGHT',
               options='OPTIONS', share='SHARE')
TRIGGERS = dict(l2=('TRIGGER_LEFT', 'left_trigger'), r2=('TRIGGER_RIGHT', 'right_trigger'))
SPECIAL = dict(ps='PS', touchpad='TOUCHPAD')
DPAD = dict(up='NORTH', down='SOUTH', left='WEST', right='EAST')
_D = 0.7071
STICK_DIRECTIONS = {'up': (0.0, -1.0), 'down': (0.0, 1.0), 'left': (-1.0, 0.0), 'right': (1.0, 0.0),  # DS4 y grows downwards
                    'up-left': (-_D, -_D), 'up-right': (_D, -_D), 'down-left': (-_D, _D), 'down-right': (_D, _D)}
NAMES = sorted([*BUTTONS, *TRIGGERS, *SPECIAL, *(f'dpad-{name}' for name in DPAD)])


def check_seconds(seconds):
    if not 0 < seconds <= MAX_HOLD_SECONDS:
        raise ValueError(f'Hold must be within (0, {MAX_HOLD_SECONDS}] seconds')


def hold(pad, press, release, seconds):
    check_seconds(seconds)
    try:
        press()
        pad.update()
        time.sleep(seconds)
    finally:
        release()
        pad.update()


def press(pad, vg, name, seconds):
    if name in BUTTONS:
        button = getattr(vg.DS4_BUTTONS, 'DS4_BUTTON_' + BUTTONS[name])
        hold(pad, lambda: pad.press_button(button=button), lambda: pad.release_button(button=button), seconds)
    elif name in TRIGGERS:
        flag, axis = TRIGGERS[name]
        button = getattr(vg.DS4_BUTTONS, 'DS4_BUTTON_' + flag)
        set_axis = getattr(pad, axis)
        hold(pad, lambda: (pad.press_button(button=button), set_axis(255)),
             lambda: (pad.release_button(button=button), set_axis(0)), seconds)
    elif name in SPECIAL:
        button = getattr(vg.DS4_SPECIAL_BUTTONS, 'DS4_SPECIAL_BUTTON_' + SPECIAL[name])
        hold(pad, lambda: pad.press_special_button(special_button=button),
             lambda: pad.release_special_button(special_button=button), seconds)
    elif name.startswith('dpad-') and name[5:] in DPAD:
        direction = getattr(vg.DS4_DPAD_DIRECTIONS, 'DS4_BUTTON_DPAD_' + DPAD[name[5:]])
        none = vg.DS4_DPAD_DIRECTIONS.DS4_BUTTON_DPAD_NONE
        hold(pad, lambda: pad.directional_pad(direction=direction), lambda: pad.directional_pad(direction=none), seconds)
    else:
        raise ValueError(f'Unknown control {name}; use one of {", ".join(NAMES)}')


def _unit(text):
    """vec:X,Y as a unit stick vector (screen axes, y down)."""
    x, y = (float(part) for part in text.split(','))
    length = (x * x + y * y) ** 0.5
    if length == 0:
        raise ValueError('vec:X,Y must not be zero')
    return x / length, y / length


def stick(pad, side, direction, amount, seconds):
    if side not in ('left', 'right') or not (direction in STICK_DIRECTIONS or direction.startswith('vec:')) or not 0 < amount <= 1:
        raise ValueError('Use left|right, up|down|left|right or vec:X,Y (screen, y down) and an amount in (0, 1]')
    x, y = STICK_DIRECTIONS[direction] if direction in STICK_DIRECTIONS else _unit(direction[4:])
    move = getattr(pad, f'{side}_joystick_float')
    hold(pad, lambda: move(x_value_float=x * amount, y_value_float=y * amount),
         lambda: move(x_value_float=0.0, y_value_float=0.0), seconds)


def chord(pad, vg, name, side, direction):
    """Hold a button, flick a stick while it is held, then release both (the mod's L3 + right stick scanner)."""
    if name not in BUTTONS or side not in ('left', 'right') or direction not in STICK_DIRECTIONS:
        raise ValueError('Use a button name, left|right and up|down|left|right')
    button = getattr(vg.DS4_BUTTONS, 'DS4_BUTTON_' + BUTTONS[name])
    x, y = STICK_DIRECTIONS[direction]
    move = getattr(pad, f'{side}_joystick_float')
    try:
        pad.press_button(button=button)
        pad.update()
        time.sleep(0.15)
        move(x_value_float=x, y_value_float=y)
        pad.update()
        time.sleep(0.25)
        move(x_value_float=0.0, y_value_float=0.0)
        pad.update()
        time.sleep(0.15)
    finally:
        move(x_value_float=0.0, y_value_float=0.0)
        pad.release_button(button=button)
        pad.update()


class XboxAsDs4:
    """An Xbox 360 pad behind the DualShock calls above. Steam Input can show the game a virtual DualShock twice
    (itself and an XInput copy): character selection then waits for "P2 press A to join" and A selects nothing
    (2026-10-24). An XInput pad is seen once."""

    def __init__(self, vg):
        self.pad = vg.VX360Gamepad()
        x, b, d = vg.XUSB_BUTTON, vg.DS4_BUTTONS, vg.DS4_DPAD_DIRECTIONS
        self.buttons = {b.DS4_BUTTON_CROSS: x.XUSB_GAMEPAD_A, b.DS4_BUTTON_CIRCLE: x.XUSB_GAMEPAD_B,
                        b.DS4_BUTTON_SQUARE: x.XUSB_GAMEPAD_X, b.DS4_BUTTON_TRIANGLE: x.XUSB_GAMEPAD_Y,
                        b.DS4_BUTTON_SHOULDER_LEFT: x.XUSB_GAMEPAD_LEFT_SHOULDER,
                        b.DS4_BUTTON_SHOULDER_RIGHT: x.XUSB_GAMEPAD_RIGHT_SHOULDER,
                        b.DS4_BUTTON_THUMB_LEFT: x.XUSB_GAMEPAD_LEFT_THUMB, b.DS4_BUTTON_THUMB_RIGHT: x.XUSB_GAMEPAD_RIGHT_THUMB,
                        b.DS4_BUTTON_OPTIONS: x.XUSB_GAMEPAD_START, b.DS4_BUTTON_SHARE: x.XUSB_GAMEPAD_BACK}
        self.axis_flags = {b.DS4_BUTTON_TRIGGER_LEFT, b.DS4_BUTTON_TRIGGER_RIGHT}  # the trigger axes carry them
        self.special = {vg.DS4_SPECIAL_BUTTONS.DS4_SPECIAL_BUTTON_PS: x.XUSB_GAMEPAD_GUIDE}
        self.dpad = {d.DS4_BUTTON_DPAD_NORTH: x.XUSB_GAMEPAD_DPAD_UP, d.DS4_BUTTON_DPAD_SOUTH: x.XUSB_GAMEPAD_DPAD_DOWN,
                     d.DS4_BUTTON_DPAD_WEST: x.XUSB_GAMEPAD_DPAD_LEFT, d.DS4_BUTTON_DPAD_EAST: x.XUSB_GAMEPAD_DPAD_RIGHT}

    def _button(self, table, button, down):
        if button in self.axis_flags:
            return
        if button not in table:
            raise ValueError(f'{button} has no Xbox equivalent')
        (self.pad.press_button if down else self.pad.release_button)(button=table[button])

    def press_button(self, button):
        self._button(self.buttons, button, True)

    def release_button(self, button):
        self._button(self.buttons, button, False)

    def press_special_button(self, special_button):
        self._button(self.special, special_button, True)

    def release_special_button(self, special_button):
        self._button(self.special, special_button, False)

    def directional_pad(self, direction):
        for button in self.dpad.values():
            self.pad.release_button(button=button)
        if direction in self.dpad:
            self.pad.press_button(button=self.dpad[direction])

    def left_trigger(self, value):
        self.pad.left_trigger(value=value)

    def right_trigger(self, value):
        self.pad.right_trigger(value=value)

    def left_joystick_float(self, x_value_float, y_value_float):
        self.pad.left_joystick_float(x_value_float=x_value_float, y_value_float=-y_value_float)  # XInput y grows upwards

    def right_joystick_float(self, x_value_float, y_value_float):
        self.pad.right_joystick_float(x_value_float=x_value_float, y_value_float=-y_value_float)

    def reset(self):
        self.pad.reset()

    def update(self):
        self.pad.update()


def serve(xbox):
    import vgamepad as vg  # only the server needs the driver client
    pad = XboxAsDs4(vg) if xbox else vg.VDS4Gamepad()
    pad.reset()
    pad.update()
    print(json.dumps(dict(ok=True, serving=ADDRESS)), flush=True)
    with Listener(ADDRESS, family='AF_PIPE', authkey=AUTHKEY) as listener:
        while True:
            with listener.accept() as connection:
                request = connection.recv()
                try:
                    if request['action'] == 'quit':
                        connection.send(dict(ok=True))
                        return 0
                    if request['action'] == 'press':
                        press(pad, vg, request['name'], request['seconds'])
                    elif request['action'] == 'chord':
                        chord(pad, vg, request['name'], request['side'], request['direction'])
                    elif request['action'] == 'stick':
                        stick(pad, request['side'], request['direction'], request['amount'], request['seconds'])
                    else:
                        raise ValueError(f'Unknown action {request["action"]}')
                    connection.send(dict(ok=True))
                except (KeyError, ValueError, OSError) as error:
                    connection.send(dict(ok=False, error=str(error)))


def send(request):
    """Forward one request to the running server; raises when it is not running or refuses."""
    try:
        with Client(ADDRESS, family='AF_PIPE', authkey=AUTHKEY) as connection:
            connection.send(request)
            reply = connection.recv()
    except FileNotFoundError as error:
        raise RuntimeError('Pad server is not running; start `pad.py serve` first') from error
    if not reply['ok']:
        raise RuntimeError(reply['error'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['serve', 'quit'])
    parser.add_argument('--xbox', action='store_true', help='serve an Xbox 360 pad (seen once through XInput)')
    args = parser.parse_args()
    if args.action == 'serve':
        return serve(args.xbox)
    send(dict(action='quit'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
