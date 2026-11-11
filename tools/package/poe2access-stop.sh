#!/bin/sh
# Stops the mod running under Proton, as "Stop the mod.bat" does on Windows; the game keeps running.
#   sh "/path/to/Poe2Access/poe2access-stop.sh"
# It runs "Poe2Access.exe stop" in the game's Wine session (Proton's runinprefix: "run" goes through Proton's steam.exe,
# which waits for the game to close), with what poe2access-proton.sh recorded when it started.
here=$(cd "$(dirname "$0")" && pwd) || exit 1
state="${XDG_RUNTIME_DIR:-$here/app}/poe2access-proton.env"
if [ ! -f "$state" ]; then
	echo "Poe2Access is not running under Proton."
	exit 0
fi

# The record is run as sh below: only one this user wrote.
if [ ! -O "$state" ]; then
	echo "Error: $state is not yours; it was not used." >&2
	exit 1
fi

. "$state"
DOTNET_SYSTEM_GLOBALIZATION_USENLS=1 # Wine has no usable ICU (see poe2access-proton.sh)
export STEAM_COMPAT_CLIENT_INSTALL_PATH STEAM_COMPAT_DATA_PATH SteamAppId SteamGameId DOTNET_SYSTEM_GLOBALIZATION_USENLS
cd "$here" || exit 1
"$proton" runinprefix ./Poe2Access.exe stop
echo "Poe2Access stopped."
