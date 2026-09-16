param(
    # Defaults to the version in windows8\Directory.Build.props; any value given must match it.
    [string]$Version,
    [string]$UcrtRedistRoot,
    # Replace an existing installer with the same version instead of stopping.
    [switch]$Force
)
$ErrorActionPreference = 'Stop'

function Get-PeMachine {
    param([string]$Path)
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $reader = New-Object System.IO.BinaryReader($stream)
        if ($reader.ReadUInt16() -ne 0x5a4d) { throw "Invalid DOS header: $Path" }
        $stream.Position = 0x3c
        $peOffset = $reader.ReadInt32()
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) { throw "Invalid PE header: $Path" }
        return $reader.ReadUInt16()
    }
    finally { $stream.Dispose() }
}

function Test-DownlevelUcrtRoot {
    param([string]$Root)
    if ([string]::IsNullOrWhiteSpace($Root)) { return $false }
    $required = @(
        'ucrtbase.dll',
        'api-ms-win-crt-conio-l1-1-0.dll',
        'api-ms-win-crt-convert-l1-1-0.dll',
        'api-ms-win-crt-environment-l1-1-0.dll',
        'api-ms-win-crt-filesystem-l1-1-0.dll',
        'api-ms-win-crt-heap-l1-1-0.dll',
        'api-ms-win-crt-locale-l1-1-0.dll',
        'api-ms-win-crt-math-l1-1-0.dll',
        'api-ms-win-crt-multibyte-l1-1-0.dll',
        'api-ms-win-crt-private-l1-1-0.dll',
        'api-ms-win-crt-process-l1-1-0.dll',
        'api-ms-win-crt-runtime-l1-1-0.dll',
        'api-ms-win-crt-stdio-l1-1-0.dll',
        'api-ms-win-crt-string-l1-1-0.dll',
        'api-ms-win-crt-time-l1-1-0.dll',
        'api-ms-win-crt-utility-l1-1-0.dll'
    )
    foreach ($architecture in @('x86', 'x64')) {
        foreach ($name in $required) {
            if (-not (Test-Path -LiteralPath (Join-Path (Join-Path $Root $architecture) $name))) { return $false }
        }
        $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
            (Join-Path (Join-Path $Root $architecture) 'ucrtbase.dll'))
        # 10.0.14393 is Microsoft's last down-level UCRT baseline for Vista-8.1.
        if ($version.FileMajorPart -ne 10 -or $version.FileBuildPart -ne 14393) { return $false }
        $expectedMachine = if ($architecture -eq 'x86') { 0x014c } else { 0x8664 }
        if ((Get-PeMachine (Join-Path (Join-Path $Root $architecture) 'ucrtbase.dll')) -ne $expectedMachine) { return $false }
    }
    return $true
}

function Find-DownlevelUcrtRoot {
    param([string]$ExplicitRoot)
    $candidates = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($ExplicitRoot)) { $candidates.Add($ExplicitRoot) }
    $programFilesX86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $candidates.Add((Join-Path $programFilesX86 'Windows Kits\10\Redist\10.0.14393.0\ucrt\DLLs'))
        $candidates.Add((Join-Path $programFilesX86 'Windows Kits\10\Redist\ucrt\DLLs'))
        $visualStudioRoot = Join-Path $programFilesX86 'Microsoft Visual Studio'
        if (Test-Path -LiteralPath $visualStudioRoot) {
            foreach ($release in Get-ChildItem -LiteralPath $visualStudioRoot -Directory -ErrorAction SilentlyContinue) {
                foreach ($edition in Get-ChildItem -LiteralPath $release.FullName -Directory -ErrorAction SilentlyContinue) {
                    $remoteDebugger = Join-Path $edition.FullName 'Common7\IDE\Remote Debugger'
                    if (Test-Path -LiteralPath $remoteDebugger) { $candidates.Add($remoteDebugger) }
                }
            }
        }
    }
    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (Test-DownlevelUcrtRoot $candidate) { return (Resolve-Path -LiteralPath $candidate).Path }
    }
    throw 'The Windows 8 down-level Universal CRT 10.0.14393 redistributables were not found. Install the Visual Studio C++ build tools/remote debugger components, the Windows 10 SDK 14393, or pass -UcrtRedistRoot with x86 and x64 subdirectories.'
}

