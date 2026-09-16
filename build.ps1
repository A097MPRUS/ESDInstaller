param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$runtimeIdentifier = "win-$($Platform.ToLowerInvariant())"

# External programs do not raise PowerShell errors, so check every exit code.
function Invoke-Checked {
    param([string]$Name, [scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
}

Invoke-Checked 'Restore' { dotnet restore (Join-Path $root 'ESDInstaller.slnx') }
Invoke-Checked 'Build' { dotnet build (Join-Path $root 'src\ESDInstaller.App\ESDInstaller.App.csproj') -c $Configuration -p:Platform=$Platform -r $runtimeIdentifier --no-restore }
Invoke-Checked 'Tests' { dotnet run --project (Join-Path $root 'tests\ESDInstaller.Core.Tests\ESDInstaller.Core.Tests.csproj') -c $Configuration --no-restore }
