using ESDInstaller.Core.Models;

namespace ESDInstaller.Core.Services;

public sealed class ExecutionPlanValidator
{
    private readonly DiskService _diskService;
    private readonly WimService _wimService;

    public ExecutionPlanValidator(DiskService diskService, WimService wimService)
    {
        _diskService = diskService;
        _wimService = wimService;
    }

    public async Task ValidateAsync(InstallationPlan plan, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlanStructure(plan);
        if (plan.Engine != InstallationEngineKind.ModernWindows)
            throw new ESDInstallerException("LegacyEngineUnavailable", plan.Engine.ToString());
        if (plan.BypassWindows11Requirements && plan.Generation != WindowsGeneration.Windows11)
            throw new ESDInstallerException("ValidationPlanUnreadable", "The Windows 11 bypass was set for a non-Windows 11 image.");
        if (!File.Exists(plan.Source.SourcePath) || !File.Exists(plan.Source.ImagePath))
            throw new ESDInstallerException("ErrorImageNotFound", plan.Source.SourcePath);

        var sourceInfo = new FileInfo(plan.Source.SourcePath);
        if (sourceInfo.Length != plan.Source.SourceSizeBytes || sourceInfo.LastWriteTimeUtc != plan.Source.SourceLastWriteUtc)
            throw new ESDInstallerException("ValidationSourceChanged", plan.Source.SourcePath);

        var editions = await _wimService.ReadEditionsAsync(plan.Source.ImagePath, cancellationToken).ConfigureAwait(false);
        var edition = editions.FirstOrDefault(candidate => candidate.Index == plan.Edition.Index);
        if (edition is null || edition.Build != plan.Edition.Build || edition.Architecture != plan.Edition.Architecture)
            throw new ESDInstallerException("ValidationEditionChanged", plan.Edition.Index.ToString());

        var disks = await _diskService.GetDisksAsync(cancellationToken).ConfigureAwait(false);
        var disk = disks.FirstOrDefault(candidate => candidate.Number == plan.DestinationDisk.DiskNumber);
        if (disk is null) throw new ESDInstallerException("ValidationDiskMissing", plan.DestinationDisk.DiskNumber.ToString());
        if (string.IsNullOrWhiteSpace(plan.DestinationDisk.UniqueId) && string.IsNullOrWhiteSpace(plan.DestinationDisk.SerialNumber))
            throw new ESDInstallerException("ValidationDiskIdentityUnavailable", plan.DestinationDisk.Model);
        if (!DiskMatches(disk, plan.DestinationDisk))
            throw new ESDInstallerException("ValidationDiskChanged", $"Expected {plan.DestinationDisk.UniqueId}; found {disk.UniqueId}");

        if (disk.IsReadOnly || disk.IsOffline)
            throw new ESDInstallerException("ValidationDiskUnavailable", disk.StableKey);
        var destination = FindPartition(disk, plan.DestinationPartition);
        if (destination is null) throw new ESDInstallerException("ValidationPartitionChanged", "Destination partition identity did not match.");
        if (destination.IsProtected || destination.IsBitLocker)
            throw new ESDInstallerException("ValidationProtectedPartition", destination.StableKey);
        if (destination.PartitionNumber <= 0 || destination.Role != PartitionRole.BasicData)
            throw new ESDInstallerException("ValidationDestinationMustBeBasicData", destination.StableKey);

        var boot = FindPartition(disk, plan.BootPartition);
        if (boot is null) throw new ESDInstallerException("ValidationBootPartitionChanged", "Boot partition identity did not match.");
        if (plan.FirmwareMode == FirmwareMode.Uefi && boot.Role != PartitionRole.EfiSystem)
            throw new ESDInstallerException("ValidationEfiPartitionRequired", boot.StableKey);
        if (boot.IsReadOnly || boot.IsOffline || boot.IsBitLocker)
            throw new ESDInstallerException("ErrorBootPartitionAccess", "The boot partition is unavailable or encrypted.");
        if (plan.FirmwareMode == FirmwareMode.Uefi && !string.Equals(boot.FileSystem, "FAT32", StringComparison.OrdinalIgnoreCase))
            throw new ESDInstallerException("ValidationEfiFat32Required", boot.FileSystem);
        if (plan.FirmwareMode == FirmwareMode.Bios && !boot.IsActive)
            throw new ESDInstallerException("ValidationActivePartitionRequired", boot.StableKey);
        if (plan.FirmwareMode == FirmwareMode.Uefi && disk.PartitionScheme != PartitionScheme.Gpt ||
            plan.FirmwareMode == FirmwareMode.Bios && disk.PartitionScheme != PartitionScheme.Mbr)
            throw new ESDInstallerException("ValidationFirmwareSchemeChanged", $"{plan.FirmwareMode}/{disk.PartitionScheme}");
    }

