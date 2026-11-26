#!/bin/sh
# (Re)starts the mod under Proton while the game keeps running, as "Poe2Access.exe restart" does on Windows:
#   sh "/path/to/Poe2Access/poe2access-restart.sh"
# after poe2access-stop.sh, with the files just replaced. It runs until stopped (the terminal stays busy,
# as a Windows restart's console does); "setsid nohup sh poe2access-restart.sh &" detaches it.
# WINEDLLPATH must name the speech bridges or PRISM finds no backend and the mod is mute (2026-11-25: three
# agent restarts by hand, each mute, where this was missed): the session record carries it from the launch,
# else the packaged bridge folder beside this script stands in.
here=$(cd "$(dirname "$0")" && pwd) || exit 1
state="${XDG_RUNTIME_DIR:-$here/app}/poe2access-proton.env"
if [ ! -f "$state" ]; then
	echo "Poe2Access is not running under Proton: start the game from Steam first." >&2
	exit 1
fi

# The record is run as sh below: only one this user wrote.
if [ ! -O "$state" ]; then
	echo "Error: $state is not yours; it was not used." >&2
	exit 1
fi

script_here=$here
. "$state"

# The record's own "here=" names the folder the game was launched from, which may be an older copy:
# this script's folder is the one whose files were just replaced, and its bridges come first.
here=$script_here
if [ -z "$STEAM_COMPAT_DATA_PATH" ] || [ ! -f "$proton" ] || [ ! -f "$here/Poe2Access.exe" ]; then
	echo "Error: the Proton session record is incomplete or stale; start the game from Steam first." >&2
	exit 1
fi

WINEDLLPATH="$here/app/Plugins/Poe2Accessibility/wine${WINEDLLPATH:+:$WINEDLLPATH}"
DOTNET_SYSTEM_GLOBALIZATION_USENLS=1 # Wine has no usable ICU (see poe2access-proton.sh)
export STEAM_COMPAT_CLIENT_INSTALL_PATH STEAM_COMPAT_DATA_PATH SteamAppId SteamGameId WINEDLLPATH DOTNET_SYSTEM_GLOBALIZATION_USENLS
cd "$here" || exit 1
"$proton" runinprefix ./Poe2Access.exe restart
