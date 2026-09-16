using System.Runtime.InteropServices;
using System.Text.Json;
using ESDInstaller.Core.Models;

namespace ESDInstaller.Core.Services;

public sealed class CompatibilityService
{
    private readonly ProcessRunner _processRunner;

    public CompatibilityService(ProcessRunner processRunner) => _processRunner = processRunner;

    public async Task<CompatibilitySnapshot> InspectHostAsync(CancellationToken cancellationToken = default)
    {
        var firmware = GetFirmwareMode();
        var architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => CpuArchitecture.X64,
            Architecture.X86 => CpuArchitecture.X86,
            Architecture.Arm64 => CpuArchitecture.Arm64,
            Architecture.Arm => CpuArchitecture.Arm,
            _ => CpuArchitecture.Unknown
        };
        var memory = GetPhysicalMemory();

        const string script = """
            $tpmPresent = $false; $tpmReady = $false
            try { $tpm = Get-Tpm -ErrorAction Stop; $tpmPresent = [bool]$tpm.TpmPresent; $tpmReady = [bool]$tpm.TpmReady } catch {}
            $secureCapable = $false; $secureEnabled = $false
            try { $secureEnabled = [bool](Confirm-SecureBootUEFI -ErrorAction Stop); $secureCapable = $true }
            catch [System.PlatformNotSupportedException] { $secureCapable = $false }
            catch { if ($_.Exception.Message -notmatch 'not supported') { $secureCapable = $true } }
            [pscustomobject]@{ TpmPresent=$tpmPresent; TpmReady=$tpmReady; SecureBootCapable=$secureCapable; SecureBootEnabled=$secureEnabled } | ConvertTo-Json -Compress
            """;
        var result = await _processRunner.RunPowerShellAsync(script, cancellationToken: cancellationToken).ConfigureAwait(false);
        var security = new SecurityDto();
        if (result.Succeeded)
        {
            try
            {
                var start = result.StandardOutput.IndexOf('{');
                var end = result.StandardOutput.LastIndexOf('}');
                if (start >= 0 && end >= start)
                    security = JsonSerializer.Deserialize<SecurityDto>(result.StandardOutput[start..(end + 1)],
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? security;
            }
            catch { }
        }

        return new CompatibilitySnapshot(firmware, architecture, security.TpmPresent, security.TpmReady,
            security.SecureBootCapable, security.SecureBootEnabled, memory);
    }

    public PlanValidationResult CheckImageCompatibility(WindowsImage image, WindowsImageEdition edition,
        DiskInfo disk, PartitionInfo destination, PartitionInfo? boot, CompatibilitySnapshot host,
        bool bypassWindows11Requirements = false)
    {
        var issues = new List<PlanIssue>();
        if (host.FirmwareMode != FirmwareMode.Bios && host.FirmwareMode != FirmwareMode.Uefi)
            issues.Add(Error("unknown-firmware", "ValidationFirmwareSchemeChanged", host.FirmwareMode.ToString()));
        if (destination.DiskNumber != disk.Number)
            issues.Add(Error("target-other-disk", "ValidationPartitionChanged", destination.StableKey));
        var bypass = bypassWindows11Requirements && image.Generation == WindowsGeneration.Windows11;
        if (image.RequiresLegacyEngine)
            issues.Add(Error("legacy-engine", image.LegacyReason ?? "LegacyEngineUnavailable", image.DisplayVersion));
        if (image.Generation == WindowsGeneration.Unknown)
            issues.Add(Error("unknown-version", "ValidationUnknownWindowsVersion", edition.Build.ToString()));
        if (!ArchitectureCompatible(edition.Architecture, host.HostArchitecture, host.FirmwareMode))
            issues.Add(Error("architecture", "ValidationArchitectureMismatch", $"{edition.Architecture} / {host.HostArchitecture}"));
        if (destination.IsProtected)
            issues.Add(Error("protected-target", "ValidationProtectedPartition", destination.StableKey));
        if (destination.PartitionNumber <= 0 || destination.Role != PartitionRole.BasicData)
            issues.Add(Error("unsupported-target", "ValidationDestinationMustBeBasicData", destination.StableKey));
        if (destination.IsBitLocker)
            issues.Add(Error("bitlocker", "ValidationBitLockerTarget", destination.DriveDisplay));
        if (destination.LengthBytes < MinimumPartitionBytes(image.Generation))
            issues.Add(bypass
                ? Warning("capacity", "ValidationInsufficientSpace", destination.LengthBytes.ToString())
                : Error("capacity", "ValidationInsufficientSpace", destination.LengthBytes.ToString()));
        if (disk.IsReadOnly || disk.IsOffline)
            issues.Add(Error("disk-state", "ValidationDiskUnavailable", disk.StableKey));
        if (host.FirmwareMode == FirmwareMode.Uefi && disk.PartitionScheme != PartitionScheme.Gpt)
            issues.Add(Error("uefi-gpt", "ValidationUefiRequiresGpt", disk.PartitionScheme.ToString()));
        if (host.FirmwareMode == FirmwareMode.Uefi && !disk.IsSystem)
            issues.Add(Warning("uefi-secondary-disk", "ValidationSecondaryDiskFirmwareBoot", disk.StableKey));
        if (host.FirmwareMode == FirmwareMode.Bios && disk.PartitionScheme != PartitionScheme.Mbr)
            issues.Add(Error("bios-mbr", "ValidationBiosRequiresMbr", disk.PartitionScheme.ToString()));
        if (boot is null)
            issues.Add(Error("boot-missing", "ValidationBootPartitionMissing", disk.StableKey));
        else
        {
            if (boot.DiskNumber != destination.DiskNumber)
                issues.Add(Error("boot-other-disk", "ValidationBootPartitionOtherDisk", boot.StableKey));
            if (host.FirmwareMode == FirmwareMode.Uefi && boot.Role != PartitionRole.EfiSystem)
                issues.Add(Error("boot-not-efi", "ValidationEfiPartitionRequired", boot.StableKey));
            if (host.FirmwareMode == FirmwareMode.Uefi && !boot.FileSystem.Equals("FAT32", StringComparison.OrdinalIgnoreCase))
                issues.Add(Error("boot-not-fat32", "ValidationEfiFat32Required", boot.FileSystem));
            if (host.FirmwareMode == FirmwareMode.Bios && !boot.IsActive)
                issues.Add(Error("boot-not-active", "ValidationActivePartitionRequired", boot.StableKey));
        }

        if (image.Generation == WindowsGeneration.Windows7 && host.FirmwareMode == FirmwareMode.Uefi && edition.Architecture != CpuArchitecture.X64)
            issues.Add(Error("win7-uefi-x86", "ValidationWindows7UefiX64", edition.Architecture.ToString()));

        if (image.Generation == WindowsGeneration.Windows11)
        {
            if (edition.Architecture is not (CpuArchitecture.X64 or CpuArchitecture.Arm64))
                issues.Add(Error("win11-arch", "ValidationWindows11Architecture", edition.Architecture.ToString()));
            if (host.FirmwareMode != FirmwareMode.Uefi)
                issues.Add(bypass
                    ? Warning("win11-uefi", "ValidationWindows11Uefi", host.FirmwareMode.ToString())
                    : Error("win11-uefi", "ValidationWindows11Uefi", host.FirmwareMode.ToString()));
            if (!host.TpmPresent || !host.TpmReady)
                issues.Add(bypass
                    ? Warning("win11-tpm", "ValidationWindows11Tpm", $"Present={host.TpmPresent}; Ready={host.TpmReady}")
                    : Error("win11-tpm", "ValidationWindows11Tpm", $"Present={host.TpmPresent}; Ready={host.TpmReady}"));
            if (!host.SecureBootCapable)
                issues.Add(bypass
                    ? Warning("win11-secureboot", "ValidationWindows11SecureBoot", "Not capable or unavailable")
                    : Error("win11-secureboot", "ValidationWindows11SecureBoot", "Not capable or unavailable"));
            else if (!host.SecureBootEnabled)
                issues.Add(Warning("win11-secureboot-disabled", "ValidationSecureBootDisabled", string.Empty));
            if (host.PhysicalMemoryBytes < 4L * 1024 * 1024 * 1024)
                issues.Add(bypass
                    ? Warning("win11-memory", "ValidationWindows11Memory", host.PhysicalMemoryBytes.ToString())
                    : Error("win11-memory", "ValidationWindows11Memory", host.PhysicalMemoryBytes.ToString()));
            if (bypass)
                issues.Add(Warning("win11-bypass", "ValidationWindows11BypassEnabled", string.Empty));
        }

        return new PlanValidationResult(issues);
    }

