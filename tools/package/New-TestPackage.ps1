<#
.SYNOPSIS
    Builds a test package of the mod: unzip, run Poe2Access.exe, play. Writes dist\package\Poe2Access-test-<date>.zip, and a
    smaller .7z beside it when 7-Zip is installed (about 35 MB; the zip is about 51 MB).
.DESCRIPTION
    The top folder holds the mod's one executable (Poe2Access.exe: self-contained, single file, not trimmed because the
    plugin is loaded at run time and uses framework APIs the executable does not), the instructions, the controls and a
    stop shortcut. "app" holds the accessibility plugin with its catalogs and assets, and oo2core.dll for reading the
    game's bundles: nothing of the .NET runtime. The game's sounds are not shipped: the mod regenerates them from the
    player's own install on the first start and after each game patch (GameDataSetup.cs). Run with the mod stopped (the
    plugin build deploys into its folder).
    -Version (the release tag) goes into app\version.txt, which the mod compares with the latest release to offer
    an update; a package without it never updates itself.#>
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\..\dist\package'), # never dist itself: dist\Poe2Access is a play install
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Output = [System.IO.Path]::GetFullPath($Output)
$stage = Join-Path $Output 'Poe2Access'
$app = Join-Path $stage 'app'

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed" }
}

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

# One executable (the extractor is a library inside it): self-contained, one file, uncompressed (the archive compresses it
# better and it starts faster), ahead-of-time compiled for a quicker start on handhelds. Built under dist\build so the
# development folder keeps its framework-dependent build.
$artifacts = Join-Path $Output 'build'
Invoke-Dotnet @('publish', (Join-Path $repo 'host\Poe2Access\Poe2Access.csproj'), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $stage, '--artifacts-path', $artifacts,
    '-p:PublishSingleFile=true', '-p:PublishTrimmed=false', '-p:EnableCompressionInSingleFile=false', '-p:PublishReadyToRun=true', '-p:DebugType=none',
    '-p:GenerateDocumentationFile=false', '-p:SatelliteResourceLanguages=en', '-nologo', '-v', 'q')
# The Oodle library is read from "app"; the publish also leaves the extractor's runtime config at the top.
New-Item -ItemType Directory -Force $app | Out-Null
if (Test-Path (Join-Path $stage 'oo2core.dll')) { Move-Item (Join-Path $stage 'oo2core.dll') $app }
Get-ChildItem $stage -File | Where-Object { $_.Name -ne 'Poe2Access.exe' } | Remove-Item -Force
# The accessibility plugin, built as for development, then copied without logs, dumps, settings or extracted sounds.
Invoke-Dotnet @('build', (Join-Path $repo 'Poe2Accessibility'), '-c', 'Release', '-nologo', '-v', 'q')
$pluginBuild = Join-Path $repo 'Poe2Accessibility\bin\Release\net10.0-windows\win-x64'
$plugin = Join-Path $app 'Plugins\Poe2Accessibility'
New-Item -ItemType Directory -Force (Join-Path $plugin 'catalog\lang'), (Join-Path $plugin 'config') | Out-Null
foreach ($file in 'Poe2Accessibility.dll', 'Poe2Accessibility.pdb', 'Prismatoid.dll', 'prism.dll') {
    Copy-Item (Join-Path $pluginBuild $file) $plugin
}
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\*.json') (Join-Path $plugin 'catalog')
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\lang\*.json') (Join-Path $plugin 'catalog\lang') # phrases in the other game languages
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\dodge-timings.txt') (Join-Path $plugin 'catalog') # measured dodge timings
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\hit-times.txt') (Join-Path $plugin 'catalog') # hit times from the game files
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\hazards.txt') (Join-Path $plugin 'catalog') # harmful-ground buffs from the game files
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\attack-reach.txt') (Join-Path $plugin 'catalog') # how far monster blows reach, from the game files
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\action-types.txt') (Join-Path $plugin 'catalog') # the game's action names
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\telegraph-skills.txt') (Join-Path $plugin 'catalog') # what the game draws before a monster skill lands
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\telegraph-effects.txt') (Join-Path $plugin 'catalog') # the effect files those skills play
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\code-fields.txt') (Join-Path $plugin 'catalog') # what the game's code reads; no step makes it
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\semantic-fields.txt') (Join-Path $plugin 'catalog') # the semantic rules, read with it
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\cull-skills.txt') (Join-Path $plugin 'catalog') # skills that deal Culling Strike, from the game files
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\inline-icons.txt') (Join-Path $plugin 'catalog') # the markup images texts show as pictures, from the game files
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\important_tgt_files.txt') (Join-Path $plugin 'catalog') # named places: a vendored data file (formerly GameHelper2's Radar table)
Copy-Item (Join-Path $repo 'Poe2Accessibility\assets') $plugin -Recurse # the mod's own recorded sounds (unexplored-area loop, Diablo IV item set)
Set-Content -Path (Join-Path $plugin 'config\settings.txt') -Value '{ "ResearchMode": false }' -Encoding utf8

if ($Version.Length -gt 0) { Set-Content -Path (Join-Path $app 'version.txt') -Value $Version -Encoding ascii }

# Instructions for the testers, the controls, and a stop shortcut.
Copy-Item (Join-Path $PSScriptRoot 'README.txt') $stage
Copy-Item (Join-Path $repo 'docs\controls.md') (Join-Path $stage 'Controls.md')
Set-Content -Path (Join-Path $stage 'Stop the mod.bat') -Value "@start `"`" `"%~dp0Poe2Access.exe`" stop" -Encoding ascii

# The layout the mod's start-up cleanup (AppCleanup.cs) expects: no runtime or tool files at the top of "app".
$stray = Get-ChildItem $app -File | Where-Object { $_.Name -notin 'oo2core.dll', 'version.txt' }
if ($stray) { throw "Unexpected files in app: $($stray.Name -join ', ')" }

$zip = Join-Path $Output ("Poe2Access-test-{0:yyyyMMdd}.zip" -f (Get-Date))
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
Write-Host ("Package: {0} ({1:N0} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))

# The same folder as .7z (LZMA2): Windows 11 opens it; Windows 10 needs 7-Zip or WinRAR.
$sevenZip = Join-Path $env:ProgramFiles '7-Zip\7z.exe'
if (Test-Path $sevenZip) {
    $archive = [System.IO.Path]::ChangeExtension($zip, '.7z')
    if (Test-Path $archive) { Remove-Item $archive -Force }
    & $sevenZip a -t7z -mx=9 -m0=lzma2 -md=64m -ms=on $archive $stage | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "7-Zip failed ($LASTEXITCODE)" }
    Write-Host ("Package: {0} ({1:N1} MB)" -f $archive, ((Get-Item $archive).Length / 1MB))
}
