<#
.SYNOPSIS
    Judges a build of the game from its exe on disk, before the game is started: the static patterns, the code-field
    catalog and the semantic rules.

.DESCRIPTION
    Nothing is run or attached to. A pattern that no longer matches once, or a field that no longer reads, is named;
    a field that moved is followed by the mod at the start and is only listed. What to do with each: docs/patch-day.md.

.PARAMETER Exe
    The game's exe: the installed one after a patch, or a copy kept of an older build.

.EXAMPLE
    powershell -File tools/Check-GameExe.ps1 "D:\games\steam\steamapps\common\Path of Exile 2\PathOfExileSteam.exe"
#>
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Exe
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $Exe)) {
    throw "No such file: $Exe"
}

$tests = Join-Path $root 'tests\Poe2Accessibility.Tests'
$catalogs = Join-Path $root 'Poe2Accessibility\catalog'
dotnet run --project $tests -c Release -- exe $Exe $catalogs
exit $LASTEXITCODE
