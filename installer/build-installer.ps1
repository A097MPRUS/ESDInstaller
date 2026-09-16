param(
    # Defaults to the version in Directory.Build.props; any value given must match it.
    [string]$Version,
    # Replace an existing installer with the same version instead of stopping.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$declared = ((Select-Xml -LiteralPath (Join-Path $root 'Directory.Build.props') -XPath '//Version' |
    Select-Object -First 1).Node.InnerText -replace '-.*$', '').Trim()
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = $declared }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "The version must look like 1.2.3: $Version" }
if ($Version -ne $declared) {
    throw "Version $Version does not match Directory.Build.props ($declared). Update the project version and rebuild first."
}

$publishPath = Join-Path $root "work\publish-$Version-final-win-x64"
$iconPath = Join-Path $root 'src\ESDInstaller.App\Assets\ESDInstaller.ico'
$scriptPath = Join-Path $PSScriptRoot 'ESDInstaller.nsi'
$outputPath = Join-Path $root "outputs\ESD-Installer-Setup-$Version.exe"
$checksumPath = [System.IO.Path]::ChangeExtension($outputPath, '.sha256.txt')
$compiler = Get-ChildItem -LiteralPath (Join-Path $root 'work\tools') -Recurse -Filter makensis.exe |
    Select-Object -First 1

if ($null -eq $compiler) { throw 'makensis.exe was not found under work\tools.' }
$appPath = Join-Path $publishPath 'ESDInstaller.exe'
if (-not (Test-Path -LiteralPath $appPath)) {
    throw "The self-contained publish directory is missing: $publishPath"
}
$workerPath = Join-Path $publishPath 'Worker\ESDInstaller.Worker.exe'
if (-not (Test-Path -LiteralPath $workerPath)) {
    throw "The self-contained elevated worker is missing from the publish directory: $workerPath"
}
# The app and its worker must come from the build of the version being packaged.
foreach ($binary in @($appPath, $workerPath)) {
    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($binary)
    $built = "$($info.FileMajorPart).$($info.FileMinorPart).$($info.FileBuildPart)"
    if ($built -ne $Version) { throw "$binary is version $built, not $Version. Publish again before packaging." }
}
$worker = Start-Process -FilePath $workerPath -WorkingDirectory (Split-Path $workerPath -Parent) -PassThru -Wait -WindowStyle Hidden
if ($worker.ExitCode -ne 64) {
    throw "The packaged elevated worker failed its startup smoke test with exit code $($worker.ExitCode)."
}
if (-not (Test-Path -LiteralPath $iconPath)) { throw "The installer icon is missing: $iconPath" }
if (Test-Path -LiteralPath $outputPath) {
    if (-not $Force) { throw "$outputPath already exists. Pass -Force to replace it." }
    Remove-Item -LiteralPath $outputPath -Force
}
if (Test-Path -LiteralPath $checksumPath) { Remove-Item -LiteralPath $checksumPath -Force }

$bytes = (Get-ChildItem -LiteralPath $publishPath -Recurse -File | Measure-Object Length -Sum).Sum
$estimatedSizeKb = [Math]::Ceiling($bytes / 1KB)
$arguments = @(
    '/V2',
    '/INPUTCHARSET', 'UTF8',
    "/DAPP_VERSION=$Version",
    "/DAPP_VERSION_NUMERIC=$Version.0",
    "/DAPP_SOURCE=$publishPath",
    "/DAPP_ICON=$iconPath",
    "/DOUTPUT_FILE=$outputPath",
    "/DAPP_SIZE_KB=$estimatedSizeKb",
    $scriptPath
)

& $compiler.FullName @arguments
if ($LASTEXITCODE -ne 0) { throw "NSIS compilation failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $outputPath)) { throw 'NSIS did not create the expected installer.' }

$item = Get-Item -LiteralPath $outputPath
$hash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText($checksumPath, "$hash *$($item.Name)`n", [System.Text.Encoding]::ASCII)
Write-Host "Created $($item.FullName)"
Write-Host "Installer bytes: $($item.Length)"
Write-Host "SHA-256: $hash"
Write-Host "Installed size estimate: $estimatedSizeKb KB"
