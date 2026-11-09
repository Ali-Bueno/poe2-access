#!/bin/sh
# Path of Exile 2 with the accessibility mod under Proton (Linux, Steam Deck), from Steam's Play button.
# In Steam: Path of Exile 2 > Properties > General > Launch options:
#   sh "/path/to/Poe2Access/poe2access-proton.sh" %command%
#
# Wine loads PRISM's speech bridges (prism_*_bridge.dll.so) by name from WINEDLLPATH, which it reads
# when a process starts, so it is set here: protontricks-launch does not pass it through (the D2R
# mod's tester under Proton, 2026-11-09). Steam's own command is not run as it is: this runs Proton
# itself on Poe2Access.exe in the game's prefix, with the environment Steam gives a launch option, and
# Poe2Access.exe starts the game from the path and arguments Steam gave, as its launch option does on
# Windows (docs/proton.md). The zip keeps no execute bit, hence "sh" in the launch option.
here=$(cd "$(dirname "$0")" && pwd) || exit 1

# Steam's command: wrappers, the Proton it picked (the argument ending in /proton), the game's .exe, its arguments.
proton=
game=
while [ $# -gt 0 ]; do
	case $1 in
	*/proton) proton=$1 ;;
	*.exe | *.EXE)
		game=$1
		shift
		break
		;;
	esac
	shift
done
# "$@" now holds the game's own arguments.

if [ -z "$game" ]; then
	echo "Use this script as the game's launch option in Steam: sh \"$here/poe2access-proton.sh\" %command%" >&2
	exit 1
fi

steam_root=${STEAM_COMPAT_CLIENT_INSTALL_PATH:-$HOME/.local/share/Steam}
proton=${proton:-${PROTON:-$steam_root/steamapps/common/Proton - Experimental/proton}}
if [ ! -f "$proton" ]; then
	echo "Proton not found ($proton): pick a Proton version for the game in Steam" >&2
	exit 1
fi

STEAM_COMPAT_CLIENT_INSTALL_PATH=$steam_root
STEAM_COMPAT_DATA_PATH=${STEAM_COMPAT_DATA_PATH:-$steam_root/steamapps/compatdata/2694490}
SteamAppId=${SteamAppId:-2694490}
SteamGameId=${SteamGameId:-2694490}
WINEDLLPATH="$here/app/Plugins/Poe2Accessibility/wine${WINEDLLPATH:+:$WINEDLLPATH}"
export STEAM_COMPAT_CLIENT_INSTALL_PATH STEAM_COMPAT_DATA_PATH SteamAppId SteamGameId WINEDLLPATH

# The game's path as Wine names it: the prefix's Z: drive is the Linux root.
windows_game="Z:$(printf '%s' "$game" | tr '/' '\\')"
cd "$here" || exit 1
exec "$proton" waitforexitandrun ./Poe2Access.exe "$windows_game" "$@"
