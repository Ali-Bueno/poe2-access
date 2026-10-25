<#
.SYNOPSIS
    Brings GameHelper2 (external\GameHelper2) up to date after a game patch: fetches upstream, lists its new commits,
    fast-forwards the clone, rebuilds the host and the plugin and runs the tests. Then start the host, run the "layout"
    dev command in game (docs/compatibility.md) and package with New-TestPackage.ps1 (or pass -Package).
.DESCRIPTION
    Refuses to pull over local changes in the clone: the mod keeps none there (what it needs from GameHelper is read at
    run time or reimplemented). Run with GameHelper stopped: both builds deploy into its folder.
#>
param(
    # Only list the new upstream commits.
    [switch]$CheckOnly,
    # Also build the test package once everything passed.
    [switch]$Package
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$clone = Join-Path $repo 'external\GameHelper2'
$devHost = Join-Path $clone 'GameHelper\bin\Release\net10.0-windows\win-x64'

function Invoke-Native([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$Command $($Arguments -join ' ') failed" }
}

if (-not (Test-Path (Join-Path $clone '.git'))) {
    throw "No GameHelper2 clone at $clone (git clone https://github.com/Gordin/GameHelper2 external\GameHelper2)"
}

Invoke-Native git @('-C', $clone, 'fetch', '--quiet', 'origin')
$new = @(git -C $clone log --oneline 'HEAD..@{u}')
if ($new.Count -eq 0) {
    Write-Host "GameHelper2 is up to date ($(git -C $clone log --oneline -1))."
} else {
    Write-Host "New GameHelper2 commits upstream:"
    $new | ForEach-Object { Write-Host "  $_" }
}

if ($CheckOnly) { return }

if ($new.Count -gt 0) {
    if (git -C $clone status --porcelain) { throw 'external\GameHelper2 has local changes: commit or stash them first.' }
    Invoke-Native git @('-C', $clone, 'merge', '--ff-only', '--quiet', '@{u}')
}

# The launcher starts the host under a renamed copy of GameHelper.exe, so it is found by its folder, not its name.
$running = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($devHost, [System.StringComparison]::OrdinalIgnoreCase) }
if ($running) { throw "Stop GameHelper first (running from its build folder: $($running.ProcessName -join ', '))." }

Invoke-Native dotnet @('build', (Join-Path $clone 'GameHelper\GameHelper.csproj'), '-c', 'Release', '-nologo', '-v', 'q')
Invoke-Native dotnet @('build', (Join-Path $repo 'Poe2Accessibility'), '-c', 'Release', '-nologo', '-v', 'q')
Invoke-Native dotnet @('run', '--project', (Join-Path $repo 'tests\Poe2Accessibility.Tests'), '-c', 'Release')

if ($Package) {
    & (Join-Path $PSScriptRoot 'New-TestPackage.ps1')
}

Write-Host 'Next: start the host (Poe2Access.exe or tools/computer_use/launch.py host), log in, enter an area and write "layout"'
Write-Host 'to Plugins\Poe2Accessibility\debug\command.txt; every part should read "ok" or "moved" (docs/compatibility.md).'
