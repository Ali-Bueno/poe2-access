"""Development launcher: starts PoE2 and drives Poe2Access.exe to (re)start or stop GameHelper."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time

import win32 as w

REPOSITORY = Path(__file__).resolve().parents[2]
HOST_DIRECTORY = REPOSITORY / 'external/GameHelper2/GameHelper/bin/Release/net10.0-windows/win-x64'
PLUGIN_LOG = HOST_DIRECTORY / 'Plugins/Poe2Accessibility/logs/accessibility.log'
STEAM_RUN_URL = 'steam://rungameid/2694490'  # Path of Exile 2 Steam app id
GAME_START_SECONDS = 240  # first start compiles shaders
HOST_START_SECONDS = 60
POLL_SECONDS = 0.5
LAUNCHER = HOST_DIRECTORY / 'Poe2Access.exe'  # tools/Poe2AccessLauncher, deployed beside GameHelper.exe


def wait_for(condition, seconds, what):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        value = condition()
        if value:
            return value
        time.sleep(POLL_SECONDS)
    raise RuntimeError(f'Timed out waiting for {what}')


def host_processes():
    """GameHelper runs under a random executable name, so match every process image in the host folder."""
    output = subprocess.run(['powershell', '-NoProfile', '-Command',
                             'Get-Process | Where-Object Path | Select-Object Id,Path | ConvertTo-Json'],
                            capture_output=True, text=True, check=True).stdout
    rows = json.loads(output or '[]')
    rows = rows if isinstance(rows, list) else [rows]
    folder = str(HOST_DIRECTORY.resolve()).lower()
    return [row for row in rows if str(Path(row['Path']).parent).lower() == folder]


def start_game():
    windows = w.game_windows()
    if not windows:
        os.startfile(STEAM_RUN_URL)
        windows = wait_for(w.game_windows, GAME_START_SECONDS, 'the game window')
    return windows[0]


def run_launcher(command):
    """Runs Poe2Access.exe <command>; on failure it shows a message box, so the wait is bounded."""
    try:
        done = subprocess.run([str(LAUNCHER), command], cwd=HOST_DIRECTORY, timeout=HOST_START_SECONDS)
    except subprocess.TimeoutExpired:
        raise RuntimeError(f'Poe2Access.exe {command} did not finish; see Poe2Access.log') from None
    if done.returncode:
        raise RuntimeError(f'Poe2Access.exe {command} failed ({done.returncode}); see Poe2Access.log')


def stop_host():
    """GameHelper locks the deployed plugin DLL, so stop it before every plugin build."""
    stopped = [row['Id'] for row in host_processes()]
    run_launcher('stop')
    wait_for(lambda: not host_processes(), HOST_START_SECONDS, 'GameHelper to exit')
    return stopped


def restart_host():
    log_start = PLUGIN_LOG.stat().st_size if PLUGIN_LOG.exists() else 0
    run_launcher('restart')
    host = wait_for(host_processes, HOST_START_SECONDS, 'GameHelper')

    def enabled_lines():
        if not PLUGIN_LOG.exists() or PLUGIN_LOG.stat().st_size <= log_start:
            return None
        with PLUGIN_LOG.open('rb') as stream:
            stream.seek(log_start)
            lines = stream.read().decode('utf-8', errors='replace').splitlines()
        return lines if any('Attached to game' in line or 'game open: True' in line for line in lines) else None

    return dict(host=host, log=wait_for(enabled_lines, HOST_START_SECONDS, 'the plugin to attach')[-8:])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['game', 'host', 'stop', 'all'])
    args = parser.parse_args()
    result = dict(action=args.action)
    try:
        if args.action in ('game', 'all'):
            result['game'] = start_game()
        if args.action == 'stop':
            result['stopped'] = stop_host()
        if args.action in ('host', 'all'):
            result.update(restart_host())
        result['ok'] = True
    except (OSError, RuntimeError, subprocess.SubprocessError) as error:
        result.update(ok=False, error=str(error))
    print(json.dumps(result, ensure_ascii=False, indent=1))
    return 0 if result['ok'] else 1


if __name__ == '__main__':
    sys.exit(main())
