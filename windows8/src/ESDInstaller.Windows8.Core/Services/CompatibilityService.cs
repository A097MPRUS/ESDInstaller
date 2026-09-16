using System.Management;
using System.Runtime.InteropServices;
using ESDInstaller.Windows8.Core.Models;

namespace ESDInstaller.Windows8.Core.Services;

public sealed class CompatibilityService
{
    public Task<CompatibilitySnapshot> InspectHostAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tpm = InspectTpm();
            var firmware = GetFirmwareMode();
            return new CompatibilitySnapshot(firmware, GetArchitecture(), tpm.Item1, tpm.Item2,
                firmware == FirmwareMode.Uefi, ReadSecureBootState(), GetPhysicalMemory());
        }, cancellationToken);

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
            issues.Add(Error("architecture", "ValidationArchitectureMismatch", edition.Architecture + " / " + host.HostArchitecture));
        if (destination.IsProtected)
            issues.Add(Error("protected-target", "ValidationProtectedPartition", destination.StableKey));
        if (destination.PartitionNumber <= 0 || destination.Role != PartitionRole.BasicData)
            issues.Add(Error("unsupported-target", "ValidationDestinationMustBeBasicData", destination.StableKey));
        if (destination.IsBitLocker)
            issues.Add(Error("bitlocker", "ValidationBitLockerTarget", destination.DriveDisplay));
        if (destination.LengthBytes < MinimumPartitionBytes(image.Generation))
            issues.Add(bypass ? Warning("capacity", "ValidationInsufficientSpace", destination.LengthBytes.ToString())
                              : Error("capacity", "ValidationInsufficientSpace", destination.LengthBytes.ToString()));
        if (disk.IsReadOnly || disk.IsOffline)
            issues.Add(Error("disk-state", "ValidationDiskUnavailable", disk.StableKey));
        if (host.FirmwareMode == FirmwareMode.Uefi && disk.PartitionScheme != PartitionScheme.Gpt)
            issues.Add(Error("uefi-gpt", "ValidationUefiRequiresGpt", disk.PartitionScheme.ToString()));
        if (host.FirmwareMode == FirmwareMode.Bios && disk.PartitionScheme != PartitionScheme.Mbr)
            issues.Add(Error("bios-mbr", "ValidationBiosRequiresMbr", disk.PartitionScheme.ToString()));
        if (boot == null)
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

        if (image.Generation == WindowsGeneration.Windows7 && host.FirmwareMode == FirmwareMode.Uefi &&
            edition.Architecture != CpuArchitecture.X64)
            issues.Add(Error("win7-uefi-x86", "ValidationWindows7UefiX64", edition.Architecture.ToString()));
        if (image.Generation == WindowsGeneration.Windows11)
        {
            if (edition.Architecture != CpuArchitecture.X64 && edition.Architecture != CpuArchitecture.Arm64)
                issues.Add(Error("win11-arch", "ValidationWindows11Architecture", edition.Architecture.ToString()));
            if (host.FirmwareMode != FirmwareMode.Uefi)
                issues.Add(bypass ? Warning("win11-uefi", "ValidationWindows11Uefi", host.FirmwareMode.ToString())
                                  : Error("win11-uefi", "ValidationWindows11Uefi", host.FirmwareMode.ToString()));
            if (!host.TpmPresent || !host.TpmReady)
                issues.Add(bypass ? Warning("win11-tpm", "ValidationWindows11Tpm", "")
                                  : Error("win11-tpm", "ValidationWindows11Tpm", ""));
            if (!host.SecureBootCapable)
                issues.Add(bypass ? Warning("win11-secureboot", "ValidationWindows11SecureBoot", "")
                                  : Error("win11-secureboot", "ValidationWindows11SecureBoot", ""));
            else if (!host.SecureBootEnabled)
                issues.Add(Warning("win11-secureboot-disabled", "ValidationSecureBootDisabled", ""));
            if (host.PhysicalMemoryBytes < 4L * 1024 * 1024 * 1024)
                issues.Add(bypass ? Warning("win11-memory", "ValidationWindows11Memory", host.PhysicalMemoryBytes.ToString())
                                  : Error("win11-memory", "ValidationWindows11Memory", host.PhysicalMemoryBytes.ToString()));
            if (bypass) issues.Add(Warning("win11-bypass", "ValidationWindows11BypassEnabled", ""));
        }
        return new PlanValidationResult(issues);
    }

    public static PartitionInfo? FindBootPartition(DiskInfo disk, FirmwareMode firmwareMode, PartitionInfo destination)
    {
        if (firmwareMode != FirmwareMode.Bios && firmwareMode != FirmwareMode.Uefi) return null;
        if (firmwareMode == FirmwareMode.Uefi)
            return disk.Partitions.FirstOrDefault(p => p.Role == PartitionRole.EfiSystem && !p.IsOffline && !p.IsReadOnly);
        return disk.Partitions.FirstOrDefault(p => p.IsActive && p.FileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
               ?? (destination.IsActive ? destination : null);
    }

    private static bool ArchitectureCompatible(CpuArchitecture image, CpuArchitecture host, FirmwareMode firmware)
    {
        if (image == CpuArchitecture.Unknown || host == CpuArchitecture.Unknown) return false;
        if (image == host) return true;
        return image == CpuArchitecture.X86 && host == CpuArchitecture.X64 && firmware == FirmwareMode.Bios;
    }

    private static long MinimumPartitionBytes(WindowsGeneration generation)
    {
        switch (generation)
        {
            case WindowsGeneration.Windows11: return 64L * 1024 * 1024 * 1024;
            case WindowsGeneration.Windows10: return 32L * 1024 * 1024 * 1024;
            case WindowsGeneration.Windows8: case WindowsGeneration.Windows81: return 20L * 1024 * 1024 * 1024;
            case WindowsGeneration.Windows7: return 16L * 1024 * 1024 * 1024;
            default: return 20L * 1024 * 1024 * 1024;
        }
    }

    private static Tuple<bool, bool> InspectTpm()
    {
        try
        {
            var scope = new ManagementScope(@"\\.\root\CIMV2\Security\MicrosoftTpm");
            scope.Connect();
            using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_Tpm")))
            using (var results = searcher.Get())
            {
                var tpm = results.Cast<ManagementObject>().FirstOrDefault();
                if (tpm == null) return Tuple.Create(false, false);
                var enabled = InvokeTpmBoolean(tpm, "IsEnabled");
                var activated = InvokeTpmBoolean(tpm, "IsActivated");
                var owned = InvokeTpmBoolean(tpm, "IsOwned");
                return Tuple.Create(true, enabled && activated && owned);
            }
        }
        catch { return Tuple.Create(false, false); }
    }

    private static bool InvokeTpmBoolean(ManagementObject tpm, string method)
    {
        try
        {
            var result = tpm.InvokeMethod(method, null, null);
            return result != null && Convert.ToBoolean(result.Properties["Is" + method.Substring(2)].Value);
        }
        catch { return false; }
    }

    private static FirmwareMode GetFirmwareMode()
    {
        try
        {
            uint type;
            if (GetFirmwareType(out type)) return type == 2 ? FirmwareMode.Uefi : type == 1 ? FirmwareMode.Bios : FirmwareMode.Unknown;
        }
        catch (EntryPointNotFoundException) { }

        SetLastError(0);
        GetFirmwareEnvironmentVariable("", "{00000000-0000-0000-0000-000000000000}", IntPtr.Zero, 0);
        return Marshal.GetLastWin32Error() == 1 ? FirmwareMode.Bios : FirmwareMode.Uefi;
    }

    private static CpuArchitecture GetArchitecture()
    {
        if (!Environment.Is64BitOperatingSystem) return CpuArchitecture.X86;
        var architecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ??
                           Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
        return architecture.IndexOf("ARM64", StringComparison.OrdinalIgnoreCase) >= 0
            ? CpuArchitecture.Arm64 : CpuArchitecture.X64;
    }

    private static bool ReadSecureBootState()
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State"))
                return Convert.ToInt32(key?.GetValue("UEFISecureBootEnabled", 0)) == 1;
        }
        catch { return false; }
    }

    private static long GetPhysicalMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx)) };
        return GlobalMemoryStatusEx(ref status) ? (long)Math.Min(status.TotalPhysical, (ulong)long.MaxValue) : 0;
    }

    private static PlanIssue Error(string code, string key, string detail) => new PlanIssue(PlanSeverity.Error, code, key, detail);
    private static PlanIssue Warning(string code, string key, string detail) => new PlanIssue(PlanSeverity.Warning, code, key, detail);

    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFirmwareType(out uint firmwareType);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFirmwareEnvironmentVariable(string name, string guid, IntPtr buffer, uint size);
    [DllImport("kernel32.dll")] private static extern void SetLastError(uint errorCode);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual,
            AvailableVirtual, AvailableExtendedVirtual;
    }
}
