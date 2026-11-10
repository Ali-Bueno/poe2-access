<#
.SYNOPSIS
    Builds a test package of the mod: unzip, run Poe2Access.exe, play. Builds and checks the mod first (the steps of
    tools/build.ps1), then writes dist\package\Poe2Access-test-<date>.zip and, with 7-Zip installed, a smaller .7z beside it.
.DESCRIPTION
    The layout is described in tools/README.md (package). -Version (a release tag) goes into app\version.txt, which the mod
    compares with the latest release to offer an update; a package without it never updates itself. Run it with the mod
    stopped: the build deploys the plugin beside the development executable.
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\..\dist\package'), # never dist itself: dist\Poe2Access is a play install
    [string]$Version = '',
    [switch]$SkipTests # no unit tests and no phrases check before packaging
)

. (Join-Path $PSScriptRoot '..\BuildSteps.ps1')
try {
    Invoke-ModBuild -Configuration Release -SkipTests:$SkipTests
    New-ModPackage -Output $Output -Name ('Poe2Access-test-{0:yyyyMMdd}' -f (Get-Date)) -Version $Version | Out-Null
    Complete-Run
}
catch {
    Write-Failure $_
    exit 1
}
