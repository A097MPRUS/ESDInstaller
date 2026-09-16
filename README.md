# ESD Installer

ESD Installer is a native Windows desktop utility for applying a Windows installation image directly to one existing partition, installing the matching boot files, verifying the result, and then restarting into the deployed system. It does not use Electron or embedded web content.

Three native editions are maintained in this repository:

- **Windows 10/11 edition:** C#, .NET 8, and WinUI 3.
- **Windows 8/8.1 edition:** C#, .NET Framework 4.6.1, and WPF with the same Windows 10-style interface.
- **Windows 7 edition:** C#, .NET Framework 4.8, and WPF, with Aero integration and a Windows 7 Basic fallback.

> [!CAUTION]
> **Destructive-use and liability notice:** ESD Installer performs privileged disk-formatting, image-deployment, and boot-configuration operations. A mistake, software defect, power loss, storage failure, or incorrect selection can permanently erase data, damage partition layouts, or leave a computer unbootable. You are solely responsible for verified backups, correct target selection, installation media, and recovery capability. To the fullest extent permitted by law, the author and contributors are not responsible for data loss, unintended wipes, failed or "dead" storage devices, downtime, or hardware, software, or consequential damage. Read the full [Disclaimer](DISCLAIMER.md) before use.

The application includes multiple safety checks and refuses workflows it cannot validate, but no disk-deployment utility can eliminate every risk.

## Platform support

| Edition | Host operating system | UI framework | Availability |
| --- | --- | --- | --- |
| Windows 10/11 | Windows 10 or Windows 11 | .NET 8 and WinUI 3 | Version 0.1.12 |
| Windows 8/8.1 | Windows 8 or Windows 8.1 | .NET Framework 4.6.1 and WPF | Version 0.1.11 |
| Windows 7 | Windows 7 SP1 or later | .NET Framework 4.8 and WPF | Version 0.1.11 |

The Windows 7-compatible edition has now been added under [`windows7/`](windows7/). It preserves the same six-page installation workflow, safety model, features, controls, localization, and elevated-worker separation while replacing Windows 10-only platform dependencies.

The Windows 8/8.1 edition is isolated under [`windows8/`](windows8/). It targets .NET Framework 4.6.1 for genuine Windows 8 support, bundles its Windows 10-style vector UI, and has separate executable, settings, worker, installer, registry, Start-menu, and uninstall identities. It does not replace either existing edition.

## Downloads

