<#
.SYNOPSIS
    Builds a test package of the mod: unzip, run Poe2Access.exe, play. Writes dist\Poe2Access-test-<date>.zip, and a
    smaller .7z beside it when 7-Zip is installed (under the 30 MB a chat upload takes; the zip is about 45 MB).
.DESCRIPTION
    The top folder holds only the launcher (one self-contained, trimmed executable), the instructions, the controls and
    a stop shortcut. The host (Poe2AccessHost) and the sound extractor are published self-contained into "app" (they
    share the same .NET 10 runtime files), so nothing has to be installed. Only the accessibility plugin is shipped, with
    research mode off. The game's sounds are not shipped: the launcher regenerates them from the player's own install on
    the first start and after each game patch (GameDataSetup.cs). Run with the host stopped (the plugin build deploys into its folder).
    -Version (the release tag) goes into app\version.txt, which the launcher compares with the latest release to offer
    an update; a package without it never updates itself.
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\..\dist'),
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

# Host and extractor: self-contained, into the same "app" folder. The launcher: one file at the top.
# Built under dist\build: in the host's own bin the self-contained build would leave the .NET runtime in the
# development host folder, where the framework-dependent development launcher then fails to start.
$artifacts = Join-Path $Output 'build'
foreach ($project in 'host\Poe2AccessHost\Poe2AccessHost.csproj', 'tools\Poe2UiExtract\Poe2UiExtract.csproj') {
    Invoke-Dotnet @('publish', (Join-Path $repo $project), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $app, '--artifacts-path', $artifacts,
        '-p:GenerateDocumentationFile=false', '-p:SatelliteResourceLanguages=en', '-nologo', '-v', 'q')
}
# Unused here: the runtime's native PDB reader and crash-dump tool serve debuggers.
Remove-Item -Force (Join-Path $app 'Microsoft.DiaSymReader.Native.amd64.dll'), (Join-Path $app 'createdump.exe') -ErrorAction SilentlyContinue
# Uncompressed inside: the archive compresses it better than the bundle's own compression (and it starts faster).
Invoke-Dotnet @('publish', (Join-Path $repo 'tools\Poe2AccessLauncher\Poe2Access.csproj'), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $stage,
    '-p:PublishSingleFile=true', '-p:PublishTrimmed=true', '-p:EnableCompressionInSingleFile=false', '-p:DebugType=none', '-nologo', '-v', 'q')

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
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\code-fields.txt') (Join-Path $plugin 'catalog') # what the game's code reads; no step makes it
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\cull-skills.txt') (Join-Path $plugin 'catalog') # skills that deal Culling Strike, from the game files
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\important_tgt_files.txt') (Join-Path $plugin 'catalog') # named places, from GameHelper2's Radar plugin
Copy-Item (Join-Path $repo 'Poe2Accessibility\assets') $plugin -Recurse # the mod's own recorded sounds (unexplored-area loop, Diablo IV item set)
Set-Content -Path (Join-Path $plugin 'config\settings.txt') -Value '{ "ResearchMode": false }' -Encoding utf8

if ($Version.Length -gt 0) { Set-Content -Path (Join-Path $app 'version.txt') -Value $Version -Encoding ascii }

# Instructions for the testers, the controls, and a stop shortcut.
Copy-Item (Join-Path $PSScriptRoot 'README.txt') $stage
Copy-Item (Join-Path $repo 'docs\controls.md') (Join-Path $stage 'Controls.md')
Set-Content -Path (Join-Path $stage 'Stop the mod.bat') -Value "@start `"`" `"%~dp0Poe2Access.exe`" stop" -Encoding ascii

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
