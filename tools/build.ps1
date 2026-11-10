<#
.SYNOPSIS
    Builds the whole mod and checks it in one command: the executable, the plugin (deployed beside it) and the tests'
    runner through Poe2Access.slnx, then the unit tests and the phrase tables' check. Plain text, one line a step; it
    stops at the first failure and says which step failed (exit code 1).
.EXAMPLE
    pwsh tools/build.ps1                                # Release, with tests and the phrases check
    pwsh tools/build.ps1 -SkipTests                     # build and deploy only
    pwsh tools/build.ps1 -Configuration Debug -ShowWarnings
#>
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests,    # no unit tests and no phrases check
    [switch]$ShowWarnings  # each warning, not only their count
)

. (Join-Path $PSScriptRoot 'BuildSteps.ps1')
try {
    Invoke-ModBuild -Configuration $Configuration -SkipTests:$SkipTests -ShowWarnings:$ShowWarnings
    Complete-Run
}
catch {
    Write-Failure $_
    exit 1
}
