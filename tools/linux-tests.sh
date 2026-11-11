#!/bin/sh
# Builds the mod on Linux and runs the unit tests under Wine (Proton's), where the Windows-only parts run as they do
# for players under Proton. Native "dotnet run" on Linux fails the tests that build Windows paths.
#   sh tools/linux-tests.sh            build, then run the tests
#   sh tools/linux-tests.sh prism      also load the built prism.dll and say one line (needs Speech Dispatcher or Orca)
# Needs the .NET 10 SDK (dotnet on PATH, or ~/.dotnet) and Steam's Proton (PROTON_DIR overrides where it is).
set -eu
root=$(cd "$(dirname "$0")/.." && pwd)
export PATH="$HOME/.dotnet:$PATH" DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
[ -d "$HOME/.dotnet" ] && export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
proton_dir=${PROTON_DIR:-$HOME/.local/share/Steam/steamapps/common/Proton - Experimental}
wine="$proton_dir/files/bin/wine"
[ -x "$wine" ] || { echo "Proton's wine not found at $wine: set PROTON_DIR to a Proton folder" >&2; exit 1; }

out="$root/tests/Poe2Accessibility.Tests/bin/win-x64-wine"
plugin="$root/host/Poe2Access/bin/Release/net10.0-windows/win-x64/Plugins/Poe2Accessibility"
dotnet build "$root/Poe2Access.slnx" -c Release -p:EnableWindowsTargeting=true -nologo -v q
dotnet publish "$root/tests/Poe2Accessibility.Tests" -c Release -r win-x64 --self-contained -p:EnableWindowsTargeting=true -nologo -v q -o "$out"

# A prefix of its own, never a game's; Wine's ICU is unusable for .NET, Windows' NLS calls serve instead.
export WINEPREFIX="${WINEPREFIX:-${XDG_CACHE_HOME:-$HOME/.cache}/poe2access-tests-prefix}" WINEDEBUG=-all DOTNET_SYSTEM_GLOBALIZATION_USENLS=1
mkdir -p "$WINEPREFIX"
cd "$out"
"$wine" Poe2Accessibility.Tests.exe
if [ "${1:-}" = prism ]; then
	WINEDLLPATH="$plugin/wine" "$wine" Poe2Accessibility.Tests.exe prism "Z:$(printf '%s' "$plugin" | tr '/' '\\')"
fi
