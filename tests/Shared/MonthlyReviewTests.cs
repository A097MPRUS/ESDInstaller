using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
#if WINDOWS7_REVIEW
using ESDInstaller.Windows7.Core.Models;
using ESDInstaller.Windows7.Core.Services;
using ESDInstaller.Windows7.Services;
namespace ESDInstaller.Windows7.Services
#elif WINDOWS8_REVIEW
using ESDInstaller.Windows8.Core.Models;
using ESDInstaller.Windows8.Core.Services;
using ESDInstaller.Windows8.Services;
namespace ESDInstaller.Windows8.Services
#else
using ESDInstaller.Core.Models;
using ESDInstaller.Core.Services;
using ESDInstaller.Services;
namespace ESDInstaller.Services
#endif
{
    // Only compiled into the test executable. No real settings or network writes.
    public sealed class AppSettings
    {
        public bool CheckForUpdatesAutomatically { get; set; } = true;
        public DateTimeOffset? LastUpdateCheckUtc { get; set; }
    }
    public sealed class SettingsService
    {
        public AppSettings Current { get; } = new AppSettings();
        public bool FailWrite { get; set; }
        public void RecordUpdateCheck(DateTimeOffset value)
        {
            if (FailWrite) throw new IOException("Simulated settings write failure");
            Current.LastUpdateCheckUtc = value;
        }
    }
}

internal static class MonthlyReviewTests
{
    public static void RunSafetyChecks()
    {
        var target = Partition(2, false) with { DriveLetter = 'W' };
        var boot = Partition(1, true);
        var disk = new DiskInfo(1, "Test", "Test", "SERIAL", "UNIQUE", "path", "SATA", 128L << 30,
            PartitionScheme.Mbr, false, false, false, false, new[] { boot, target });
        var edition = new WindowsImageEdition(1, "Windows 10", "", CpuArchitecture.X64, 19045,
            new Version(10, 0, 19045), 10L << 30);
        var image = new WindowsImage("C:\\test.wim", "C:\\test.wim", WindowsImageKind.Wim,
            WindowsGeneration.Windows10, "Windows 10", CpuArchitecture.X64, 100, DateTime.UtcNow,
            null, new[] { edition });
        var host = new CompatibilitySnapshot(FirmwareMode.Bios, CpuArchitecture.X64, false, false,
            false, false, 8L << 30);
        var plan = new InstallationPlanFactory().Create(new SessionState
        {
            Image = image, Edition = edition, DestinationDisk = disk, DestinationPartition = target,
            BootPartition = boot, Compatibility = host
        });
        ExecutionPlanValidator.ValidatePlanStructure(plan);
        Reject(() => ExecutionPlanValidator.ValidatePlanStructure(plan with
            { DestinationPartition = plan.DestinationPartition with { DiskNumber = 2 } }));
        Reject(() => ExecutionPlanValidator.ValidatePlanStructure(plan with
            { BootPartition = plan.BootPartition with { DiskNumber = 2 } }));
        Reject(() => ExecutionPlanValidator.ValidatePlanStructure(plan with { FirmwareMode = FirmwareMode.Unknown }));
        Reject(() => ExecutionPlanValidator.ValidatePlanStructure(plan with { PartitionScheme = PartitionScheme.Gpt }));
#if MODERN_REVIEW
        var compatibility = new CompatibilityService(new ProcessRunner());
#else
        var compatibility = new CompatibilityService();
        Require(DiskPartService.ConfirmDestinationAccess(target, 'W', false).Root == "W:\\", "Confirmed drive changed");
        Reject(() => DiskPartService.ConfirmDestinationAccess(target with { DriveLetter = null }, 'W', true));
        Reject(() => DiskPartService.ConfirmDestinationAccess(target with { DriveLetter = 'C' }, 'W', false));
        Reject(() => DiskPartService.ConfirmDestinationAccess(target with { FileSystem = "FAT32" }, 'W', false));
        var unknownEdition = edition with { Architecture = CpuArchitecture.Unknown };
        Require(!compatibility.CheckImageCompatibility(image, unknownEdition, disk, target, boot,
            host with { HostArchitecture = CpuArchitecture.Unknown }).IsValid, "Unknown architectures passed");
#endif
        Require(!compatibility.CheckImageCompatibility(image, edition, disk, target, boot,
            host with { FirmwareMode = FirmwareMode.Unknown }).IsValid, "Unknown firmware passed");
        Require(CompatibilityService.FindBootPartition(disk, FirmwareMode.Unknown, target) == null,
            "Unknown firmware selected a BIOS boot partition");
        Require(!compatibility.CheckImageCompatibility(image, edition, disk,
            target with { DiskNumber = 2 }, boot, host).IsValid, "Wrong target disk passed");
    }