$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$windows8 = Split-Path $PSScriptRoot -Parent
$declared = ((Select-Xml -LiteralPath (Join-Path $windows8 'Directory.Build.props') -XPath '//Version' |
    Select-Object -First 1).Node.InnerText -replace '-.*$', '').Trim()
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = $declared }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "The version must look like 1.2.3: $Version" }
if ($Version -ne $declared) { throw "Version $Version does not match windows8\Directory.Build.props ($declared). Update the project version and rebuild first." }
$source = Join-Path $windows8 'src\ESDInstaller.Windows8.App\bin\Release\net461'
$staging = Join-Path $repository "work\windows8-package-$Version"
$outputs = Join-Path $repository 'outputs'
$installer = Join-Path $outputs "ESD-Installer-Windows8-Setup-$Version.exe"
$checksum = [System.IO.Path]::ChangeExtension($installer, '.sha256.txt')
$icon = Join-Path $windows8 'src\ESDInstaller.Windows8.App\Assets\ESDInstaller.Windows8.ico'
$nsi = Join-Path $PSScriptRoot 'ESDInstaller.Windows8.nsi'
$compiler = Get-ChildItem -LiteralPath (Join-Path $repository 'work\tools') -Recurse -Filter makensis.exe | Select-Object -First 1
if ($null -eq $compiler) { throw 'makensis.exe was not found under work\tools.' }
if (-not (Test-Path -LiteralPath (Join-Path $source 'ESDInstaller.Windows8.exe'))) { throw 'Build the Windows 8/8.1 solution first.' }
# Refuse to package an incomplete build or binaries from a different version.
foreach ($required in @('ESDInstaller.Windows8.exe', 'Worker\ESDInstaller.Windows8.Worker.exe', 'x86\libwim-15.dll', 'x64\libwim-15.dll', 'Worker\x86\libwim-15.dll', 'Worker\x64\libwim-15.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required))) { throw "The Windows 8/8.1 build is incomplete; missing $required. Rebuild the solution." }
}
foreach ($binary in @('ESDInstaller.Windows8.exe', 'Worker\ESDInstaller.Windows8.Worker.exe')) {
    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $source $binary))
    $built = "$($info.FileMajorPart).$($info.FileMinorPart).$($info.FileBuildPart)"
    if ($built -ne $Version) { throw "$binary is version $built, not $Version. Rebuild before packaging." }
}
if ((Test-Path -LiteralPath $installer) -and -not $Force) { throw "$installer already exists. Pass -Force to replace it." }
$ucrtSource = Find-DownlevelUcrtRoot $UcrtRedistRoot
if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $staging -Recurse -Force
Get-ChildItem -LiteralPath $staging -Recurse -Filter '*.pdb' | Remove-Item -Force
foreach ($architecture in @('x86', 'x64')) {
    $ucrtDestination = Join-Path $staging "Redist\UCRT\$architecture"
    New-Item -ItemType Directory -Path $ucrtDestination -Force | Out-Null
    $ucrtArchitectureSource = Join-Path $ucrtSource $architecture
    Copy-Item -Path (Join-Path $ucrtArchitectureSource 'api-ms-win-crt-*.dll') -Destination $ucrtDestination -Force
    Copy-Item -LiteralPath (Join-Path $ucrtArchitectureSource 'ucrtbase.dll') -Destination $ucrtDestination -Force
}
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE') -Destination (Join-Path $staging 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $windows8 'README.md') -Destination (Join-Path $staging 'README.md')
Copy-Item -LiteralPath (Join-Path $windows8 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $staging 'THIRD-PARTY-NOTICES.md')
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