The [latest release](https://github.com/A097MPRUS/ESDInstaller/releases/latest) contains one installer for each supported host edition and a matching SHA-256 checksum file:

- `ESD-Installer-Setup-0.1.12.exe` for Windows 10 and Windows 11.
- `ESD-Installer-Windows8-Setup-0.1.11.exe` for Windows 8 and Windows 8.1.
- `ESD-Installer-Windows7-Setup-0.1.11.exe` for Windows 7 SP1.

Portable builds are not published. Review the [changelog](CHANGELOG.md) before installing.

Version 0.1.14 of all three editions is available as a [test release](https://github.com/A097MPRUS/ESDInstaller/releases/tag/v0.1.14). It has not yet been tested through a full Windows installation, so try it in a disposable virtual machine first.

## Optional update notifications

Each edition can check for updates at startup, at most once every 24 hours. Automatic checks are enabled by default and can be disabled in Settings; **Check for Updates** always remains available for a manual check. A failed automatic check never interrupts startup, and updates are never installed without confirmation.

The three editions use separate manifests in [`updates/`](updates/) so a host is offered only its matching installer. Before publishing a release, update the appropriate manifest with the application's semantic version, the release asset's HTTPS URL, its SHA-256 checksum, and optional release notes. The application downloads only after the user selects **Update**, shows actual transfer progress, verifies the complete SHA-256 checksum, and launches the installer only when verification succeeds.

## Supported first-release workflow

- Host application: Windows 7 SP1, Windows 8/8.1, Windows 10, or Windows 11, using the corresponding native edition.
- Source files: `.iso`, `.wim`, and `.esd`.
- Image deployment: Windows 7, 8, 8.1, 10, and 11 WIM/ESD indexes.
- Destination: one existing, suitable partition on an enumerated physical disk.
- Firmware layouts: UEFI/GPT and Legacy BIOS/MBR when the image and machine support them.
- Boot files: an existing same-disk EFI System Partition for UEFI, or an existing active NTFS partition for BIOS.
- Application and installer languages in every edition: English, French, German, Luxembourgish, Serbian (Latin), Russian, Simplified Chinese, Traditional Chinese, Spanish, Polish, Greek, Danish, Norwegian Bokmål, Norwegian Nynorsk, Finnish, Swedish, Mongolian (Cyrillic), Armenian, Kazakh, Bashkir, Tatar, Crimean Tatar, Abkhazian, Ossetian, Arabic, Hebrew, Persian, Afrikaans, Hungarian, Portuguese, Czech, Uyghur (Cyrillic), Turkish, Thai, Korean, Japanese, Georgian, Azerbaijani, Kyrgyz, Italian, Romanian, and Icelandic, plus system-language selection.

Windows Vista media is routed to the separate legacy NT6 engine and Windows XP media to the legacy NT5 engine. Those engines deliberately report that they are unavailable instead of attempting an invalid WIM-era deployment. Split `install.swm` media, raw/unpartitioned disks, partition creation, and cross-disk EFI modification are blocked in this release. Unsupported Windows 11 hardware remains blocked by default; Advanced Mode provides a separately confirmed bypass for CPU, TPM, Secure Boot, RAM, storage, and UEFI policy checks while retaining architecture, disk-identity, partition, and boot safety checks.

## Safety architecture

The unelevated UI process (WinUI on Windows 10/11 or WPF on Windows 7/8/8.1) performs inspection and planning. Installation creates an immutable `InstallationPlan` containing the source metadata, WIM index, disk PnP ID and serial, disk size and scheme, partition number, offset, length, GUID, boot partition, and firmware mode. The confirmation page shows the target model, disk number, partition number, drive letter, label, and capacity.

Only after confirmation does ESD Installer launch `ESDInstaller.Worker.exe` with UAC. The worker:

1. Reopens the WIM/ESD and verifies the selected index.
2. Re-enumerates the physical disk and checks its stable identity.
3. Rechecks the target and boot partition geometry, GUID, protected state, and firmware layout.
4. Formats only the selected partition as NTFS.
5. Runs DISM with `/Apply-Image`, `/CheckIntegrity`, and `/Verify` while reading real progress.
6. Runs BCDBoot against the validated same-disk boot partition.
7. Verifies critical installed files, Windows Boot Manager, and the architecture-appropriate UEFI fallback file (or the BIOS BCD store).
8. Reports success only if every critical operation returns success and every file check passes.

On a secondary UEFI disk, BCDBoot's `/s` behavior intentionally avoids modifying NVRAM. The standard UEFI fallback loader is verified; the user may need to select that disk once in the firmware boot menu. This follows Microsoft's documented [BCDBoot `/s` behavior](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/bcdboot-command-line-options-techref-di?view=windows-11).

## Build the Windows 10/11 edition

Requirements:

- Windows 10 version 1809 or later, or Windows 11
- .NET 8 SDK (a newer SDK capable of targeting .NET 8 also works)
- Windows App SDK NuGet packages (restored automatically)
- Windows 10/11 SDK

```powershell
dotnet restore ESDInstaller.slnx
dotnet build src\ESDInstaller.App\ESDInstaller.App.csproj -c Release -r win-x64 -p:Platform=x64
dotnet run --project tests\ESDInstaller.Core.Tests\ESDInstaller.Core.Tests.csproj -c Release
```

The main executable is `ESDInstaller.exe`. The elevated worker and its private runtime files in the `Worker` subdirectory are required and must remain with the application.

## Build the Windows 7 edition

Requirements:

- Windows 7 SP1 or later
- .NET Framework 4.8 on the runtime computer
- A current .NET SDK for building; the project restores its .NET Framework reference assemblies automatically

```powershell
dotnet build windows7\ESDInstaller.Windows7.sln -c Release
windows7\tests\ESDInstaller.Windows7.Tests\bin\Release\net48\ESDInstaller.Windows7.Tests.exe
```

The Windows 7 edition reads ISO/UDF media directly because Windows 7 does not provide `Mount-DiskImage`. It uses ManagedWimLib/wimlib for WIM and ESD operations, WMI for disk enumeration, a validated narrowly scoped DiskPart operation, and the host's BCDBoot. See the dedicated [Windows 7 documentation](windows7/README.md) for details. Its installer is published separately from the Windows 10/11 package.

## Build the Windows 8/8.1 edition

Requirements:

- Windows 8 or Windows 8.1 desktop (x86 or x64; Windows RT is not supported)
- .NET Framework 4.6.1 or a later compatible in-place .NET Framework 4.x runtime
- A current .NET SDK for building; reference assemblies restore automatically

```powershell
dotnet build windows8\ESDInstaller.Windows8.sln -c Release
windows8\tests\ESDInstaller.Windows8.Tests\bin\Release\net461\ESDInstaller.Windows8.Tests.exe
powershell.exe -NoProfile -ExecutionPolicy Bypass -File windows8\installer\build-installer.ps1
```

This edition uses direct ISO/UDF reading, ManagedWimLib/wimlib 1.14.3, WMI disk enumeration, an immutable validated plan, narrowly scoped DiskPart formatting, and the host's BCDBoot. See [the Windows 8/8.1 documentation](windows8/README.md). The installer intentionally accepts only Windows 8 and Windows 8.1 hosts.

## Logs

Installation logs are written to the edition-specific local application-data directory:

```text
Windows 10/11: %LOCALAPPDATA%\ESDInstaller\Logs\Install-yyyy-MM-dd-HHmmss.log
Windows 8/8.1: %LOCALAPPDATA%\ESDInstallerWindows8\Logs\Install-yyyy-MM-dd-HHmmss.log
Windows 7:     %LOCALAPPDATA%\ESDInstallerWindows7\Logs\Install-yyyy-MM-dd-HHmmss.log
```

Logs include the selected image/index, stable disk and partition identifiers, commands, output, exit codes, elapsed times, verification stages, and failures. They intentionally avoid collecting credentials or unrelated user data.

## Project layout

```text
src/
  ESDInstaller.App/       WinUI 3 wizard, localization, disk map, UAC bridge
  ESDInstaller.Core/      models, WIM API, disk/image services, validators, engines
  ESDInstaller.Worker/    narrowly scoped elevated execution process
tests/
  ESDInstaller.Core.Tests/ dependency-free safety and read-only integration tests
docs/
  SAFETY.md            threat model and first-release limitations
windows7/
  src/                 separate WPF/.NET Framework 4.8 application, core, and worker
  tests/               Windows 7-compatible safety tests
  installer/           installer source (no untested binary is published)
windows8/
  src/                 separate WPF/.NET Framework 4.6.1 application, core, and worker
  tests/               Windows 8/8.1-compatible safety and integration tests
  installer/           distinct Windows 8/8.1 installer source
```

DISM `/Apply-Image` and its verification options are documented in Microsoft's [DISM image-management reference](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-image-management-command-line-options-s14?view=windows-11). WIM metadata is read with Microsoft's [Windows Imaging API](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/wim/dd834949%28v%3Dmsdn.10%29?view=windows-11).

## License

ESD Installer is released under the [MIT License](LICENSE).
