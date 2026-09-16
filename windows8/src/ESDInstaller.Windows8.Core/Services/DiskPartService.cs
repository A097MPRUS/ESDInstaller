using System.Text;
using ESDInstaller.Windows8.Core.Models;

namespace ESDInstaller.Windows8.Core.Services;

public sealed class DiskPartService
{
    private readonly ProcessRunner _processes;
    private readonly DiskService _disks;
    public DiskPartService(ProcessRunner processes, DiskService disks) { _processes = processes; _disks = disks; }

    public async Task<VolumeAccess> FormatDestinationAsync(InstallationPlan plan, InstallationLog log,
        CancellationToken cancellationToken)
    {
        var beforeFormat = await ValidateIdentityAsync(plan, plan.DestinationPartition, cancellationToken).ConfigureAwait(false);
        var letter = beforeFormat.DriveLetter ?? FindFreeLetter('W');
        var added = !beforeFormat.DriveLetter.HasValue;
        var lines = new List<string>
        {
            "select disk " + plan.DestinationDisk.DiskNumber,
            "select partition " + plan.DestinationPartition.PartitionNumber,
            "format fs=ntfs quick label=Windows"
        };
        if (added) lines.Add("assign letter=" + letter);
        await RunScriptAsync(lines, log, cancellationToken).ConfigureAwait(false);
        var actual = await ValidateIdentityAsync(plan, plan.DestinationPartition, cancellationToken).ConfigureAwait(false);
        return ConfirmDestinationAccess(actual, letter, added);
    }

    internal static VolumeAccess ConfirmDestinationAccess(PartitionInfo actual, char expectedLetter, bool added)
    {
        if (!string.Equals(actual.FileSystem, "NTFS", StringComparison.OrdinalIgnoreCase))
            throw new ESDInstallerException("ErrorFormatDestination", "The destination did not report NTFS after formatting.");
        if (!actual.DriveLetter.HasValue || char.ToUpperInvariant(actual.DriveLetter.Value) != char.ToUpperInvariant(expectedLetter))
            throw new ESDInstallerException("ErrorFormatDestination", "The destination drive letter could not be confirmed after formatting.");
        return new VolumeAccess(actual.DriveLetter.Value + @":\", added, actual.PartitionNumber);
    }

    public async Task<VolumeAccess> AcquireBootAccessAsync(InstallationPlan plan, InstallationLog log,
        CancellationToken cancellationToken)
    {
        var actual = await ValidateIdentityAsync(plan, plan.BootPartition, cancellationToken).ConfigureAwait(false);
        if (actual.DriveLetter.HasValue) return new VolumeAccess(actual.DriveLetter + @":\", false, actual.PartitionNumber);
        var letter = FindFreeLetter('S');
        await RunScriptAsync(new[]
        {
            "select disk " + plan.DestinationDisk.DiskNumber,
            "select partition " + plan.BootPartition.PartitionNumber,
            "assign letter=" + letter
        }, log, cancellationToken).ConfigureAwait(false);
        actual = await ValidateIdentityAsync(plan, plan.BootPartition, cancellationToken).ConfigureAwait(false);
        if (!actual.DriveLetter.HasValue || actual.DriveLetter.Value != letter)
            throw new ESDInstallerException("ErrorBootPartitionAccess", "DiskPart did not assign the requested boot access path.");
        return new VolumeAccess(letter + @":\", true, actual.PartitionNumber);
    }

    public async Task ReleaseBootAccessAsync(InstallationPlan plan, VolumeAccess access, InstallationLog log)
    {
        if (!access.AddedDriveLetter) return;
        try
        {
            await ValidateIdentityAsync(plan, plan.BootPartition, CancellationToken.None).ConfigureAwait(false);
            await RunScriptAsync(new[]
            {
                "select disk " + plan.DestinationDisk.DiskNumber,
                "select partition " + plan.BootPartition.PartitionNumber,
                "remove letter=" + access.Root[0]
            }, log, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) { log.Write("WARNING", "Could not remove temporary boot letter: " + exception.Message); }
    }

    private async Task<PartitionInfo> ValidateIdentityAsync(InstallationPlan plan, PartitionIdentity expected,
        CancellationToken cancellationToken)
    {
        ExecutionPlanValidator.ValidatePlanStructure(plan);
        var disk = (await _disks.GetDisksAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(x => x.Number == plan.DestinationDisk.DiskNumber);
        if (disk == null || disk.IsReadOnly || disk.IsOffline ||
            disk.PartitionScheme != plan.DestinationDisk.PartitionScheme ||
            expected.DiskNumber != disk.Number || disk.SizeBytes != plan.DestinationDisk.SizeBytes ||
            (!string.IsNullOrWhiteSpace(plan.DestinationDisk.UniqueId) &&
             !string.Equals(disk.UniqueId.Trim(), plan.DestinationDisk.UniqueId.Trim(), StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(plan.DestinationDisk.SerialNumber) &&
             !string.Equals(disk.SerialNumber.Trim(), plan.DestinationDisk.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ESDInstallerException("ValidationDiskChanged", plan.DestinationDisk.Model);
        var partition = disk.Partitions.FirstOrDefault(x => !x.IsUnallocated && x.PartitionNumber == expected.PartitionNumber &&
            x.OffsetBytes == expected.OffsetBytes && x.LengthBytes == expected.LengthBytes &&
            (string.IsNullOrWhiteSpace(expected.PartitionGuid) ||
             string.Equals(x.PartitionGuid, expected.PartitionGuid, StringComparison.OrdinalIgnoreCase)));
        if (partition == null) throw new ESDInstallerException("ValidationPartitionChanged", expected.PartitionNumber.ToString());
        if (expected == plan.DestinationPartition &&
            (partition.IsProtected || partition.IsBitLocker || partition.Role != PartitionRole.BasicData || partition.PartitionNumber <= 0))
            throw new ESDInstallerException("ValidationProtectedPartition", partition.StableKey);
        return partition;
    }

    private async Task RunScriptAsync(IEnumerable<string> commands, InstallationLog log,
        CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "ESDInstallerWindows8-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllLines(scriptPath, commands.Concat(new[] { "exit" }), Encoding.ASCII);
            log.Write("COMMAND", "diskpart /s <validated-script>: " + string.Join("; ", commands));
            var result = await _processes.RunAsync(Path.Combine(Environment.SystemDirectory, "diskpart.exe"),
                new[] { "/s", scriptPath }, output: (line, error) => log.Write(error ? "DISKPART-STDERR" : "DISKPART", line),
                cancellationToken: cancellationToken, timeout: TimeSpan.FromMinutes(30)).ConfigureAwait(false);
            var combined = result.StandardOutput + "\n" + result.StandardError;
            if (!result.Succeeded || combined.IndexOf("DiskPart has encountered an error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("Virtual Disk Service error", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new ESDInstallerException("ErrorFormatDestination",
                    "DiskPart failed with exit code " + result.ExitCode + ". " + result.StandardError.Trim());
        }
        finally { try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { } }
    }

    private static char FindFreeLetter(char preferred)
    {
        var used = DriveInfo.GetDrives().Select(x => char.ToUpperInvariant(x.Name[0])).ToArray();
        if (!used.Contains(preferred)) return preferred;
        for (var letter = 'Z'; letter >= 'D'; letter--) if (!used.Contains(letter)) return letter;
        throw new ESDInstallerException("ErrorBootPartitionAccess", "No free drive letter is available.");
    }
}

public sealed record VolumeAccess(string Root, bool AddedDriveLetter, int PartitionNumber);
