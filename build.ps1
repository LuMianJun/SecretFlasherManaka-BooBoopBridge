param(
    [string]$GameDir = $env:BOOBOOP_GAME_DIR,
    [string]$Configuration = 'Release',
    [string]$GameSignalsProject,
    [string]$HardwareControlProject,
    [switch]$MockOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/common.ps1')
$dependencies = Get-DependencyPaths $PSScriptRoot $GameSignalsProject $HardwareControlProject
if (-not $MockOnly -and [string]::IsNullOrWhiteSpace($GameDir)) { throw 'Set -GameDir to your game installation, or BOOBOOP_GAME_DIR; use test.ps1 for mocks only.' }
& (Join-Path $PSScriptRoot 'test.ps1') -Configuration $Configuration -GameSignalsProject $dependencies.Game -HardwareControlProject $dependencies.Hardware
if ($MockOnly) { return }
& dotnet build (Join-Path $PSScriptRoot 'SecretFlasherManaka.BooBoopBridge.csproj') -c $Configuration "-p:GameDir=$GameDir" "-p:GameSignalsProject=$($dependencies.Game)" "-p:HardwareControlProject=$($dependencies.Hardware)"
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }
$stage = Join-Path $PSScriptRoot 'artifacts/plugins/SecretFlasherManakaBooBoop'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($name in @('SecretFlasherManaka.ForEveryThing', 'BooBoopControl', 'SecretFlasherManaka.BooBoopBridge')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "bin/$Configuration/net6.0/$name.dll") -Destination (Join-Path $stage "$name.dll") -Force
}
Write-Host "Tests and build passed. Three plugin DLLs staged at: $stage"
