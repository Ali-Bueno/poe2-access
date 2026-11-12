# Steps shared by tools/build.ps1 and tools/package/*.ps1, dot-sourced by them. Output is plain text for a screen reader:
# one line a step, and a failure names the step it stopped at.

$ErrorActionPreference = 'Stop'
$ToolsRoot = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$script:currentStep = 'start'
$script:runTimer = [Diagnostics.Stopwatch]::StartNew()

function Start-Step([string]$Name) { $script:currentStep = $Name }

function Write-Failure($ErrorRecord) {
    Write-Host "FAILED at step $($script:currentStep): $($ErrorRecord.Exception.Message)"
}

function Complete-Run([string]$Text = 'Done') {
    Write-Host ('{0} in {1:N0} s.' -f $Text, $script:runTimer.Elapsed.TotalSeconds)
}

# Runs a program and returns its output lines (stderr included). A non-zero exit code fails the step with the program's
# last line, unless -AllowFailure, when the caller reads $LASTEXITCODE.
function Invoke-Native([string]$FilePath, [string[]]$Arguments, [switch]$AllowFailure) {
    $ErrorActionPreference = 'Continue'
    $output = @(& $FilePath @Arguments 2>&1 | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        $last = $output | Where-Object { $_.Trim() } | Select-Object -Last 1
        throw "$FilePath $($Arguments[0]) exited with code $LASTEXITCODE. $last"
    }
    return $output
}

