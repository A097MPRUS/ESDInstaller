using System.Security.Cryptography;
using System.Text;
using ESDInstaller.Windows8.Core.Models;

namespace ESDInstaller.Windows8.Core.Services;

public sealed class InstallationPlanFactory
{
    public InstallationPlan Create(SessionState session, bool requestOneTimeBoot = false)
    {
        var image = session.Image ?? throw new InvalidOperationException("No image was selected.");
        var edition = session.Edition ?? throw new InvalidOperationException("No edition was selected.");
        var disk = session.DestinationDisk ?? throw new InvalidOperationException("No destination disk was selected.");
        var partition = session.DestinationPartition ?? throw new InvalidOperationException("No destination partition was selected.");
        var boot = session.BootPartition ?? throw new InvalidOperationException("No valid boot partition was selected.");
        var compatibility = session.Compatibility ?? throw new InvalidOperationException("Compatibility was not inspected.");
        var source = new SourceIdentity(image.SourcePath, image.ResolvedImagePath, image.Kind, image.FileSizeBytes,
            image.SourceLastWriteUtc, image.ResolvedImageSha256);
        var diskIdentity = new DiskIdentity(disk.Number, disk.UniqueId, disk.SerialNumber, disk.SafeDisplayName,
            disk.SizeBytes, disk.PartitionScheme);
        var target = ToIdentity(partition);
        var bootIdentity = ToIdentity(boot);
        var operations = new List<PlannedOperation>
        {
            new PlannedOperation("validate", "OperationValidatePlan", false),
            new PlannedOperation("format", "OperationFormatDestination", true),
            new PlannedOperation("apply", "OperationApplyImage", true)
        };
        if (session.BypassWindows11Requirements)
            operations.Add(new PlannedOperation("win11-bypass", "OperationConfigureWindows11Bypass", true));
        operations.Add(new PlannedOperation("boot", "OperationInstallBootFiles", true));
        operations.Add(new PlannedOperation("verify", "OperationVerifyInstallation", false));
        return new InstallationPlan(Guid.NewGuid(), DateTime.UtcNow, source, edition, image.Generation,
            SelectEngine(image), diskIdentity, target, bootIdentity, compatibility.FirmwareMode,
            disk.PartitionScheme, true, session.BypassWindows11Requirements, false, requestOneTimeBoot,
            operations, Fingerprint(source, edition, diskIdentity, target, bootIdentity,
                session.BypassWindows11Requirements));
    }

    private static InstallationEngineKind SelectEngine(WindowsImage image)
    {
        if (image.Generation == WindowsGeneration.WindowsXp) return InstallationEngineKind.LegacyXpNt5;
        if (image.Generation == WindowsGeneration.WindowsVista) return InstallationEngineKind.LegacyNt6;
        return InstallationEngineKind.ModernWindows;
    }

    private static PartitionIdentity ToIdentity(PartitionInfo partition) => new PartitionIdentity(
        partition.DiskNumber, partition.PartitionNumber, partition.OffsetBytes, partition.LengthBytes,
        partition.PartitionGuid, partition.DriveLetter, partition.VolumeLabel, partition.FileSystem, partition.Role);

    private static string Fingerprint(SourceIdentity source, WindowsImageEdition edition, DiskIdentity disk,
        PartitionIdentity destination, PartitionIdentity boot, bool bypass)
    {
        var value = string.Join("|", new object[] { source.SourcePath, source.SourceSizeBytes,
            source.SourceLastWriteUtc.Ticks, edition.Index, edition.Build, disk.UniqueId, disk.SerialNumber,
            disk.SizeBytes, destination.PartitionNumber, destination.OffsetBytes, destination.LengthBytes,
            destination.PartitionGuid, boot.PartitionNumber, boot.OffsetBytes, boot.LengthBytes,
            boot.PartitionGuid, bypass, source.ImageSha256 ?? "" });
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").Substring(0, 12);
    }
}
