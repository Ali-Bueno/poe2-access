<#
.SYNOPSIS
    One list of what a game patch may have broken: the exe on disk, then the logs of the last start.

.DESCRIPTION
    The runbook's checks in one command (docs/patch-day.md, sections 2 and 3): keeps a copy of the new build's exe,
    judges the exe (Check-GameExe.ps1 and exe_reader's second opinion) and reads, in each log given, what the mod wrote
    since it last attached to the game. Nothing is attached to and the game is not started. It exits 0 when nothing
    needs a look.

.PARAMETER Game
    The game's folder.

.PARAMETER Builds
    Where each build's exe is kept, a folder per Steam build id; no copy is made when it does not exist.

.PARAMETER Logs
    The plugin logs to read; by default the development host's and the packaged copy's under dist.

.EXAMPLE
    pwsh tools/Check-Patch.ps1
#>
param(
    [string]$Game = 'D:\games\steam\steamapps\common\Path of Exile 2',
    [string]$Builds = 'D:\code\decompilers\projects\poe2-research\builds',
    [string[]]$Logs
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $Game 'PathOfExileSteam.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "No game exe at $exe"
}

if (-not $Logs) {
    $Logs = @(
        (Join-Path $root 'host\Poe2Access\bin\Release\net10.0-windows\win-x64\Plugins\Poe2Accessibility\logs\accessibility.log'),
        (Join-Path $root 'dist\Poe2Access\app\Plugins\Poe2Accessibility\logs\accessibility.log')
    )
}

$toLook = [System.Collections.Generic.List[string]]::new()

# The build: Steam's manifest of the game names it (the folder is steamapps\common\<game>).
$manifest = Join-Path (Split-Path -Parent (Split-Path -Parent $Game)) 'appmanifest_2694490.acf'
$build = if ((Test-Path -LiteralPath $manifest) -and ((Get-Content -LiteralPath $manifest -Raw) -match '"buildid"\s+"(\d+)"')) { $Matches[1] } else { $null }
$size = (Get-Item -LiteralPath $exe).Length
Write-Host "Build $($build ?? 'unknown'), exe $size bytes, written $((Get-Item -LiteralPath $exe).LastWriteTimeUtc.ToString('yyyy-MM-dd HH:mm')) UTC"
if ($build -and (Test-Path -LiteralPath $Builds)) {
    $kept = Join-Path (Join-Path $Builds $build) 'PathOfExileSteam.exe'
    if (Test-Path -LiteralPath $kept) {
        Write-Host "  kept already: $kept"
    }
    else {
        New-Item -ItemType Directory -Force (Split-Path -Parent $kept) | Out-Null
        Copy-Item -LiteralPath $exe -Destination $kept
        Write-Host "  copy kept: $kept"
    }
}

# The exe on disk: every line that is not right, then the tool's own count.
$judged = & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Check-GameExe.ps1') $exe 2>&1 | ForEach-Object { "$_" }
$exeCode = $LASTEXITCODE
$notRight = @($judged | Where-Object { $_ -match '^(MOVED|UNREAD|WRONG)\b' -or $_ -match 'pattern.*(not|no match|twice)' })
Write-Host "Exe: $(($judged | Where-Object { $_ -match 'rules right' } | Select-Object -Last 1) ?? 'no rule count'); exit code $exeCode"
$notRight | ForEach-Object { Write-Host "  $_" }
if ($exeCode -ne 0) {
    $toLook.Add('the exe check (tools/Check-GameExe.ps1)')
}

$second = & py -I (Join-Path $PSScriptRoot 'exe_reader\reader.py') $exe 2>&1 | ForEach-Object { "$_" }
$matched = $second | Where-Object { $_ -match '^matched (\d+) of (\d+)' } | Select-Object -Last 1
Write-Host "Second opinion: $($matched ?? 'exe_reader gave no count')"
if (-not $matched -or $matched -notmatch '^matched (\d+) of \1\b') {
    $second | Where-Object { $_ -match '\bwant\b' -and $_ -notmatch '\bOK\s*$' } | ForEach-Object { Write-Host "  $_" }
    $toLook.Add('exe_reader (tools/exe_reader/reader.py)')
}

# The logs: what was written since the mod last attached to a game.
$signs = 'NOT FOUND', 'moved from', ' lost', 'FAILS', 'does not match the catalog', 'Aviso de compatibilidad', 'Compatibility warning', 'gone from the game''s files'
foreach ($log in $Logs) {
    if (-not (Test-Path -LiteralPath $log)) {
        Write-Host "Log not there: $log"
        continue
    }

    $lines = Get-Content -LiteralPath $log -Encoding utf8
    $attached = ($lines | Select-String -SimpleMatch 'Game build:' | Select-Object -Last 1)
    $since = if ($attached) { $lines[($attached.LineNumber - 1)..($lines.Count - 1)] } else { $lines }
    Write-Host "Log $log"
    Write-Host "  $($attached ? $attached.Line : 'no start in it: the whole log is read')"
    $found = 0
    foreach ($sign in $signs) {
        # "0 lost" is the sweep's own good news, and the game closing is no loss of a field.
        $hits = @($since | Where-Object { $_.Contains($sign) -and $_ -notmatch '\b0 lost\b|Game process lost' })
        if ($hits.Count -gt 0) {
            $found += $hits.Count
            Write-Host "  '$sign': $($hits.Count), first: $($hits[0])"
        }
    }

    if ($found -gt 0) {
        $toLook.Add("the log $log")
    }
    else {
        Write-Host '  nothing lost, moved or failing'
    }
}

if ($toLook.Count -eq 0) {
    Write-Host 'PASS: nothing needs a look. After the first start, run the dev commands structs (twice) and layout (docs/patch-day.md, section 3).'
    exit 0
}

Write-Host "LOOK AT: $($toLook -join '; ') (docs/patch-day.md, section 4)"
exit 1