# MSBuild's errors or warnings, once each, with paths relative to the repository and the project as its own field.
function Get-Diagnostics([string[]]$Lines, [string]$Kind) {
    $seen = @{}
    foreach ($line in $Lines) {
        if ($line -notmatch "^\s*(\d+>)?(?<text>.*?: $Kind [A-Z]+\d+: .*?)( \[(?<project>[^\]]+)\])?\s*$") { continue }
        $text = $Matches.text.Replace("$RepoRoot\", '')
        if ($seen.ContainsKey($text)) { continue }
        $seen[$text] = $true
        [pscustomobject]@{ Text = $text; Project = if ($Matches.project) { [IO.Path]::GetFileNameWithoutExtension($Matches.project) } else { '' } }
    }
}

# Builds everything through Poe2Access.slnx (the plugin deploys itself beside the executable), then runs the unit tests
# and the phrase tables' check.
function Invoke-ModBuild([string]$Configuration = 'Release', [switch]$SkipTests, [switch]$ShowWarnings) {
    Push-Location $RepoRoot
    try {
        Start-Step 'build'
        $running = Get-Process Poe2Access -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$RepoRoot\host\*" }
        if ($running) { throw 'The mod is running from this build folder: stop it first (Poe2Access.exe stop).' }
        $lines = @(Invoke-Native dotnet @('build', 'Poe2Access.slnx', '-c', $Configuration, '-nologo', '-tl:off', '-v', 'q', '-clp:NoSummary') -AllowFailure)
        $code = $LASTEXITCODE
        $errors = @(Get-Diagnostics $lines 'error')
        $warnings = @(Get-Diagnostics $lines 'warning')
        Write-Host "Build ${Configuration}: $($errors.Count) errors, $($warnings.Count) warnings."
        if ($ShowWarnings) { $warnings | ForEach-Object { Write-Host $_.Text } }
        if ($code -ne 0) {
            $errors | ForEach-Object { Write-Host $_.Text }
            if (-not $errors) { $lines | Select-Object -Last 10 | ForEach-Object { Write-Host $_ } }
            $projects = @($errors | ForEach-Object Project | Where-Object { $_ } | Sort-Object -Unique)
            throw "dotnet build exited with code $code" + $(if ($projects) { ", errors in $($projects -join ', ')." } else { '.' })
        }
        if ($SkipTests) { return }

        Start-Step 'tests'
        $lines = @(Invoke-Native dotnet @('run', '--project', 'tests\Poe2Accessibility.Tests', '-c', $Configuration, '--no-build') -AllowFailure)
        $code = $LASTEXITCODE
        $passed = @($lines -match '^PASS ').Count
        $failed = @($lines -match '^FAIL ')
        Write-Host "Tests: $passed passed, $($failed.Count) failed."
        $failed | ForEach-Object { Write-Host $_ }
        if ($code -ne 0 -or $failed -or $passed -eq 0) {
            if (-not $failed) { $lines | Select-Object -Last 10 | ForEach-Object { Write-Host $_ } }
            throw "the test runner exited with code $code."
        }

        Start-Step 'phrases'
        # UTF-8 both ways: a mismatch line quotes Thai or Japanese text.
        $encoding, $pythonEncoding = [Console]::OutputEncoding, $env:PYTHONIOENCODING
        try {
            [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
            $env:PYTHONIOENCODING = 'utf-8'
            $lines = @(Invoke-Native py @('-B', 'tools\localization\phrases.py', 'check') -AllowFailure)
            $code = $LASTEXITCODE
        }
        finally {
            [Console]::OutputEncoding = $encoding
            $env:PYTHONIOENCODING = $pythonEncoding
        }
        $languages = @($lines -match '^\S+: \d+ entries,')
        $incomplete = @($languages -notmatch ', 0 missing, 0 stale, 0 placeholder mismatches$')
        Write-Host "Phrases: $($lines[0]), $($languages.Count) languages, $($incomplete.Count) incomplete."
        if ($code -ne 0 -or $languages.Count -eq 0) {
            $lines | Where-Object { $_ -in $incomplete -or $_ -match '^(unreadable:|  mismatch:)' } | ForEach-Object { Write-Host $_ }
            throw 'tools/localization/phrases.py check found problems (fix them with its missing and merge commands).'
        }
    }
    finally {
        Pop-Location
    }
}

# The package testers unzip and the updater installs: Poe2Access.exe, the instructions and a stop shortcut at the top,
# everything else in app\ (the layout AppCleanup.cs expects). Writes <Name>.zip, and <Name>.7z when 7-Zip is installed
# (required with -Require7z), into $Output and returns them.
function New-ModPackage([string]$Output, [string]$Name, [string]$Version = '', [switch]$Require7z) {
    Start-Step 'package inputs'
    $Output = [IO.Path]::GetFullPath($Output)
    $stage = Join-Path $Output 'Poe2Access'
    $app = Join-Path $stage 'app'
    $plugin = Join-Path $app 'Plugins\Poe2Accessibility'
    $oodle = Join-Path $RepoRoot 'external\oodle\oo2core.dll'
    if (-not (Test-Path $oodle)) { throw 'external\oodle\oo2core.dll is missing: the mod reads the game''s bundles with it (tools/README.md, ooz-shim).' }
    # The Linux scripts (Proton launch option, stop, installer) beside Poe2Access.exe; sh fails on CR line ends.
    $linuxScripts = 'poe2access-proton.sh', 'poe2access-stop.sh', 'install-linux.sh' | ForEach-Object { Join-Path $ToolsRoot "package\$_" }
    foreach ($script in $linuxScripts) {
        if ([IO.File]::ReadAllText($script).Contains("`r")) { throw "$script has CR line ends: sh would fail on it." }
    }
    $sevenZip = Join-Path $env:ProgramFiles '7-Zip\7z.exe'
    if (-not (Test-Path $sevenZip)) {
        if ($Require7z) { throw "7-Zip is not installed ($sevenZip): the release's Poe2Access.7z needs it." }
        Write-Host 'No 7-Zip: the zip only.'
        $sevenZip = $null
    }
    if ((Test-Path (Join-Path $app 'GameData')) -or (Test-Path (Join-Path $app 'Poe2Access.log'))) {
        throw "$stage has been played from (app\GameData or a log): pick another -Output, a package folder is deleted first."
    }
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory -Force $app | Out-Null

    # One self-contained, single-file executable (the extractor is a library inside it), not trimmed (the plugin uses
    # framework APIs it does not), uncompressed and ReadyToRun to start fast. Built under $Output\build so the
    # development folder keeps its framework-dependent build.
    Start-Step 'publish executable'
    Invoke-Native dotnet @('publish', (Join-Path $RepoRoot 'host\Poe2Access\Poe2Access.csproj'), '-c', 'Release', '-r', 'win-x64',
        '--self-contained', 'true', '-o', $stage, '--artifacts-path', (Join-Path $Output 'build'), '-p:PublishSingleFile=true',
        '-p:PublishTrimmed=false', '-p:EnableCompressionInSingleFile=false', '-p:PublishReadyToRun=true', '-p:DebugType=none',
        '-p:SatelliteResourceLanguages=en', '-nologo', '-tl:off', '-v', 'q') | Out-Null
    # The publish also leaves the extractor's runtime config and its copy of oo2core.dll beside the executable.
    $leftovers = Get-ChildItem $stage -File | Where-Object Name -ne 'Poe2Access.exe'
    $unexpected = @($leftovers | Where-Object Name -notin 'oo2core.dll', 'Poe2UiExtract.runtimeconfig.json')
    if ($unexpected -or (Get-ChildItem $stage -Directory | Where-Object Name -ne 'app')) {
        throw "The publish left files the package does not expect: $((@($unexpected.Name) + @((Get-ChildItem $stage -Directory | Where-Object Name -ne 'app').Name)) -join ', ')."
    }
    $leftovers | Remove-Item -Force
    Copy-Item $oodle $app

    # The plugin through its own deploy step, as for development, without the game's extracted sounds.
    Start-Step 'plugin'
    Invoke-Native dotnet @('build', (Join-Path $RepoRoot 'Poe2Accessibility'), '-c', 'Release', '-nologo', '-tl:off', '-v', 'q',
        "-p:PluginDeployDir=$plugin", '-p:DeployGameSounds=false') | Out-Null
    $licenses = Join-Path $plugin 'licenses\prism'
    New-Item -ItemType Directory -Force $licenses, (Join-Path $plugin 'config') | Out-Null
    Copy-Item (Join-Path $RepoRoot 'third_party\prism\NOTICE'), (Join-Path $RepoRoot 'third_party\prism\LICENSES') $licenses -Recurse
    Set-Content -Path (Join-Path $plugin 'config\settings.txt') -Value '{ "ResearchMode": false }' -Encoding utf8
    # The release tag the updater compares with the latest release; a package without it never updates itself.
    if ($Version) { Set-Content -Path (Join-Path $app 'version.txt') -Value $Version -Encoding ascii }

    Copy-Item (Join-Path $ToolsRoot 'package\README.txt') $stage
    Copy-Item $linuxScripts $stage
    Copy-Item (Join-Path $RepoRoot 'docs\controls.md') (Join-Path $stage 'Controls.md')
    Set-Content -Path (Join-Path $stage 'Stop the mod.bat') -Value "@start `"`" `"%~dp0Poe2Access.exe`" stop" -Encoding ascii

    # The package's own files, a path a line: what the next update removes when its package no longer holds one
    # (PackageInstall.Prune in the launcher). Written last, itself listed.
    $list = Join-Path $app 'package-files.txt'
    Set-Content -Path $list -Value '' -Encoding ascii
    $files = Get-ChildItem $stage -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($stage, $_.FullName).Replace('\', '/') }
    [IO.File]::WriteAllLines($list, [string[]]($files | Sort-Object))

    Start-Step 'package layout'
    Test-PackageLayout $stage $Version

    Start-Step 'zip'
    $zip = Join-Path $Output "$Name.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
    $archives = @(Get-Item $zip)
    # The same folder as .7z (LZMA2): Windows 11 opens it; Windows 10 needs 7-Zip or WinRAR.
    if ($sevenZip) {
        Start-Step '7z'
        $archive = Join-Path $Output "$Name.7z"
        if (Test-Path $archive) { Remove-Item $archive -Force }
        Invoke-Native $sevenZip @('a', '-t7z', '-mx=9', '-m0=lzma2', '-md=64m', '-ms=on', $archive, $stage) | Out-Null
        $archives += Get-Item $archive
    }
    $archives | ForEach-Object { Write-Host ('Package: {0} ({1:N1} MB)' -f $_.FullName, ($_.Length / 1MB)) }
    return $archives
}

# What the updater (Updater.cs) and the start-up cleanup (AppCleanup.cs) rely on, checked before anything is archived.
function Test-PackageLayout([string]$Stage, [string]$Version) {
    $problems = @()
    $top = @(Get-ChildItem $Stage | ForEach-Object Name)
    $expected = 'Poe2Access.exe', 'README.txt', 'Controls.md', 'Stop the mod.bat', 'poe2access-proton.sh', 'poe2access-stop.sh', 'install-linux.sh', 'app'
    $problems += $expected | Where-Object { $_ -notin $top } | ForEach-Object { "missing $_" }
    $problems += $top | Where-Object { $_ -notin $expected } | ForEach-Object { "unexpected $_ at the top" }
    $app = Join-Path $Stage 'app'
    $problems += Get-ChildItem $app | Where-Object Name -notin 'oo2core.dll', 'version.txt', 'package-files.txt', 'Plugins' | ForEach-Object { "unexpected app\$($_.Name)" }
    $listed = @(Get-Content (Join-Path $app 'package-files.txt') -ErrorAction SilentlyContinue)
    $shipped = @(Get-ChildItem $Stage -File -Recurse)
    if ($listed.Count -ne $shipped.Count -or 'Poe2Access.exe' -notin $listed -or 'app/package-files.txt' -notin $listed) {
        $problems += "app\package-files.txt lists $($listed.Count) files, the package holds $($shipped.Count)"
    }
    $plugin = Join-Path $app 'Plugins\Poe2Accessibility'
    foreach ($file in 'Poe2Accessibility.dll', 'prism.dll', 'catalog\lang', 'catalog\ui-catalog.json', 'assets', 'wine', 'licenses\prism\NOTICE', 'config\settings.txt') {
        if (-not (Test-Path (Join-Path $plugin $file))) { $problems += "missing Plugins\Poe2Accessibility\$file" }
    }
    foreach ($folder in 'sounds', 'logs', 'debug') {
        if (Test-Path (Join-Path $plugin $folder)) { $problems += "Plugins\Poe2Accessibility\$folder must not ship" }
    }
    if ($Version -and (Get-Content (Join-Path $app 'version.txt') -Raw).Trim() -ne $Version) { $problems += "app\version.txt is not $Version" }
    if ($problems) { throw "The package is not as the updater expects: $($problems -join '; ')." }
}
