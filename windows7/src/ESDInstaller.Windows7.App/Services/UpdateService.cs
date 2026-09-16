using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ESDInstaller.Windows7.Core.Services;

namespace ESDInstaller.Windows7.Services;

public enum UpdateCheckStatus { Skipped, Current, Available, Failed }

public sealed class UpdateManifest
{
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; } = string.Empty;
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
    [JsonPropertyName("notes")] public string Notes { get; set; } = string.Empty;
}

public sealed class UpdateCheckResult
{
    public UpdateCheckResult(UpdateCheckStatus status, UpdateManifest? manifest = null, Exception? error = null)
    { Status = status; Manifest = manifest; Error = error; }
    public UpdateCheckStatus Status { get; }
    public UpdateManifest? Manifest { get; }
    public Exception? Error { get; }
}

public sealed class UpdateTransferProgress
{
    public UpdateTransferProgress(double? percentage, bool verifying = false)
    { Percentage = percentage; Verifying = verifying; }
    public double? Percentage { get; }
    public bool Verifying { get; }
}

public sealed class UpdateVerificationException : Exception
{
    public UpdateVerificationException(string message) : base(message) { }
}

public sealed class UpdateService : IDisposable
{
    public const string ManifestUrl = "https://raw.githubusercontent.com/A097MPRUS/ESDInstaller/main/updates/windows7.json";
    private static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromHours(24);
    /// <summary>Largest accepted installer download; current installers are far smaller.</summary>
    internal const long MaximumInstallerBytes = 512L * 1024 * 1024;
    private const int MaximumManifestBytes = 64 * 1024;
    internal TimeSpan DownloadStallTimeout { get; set; } = TimeSpan.FromSeconds(60);
    private readonly SettingsService _settings;
    private readonly HttpClient _client;

    public UpdateService(SettingsService settings) : this(settings, new HttpClientHandler()) { }

