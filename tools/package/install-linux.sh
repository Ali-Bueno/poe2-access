#!/bin/sh
# Sets up Path of Exile 2 Access on Linux (Steam with Proton, Steam Deck). Run it from the unzipped mod folder:
#   sh install-linux.sh             set Steam's launch option for the game and check speech
#   sh install-linux.sh --uninstall take the launch option out again (the mod's folder stays; delete it yourself)
#
# What it does:
#   1. finds Path of Exile 2 in your Steam libraries
#   2. checks what the mod needs: Python 3 (Proton's script), speech (Speech Dispatcher or Orca),
#      zenity or kdialog (questions and errors Orca can read) and the kernel's ptrace setting
#   3. sets the game's launch option in Steam to: sh "<this folder>/poe2access-proton.sh" %command%
#      (Steam must be closed for that: it rewrites its settings when it exits; otherwise it prints the steps)
#
# Text output only, one line per step, so it reads well with a screen reader.

here=$(cd "$(dirname "$0")" && pwd) || exit 1
# Lists below are one path per line; paths may hold spaces.
IFS='
'
app_id=2694490
script="$here/poe2access-proton.sh"
mode=set

say() { printf '%s\n' "$*"; }
die() {
	printf 'Error: %s\n' "$*" >&2
	exit 1
}

case ${1:-} in
"") ;;
--uninstall) mode=unset ;;
-h | --help)
	sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'
	exit 0
	;;
*) die "unknown option: $1 (see --help)" ;;
esac

[ -f "$script" ] || die "poe2access-proton.sh is not beside this script: unzip the whole mod and run install-linux.sh from its folder."
command -v python3 >/dev/null 2>&1 || die "Python 3 is required (Proton's own script runs on it). Install the python3 package."
chmod +x "$here"/*.sh 2>/dev/null

# --- Steam and the game ------------------------------------------------------

steam_roots() {
	for root in "$HOME/.local/share/Steam" "$HOME/.steam/steam" "$HOME/.steam/debian-installation" \
		"$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam" "$HOME/snap/steam/common/.local/share/Steam"; do
		[ -d "$root/steamapps" ] && readlink -f "$root"
	done | awk '!seen[$0]++'
}

game_dir=
for root in $(steam_roots); do
	for library in "$root" $(sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)".*/\1/p' "$root/steamapps/libraryfolders.vdf" 2>/dev/null); do
		if [ -f "$library/steamapps/appmanifest_$app_id.acf" ]; then
			dir=$(sed -n 's/^[[:space:]]*"installdir"[[:space:]]*"\(.*\)".*/\1/p' "$library/steamapps/appmanifest_$app_id.acf")
			game_dir="$library/steamapps/common/${dir:-Path of Exile 2}"
			break 2
		fi
	done
done

case $(steam_roots | head -n 1) in
*com.valvesoftware.Steam*) say "Note: Steam from Flatpak runs Proton in its sandbox; the mod is untested there and may stay silent." ;;
esac

if [ "$mode" = set ]; then
	if [ -n "$game_dir" ]; then
		say "Path of Exile 2 found: $game_dir"
		[ -f "$game_dir/PathOfExileSteam.exe" ] || say "Its PathOfExileSteam.exe is not there yet: let Steam finish installing the game."
	else
		say "Path of Exile 2 was not found in your Steam libraries. Install it in Steam; the launch option can still be set once it is."
	fi

	# --- What the mod needs on this system ----------------------------------

	if command -v spd-say >/dev/null 2>&1 || ldconfig -p 2>/dev/null | grep -q 'libspeechd\.so\.2'; then
		say "Speech Dispatcher found: the mod speaks through it (or through Orca, when Orca offers its D-Bus service)."
	elif pgrep -x orca >/dev/null 2>&1; then
		say "Orca is running, but Speech Dispatcher is not installed. Orca 47 and older cannot take speech from the mod: install speech-dispatcher."
	else
		say "Warning: no Speech Dispatcher found. Install the speech-dispatcher package, or the mod stays silent."
	fi

	if command -v zenity >/dev/null 2>&1 || command -v kdialog >/dev/null 2>&1; then
		say "zenity or kdialog found: the mod's questions (a new version) and errors open in windows Orca reads."
	else
		say "Warning: neither zenity nor kdialog is installed: the mod's questions and errors open in Wine windows, which Orca cannot read. Install zenity."
	fi

	scope=$(cat /proc/sys/kernel/yama/ptrace_scope 2>/dev/null || echo 0)
	if [ "$scope" -ge 2 ] 2>/dev/null; then
		say "Warning: kernel.yama.ptrace_scope is $scope, which stops the mod reading the game. Set it to 1:"
		say "  echo kernel.yama.ptrace_scope=1 | sudo tee /etc/sysctl.d/60-poe2access.conf && sudo sysctl --system"
	else
		say "The kernel lets the mod read the game (ptrace_scope $scope)."
	fi
fi

# --- Steam's launch option ---------------------------------------------------

