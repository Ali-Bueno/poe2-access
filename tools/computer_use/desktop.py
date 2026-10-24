"""Local computer-use CLI for PoE2. Inspect a screenshot or the plugin log before sending input."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import sys
import time

import capture
import controls
import pad
import win32 as w

REPOSITORY = Path(__file__).resolve().parents[2]
PLUGIN_LOG = REPOSITORY / 'external/GameHelper2/GameHelper/bin/Release/net10.0-windows/win-x64/Plugins/Poe2Accessibility/logs/accessibility.log'
MAX_WAIT_SECONDS = 5.0  # long enough for a screen transition, short enough to keep steps observable
MAX_LOG_LINES = 60  # keep one step's log excerpt readable


def log_size(path):
    try:
        return path.stat().st_size
    except FileNotFoundError:
        return 0


def new_log_lines(path, start):
    """Plugin log lines written since `start` (a byte offset), oldest first."""
    if log_size(path) <= start:
        return []
    with path.open('rb') as stream:
        stream.seek(start)
        lines = stream.read().decode('utf-8', errors='replace').splitlines()
    return lines[-MAX_LOG_LINES:]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pid', type=int, help='Required for all actions except list')
    parser.add_argument('--output-dir', type=Path, default=Path(os.environ['LOCALAPPDATA']) / 'Poe2Access' / 'computer-use')
    parser.add_argument('--log', type=Path, default=PLUGIN_LOG, help='Plugin log whose new lines each action reports')
    parser.add_argument('--wait', type=float, default=0.6, help='Seconds to wait after the input before reading the log')
    parser.add_argument('--capture', action='store_true', help='Also save a screenshot after the wait')
    parser.add_argument('--scale', type=int, choices=[1, 2, 4], default=2, help='Screenshot size divisor; multiply image pixels by it for client pixels')
    sub = parser.add_subparsers(dest='action', required=True)
    for action in ['list', 'focus', 'capture']:
        sub.add_parser(action)
    button = sub.add_parser('pad', help='Press one virtual DualShock 4 control')
    button.add_argument('name', choices=pad.NAMES)
    button.add_argument('--seconds', type=float, default=0.1)
    joystick = sub.add_parser('stick', help='Push a virtual stick, then centre it')
    joystick.add_argument('side', choices=['left', 'right'])
    joystick.add_argument('direction', help='up, down, left, right, up-left... or vec:X,Y (screen axes, y down)')
    joystick.add_argument('--amount', type=float, default=1.0)
    joystick.add_argument('--seconds', type=float, default=0.15)
    flick = sub.add_parser('chord', help='Hold a button and flick a stick meanwhile (mod scanner: l3 right up)')
    flick.add_argument('name', choices=sorted(pad.BUTTONS))
    flick.add_argument('side', choices=['left', 'right'])
    flick.add_argument('direction', choices=list(pad.STICK_DIRECTIONS))
    key = sub.add_parser('key')
    key.add_argument('keys', help='For example escape or alt+enter')
    key.add_argument('--seconds', type=float, default=0.08)
    typing = sub.add_parser('type', help='Type text into the focused game field; only its length is logged')
    typing.add_argument('text')
    mouse = sub.add_parser('click')
    mouse.add_argument('x', type=int)
    mouse.add_argument('y', type=int)
    mouse.add_argument('--button', choices=['left', 'right'], default='left')
    move = sub.add_parser('move')
    move.add_argument('x', type=int)
    move.add_argument('y', type=int)
    wheel = sub.add_parser('scroll')
    wheel.add_argument('x', type=int)
    wheel.add_argument('y', type=int)
    wheel.add_argument('steps', type=int)
    sub.add_parser('log', help='Only wait, then report new plugin log lines')
    args = parser.parse_args()
    if args.action not in ('list', 'log') and args.pid is None:
        parser.error('--pid is required for a targeted action')
    if not 0 <= args.wait <= MAX_WAIT_SECONDS:
        parser.error(f'--wait must be within [0, {MAX_WAIT_SECONDS}]')
    result = dict(time=datetime.now(timezone.utc).isoformat(), action=args.action)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    log_start = log_size(args.log)
    try:
        if args.action == 'list':
            result['windows'] = w.game_windows()
        elif args.action == 'log':
            time.sleep(args.wait)
        else:
            info = w.target(args.pid)
            result['window'] = info
            if args.action == 'focus':
                if info['minimized']:
                    w.show_window(info['hwnd'], 9)  # SW_RESTORE
                controls.tap_alt()  # the last input event lets this process take the foreground
                w.set_foreground(info['hwnd'])
                time.sleep(0.15)
                result['window'] = w.require_foreground(info)
            elif args.action == 'capture':
                pass
            elif args.action == 'pad':
                w.require_foreground(info)
                result.update(name=args.name, seconds=args.seconds)
                pad.send(dict(action='press', name=args.name, seconds=args.seconds))
            elif args.action == 'stick':
                w.require_foreground(info)
                result.update(side=args.side, direction=args.direction, amount=args.amount, seconds=args.seconds)
                pad.send(dict(action='stick', side=args.side, direction=args.direction,
                              amount=args.amount, seconds=args.seconds))
            elif args.action == 'chord':
                w.require_foreground(info)
                result.update(name=args.name, side=args.side, direction=args.direction)
                pad.send(dict(action='chord', name=args.name, side=args.side, direction=args.direction))
            elif args.action == 'key':
                result.update(keys=args.keys, seconds=args.seconds)
                controls.press_keys(info, args.keys, args.seconds)
            elif args.action == 'type':
                result.update(characters=len(args.text))
                controls.type_text(info, args.text)
            elif args.action == 'click':
                result.update(x=args.x, y=args.y, button=args.button)
                controls.click(info, args.x, args.y, args.button)
            elif args.action == 'move':
                result.update(x=args.x, y=args.y)
                controls.move_mouse(info, args.x, args.y)
            elif args.action == 'scroll':
                result.update(x=args.x, y=args.y, steps=args.steps)
                controls.scroll(info, args.x, args.y, args.steps)
            if args.action not in ('focus', 'capture'):
                time.sleep(args.wait)
            if args.action == 'capture' or args.capture:
                stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S-%f')
                result['capture'] = capture.screenshot(info, args.output_dir / f'{stamp}.png', args.scale)
        result['ok'] = True
    except (OSError, RuntimeError, ValueError) as error:
        result.update(ok=False, error=str(error))
    if args.action not in ('list', 'capture'):
        result['log'] = new_log_lines(args.log, log_start)
    with (args.output_dir / 'actions.jsonl').open('a', encoding='utf-8') as stream:
        stream.write(json.dumps(result, ensure_ascii=False) + '\n')
    print(json.dumps(result, ensure_ascii=False, indent=1))
    return 0 if result['ok'] else 1


if __name__ == '__main__':
    sys.exit(main())
