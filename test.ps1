param(
    [string]$Configuration = 'Release',
    [string]$GameSignalsProject,
    [string]$HardwareControlProject
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/common.ps1')
$dependencies = Get-DependencyPaths $PSScriptRoot $GameSignalsProject $HardwareControlProject
& dotnet run --project (Join-Path $PSScriptRoot 'tests/Bridge.MockTests/Bridge.MockTests.csproj') -c $Configuration "-p:GameSignalsProject=$($dependencies.Game)" "-p:HardwareControlProject=$($dependencies.Hardware)"
if ($LASTEXITCODE -ne 0) { throw "Mock tests failed (exit $LASTEXITCODE)." }
