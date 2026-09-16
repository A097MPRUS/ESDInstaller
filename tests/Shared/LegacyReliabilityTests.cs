#if WINDOWS7_REVIEW || WINDOWS8_REVIEW
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
#if WINDOWS7_REVIEW
using ESDInstaller.Windows7.Core.Models;
using ESDInstaller.Windows7.Core.Services;
#else
using ESDInstaller.Windows8.Core.Models;
using ESDInstaller.Windows8.Core.Services;
#endif

/// <summary>Plan binding, source protection and ISO cache integrity for the Windows 7 and 8/8.1 editions.</summary>
internal static class LegacyReliabilityTests
{
    public static void RunApprovedPlanChecks()
    {
        WithDirectory(directory =>
        {
            var path = Path.Combine(directory, "source.wim");
            File.WriteAllText(path, "Harmless test content, not an actual Windows image.");
            var plan = Plan(path);
            var bytes = ApprovedPlan.Serialize(plan);
            var digest = ApprovedPlan.Digest(bytes);
            Require(ApprovedPlan.Verify(bytes, digest).PlanId == plan.PlanId, "Approved plan round-trip");
            Require(ApprovedPlan.Verify(bytes, digest.ToLowerInvariant()).PlanId == plan.PlanId, "Lower-case digest");
            var altered = ApprovedPlan.Serialize(plan with
                { DestinationPartition = plan.DestinationPartition with { OffsetBytes = 2000 } });
            Reject<ESDInstallerException>(() => ApprovedPlan.Verify(altered, digest));
            Reject<ESDInstallerException>(() => ApprovedPlan.Verify(bytes, new string('z', 64)));
            Reject<ESDInstallerException>(() => ApprovedPlan.Verify(bytes, null));
            Reject<ESDInstallerException>(() => ApprovedPlan.Verify(new byte[ApprovedPlan.MaximumBytes + 1], digest));
            var planPath = Path.Combine(directory, "approved.json");
            File.WriteAllBytes(planPath, bytes);
            Require(ApprovedPlan.Read(planPath, digest).PlanId == plan.PlanId, "Approved file read");
            File.WriteAllBytes(planPath, altered);
            Reject<ESDInstallerException>(() => ApprovedPlan.Read(planPath, digest));

            // Extracted ISO images must carry a valid content checksum.
            Reject<ESDInstallerException>(() => ExecutionPlanValidator.ValidatePlanStructure(plan with
                { Source = plan.Source with { Kind = WindowsImageKind.Iso } }));
            Reject<ESDInstallerException>(() => ExecutionPlanValidator.ValidatePlanStructure(plan with
                { Source = plan.Source with { ImageSha256 = "not-a-checksum" } }));
            ExecutionPlanValidator.ValidatePlanStructure(plan with
                { Source = plan.Source with { Kind = WindowsImageKind.Iso, ImageSha256 = new string('a', 64) } });
        });
    }

    public static void RunSourceProtectionChecks()
    {
        WithDirectory(directory =>
        {
            var path = Path.Combine(directory, "source.wim");
            File.WriteAllText(path, "Harmless test content, not an actual Windows image.");
            var plan = Plan(path);

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
            File.WriteAllText(path, "unlocked");
            Require(File.ReadAllText(path) == "unlocked", "Source handles released after use");

            // The worker re-verifies pinned image content before formatting.
            var pinned = plan with { Source = plan.Source with { ImageSha256 = Sha256(File.ReadAllBytes(path)) } };
            using (SourceImageLease.Open(pinned)) { }
            var changed = pinned with { Source = pinned.Source with { ImageSha256 = new string('0', 64) } };
            try { SourceImageLease.Open(changed).Dispose(); throw new InvalidOperationException("Changed image content was accepted"); }
            catch (ESDInstallerException exception) when (exception.MessageKey == "ValidationSourceChanged") { }
            File.WriteAllText(path, "released after rejection");
        });
    }

    public static void RunImageCacheChecks()
    {
        WithDirectory(directory =>
        {
            // Two different "ISO" files with the same path, size and timestamp must not share a cache.
            var iso = Path.Combine(directory, "media.iso");
            var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.WriteAllBytes(iso, Pattern(3 * 1024 * 1024, 1));
            File.SetLastWriteTimeUtc(iso, timestamp);
            var first = ImageService.CacheFileName(iso, @"sources\install.wim", 100);
            Require(first == ImageService.CacheFileName(iso, @"sources\install.wim", 100), "Cache name is not stable");
            Require(first.EndsWith(".wim", StringComparison.Ordinal) && first.StartsWith("media-", StringComparison.Ordinal),
                "Cache name format changed");
            File.WriteAllBytes(iso, Pattern(3 * 1024 * 1024, 2));
            File.SetLastWriteTimeUtc(iso, timestamp);
            Require(first != ImageService.CacheFileName(iso, @"sources\install.wim", 100),
                "Different media with the same name, size and timestamp share a cache");
            Require(first != ImageService.CacheFileName(iso, @"sources\install.esd", 100), "Different entries share a cache");

            // Cached content is reused only when it still matches its recorded checksum.
            var cached = Path.Combine(directory, "cache.wim");
            var checksum = cached + ".sha256";
            var content = Pattern(1024 * 1024 + 7, 3);
            File.WriteAllBytes(cached, content);
            Require(ImageService.TryReuseCache(cached, checksum, content.Length, null, CancellationToken.None) == null,
                "A cache without a checksum was reused");
            string hash;
            using (var input = new MemoryStream(content))
            using (var output = new MemoryStream())
            {
                hash = ImageService.CopyWithProgress(input, output, null, CancellationToken.None);
                Require(output.Length == content.Length, "Copy length changed");
            }
            Require(hash == Sha256(content), "Extraction checksum is wrong");
            File.WriteAllText(checksum, hash);
            Require(ImageService.TryReuseCache(cached, checksum, content.Length, null, CancellationToken.None) == hash,
                "A verified cache was not reused");
            Require(ImageService.TryReuseCache(cached, checksum, content.Length + 1, null, CancellationToken.None) == null,
                "A cache with the wrong length was reused");
            content[content.Length / 2] ^= 0xFF;
            File.WriteAllBytes(cached, content);
            Require(ImageService.TryReuseCache(cached, checksum, content.Length, null, CancellationToken.None) == null,
                "A modified cache with the same length was reused");
        });
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

    private static void WithDirectory(Action<string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ESDInstaller-legacy-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { test(directory); }
        finally
        {
            // Only known disposable files created by these tests, never recursive deletion.
            foreach (var name in new[] { "source.wim", "approved.json", "media.iso", "cache.wim", "cache.wim.sha256" })
            {
                var file = Path.Combine(directory, name);
                if (File.Exists(file)) File.Delete(file);
            }
            Directory.Delete(directory);
        }
    }

    private static byte[] Pattern(int length, byte seed)
    {
        var bytes = new byte[length];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i * 31 + seed * 17);
        return bytes;
    }

    private static string Sha256(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
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
}
#endif
