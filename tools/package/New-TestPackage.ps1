<#
.SYNOPSIS
    Builds a test package of the mod: unzip, run Poe2Access.exe, play. Writes dist\Poe2Access-test-<date>.zip.
.DESCRIPTION
    GameHelper, the launcher and the sound extractor are published self-contained into one folder (they share the
    same .NET 10 runtime files), so nothing has to be installed. Only the accessibility plugin is shipped, with
    research mode off. The game's sounds are not shipped: the launcher regenerates them from the player's own install on
    the first start and after each game patch (GameDataSetup.cs). Run with GameHelper stopped (the plugin build deploys into its folder).
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\..\dist')
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Output = [System.IO.Path]::GetFullPath($Output)
$stage = Join-Path $Output 'Poe2Access'
$devHost = Join-Path $repo 'external\GameHelper2\GameHelper\bin\Release\net10.0-windows\win-x64'

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed" }
}

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

# Host, launcher and extractor: self-contained, into the same folder.
foreach ($project in 'external\GameHelper2\GameHelper\GameHelper.csproj', 'tools\Poe2AccessLauncher\Poe2Access.csproj', 'tools\Poe2UiExtract\Poe2UiExtract.csproj') {
    Invoke-Dotnet @('publish', (Join-Path $repo $project), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $stage, '-p:GenerateDocumentationFile=false', '-nologo', '-v', 'q')
}

# The accessibility plugin, built as for development, then copied without logs, dumps, settings or extracted sounds.
Invoke-Dotnet @('build', (Join-Path $repo 'Poe2Accessibility'), '-c', 'Release', '-nologo', '-v', 'q')
$pluginBuild = Join-Path $repo 'Poe2Accessibility\bin\Release\net10.0-windows\win-x64'
$plugin = Join-Path $stage 'Plugins\Poe2Accessibility'
New-Item -ItemType Directory -Force (Join-Path $plugin 'catalog'), (Join-Path $plugin 'config') | Out-Null
foreach ($file in 'Poe2Accessibility.dll', 'Poe2Accessibility.pdb', 'Prismatoid.dll', 'prism.dll') {
    Copy-Item (Join-Path $pluginBuild $file) $plugin
}
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\*.json') (Join-Path $plugin 'catalog')
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\dodge-timings.txt') (Join-Path $plugin 'catalog') # measured dodge timings
Copy-Item (Join-Path $repo 'Poe2Accessibility\catalog\hit-times.txt') (Join-Path $plugin 'catalog') # hit times from the game files
Copy-Item (Join-Path $repo 'external\GameHelper2\Plugins\Radar\important_tgt_files.txt') (Join-Path $plugin 'catalog') # named places; Radar is not shipped
Set-Content -Path (Join-Path $plugin 'config\settings.txt') -Value '{ "ResearchMode": false }' -Encoding utf8

# GameHelper settings: controller mode as in development; only the accessibility plugin enabled.
New-Item -ItemType Directory -Force (Join-Path $stage 'configs') | Out-Null
Copy-Item (Join-Path $devHost 'configs\core_settings.json') (Join-Path $stage 'configs')
Set-Content -Path (Join-Path $stage 'configs\plugins.json') -Value "{`n  `"Poe2Accessibility`": {`n    `"Enable`": true`n  }`n}" -Encoding utf8

# Instructions for the testers, the controls, and a stop shortcut.
Copy-Item (Join-Path $PSScriptRoot 'LEEME.txt') $stage
Copy-Item (Join-Path $repo 'docs\controls.md') (Join-Path $stage 'Controles (ingles).md')
Set-Content -Path (Join-Path $stage 'Detener el mod.bat') -Value "@start `"`" `"%~dp0Poe2Access.exe`" stop" -Encoding ascii

$zip = Join-Path $Output ("Poe2Access-test-{0:yyyyMMdd}.zip" -f (Get-Date))
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
Write-Host ("Package: {0} ({1:N0} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
