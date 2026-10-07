param(
    [string]$GameDir = $env:BOOBOOP_GAME_DIR,
    [string]$Configuration = 'Release',
    [string]$GameSignalsProject,
    [string]$HardwareControlProject
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir -Configuration $Configuration -GameSignalsProject $GameSignalsProject -HardwareControlProject $HardwareControlProject
[xml]$project = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SecretFlasherManaka.BooBoopBridge.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$packageRoot = Join-Path $PSScriptRoot ("artifacts/package-work/" + [Guid]::NewGuid().ToString('N'))
$pluginDirectory = Join-Path $packageRoot 'BepInEx/plugins/SecretFlasherManakaBooBoop'
New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
foreach ($name in @('SecretFlasherManaka.ForEveryThing', 'BooBoopControl', 'SecretFlasherManaka.BooBoopBridge')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "artifacts/plugins/SecretFlasherManakaBooBoop/$name.dll") -Destination (Join-Path $pluginDirectory "$name.dll")
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs/INSTALL.md') -Destination (Join-Path $packageRoot 'INSTALL.md')
foreach ($notice in @('LICENSE', 'THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $notice) -Destination (Join-Path $packageRoot $notice)
}
$releaseDirectory = Join-Path $PSScriptRoot 'artifacts/releases'
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
$archive = Join-Path $releaseDirectory "SecretFlasherManaka-BooBoopBridge-$version-win-x64.zip"
Compress-Archive -LiteralPath @((Join-Path $packageRoot 'BepInEx'), (Join-Path $packageRoot 'INSTALL.md'), (Join-Path $packageRoot 'LICENSE'), (Join-Path $packageRoot 'THIRD_PARTY_NOTICES.md')) -DestinationPath $archive -Force
& python (Join-Path $PSScriptRoot 'scripts/verify_package.py') --archive $archive
if ($LASTEXITCODE -ne 0) { throw 'Release archive validation failed.' }
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Format-List
Write-Host "Local release package created: $archive"