# Steam keeps each game's launch options in userdata/<account>/config/localconfig.vdf. Prints "set", "already",
# "removed", "absent" (nothing of ours to remove) or "missing" (the game has no entry in that file yet).
edit_launch_option() { # mode localconfig.vdf
	python3 - "$1" "$2" "$app_id" "$script" <<'PY'
import re, shutil, sys

mode, path, app_id, script = sys.argv[1:5]
ours = 'sh "' + script + '" '
text = open(path, encoding='utf-8').read()

# Tokens of Valve's text KeyValues format: quoted strings and braces (comments skipped).
tokens = [(m.start(), m.end(), m.group(1), m.group(2))
          for m in re.finditer(r'"((?:[^"\\]|\\.)*)"|([{}])|//[^\n]*', text) if not m.group(0).startswith('//')]

want = ['userlocalconfigstore', 'software', 'valve', 'steam', 'apps', app_id]
stack, pending_key = [], None
apps_close = app_close = None
option = None  # (start, end, value) of LaunchOptions inside the game's block
i = 0
while i < len(tokens):
    start, end, string, brace = tokens[i]
    if brace == '{':
        stack.append(pending_key)
    elif brace == '}':
        keys = [k.lower() for k in stack]
        if keys == want:
            app_close = start
        elif keys == want[:-1]:
            apps_close = start
        stack.pop()
    else:
        nxt = tokens[i + 1] if i + 1 < len(tokens) else None
        if nxt and nxt[3] is None:  # a key followed by its value
            if [k.lower() for k in stack] == want and string.lower() == 'launchoptions':
                option = (nxt[0], nxt[1], nxt[2])
            i += 2
            continue
        pending_key = string
    i += 1

def unescape(value):
    return value.replace('\\"', '"').replace('\\\\', '\\')

def vdf(value):
    return '"' + value.replace('\\', '\\\\').replace('"', '\\"') + '"'

def indent_before(pos):
    return text[text.rfind('\n', 0, pos) + 1:pos]

# Any earlier "sh "<somewhere>/poe2access-proton.sh" " (the mod moved, or a second copy) comes out first.
earlier = re.compile(r'sh\s+"[^"]*poe2access-proton\.sh"\s*|sh\s+\S*poe2access-proton\.sh\s*')

def write(result):
    shutil.copy2(path, path + '.poe2access-backup')
    with open(path, 'w', encoding='utf-8') as f:
        f.write(result)

old = unescape(option[2]) if option else None
if mode == 'unset':
    if old is None or not earlier.search(old):
        print('absent'); sys.exit(0)
    new = earlier.sub('', old).strip()
    if new == '%command%':
        new = ''
    write(text[:option[0]] + vdf(new) + text[option[1]:])
    print('removed'); sys.exit(0)

if old is not None:
    if ours + '%command%' in old:
        print('already'); sys.exit(0)
    rest = earlier.sub('', old).strip()
    if '%command%' in rest:
        new = rest.replace('%command%', ours + '%command%', 1)
    else:  # empty, or bare game arguments
        new = (ours + '%command% ' + rest).strip()
    result = text[:option[0]] + vdf(new) + text[option[1]:]
elif app_close is not None:
    entry = indent_before(app_close) + '\t"LaunchOptions"\t\t' + vdf(ours + '%command%') + '\n'
    line_start = text.rfind('\n', 0, app_close) + 1
    result = text[:line_start] + entry + text[line_start:]
elif apps_close is not None:
    pad = indent_before(apps_close) + '\t'
    block = (pad + '"' + app_id + '"\n' + pad + '{\n' + pad + '\t"LaunchOptions"\t\t'
             + vdf(ours + '%command%') + '\n' + pad + '}\n')
    line_start = text.rfind('\n', 0, apps_close) + 1
    result = text[:line_start] + block + text[line_start:]
else:
    print('missing'); sys.exit(0)

write(result)
print('set')
PY
}

steam_runs() {
	pgrep -x steam >/dev/null 2>&1 || pgrep -x steamwebhelper >/dev/null 2>&1
}

manual_steps() {
	say "To set it by hand: in Steam, right-click Path of Exile 2, Properties, General, Launch options, and write:"
	say "  sh \"$script\" %command%"
}

configs=$(for root in $(steam_roots); do ls "$root"/userdata/*/config/localconfig.vdf 2>/dev/null; done)
done=0
if [ -z "$configs" ]; then
	say "Steam's settings were not found (start Steam and log in once), so the launch option was not set."
elif steam_runs; then
	say "Steam is running, so the launch option was not changed: Steam would overwrite it when it closes."
	say "Close Steam completely (Steam menu, Exit), then run this script again."
	[ "$mode" = set ] && manual_steps
else
	for config in $configs; do
		case $(edit_launch_option "$mode" "$config" 2>/dev/null || echo failed) in
		set) done=1; say "Launch option set for Path of Exile 2 (backup: $config.poe2access-backup)." ;;
		already) done=1; say "The launch option was already set." ;;
		removed) done=1; say "Launch option removed (backup: $config.poe2access-backup)." ;;
		absent) done=1; say "No launch option of the mod was set in $config." ;;
		missing) say "Path of Exile 2 has no entry in $config yet: start it once from Steam, close it, then run this again." ;;
		*) say "Could not change $config." ;;
		esac
	done
fi

if [ "$mode" = unset ]; then
	say "Done. Path of Exile 2 now starts without the mod."
	exit 0
fi

if [ "$done" -eq 1 ]; then
	say "Installation finished. Start Path of Exile 2 from Steam: the mod starts with it and says when it is ready."
	say "To stop the mod while the game runs: sh \"$here/poe2access-stop.sh\". The log is $here/app/Poe2Access.log."
else
	[ -n "$configs" ] && ! steam_runs && manual_steps
fi