    public static async Task RunUpdateChecks()
    {
        var settings = new SettingsService { FailWrite = true };
        using (var service = new UpdateService(settings, new FakeHandler()))
            Require((await service.CheckAsync(false)).Status == UpdateCheckStatus.Failed, "Settings failure escaped update check");

        settings = new SettingsService();
        var handler = new FakeHandler();
        using (var service = new UpdateService(settings, handler))
        {
            settings.Current.CheckForUpdatesAutomatically = false;
            Require((await service.CheckAsync(false)).Status == UpdateCheckStatus.Skipped && handler.Calls == 0,
                "Disabled checking contacted the server");
            settings.Current.CheckForUpdatesAutomatically = true;
            settings.Current.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            Require((await service.CheckAsync(false)).Status == UpdateCheckStatus.Skipped && handler.Calls == 0,
                "24-hour interval was ignored");
            settings.Current.LastUpdateCheckUtc = DateTimeOffset.UtcNow.AddDays(5);
            Require((await service.CheckAsync(false)).Status == UpdateCheckStatus.Available && handler.Calls == 1,
                "A future timestamp prevented checks");
            Require((await service.CheckAsync(true)).Status == UpdateCheckStatus.Available && handler.Calls == 2,
                "Manual checking did not bypass the interval");

            var manifest = new UpdateManifest { Version = "99.0.0", DownloadUrl = "https://example.invalid/setup.exe", Sha256 = FakeHandler.Hash };
            var progress = new InlineProgress();
            var paths = await Task.WhenAll(service.DownloadAndVerifyAsync(manifest, progress, CancellationToken.None),
                service.DownloadAndVerifyAsync(manifest, progress, CancellationToken.None));
            try
            {
                Require(paths[0] != paths[1], "Concurrent downloads share a destination");
                Require(paths.All(File.Exists) && progress.SawVerification && progress.SawProgress, "Download/verification failed");
                // The verified installer stays locked until it has been started.
                using (await UpdateService.OpenVerifiedInstallerAsync(paths[0], FakeHandler.Hash, CancellationToken.None))
                {
                    await RejectAny<IOException>(() => Task.Run(() => File.WriteAllText(paths[0], "replaced")));
                    await RejectAny<IOException>(() => Task.Run(() => File.Delete(paths[0])));
                }
                await RejectUpdate(async () =>
                {
                    using (await UpdateService.OpenVerifiedInstallerAsync(paths[0], new string('0', 64), CancellationToken.None))
                        return paths[0];
                });
                File.WriteAllText(paths[1], "replaced after verification");
                await RejectUpdate(async () =>
                {
                    using (await UpdateService.OpenVerifiedInstallerAsync(paths[1], FakeHandler.Hash, CancellationToken.None))
                        return paths[1];
                });
            }
            finally
            {
                foreach (var path in paths) { File.Delete(path); Directory.Delete(Path.GetDirectoryName(path)!); }
            }
            manifest.Sha256 = new string('0', 64);
            await RejectUpdate(() => service.DownloadAndVerifyAsync(manifest, progress, CancellationToken.None));
            manifest.Sha256 = FakeHandler.Hash;
            handler.InsecureRedirect = true;
            await RejectUpdate(() => service.DownloadAndVerifyAsync(manifest, progress, CancellationToken.None));
            handler.InsecureRedirect = false;

            // Oversized, truncated and stalled downloads fail instead of filling the disk or hanging.
            handler.DeclaredLength = UpdateService.MaximumInstallerBytes + 1;
            await RejectUpdate(() => service.DownloadAndVerifyAsync(manifest, progress, CancellationToken.None));
            handler.DeclaredLength = 1000;
            await RejectAny<IOException>(() => service.DownloadAndVerifyAsync(manifest, progress, CancellationToken.None));
            handler.DeclaredLength = null;
            handler.Stall = true;
            service.DownloadStallTimeout = TimeSpan.FromMilliseconds(200);
            await RejectAny<IOException>(() => service.DownloadAndVerifyAsync(manifest, progress, CancellationToken.None));
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
            {
                service.DownloadStallTimeout = TimeSpan.FromSeconds(30);
                await RejectAny<OperationCanceledException>(() => service.DownloadAndVerifyAsync(manifest, progress, cancellation.Token));
            }
            handler.Stall = false;
            handler.LargeManifest = true;
            Require((await service.CheckAsync(true)).Status == UpdateCheckStatus.Failed, "An oversized update manifest was read");
        }
        using (var service = new UpdateService(new SettingsService(), new FakeHandler { Offline = true }))
            Require((await service.CheckAsync(false)).Status == UpdateCheckStatus.Failed, "Offline check was not quiet");
    }

    private static PartitionInfo Partition(int number, bool active) => new PartitionInfo(1, number,
        number * 1048576L, 80L << 30, null, "", "NTFS", "", "Basic", "", 7, PartitionRole.BasicData,
        active, false, false, false, false, false, false, false, Array.Empty<string>());

