param(
    # Defaults to the version in windows7\Directory.Build.props; any value given must match it.
    [string]$Version,
    # Replace an existing installer with the same version instead of stopping.
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$windows7 = Split-Path $PSScriptRoot -Parent
$declared = ((Select-Xml -LiteralPath (Join-Path $windows7 'Directory.Build.props') -XPath '//Version' |
    Select-Object -First 1).Node.InnerText -replace '-.*$', '').Trim()
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = $declared }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "The version must look like 1.2.3: $Version" }
if ($Version -ne $declared) { throw "Version $Version does not match windows7\Directory.Build.props ($declared). Update the project version and rebuild first." }
$source = Join-Path $windows7 'src\ESDInstaller.Windows7.App\bin\Release\net48'
$staging = Join-Path $repository "work\windows7-package-$Version"
$outputs = Join-Path $repository 'outputs'
$installer = Join-Path $outputs "ESD-Installer-Windows7-Setup-$Version.exe"
$checksum = [System.IO.Path]::ChangeExtension($installer, '.sha256.txt')
$icon = Join-Path $windows7 'src\ESDInstaller.Windows7.App\Assets\ESDInstaller.Windows7.ico'
$nsi = Join-Path $PSScriptRoot 'ESDInstaller.Windows7.nsi'
$compiler = Get-ChildItem -LiteralPath (Join-Path $repository 'work\tools') -Recurse -Filter makensis.exe | Select-Object -First 1
if ($null -eq $compiler) { throw 'makensis.exe was not found under work\tools.' }
if (-not (Test-Path -LiteralPath (Join-Path $source 'ESDInstaller.Windows7.exe'))) { throw 'Build the Windows 7 solution first.' }
# Refuse to package an incomplete build or binaries from a different version.
foreach ($required in @('ESDInstaller.Windows7.exe', 'Worker\ESDInstaller.Windows7.Worker.exe', 'x86\libwim-15.dll', 'x64\libwim-15.dll', 'Worker\x86\libwim-15.dll', 'Worker\x64\libwim-15.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required))) { throw "The Windows 7 build is incomplete; missing $required. Rebuild the solution." }
}
foreach ($binary in @('ESDInstaller.Windows7.exe', 'Worker\ESDInstaller.Windows7.Worker.exe')) {
    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $source $binary))
    $built = "$($info.FileMajorPart).$($info.FileMinorPart).$($info.FileBuildPart)"
    if ($built -ne $Version) { throw "$binary is version $built, not $Version. Rebuild before packaging." }
}
if (Test-Path -LiteralPath $installer) {
    if (-not $Force) { throw "$installer already exists. Pass -Force to replace it." }
}
if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $staging -Recurse -Force
Get-ChildItem -LiteralPath $staging -Recurse -Filter '*.pdb' | Remove-Item -Force
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE') -Destination (Join-Path $staging 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $windows7 'README.md') -Destination (Join-Path $staging 'README.md')
Copy-Item -LiteralPath (Join-Path $windows7 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $staging 'THIRD-PARTY-NOTICES.md')
New-Item -ItemType Directory -Path $outputs -Force | Out-Null
if (Test-Path -LiteralPath $installer) { Remove-Item -LiteralPath $installer -Force }
if (Test-Path -LiteralPath $checksum) { Remove-Item -LiteralPath $checksum -Force }
$bytes = (Get-ChildItem -LiteralPath $staging -Recurse -File | Measure-Object Length -Sum).Sum
$sizeKb = [Math]::Ceiling($bytes / 1KB)
& $compiler.FullName /V2 /INPUTCHARSET UTF8 "/DAPP_VERSION=$Version" "/DAPP_VERSION_NUMERIC=$Version.0" "/DAPP_SOURCE=$staging" "/DAPP_ICON=$icon" "/DOUTPUT_FILE=$installer" "/DAPP_SIZE_KB=$sizeKb" $nsi
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installer)) { throw "NSIS failed with exit code $LASTEXITCODE." }
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText($checksum, "$hash *$(Split-Path $installer -Leaf)`n", [System.Text.Encoding]::ASCII)
Get-Item -LiteralPath $installer | Select-Object FullName,Length
Write-Host "SHA-256: $hash"
