#!/bin/sh
# Path of Exile 2 with the accessibility mod under Proton (Linux, Steam Deck), from Steam's Play button.
# In Steam: Path of Exile 2 > Properties > General > Launch options (install-linux.sh sets it for you):
#   sh "/path/to/Poe2Access/poe2access-proton.sh" %command%
#
# Wine loads PRISM's speech bridges (prism_*_bridge.dll.so) by name from WINEDLLPATH, which it reads
# when a process starts, so it is set here: protontricks-launch does not pass it through (the D2R
# mod's tester under Proton, 2026-11-09). Steam's own command is not run as it is: this runs Proton
# itself on Poe2Access.exe in the game's prefix, with the environment Steam gives a launch option, and
# Poe2Access.exe starts the game from the path and arguments Steam gave, as its launch option does on
# Windows (docs/proton.md). Proton runs outside Steam's runtime container, so the bridges find the
# host's libspeechd and the mod's dialogs reach the host's zenity. The zip keeps no execute bit, hence
# "sh" in the launch option.
#
# Run without Steam's command (from a terminal or a shortcut) it asks Steam to start the game, which
# runs it again through the launch option.
here=$(cd "$(dirname "$0")" && pwd) || exit 1
app_id=2694490
log="$here/app/proton-launch.log"
state="${XDG_RUNTIME_DIR:-/tmp}/poe2access-proton.env"

# Steam shows nothing a launch option prints, so the log keeps it and errors are also said aloud.
if [ -f "$log" ] && [ "$(wc -c <"$log")" -gt 1000000 ]; then
	mv -f "$log" "$log.old"
fi
note() {
	printf '%s %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*" >>"$log" 2>/dev/null
}
say() {
	note "$*"
	printf '%s\n' "$*" >&2
	if command -v spd-say >/dev/null 2>&1; then
		spd-say -w "Poe2Access: $*" >/dev/null 2>&1
	fi
}
fail() {
	say "$*"
	exit 1
}
quote() { # a value as a single-quoted sh word
	printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"
}

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

steam_root=${STEAM_COMPAT_CLIENT_INSTALL_PATH:-$HOME/.local/share/Steam}

if [ -z "$game" ]; then
	# Started by hand: Steam starts the game, and with it this script again, when the launch option is set.
	if grep -qs "poe2access-proton.sh" "$steam_root"/userdata/*/config/localconfig.vdf 2>/dev/null; then
		note "Started without the game's command: asking Steam to start Path of Exile 2"
		if command -v steam >/dev/null 2>&1; then
			exec steam "steam://rungameid/$app_id"
		fi
		exec xdg-open "steam://rungameid/$app_id"
	fi
	fail "Set the game's launch option in Steam first: run sh \"$here/install-linux.sh\", or write sh \"$here/poe2access-proton.sh\" %command% in Path of Exile 2's launch options."
fi

note "--- start: $game $*"
proton=${proton:-${PROTON:-$steam_root/steamapps/common/Proton - Experimental/proton}}
if [ ! -f "$proton" ]; then
	fail "Proton not found ($proton): pick a Proton version for Path of Exile 2 in Steam, under Properties, Compatibility."
fi

STEAM_COMPAT_CLIENT_INSTALL_PATH=$steam_root
STEAM_COMPAT_DATA_PATH=${STEAM_COMPAT_DATA_PATH:-$steam_root/steamapps/compatdata/$app_id}
SteamAppId=${SteamAppId:-$app_id}
SteamGameId=${SteamGameId:-$app_id}
bridges="$here/app/Plugins/Poe2Accessibility/wine"
WINEDLLPATH="$bridges${WINEDLLPATH:+:$WINEDLLPATH}"
# .NET takes ICU for culture data, and Wine's icu.dll lacks most of it: Poe2Access.exe would stop at once ("Cannot get
# symbol u_charsToUChars from libicuuc"). Windows' own NLS calls, which Wine implements, serve instead.
DOTNET_SYSTEM_GLOBALIZATION_USENLS=1
export STEAM_COMPAT_CLIENT_INSTALL_PATH STEAM_COMPAT_DATA_PATH SteamAppId SteamGameId WINEDLLPATH DOTNET_SYSTEM_GLOBALIZATION_USENLS

[ -f "$here/Poe2Access.exe" ] || fail "Poe2Access.exe is missing from $here: unzip the whole mod there again."
[ -d "$bridges" ] || say "The speech bridges are missing ($bridges): the mod will be silent. Unzip the whole mod again."

# The mod reads the game's memory through Wine, which needs ptrace: Yama level 2 or 3 forbids it to ordinary users.
scope=$(cat /proc/sys/kernel/yama/ptrace_scope 2>/dev/null || echo 0)
if [ "$scope" -ge 2 ] 2>/dev/null; then
	say "The kernel forbids reading the game's memory (kernel.yama.ptrace_scope is $scope): the mod cannot follow the game. Set it to 1 with: sudo sysctl kernel.yama.ptrace_scope=1"
fi

# What poe2access-stop.sh needs to reach this Wine session.
{
	printf 'here=%s\n' "$(quote "$here")"
	printf 'proton=%s\n' "$(quote "$proton")"
	printf 'STEAM_COMPAT_CLIENT_INSTALL_PATH=%s\n' "$(quote "$STEAM_COMPAT_CLIENT_INSTALL_PATH")"
	printf 'STEAM_COMPAT_DATA_PATH=%s\n' "$(quote "$STEAM_COMPAT_DATA_PATH")"
	printf 'SteamAppId=%s\n' "$(quote "$SteamAppId")"
	printf 'SteamGameId=%s\n' "$(quote "$SteamGameId")"
} >"$state" 2>/dev/null

note "Proton: $proton; prefix: $STEAM_COMPAT_DATA_PATH; WINEDLLPATH=$WINEDLLPATH"

# The game's path as Wine names it: the prefix's Z: drive is the Linux root.
windows_game="Z:$(printf '%s' "$game" | tr '/' '\\')"
cd "$here" || exit 1
"$proton" waitforexitandrun ./Poe2Access.exe "$windows_game" "$@"
code=$?
note "Poe2Access.exe ended (exit code $code)"
rm -f "$state"
exit $code