    private static void Require(bool result, string message)
    { if (!result) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (ESDInstallerException) { return; }
        throw new InvalidOperationException("An unsafe plan/access path was accepted.");
    }
    private static async Task RejectUpdate(Func<Task<string>> action)
    {
        try { await action(); } catch (UpdateVerificationException) { return; }
        throw new InvalidOperationException("An unverified download was accepted.");
    }
    private static async Task RejectAny<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    public static async Task RunSupervisionChecks()
    {
        var runner = new ProcessRunner();
        var system = Environment.SystemDirectory;
        var finished = await runner.RunAsync(Path.Combine(system, "cmd.exe"), new[] { "/c", "exit", "3" },
            timeout: TimeSpan.FromSeconds(30));
        Require(finished.ExitCode == 3, "A command's exit code was lost");
        var ping = Path.Combine(system, "PING.EXE");
        var watch = Stopwatch.StartNew();
        await RejectAny<TimeoutException>(() => runner.RunAsync(ping, new[] { "-n", "30", "127.0.0.1" },
            timeout: TimeSpan.FromSeconds(1)));
        Require(watch.Elapsed < TimeSpan.FromSeconds(20), "A hung command was not stopped promptly");
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
            await RejectAny<OperationCanceledException>(() => runner.RunAsync(ping, new[] { "-n", "30", "127.0.0.1" },
                cancellationToken: cancellation.Token, timeout: TimeSpan.FromMinutes(1)));

        // Progress is accepted only from the process that was actually started.
        var name = "ESDInstaller-test-" + Guid.NewGuid().ToString("N");
        using (var server = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
        using (var client = new NamedPipeClientStream(".", name, PipeDirection.Out))
        {
            var waiting = server.WaitForConnectionAsync();
            client.Connect(5000);
            await waiting;
            var self = Process.GetCurrentProcess().Id;
            Require(PipeClientVerifier.IsClient(server, self), "The genuine pipe client was rejected");
            Require(!PipeClientVerifier.IsClient(server, self + 1), "A different process was accepted as the worker");
        }
    }

    public static void RunVersionChecks()
    {
        Func<string, SemanticVersion> v = SemanticVersion.Parse;
        Require(v("1.0.0-beta.10000000000").CompareTo(v("1.0.0-beta.9")) > 0, "Large prerelease numbers compared as text");
        Require(v("1.0.0-alpha").CompareTo(v("1.0.0-alpha.1")) < 0 && v("1.0.0-alpha.1").CompareTo(v("1.0.0-alpha.beta")) < 0 &&
                v("1.0.0-alpha.beta").CompareTo(v("1.0.0-beta")) < 0 && v("1.0.0-rc.1").CompareTo(v("1.0.0")) < 0,
            "Prerelease precedence is wrong");
        Require(v("v2.3.4+build.5").CompareTo(v("2.3.4")) == 0 && v("1.0.0-rc-1").PreRelease[0] == "rc-1",
            "Valid versions were misread");
        foreach (var invalid in new[] { "+1.0.0", "1.+0.0", "1. 0.0", "01.0.0", "1.0", "1.0.0-", "1.0.0-beta..1",
                     "1.0.0-beta.01", "1.0.0-be ta", "1.0.0+", "1.0.0+build..1", "99999999999.0.0", "1.0.0-é" })
            Require(!SemanticVersion.TryParse(invalid, out _), "Invalid version accepted: " + invalid);
    }

    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        // Never delivers data, like a connection that stops responding.
        public override int Read(byte[] buffer, int offset, int count) { Thread.Sleep(Timeout.Infinite); return 0; }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            new TaskCompletionSource<int>().Task;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private sealed class InlineProgress : IProgress<UpdateTransferProgress>
    {
        public bool SawVerification, SawProgress;
        public void Report(UpdateTransferProgress value)
        { SawVerification |= value.Verifying; SawProgress |= value.Percentage.HasValue; }
    }
    private sealed class FakeHandler : HttpMessageHandler
    {
        private static readonly byte[] Payload = System.Text.Encoding.UTF8.GetBytes("Harmless test content; never executed.");
        public static string Hash
        { get { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Payload)).Replace("-", ""); } }
        public int Calls;
        public bool Offline, InsecureRedirect, Stall, LargeManifest;
        public long? DeclaredLength;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            if (Offline) throw new HttpRequestException("Simulated offline connection");
            token.ThrowIfCancellationRequested();
            var isManifest = request.RequestUri!.AbsolutePath.EndsWith(".json", StringComparison.Ordinal);
            var content = isManifest
                ? (HttpContent)new StringContent("{\"version\":\"99.0.0\",\"downloadUrl\":\"https://example.invalid/setup.exe\",\"sha256\":\"" + Hash +
                    "\",\"notes\":\"" + (LargeManifest ? new string('x', 128 * 1024) : "") + "\"}")
                : Stall ? new StreamContent(new StalledStream()) : new ByteArrayContent(Payload);
            if (!isManifest && DeclaredLength.HasValue) content.Headers.ContentLength = DeclaredLength;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content,
                RequestMessage = InsecureRedirect ? new HttpRequestMessage(HttpMethod.Get, "http://example.invalid/setup.exe") : request
            });
        }
    }
}

