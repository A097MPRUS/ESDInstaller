using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ESDInstaller.Core.Services;

namespace ESDInstaller.Services;

public enum UpdateCheckStatus { Skipped, Current, Available, Failed }

public sealed class UpdateManifest
{
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; } = string.Empty;
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
    [JsonPropertyName("notes")] public string Notes { get; set; } = string.Empty;
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateManifest? Manifest = null, Exception? Error = null);
public sealed record UpdateTransferProgress(double? Percentage, bool Verifying = false);

public sealed class UpdateVerificationException : Exception
{
    public UpdateVerificationException(string message) : base(message) { }
}

public sealed class UpdateService : IDisposable
{
    public const string ManifestUrl = "https://raw.githubusercontent.com/A097MPRUS/ESDInstaller/main/updates/windows10.json";
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
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = MaximumManifestBytes };
    }

    public string InstalledVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        }
    }

    public async Task<UpdateCheckResult> CheckAsync(bool manual, CancellationToken cancellationToken = default)
    {
        if (!manual)
        {
            if (!_settings.Current.CheckForUpdatesAutomatically) return new(UpdateCheckStatus.Skipped);
            var last = _settings.Current.LastUpdateCheckUtc;
            if (last.HasValue && last.Value <= DateTimeOffset.UtcNow &&
                DateTimeOffset.UtcNow - last.Value < MinimumCheckInterval)
                return new(UpdateCheckStatus.Skipped);
        }

        try
        {
            _settings.RecordUpdateCheck(DateTimeOffset.UtcNow);
            using var request = new HttpRequestMessage(HttpMethod.Get, ManifestUrl);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            ValidateManifest(manifest);

            var installed = SemanticVersion.Parse(InstalledVersion);
            var available = SemanticVersion.Parse(manifest!.Version);
            return available.CompareTo(installed) > 0
                ? new(UpdateCheckStatus.Available, manifest)
                : new(UpdateCheckStatus.Current, manifest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new(UpdateCheckStatus.Failed, Error: exception);
        }
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
            using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                throw new UpdateVerificationException("The update download was redirected to an insecure address.");
            var total = response.Content.Headers.ContentLength;
            if (total > MaximumInstallerBytes)
                throw new UpdateVerificationException("The update download is larger than allowed.");
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
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
                    await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    progress.Report(new UpdateTransferProgress(total is > 0 ? received * 100d / total.Value : null));
                }
                if (total.HasValue && received != total.Value)
                    throw new IOException("The update download was incomplete.");
            }

            progress.Report(new UpdateTransferProgress(null, Verifying: true));
            var actualHash = await ComputeSha256Async(destination, cancellationToken);
            if (!string.Equals(actualHash, NormalizeHash(manifest.Sha256), StringComparison.OrdinalIgnoreCase))
                throw new UpdateVerificationException("The downloaded update did not match its expected SHA-256 hash.");
            return destination;
        }
        catch
        {
            TryDelete(destination);
            throw;
        }
    }

    public static async Task LaunchInstallerAsync(string path, string expectedSha256, CancellationToken cancellationToken)
    {
        await using var verified = await OpenVerifiedInstallerAsync(path, expectedSha256, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
            Verb = "runas"
        });
    }

    public void Dispose() => _client.Dispose();

    private static void ValidateManifest(UpdateManifest? manifest)
    {
        if (manifest is null || !SemanticVersion.TryParse(manifest.Version, out _))
            throw new UpdateVerificationException("The update manifest contains an invalid version.");
        if (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new UpdateVerificationException("The update manifest does not contain a secure HTTPS download URL.");
        var hash = NormalizeHash(manifest.Sha256);
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
            throw new UpdateVerificationException("The update manifest does not contain a valid SHA-256 hash.");
    }

    private static string NormalizeHash(string value) => (value ?? string.Empty).Replace("-", string.Empty).Trim();

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return await ComputeSha256Async(stream, cancellationToken);
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        using var sha = SHA256.Create();
        var buffer = new byte[81920];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken);
            if (count == 0) break;
            sha.TransformBlock(buffer, 0, count, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
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

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}