    public static PartitionInfo? FindBootPartition(DiskInfo disk, FirmwareMode firmwareMode, PartitionInfo destination)
    {
        if (firmwareMode != FirmwareMode.Bios && firmwareMode != FirmwareMode.Uefi) return null;
        if (firmwareMode == FirmwareMode.Uefi)
            return disk.Partitions.FirstOrDefault(partition => partition.Role == PartitionRole.EfiSystem && !partition.IsOffline && !partition.IsReadOnly);
        return disk.Partitions.FirstOrDefault(partition => partition.IsActive && partition.FileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            ?? (destination.IsActive ? destination : null);
    }

    private static bool ArchitectureCompatible(CpuArchitecture image, CpuArchitecture host, FirmwareMode firmware) => (image, host) switch
    {
        (CpuArchitecture.X64, CpuArchitecture.X64) => true,
        (CpuArchitecture.X86, CpuArchitecture.X86) => true,
        (CpuArchitecture.X86, CpuArchitecture.X64) => firmware == FirmwareMode.Bios,
        (CpuArchitecture.Arm64, CpuArchitecture.Arm64) => true,
        (CpuArchitecture.Arm, CpuArchitecture.Arm) => true,
        _ => false
    };

    private static long MinimumPartitionBytes(WindowsGeneration generation) => generation switch
    {
        WindowsGeneration.Windows11 => 64L * 1024 * 1024 * 1024,
        WindowsGeneration.Windows10 => 32L * 1024 * 1024 * 1024,
        WindowsGeneration.Windows81 or WindowsGeneration.Windows8 => 20L * 1024 * 1024 * 1024,
        WindowsGeneration.Windows7 => 16L * 1024 * 1024 * 1024,
        _ => 20L * 1024 * 1024 * 1024
    };

    private static PlanIssue Error(string code, string key, string detail) => new(PlanSeverity.Error, code, key, detail);
    private static PlanIssue Warning(string code, string key, string detail) => new(PlanSeverity.Warning, code, key, detail);

    private static FirmwareMode GetFirmwareMode()
    {
        try { return GetFirmwareType(out var type) && type == 2 ? FirmwareMode.Uefi : type == 1 ? FirmwareMode.Bios : FirmwareMode.Unknown; }
        catch { return FirmwareMode.Unknown; }
    }

    private static long GetPhysicalMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? checked((long)Math.Min(status.TotalPhysical, long.MaxValue)) : 0;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFirmwareType(out uint firmwareType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    private sealed class SecurityDto
    {
        public bool TpmPresent { get; set; }
        public bool TpmReady { get; set; }
        public bool SecureBootCapable { get; set; }
        public bool SecureBootEnabled { get; set; }
    }
}
