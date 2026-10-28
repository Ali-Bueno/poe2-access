<#
.SYNOPSIS
    Publishes a test version on GitHub: builds the package (New-TestPackage.ps1) and makes it the latest release, so the
    links in README.md always download the newest one:
    https://github.com/Ali-Bueno/poe2-access/releases/latest/download/Poe2Access.zip (and Poe2Access.7z).
.DESCRIPTION
    The assets keep the same names in every release; that is what keeps the "latest" links stable. The tag is the date
    (v2026.10.26), with -2, -3... for a second release the same day. The notes are -Notes, or else the subjects of the
    commits since the previous release. Refuses to run with uncommitted changes or unpushed commits, so a release is
    always the code on GitHub. Needs the GitHub CLI (gh) signed in.
#>
param(
    [string]$Notes = ''
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$output = Join-Path $repo 'dist\release'
$assets = 'Poe2Access.zip', 'Poe2Access.7z'

function Invoke-Git([string[]]$Arguments) {
    $result = & git -C $repo @Arguments
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed" }
    return $result
}

if (Invoke-Git @('status', '--porcelain')) { throw 'Uncommitted changes: commit and push them first.' }
& git -C $repo fetch --quiet
if ((Invoke-Git @('rev-parse', 'HEAD')) -ne (Invoke-Git @('rev-parse', '@{u}'))) { throw 'HEAD is not the pushed branch: push (or pull) first.' }

$tag = 'v{0:yyyy.MM.dd}' -f (Get-Date)
$taken = @(Invoke-Git @('ls-remote', '--tags', 'origin', "refs/tags/$tag*")) | ForEach-Object { ($_ -split 'refs/tags/')[1] }
for ($n = 2; $taken -contains $tag; $n++) { $tag = 'v{0:yyyy.MM.dd}-{1}' -f (Get-Date), $n }

if ($Notes.Length -eq 0) {
    $previous = & gh release view --repo Ali-Bueno/poe2-access --json tagName --jq .tagName 2>$null
    $range = if ($LASTEXITCODE -eq 0 -and $previous) { "$previous..HEAD" } else { 'HEAD~10..HEAD' }
    & git -C $repo fetch --quiet --tags
    $Notes = "Test version. Unzip it anywhere and run Poe2Access.exe (instructions in README.txt).`n`nChanges:`n" +
        ((Invoke-Git @('log', '--format=- %s', $range)) -join "`n")
}

if (Test-Path $output) { Remove-Item $output -Recurse -Force }
& (Join-Path $PSScriptRoot 'New-TestPackage.ps1') -Output $output -Version $tag
$built = Get-ChildItem $output -File | Where-Object { $_.Name -like 'Poe2Access-test-*' }
foreach ($file in $built) { Move-Item $file.FullName (Join-Path $output ('Poe2Access' + $file.Extension)) -Force }
# Relative to the repository: gh reads "path#label", and the repository's path holds a '#' (c#).
$files = $assets | Where-Object { Test-Path (Join-Path $output $_) } | ForEach-Object { "dist/release/$_" }
if (-not $files) { throw "No package in $output" }

$notesFile = Join-Path $output 'notes.md'
Set-Content -Path $notesFile -Value $Notes -Encoding utf8
Push-Location $repo
try {
    $created = & gh release create $tag @files --repo Ali-Bueno/poe2-access --target (Invoke-Git @('rev-parse', 'HEAD')) --title "Test version $tag" --notes-file $notesFile --latest 2>&1
}
finally {
    Pop-Location
}

if ($LASTEXITCODE -ne 0) { throw "gh release create failed: $created" }
Remove-Item $output -Recurse -Force
Write-Host "Released $tag. Always the newest: https://github.com/Ali-Bueno/poe2-access/releases/latest/download/Poe2Access.zip"
