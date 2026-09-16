using System.Management;
using ESDInstaller.Windows7.Core.Models;

namespace ESDInstaller.Windows7.Core.Services;

public sealed class DiskService
{
    public Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Enumerate(cancellationToken), cancellationToken);

    private static IReadOnlyList<DiskInfo> Enumerate(CancellationToken cancellationToken)
    {
        var result = new List<DiskInfo>();
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
            using (var disks = searcher.Get())
            {
                foreach (ManagementObject disk in disks)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var number = ToInt(disk["Index"]);
                    var partitions = EnumeratePartitions(number, cancellationToken);
                    var size = ToLong(disk["Size"]);
                    AddUnallocatedRegions(partitions, number, size);
                    var scheme = partitions.Any(p => p.Type.StartsWith("GPT", StringComparison.OrdinalIgnoreCase))
                        ? PartitionScheme.Gpt : PartitionScheme.Mbr;
                    var model = Text(disk["Model"]);
                    result.Add(new DiskInfo(number, model, model, Text(disk["SerialNumber"]).Trim(),
                        Text(disk["PNPDeviceID"]).Trim(), Text(disk["DeviceID"]), Text(disk["InterfaceType"]),
                        size, scheme, partitions.Any(p => p.IsCurrentWindows), partitions.Any(p => p.IsSystem),
                        false, false, partitions.OrderBy(p => p.OffsetBytes).ToArray()));
                }
            }
        }
        catch (Exception exception)
        { throw new ESDInstallerException("ErrorDiskEnumeration", exception.Message, exception); }
        return result.OrderBy(d => d.Number).ToArray();
    }

    private static List<PartitionInfo> EnumeratePartitions(int diskNumber,
        CancellationToken cancellationToken)
    {
        var result = new List<PartitionInfo>();
        var query = "SELECT * FROM Win32_DiskPartition WHERE DiskIndex=" + diskNumber;
        using (var searcher = new ManagementObjectSearcher(query))
        using (var partitions = searcher.Get())
        {
            foreach (ManagementObject partition in partitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var type = Text(partition["Type"]);
                if (type.IndexOf("Extended", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                var logical = FindLogicalDisk(partition);
                var letterText = logical == null ? string.Empty : Text(logical["DeviceID"]);
                char? letter = letterText.Length > 0 ? char.ToUpperInvariant(letterText[0]) : (char?)null;
                var current = letter.HasValue && string.Equals(letter + ":", Environment.GetEnvironmentVariable("SystemDrive"),
                    StringComparison.OrdinalIgnoreCase);
                var fileSystem = Text(logical == null ? null : logical["FileSystem"]);
                var role = ClassifyPartitionRole(type, fileSystem, letter.HasValue);
                var paths = letter.HasValue ? new[] { letter + ":\\" } : Array.Empty<string>();
                result.Add(new PartitionInfo(diskNumber, ToInt(partition["Index"]) + 1,
                    ToLong(partition["StartingOffset"]), ToLong(partition["Size"]), letter,
                    Text(logical == null ? null : logical["VolumeName"]),
                    fileSystem, string.Empty, type,
                    GptTypeForRole(role), 0, role, ToBool(partition["Bootable"]),
                    ToBool(partition["BootPartition"]) || current,
                    role == PartitionRole.EfiSystem || (ToBool(partition["Bootable"]) && !current),
                    current, letter.HasValue && IsBitLocker(letter.Value), false, false, false, paths));
                logical?.Dispose();
            }
        }
        return result;
    }

    private static ManagementObject? FindLogicalDisk(ManagementObject partition)
    {
        try
        {
            using (var logical = partition.GetRelated("Win32_LogicalDisk"))
                return logical.Cast<ManagementObject>().FirstOrDefault();
        }
        catch (ManagementException) { return null; }
    }

    private static bool IsBitLocker(char letter)
    {
        try
        {
            var scope = new ManagementScope(@"\\.\root\CIMV2\Security\MicrosoftVolumeEncryption");
            scope.Connect();
            using (var searcher = new ManagementObjectSearcher(scope,
                       new ObjectQuery("SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter='" + letter + ":'")))
            using (var items = searcher.Get())
            {
                foreach (ManagementObject item in items)
                {
                    using (item)
                    {
                        if (HasActiveBitLockerProtection(ToInt(item["ProtectionStatus"]),
                                ToInt(item["ConversionStatus"])))
                            return true;
                    }
                }
                return false;
            }
        }
        catch { return false; }
    }

    internal static bool HasActiveBitLockerProtection(int protectionStatus, int conversionStatus) =>
        protectionStatus == 1 || conversionStatus != 0;

    internal static PartitionRole ClassifyPartitionRole(string type, string fileSystem, bool hasDriveLetter)
    {
        var isEfiSystem = type.StartsWith("GPT: System", StringComparison.OrdinalIgnoreCase) ||
                          (type.IndexOf("EFI", StringComparison.OrdinalIgnoreCase) >= 0 &&
                           type.IndexOf("System", StringComparison.OrdinalIgnoreCase) >= 0);
        if (isEfiSystem) return PartitionRole.EfiSystem;
        if (type.IndexOf("Reserved", StringComparison.OrdinalIgnoreCase) >= 0) return PartitionRole.MicrosoftReserved;
        if (type.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0) return PartitionRole.Recovery;
        if (type.IndexOf("OEM", StringComparison.OrdinalIgnoreCase) >= 0) return PartitionRole.Oem;
        if (type.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) >= 0 ||
            type.IndexOf("Installable", StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(type.Trim(), "IFS", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(fileSystem) || hasDriveLetter) return PartitionRole.BasicData;
        return PartitionRole.Unknown;
    }

    private static string GptTypeForRole(PartitionRole role)
    {
        switch (role)
        {
            case PartitionRole.EfiSystem: return "c12a7328-f81f-11d2-ba4b-00a0c93ec93b";
            case PartitionRole.MicrosoftReserved: return "e3c9e316-0b5c-4db8-817d-f92df00215ae";
            case PartitionRole.Recovery: return "de94bba4-06d1-4d40-a16a-bfd50179d6ac";
            case PartitionRole.BasicData: return "ebd0a0a2-b9e5-4433-87c0-68b6b72699c7";
            default: return string.Empty;
        }
    }

    private static void AddUnallocatedRegions(List<PartitionInfo> partitions, int diskNumber, long size)
    {
        const long minimum = 1024L * 1024;
        long cursor = 0;
        foreach (var partition in partitions.OrderBy(p => p.OffsetBytes).ToArray())
        {
            var gap = partition.OffsetBytes - cursor;
            if (gap >= minimum) partitions.Add(Unallocated(diskNumber, cursor, gap));
            cursor = Math.Max(cursor, partition.OffsetBytes + partition.LengthBytes);
        }
        if (size - cursor >= minimum) partitions.Add(Unallocated(diskNumber, cursor, size - cursor));
    }

    private static PartitionInfo Unallocated(int disk, long offset, long length) =>
        new PartitionInfo(disk, 0, offset, length, null, string.Empty, string.Empty, string.Empty,
            "Unallocated", string.Empty, 0, PartitionRole.Unallocated, false, false, false, false,
            false, false, false, true, Array.Empty<string>());

    private static string Text(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int ToInt(object? value) => value == null ? 0 : Convert.ToInt32(value);
    private static long ToLong(object? value) => value == null ? 0 : Convert.ToInt64(value);
    private static bool ToBool(object? value) => value != null && Convert.ToBoolean(value);
}