    internal static void ValidatePlanStructure(InstallationPlan plan)
    {
        if (plan == null || plan.Source == null || plan.Edition == null ||
            plan.DestinationDisk == null || plan.DestinationPartition == null || plan.BootPartition == null)
            throw new ESDInstallerException("ValidationPlanUnreadable", "The installation plan is incomplete.");
        if (plan.DestinationDisk.DiskNumber < 0 ||
            plan.DestinationPartition.DiskNumber != plan.DestinationDisk.DiskNumber ||
            plan.BootPartition.DiskNumber != plan.DestinationDisk.DiskNumber)
            throw new ESDInstallerException("ValidationPlanUnreadable", "Partition disk numbers do not match the validated disk.");
        foreach (var partition in new[] { plan.DestinationPartition, plan.BootPartition })
        {
            if (partition.PartitionNumber <= 0 || partition.OffsetBytes < 0 || partition.LengthBytes <= 0 ||
                partition.OffsetBytes > long.MaxValue - partition.LengthBytes)
                throw new ESDInstallerException("ValidationPartitionChanged", "Invalid partition geometry.");
        }
        if (plan.FirmwareMode != FirmwareMode.Bios && plan.FirmwareMode != FirmwareMode.Uefi)
            throw new ESDInstallerException("ValidationFirmwareSchemeChanged", "Firmware mode could not be determined.");
        if (plan.PartitionScheme != plan.DestinationDisk.PartitionScheme ||
            (plan.FirmwareMode == FirmwareMode.Bios && plan.PartitionScheme != PartitionScheme.Mbr) ||
            (plan.FirmwareMode == FirmwareMode.Uefi && plan.PartitionScheme != PartitionScheme.Gpt))
            throw new ESDInstallerException("ValidationFirmwareSchemeChanged", "Firmware and partition scheme do not match.");
    }

    private static bool DiskMatches(DiskInfo actual, DiskIdentity expected)
    {
        var uniqueMatches = string.IsNullOrWhiteSpace(expected.UniqueId) ||
            string.Equals(actual.UniqueId.Trim(), expected.UniqueId.Trim(), StringComparison.OrdinalIgnoreCase);
        var serialMatches = string.IsNullOrWhiteSpace(expected.SerialNumber) ||
            string.Equals(actual.SerialNumber.Trim(), expected.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase);
        return uniqueMatches && serialMatches && actual.SizeBytes == expected.SizeBytes &&
            actual.PartitionScheme == expected.PartitionScheme;
    }

    private static PartitionInfo? FindPartition(DiskInfo disk, PartitionIdentity expected) => disk.Partitions.FirstOrDefault(partition =>
        !partition.IsUnallocated && partition.PartitionNumber == expected.PartitionNumber &&
        partition.OffsetBytes == expected.OffsetBytes && partition.LengthBytes == expected.LengthBytes &&
        (string.IsNullOrWhiteSpace(expected.PartitionGuid) ||
         string.Equals(partition.PartitionGuid, expected.PartitionGuid, StringComparison.OrdinalIgnoreCase)));
}
