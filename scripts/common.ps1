function Get-DependencyPaths {
    param([string]$RepositoryRoot, [string]$GameSignalsProject, [string]$HardwareControlProject)
    if ([string]::IsNullOrWhiteSpace($GameSignalsProject)) {
        $GameSignalsProject = Join-Path $RepositoryRoot 'dependencies/SecretFlasherManaka-ForEveryThing/SecretFlasherManaka.ForEveryThing.csproj'
        if (-not (Test-Path -LiteralPath $GameSignalsProject)) {
            $GameSignalsProject = Join-Path $RepositoryRoot '../SecretFlasherManaka-ForEveryThing/SecretFlasherManaka.ForEveryThing.csproj'
        }
    }
    if ([string]::IsNullOrWhiteSpace($HardwareControlProject)) {
        $HardwareControlProject = Join-Path $RepositoryRoot 'dependencies/BooBoopControl/BooBoopControl.csproj'
        if (-not (Test-Path -LiteralPath $HardwareControlProject)) {
            $HardwareControlProject = Join-Path $RepositoryRoot '../BooBoopControl/BooBoopControl.csproj'
        }
    }
    if (-not (Test-Path -LiteralPath $GameSignalsProject -PathType Leaf)) { throw 'Missing game project. Initialize dependencies or set -GameSignalsProject.' }
    if (-not (Test-Path -LiteralPath $HardwareControlProject -PathType Leaf)) { throw 'Missing hardware project. Initialize dependencies or set -HardwareControlProject.' }
    return @{
        Game = (Resolve-Path -LiteralPath $GameSignalsProject).Path
        Hardware = (Resolve-Path -LiteralPath $HardwareControlProject).Path
    }
}
