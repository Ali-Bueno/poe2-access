"""Virtual DualShock 4 for PoE2 testing. `serve` plugs one pad in for the whole session; desktop.py sends it bounded presses."""
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


def stick(pad, side, direction, amount, seconds):
    if side not in ('left', 'right') or direction not in STICK_DIRECTIONS or not 0 < amount <= 1:
        raise ValueError('Use left|right, up|down|left|right and an amount in (0, 1]')
    x, y = STICK_DIRECTIONS[direction]
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


def serve():
    import vgamepad as vg  # only the server needs the driver client
    pad = vg.VDS4Gamepad()
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
    args = parser.parse_args()
    if args.action == 'serve':
        return serve()
    send(dict(action='quit'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
