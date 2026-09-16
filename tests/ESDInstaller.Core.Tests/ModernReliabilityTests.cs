using System.Text;
using System.Text.Json;
using ESDInstaller.Core.Models;
using ESDInstaller.Core.Services;

internal static class ModernReliabilityTests
{
    public static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ESDInstaller-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "source.wim");
        try
        {
            await File.WriteAllTextAsync(path, "Harmless test content, not an actual Windows image.");
            var plan = Plan(path);
            var bytes = ApprovedPlan.Serialize(plan);
            var digest = ApprovedPlan.Digest(bytes);
            Require(ApprovedPlan.Verify(bytes, digest).PlanId == plan.PlanId, "Approved plan round-trip");
            var altered = ApprovedPlan.Serialize(plan with
                { DestinationPartition = plan.DestinationPartition with { OffsetBytes = 2000 } });
            Reject<ESDInstallerException>(() => ApprovedPlan.Verify(altered, digest));
            Reject<ESDInstallerException>(() => ApprovedPlan.Verify(bytes, new string('z', 64)));
            Reject<ESDInstallerException>(() => ApprovedPlan.Verify(new byte[ApprovedPlan.MaximumBytes + 1], digest));
            var planPath = Path.Combine(directory, "approved.json");
            await File.WriteAllBytesAsync(planPath, bytes);
            Require((await ApprovedPlan.ReadAsync(planPath, digest)).PlanId == plan.PlanId, "Approved file read");
            await File.WriteAllBytesAsync(planPath, altered);
            await RejectAsync<ESDInstallerException>(() => ApprovedPlan.ReadAsync(planPath, digest));

            var extentsBuffer = new byte[56];
            BitConverter.GetBytes(2u).CopyTo(extentsBuffer, 0);
            WriteExtent(extentsBuffer, 8, 4, 100, 100);
            WriteExtent(extentsBuffer, 32, 5, 200, 100);
            var extents = SourceImageLease.ParseExtents(extentsBuffer, extentsBuffer.Length);
            Require(extents.Count == 2 && extents[1].DiskNumber == 5, "Multi-extent native parsing");
            Reject<IOException>(() => SourceImageLease.ParseExtents(extentsBuffer, 32));
            var target = plan.DestinationPartition with { DiskNumber = 4, OffsetBytes = 199, LengthBytes = 20 };
            Reject<ESDInstallerException>(() => SourceImageLease.EnsureSeparate(extents, target, path));
            SourceImageLease.EnsureSeparate(extents, target with { OffsetBytes = 200 }, path);
            SourceImageLease.EnsureSeparate(extents, target with { DiskNumber = 6 }, path);
            WriteExtent(extentsBuffer, 8, 4, long.MaxValue, 100);
            Reject<IOException>(() => SourceImageLease.ParseExtents(extentsBuffer, extentsBuffer.Length));

            IReadOnlyList<SourceExtent> actual;
            using (var stream = File.OpenRead(path)) actual = SourceImageLease.GetExtents(stream.SafeFileHandle);
            Require(actual.Count > 0, "Read-only native source-volume resolution");
            var location = actual[0];
            var sameVolume = plan with
            {
                DestinationDisk = plan.DestinationDisk with { DiskNumber = location.DiskNumber },
                DestinationPartition = plan.DestinationPartition with
                { DiskNumber = location.DiskNumber, OffsetBytes = location.Offset, LengthBytes = location.Length },
                BootPartition = plan.BootPartition with { DiskNumber = location.DiskNumber }
            };
            Reject<ESDInstallerException>(() => SourceImageLease.Open(sameVolume));
            // A different synthetic disk is safe to compare; no disk writes occur.
            using (SourceImageLease.Open(plan))
            {
                Reject<IOException>(() => File.WriteAllText(path, "changed"));
                Require(File.ReadAllText(path).Contains("Harmless"), "Source remains readable while locked");
            }
            await File.WriteAllTextAsync(path, "unlocked");
            Require(File.ReadAllText(path) == "unlocked", "Source handles released after use");

            // A canceled operation must not even attempt to start this nonexistent executable.
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await RejectAsync<OperationCanceledException>(() => new ProcessRunner().RunAsync(
                Path.Combine(directory, "must-not-start.exe"), Array.Empty<string>(), cancellationToken: cancellation.Token));

            Console.WriteLine("PASS  Windows 10/11 approved-plan integrity, native source protection, file locks and pre-start cancellation");
        }
        finally
        {
            // Only known disposable files created by this test, never recursive deletion.
            foreach (var name in new[] { "source.wim", "approved.json" })
            {
                var file = Path.Combine(directory, name);
                if (File.Exists(file)) File.Delete(file);
            }
            Directory.Delete(directory);
        }
    }

    private static InstallationPlan Plan(string source)
    {
        var partition = new PartitionIdentity(int.MaxValue, 2, 1024, 80L << 30, "", 'W', "", "NTFS", PartitionRole.BasicData);
        return new InstallationPlan(Guid.NewGuid(), DateTime.UtcNow,
            new SourceIdentity(source, source, WindowsImageKind.Wim, new FileInfo(source).Length, File.GetLastWriteTimeUtc(source)),
            new WindowsImageEdition(1, "Test", "", CpuArchitecture.X64, 19045, new Version(10, 0, 19045), 1),
            WindowsGeneration.Windows10, InstallationEngineKind.ModernWindows,
            new DiskIdentity(int.MaxValue, "TEST", "TEST", "Synthetic target", 128L << 30, PartitionScheme.Mbr),
            partition, partition with { PartitionNumber = 1 }, FirmwareMode.Bios, PartitionScheme.Mbr,
            true, false, false, false, Array.Empty<PlannedOperation>(), "test");
    }

    private static void WriteExtent(byte[] buffer, int offset, uint disk, long start, long length)
    {
        BitConverter.GetBytes(disk).CopyTo(buffer, offset);
        BitConverter.GetBytes(start).CopyTo(buffer, offset + 8);
        BitConverter.GetBytes(length).CopyTo(buffer, offset + 16);
    }
    private static void Require(bool valid, string name)
    { if (!valid) throw new InvalidOperationException(name); }
    private static void Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    private static async Task RejectAsync<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
}

