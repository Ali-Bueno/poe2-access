<#
.SYNOPSIS
    The mod's changes to GameHelper2 (gamehelper.patch): the runtime layout searches that let the host survive a game patch
    (docs/compatibility.md). external\ is not in the repository, so the patch is what keeps them. Dot-source this file.
#>

$script:GameHelperPatch = Join-Path $PSScriptRoot 'gamehelper.patch'

function Test-GameHelperPatch([string]$Clone) {
    # Applied when it can be undone cleanly.
    & git -C $Clone apply --reverse --check --whitespace=nowarn $script:GameHelperPatch 2>$null
    return $LASTEXITCODE -eq 0
}

function Add-GameHelperPatch([string]$Clone) {
    if (Test-GameHelperPatch $Clone) { return }
    & git -C $Clone apply --whitespace=nowarn $script:GameHelperPatch
    if ($LASTEXITCODE -ne 0) {
        throw "gamehelper.patch does not apply to external\GameHelper2: upstream changed the lines it touches. Apply it by hand with 'git apply --reject', then run Update-GameHelper.ps1 -SavePatch."
    }
}

function Remove-GameHelperPatch([string]$Clone) {
    if (-not (Test-GameHelperPatch $Clone)) { return }
    & git -C $Clone apply --reverse --whitespace=nowarn $script:GameHelperPatch
    if ($LASTEXITCODE -ne 0) { throw 'Could not take gamehelper.patch out of external\GameHelper2.' }
}

function Save-GameHelperPatch([string]$Clone) {
    # New files must be seen by git diff without being staged for real.
    & git -C $Clone add --intent-to-add --all
    $diff = & git -C $Clone diff --binary
    if ($LASTEXITCODE -ne 0) { throw 'git diff failed in external\GameHelper2.' }
    & git -C $Clone reset --quiet
    [IO.File]::WriteAllText($script:GameHelperPatch, (($diff -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
    Write-Host "Saved $script:GameHelperPatch"
}