    internal UpdateService(SettingsService settings, HttpMessageHandler handler)
    {
        _settings = settings;
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = MaximumManifestBytes };
    }

    public string InstalledVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return version.Major + "." + version.Minor + "." + Math.Max(0, version.Build);
        }
    }

    public async Task<UpdateCheckResult> CheckAsync(bool manual, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (!manual)
        {
            if (!_settings.Current.CheckForUpdatesAutomatically) return new UpdateCheckResult(UpdateCheckStatus.Skipped);
            var last = _settings.Current.LastUpdateCheckUtc;
            if (last.HasValue && last.Value <= DateTimeOffset.UtcNow &&
                DateTimeOffset.UtcNow - last.Value < MinimumCheckInterval)
                return new UpdateCheckResult(UpdateCheckStatus.Skipped);
        }

        try
        {
            _settings.RecordUpdateCheck(DateTimeOffset.UtcNow);
            using (var request = new HttpRequestMessage(HttpMethod.Get, ManifestUrl))
            {
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    var manifest = JsonSerializer.Deserialize<UpdateManifest>(await response.Content.ReadAsStringAsync(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (manifest is null) throw new UpdateVerificationException("The update manifest is empty.");
                    ValidateManifest(manifest);
                    var installed = SemanticVersion.Parse(InstalledVersion);
                    var available = SemanticVersion.Parse(manifest.Version);
                    return available.CompareTo(installed) > 0
                        ? new UpdateCheckResult(UpdateCheckStatus.Available, manifest)
                        : new UpdateCheckResult(UpdateCheckStatus.Current, manifest);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { return new UpdateCheckResult(UpdateCheckStatus.Failed, null, exception); }
    }

    public async Task<string> DownloadAndVerifyAsync(UpdateManifest manifest, IProgress<UpdateTransferProgress> progress,
        CancellationToken cancellationToken)
    {
        ValidateManifest(manifest);
        var uri = new Uri(manifest.DownloadUrl, UriKind.Absolute);
        var directory = Path.Combine(Path.GetTempPath(), "ESDInstaller", "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, Path.GetFileName(uri.LocalPath));
        if (string.IsNullOrWhiteSpace(Path.GetFileName(destination)))
            throw new UpdateVerificationException("The update URL does not contain a valid file name.");
        try
        {
            using (var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                if (response.RequestMessage == null || response.RequestMessage.RequestUri == null ||
                    response.RequestMessage.RequestUri.Scheme != Uri.UriSchemeHttps)
                    throw new UpdateVerificationException("The update download was redirected to an insecure address.");
                var total = response.Content.Headers.ContentLength;
                if (total.HasValue && total.Value > MaximumInstallerBytes)
                    throw new UpdateVerificationException("The update download is larger than allowed.");
                using (var source = await response.Content.ReadAsStreamAsync())
                using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long received = 0;
                    while (true)
                    {
                        var count = await ReadWithStallTimeoutAsync(source, buffer, response, cancellationToken);
                        if (count == 0) break;
                        received += count;
                        if (received > MaximumInstallerBytes)
                            throw new UpdateVerificationException("The update download is larger than allowed.");
                        await target.WriteAsync(buffer, 0, count, cancellationToken);
                        progress.Report(new UpdateTransferProgress(
                            total.HasValue && total.Value > 0 ? (double?)(received * 100d / total.Value) : null));
                    }
                    if (total.HasValue && received != total.Value)
                        throw new IOException("The update download was incomplete.");
                }
            }
            progress.Report(new UpdateTransferProgress(null, true));
            var actualHash = await ComputeSha256Async(destination, cancellationToken);
            if (!string.Equals(actualHash, NormalizeHash(manifest.Sha256), StringComparison.OrdinalIgnoreCase))
                throw new UpdateVerificationException("The downloaded update did not match its expected SHA-256 hash.");
            return destination;
        }
        catch { TryDelete(destination); throw; }
    }

    public static async Task LaunchInstallerAsync(string path, string expectedSha256, CancellationToken cancellationToken)
    {
        using (await OpenVerifiedInstallerAsync(path, expectedSha256, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "runas" })) { }
        }
    }

    public void Dispose() => _client.Dispose();

    private static void ValidateManifest(UpdateManifest manifest)
    {
        SemanticVersion ignored;
        Uri uri;
        if (!SemanticVersion.TryParse(manifest.Version, out ignored))
            throw new UpdateVerificationException("The update manifest contains an invalid version.");
        if (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new UpdateVerificationException("The update manifest does not contain a secure HTTPS download URL.");
        var hash = NormalizeHash(manifest.Sha256);
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
            throw new UpdateVerificationException("The update manifest does not contain a valid SHA-256 hash.");
    }

    private static string NormalizeHash(string value) => (value ?? string.Empty).Replace("-", string.Empty).Trim();

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            return await ComputeSha256Async(stream, cancellationToken);
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        using (var sha = SHA256.Create())
        {
            var buffer = new byte[81920];
            while (true)
            {
                var count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (count == 0) break;
                sha.TransformBlock(buffer, 0, count, null, 0);
            }
            sha.TransformFinalBlock(new byte[0], 0, 0);
            return BitConverter.ToString(sha.Hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Opens the installer so it cannot be changed, renamed or deleted, then verifies its hash.
    /// The caller keeps the handle open until Windows has started the installer.
    /// </summary>
    internal static async Task<FileStream> OpenVerifiedInstallerAsync(string path, string expectedSha256,
        CancellationToken cancellationToken)
    {
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        try
        {
            if (!string.Equals(await ComputeSha256Async(file, cancellationToken), NormalizeHash(expectedSha256),
                    StringComparison.OrdinalIgnoreCase))
                throw new UpdateVerificationException("The downloaded update changed before it could be started.");
            return file;
        }
        catch { file.Dispose(); throw; }
    }

    private async Task<int> ReadWithStallTimeoutAsync(Stream source, byte[] buffer, IDisposable response,
        CancellationToken cancellationToken)
    {
        using (var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var read = source.ReadAsync(buffer, 0, buffer.Length, stall.Token);
            if (await Task.WhenAny(read, Task.Delay(DownloadStallTimeout, stall.Token)).ConfigureAwait(false) != read)
            {
                // Network reads do not always observe cancellation; closing the response ends them.
                stall.Cancel();
                response.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
                throw new IOException("The update download stopped responding.");
            }
            stall.Cancel();
            return await read.ConfigureAwait(false);
        }
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
