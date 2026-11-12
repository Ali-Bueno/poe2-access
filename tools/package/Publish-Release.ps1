<#
.SYNOPSIS
    Publishes a test version on GitHub as the latest release, so the links in README.md always download the newest one:
    https://github.com/Ali-Bueno/poe2-access/releases/latest/download/Poe2Access.zip (and Poe2Access.7z).
.DESCRIPTION
    Refuses unless the tree is main, committed and pushed, gh is signed in and there are commits since the latest release.
    Then builds and checks everything (the steps of tools/build.ps1, tests and phrases included), builds the package with
    the tag in app\version.txt, and publishes Poe2Access.zip, Poe2Access.7z and release.json under the tag. The assets keep
    the same names in every release (that keeps the "latest" links stable); release.json (tag, the zip's SHA-256, notes) is
    what the installed mod reads to update (ReleaseManifest.cs). The tag is the date, v2026.10.26, with -2, -3... for a
    later release the same day (ReleaseTag.cs parses it). The notes are -Notes, or else the subjects of the commits since
    the previous release.
    -DryRun does all of it except creating the tag and the release: it says what it would refuse, prints the tag, the
    assets and the notes, and leaves the files in dist\release.
#>
param(
    [string]$Notes = '',
    [switch]$DryRun
)

. (Join-Path $PSScriptRoot '..\BuildSteps.ps1')
$repository = 'Ali-Bueno/poe2-access' # the repository the installed mod updates from (ReleaseManifest.cs)
$output = Join-Path $RepoRoot 'dist\release'

function Invoke-Git([string[]]$Arguments, [switch]$AllowFailure) {
    Push-Location $RepoRoot
    try { Invoke-Native git $Arguments -AllowFailure:$AllowFailure } finally { Pop-Location }
}

# A tag as ReleaseTag.Parse reads it: year, month, day, and the release's number that day (1 when unnumbered).
function ConvertTo-TagVersion([string]$Tag) {
    if ($Tag -notmatch '^v(\d{4})\.(\d{2})\.(\d{2})(?:-(\d+))?$') { return $null }
    $number = if ($Matches[4]) { [int]$Matches[4] } else { 1 }
    return [version]::new([int]$Matches[1], [int]$Matches[2], [int]$Matches[3], $number)
}

try {
    # Everything that makes a release refuse is listed at once; a dry run lists it and goes on.
    Start-Step 'checks'
    $blockers = @()
    Invoke-Native gh @('auth', 'status') -AllowFailure | Out-Null
    if ($LASTEXITCODE -ne 0) { $blockers += 'gh is not signed in (gh auth login).' }
    $branch = (Invoke-Git @('rev-parse', '--abbrev-ref', 'HEAD')) | Select-Object -Last 1
    if ($branch -ne 'main') { $blockers += "the checkout is on $branch, not main." }
    $dirty = @(Invoke-Git @('status', '--porcelain'))
    if ($dirty) {
        $names = $dirty | Select-Object -First 10 | ForEach-Object { $_.Substring(3) }
        $blockers += "$($dirty.Count) uncommitted files: $($names -join ', ')$(if ($dirty.Count -gt 10) { ', ...' })."
    }
    Invoke-Git @('fetch', '--quiet', '--tags', 'origin') | Out-Null
    $upstream = @(Invoke-Git @('rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{u}') -AllowFailure)
    if ($LASTEXITCODE -ne 0) {
        $blockers += "$branch has no upstream branch: push it first."
    }
    else {
        $ahead, $behind = ((Invoke-Git @('rev-list', '--left-right', '--count', 'HEAD...@{u}')) | Select-Object -Last 1) -split '\s+'
        if ([int]$ahead -gt 0) { $blockers += "$ahead commits are not pushed to $($upstream[-1])." }
        if ([int]$behind -gt 0) { $blockers += "$($upstream[-1]) has $behind commits not pulled." }
    }

    Start-Step 'tag'
    $latest = @(Invoke-Native gh @('release', 'view', '--repo', $repository, '--json', 'tagName', '--jq', '.tagName') -AllowFailure)
    $latest = if ($LASTEXITCODE -eq 0) { $latest[-1].Trim() } else { '' }
    $tag = 'v{0:yyyy.MM.dd}' -f (Get-Date)
    $taken = @(Invoke-Git @('ls-remote', '--tags', 'origin', "refs/tags/$tag*")) | ForEach-Object { ($_ -split 'refs/tags/')[1] }
    for ($n = 2; $taken -contains $tag; $n++) { $tag = 'v{0:yyyy.MM.dd}-{1}' -f (Get-Date), $n }
    $version = ConvertTo-TagVersion $tag
    if (-not $version) { throw "$tag is not a tag the updater can read." }
    if ($latest) {
        $latestVersion = ConvertTo-TagVersion $latest
        if ($latestVersion -and $version -le $latestVersion) { $blockers += "$tag would not be newer than the latest release $latest (check the clock)." }
        $since = @(Invoke-Git @('rev-list', '--count', "$latest..HEAD") -AllowFailure)
        if ($LASTEXITCODE -eq 0 -and $since[-1] -eq '0') { $blockers += "nothing was committed since $latest." }
    }
    $commit = (Invoke-Git @('rev-parse', 'HEAD')) | Select-Object -Last 1

    if ($blockers) {
        $blockers | ForEach-Object { Write-Host "Refuses: $_" }
        if (-not $DryRun) { throw "$($blockers.Count) reasons above; nothing was built or published." }
    }

    if (-not $Notes) {
        $range = if ($latest) { "$latest..HEAD" } else { 'HEAD~10..HEAD' }
        $Notes = "Test version. Unzip it anywhere and run Poe2Access.exe (instructions in README.txt).`n`nChanges:`n" +
            ((Invoke-Git @('log', '--format=- %s', $range)) -join "`n")
    }

    if (Test-Path $output) { Remove-Item $output -Recurse -Force }
    Invoke-ModBuild -Configuration Release
    $archives = New-ModPackage -Output $output -Name 'Poe2Access' -Version $tag -Require7z

    Start-Step 'manifest'
    $zip = Join-Path $output 'Poe2Access.zip'
    # Hashed with .NET itself: Get-FileHash is missing when Windows PowerShell is started from a PowerShell 7 or Git Bash
    # session, whose module path it inherits (the release of 2026-11-10 stopped here).
    $stream = [System.IO.File]::OpenRead($zip)
    try { $hash = [System.BitConverter]::ToString([System.Security.Cryptography.SHA256]::Create().ComputeHash($stream)) -replace '-', '' }
    finally { $stream.Dispose() }
    $manifest = [ordered]@{ tag = $tag; sha256 = $hash; notes = $Notes }
    [IO.File]::WriteAllText((Join-Path $output 'release.json'), ($manifest | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    # The updater opens the zip, finds Poe2Access.exe beside an app folder and installs it; the tag inside is what it compares.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $entries = $archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }
        $versionEntry = $archive.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq 'Poe2Access/app/version.txt' }
        $packed = if ($versionEntry) { [IO.StreamReader]::new($versionEntry.Open()).ReadToEnd().Trim() } else { '' }
    }
    finally {
        $archive.Dispose()
    }
    if ('Poe2Access/Poe2Access.exe' -notin $entries -or $packed -ne $tag) { throw "Poe2Access.zip does not hold Poe2Access/Poe2Access.exe and app/version.txt $tag." }
    # Relative to the repository: gh reads "path#label", and the repository's path holds a '#' (c#).
    $assets = @($archives | ForEach-Object { "dist/release/$($_.Name)" }) + 'dist/release/release.json'
    $notesFile = Join-Path $output 'notes.md'
    Set-Content -Path $notesFile -Value $Notes -Encoding utf8

    if ($DryRun) {
        Start-Step 'dry run'
        Write-Host "Dry run: would publish $tag, titled Test version $tag, at commit $($commit.Substring(0, 7)) of $branch on $repository, marked latest$(if ($latest) { "; the latest now is $latest" })."
        $archives | ForEach-Object { Write-Host ('Asset: {0}, {1:N1} MB.' -f $_.Name, ($_.Length / 1MB)) }
        Write-Host "Asset: release.json, SHA-256 of the zip $($manifest.sha256)."
        Write-Host 'Notes:'
        Write-Host $Notes
        Write-Host "Nothing was tagged or published$(if ($blockers) { ", and a real release would refuse ($($blockers.Count) reasons above)" }). The files are in $output."
        Complete-Run 'Dry run done'
        exit 0
    }

    Start-Step 'publish'
    Push-Location $RepoRoot
    try {
        Invoke-Native gh (@('release', 'create', $tag) + $assets + @('--repo', $repository, '--target', $commit, '--title', "Test version $tag", '--notes-file', $notesFile, '--latest')) | Out-Null
    }
    finally {
        Pop-Location
    }
    $published = @(Invoke-Native gh @('release', 'view', $tag, '--repo', $repository, '--json', 'assets', '--jq', '.assets[].name'))
    $missing = @($assets | ForEach-Object { Split-Path $_ -Leaf } | Where-Object { $_ -notin $published })
    if ($missing) { throw "$tag was created but lacks $($missing -join ', '): upload them with gh release upload $tag, or delete the release." }

    # What players download must be what was built: the zip as GitHub serves it is hashed again and set against the manifest
    # served beside it, by which the launcher accepts or refuses the package (tried against v2026.11.12-4 on 2026-11-12).
    Start-Step 'verify published'
    $check = Join-Path ([IO.Path]::GetTempPath()) "Poe2AccessRelease-$tag"
    if (Test-Path $check) { Remove-Item $check -Recurse -Force }
    Invoke-Native gh @('release', 'download', $tag, '--repo', $repository, '--pattern', 'Poe2Access.zip', '--pattern', 'release.json', '--dir', $check) | Out-Null
    $stream = [System.IO.File]::OpenRead((Join-Path $check 'Poe2Access.zip'))
    try { $served = [System.BitConverter]::ToString([System.Security.Cryptography.SHA256]::Create().ComputeHash($stream)) -replace '-', '' }
    finally { $stream.Dispose() }
    $servedManifest = Get-Content (Join-Path $check 'release.json') -Raw | ConvertFrom-Json
    Remove-Item $check -Recurse -Force
    if ($served -ne $hash -or $servedManifest.sha256 -ne $hash -or $servedManifest.tag -ne $tag) {
        throw "$tag is published, but what GitHub serves is not what was built (zip $served, manifest $($servedManifest.sha256) for $($servedManifest.tag), built $hash): the mod will refuse that update. Upload the assets again with gh release upload $tag --clobber, or delete the release."
    }

    Remove-Item $output -Recurse -Force
    Write-Host "Released $tag. Always the newest: https://github.com/$repository/releases/latest/download/Poe2Access.zip"
    Complete-Run
}
catch {
    Write-Failure $_
    exit 1
}
