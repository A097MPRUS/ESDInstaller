using System.Security.Cryptography;
using System.Text;
using DiscUtils;
using DiscUtils.Iso9660;
using DiscUtils.Udf;
using ESDInstaller.Windows7.Core.Models;

namespace ESDInstaller.Windows7.Core.Services;

public sealed class ImageService : IDisposable
{
    private readonly WimService _wimService;
    private readonly HashSet<string> _temporaryFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public ImageService(WimService wimService) => _wimService = wimService;

    public async Task<WindowsImage> InspectAsync(string sourcePath, Action<int>? extractionProgress = null,
        CancellationToken cancellationToken = default)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourcePath)) throw new ESDInstallerException("ErrorImageNotFound", sourcePath);
        switch (Path.GetExtension(sourcePath).ToLowerInvariant())
        {
            case ".iso": return await InspectIsoAsync(sourcePath, extractionProgress, cancellationToken).ConfigureAwait(false);
            case ".wim": return await InspectWimAsync(sourcePath, sourcePath, WindowsImageKind.Wim, null, cancellationToken).ConfigureAwait(false);
            case ".esd": return await InspectWimAsync(sourcePath, sourcePath, WindowsImageKind.Esd, null, cancellationToken).ConfigureAwait(false);
            default: throw new ESDInstallerException("ErrorUnsupportedImageType", Path.GetExtension(sourcePath));
        }
    }

    private async Task<WindowsImage> InspectIsoAsync(string sourcePath, Action<int>? progress, CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            using (var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                       1024 * 1024, FileOptions.SequentialScan))
            using (var fileSystem = OpenIsoFileSystem(stream))
            {
                var imageEntry = FindEntry(fileSystem, @"sources\install.wim") ??
                                 FindEntry(fileSystem, @"sources\install.esd");
                if (imageEntry == null)
                {
                    if (Exists(fileSystem, @"I386\SETUPLDR.BIN") || Exists(fileSystem, @"I386\NTLDR"))
                    {
                        var info = new FileInfo(sourcePath);
                        return new WindowsImage(sourcePath, sourcePath, WindowsImageKind.LegacyIso,
                            WindowsGeneration.WindowsXp, "Windows XP", CpuArchitecture.Unknown, info.Length,
                            info.LastWriteTimeUtc, null, Array.Empty<WindowsImageEdition>(),
                            "LegacyXpEngineUnavailable");
                    }
                    if (FindEntry(fileSystem, @"sources\install.swm") != null)
                        throw new ESDInstallerException("ErrorSplitWimUnsupported", "The ISO contains a split install.swm image.");
                    throw new ESDInstallerException("ErrorNoInstallImage", sourcePath);
                }

                var cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ESDInstallerWindows7", "ImageCache");
                Directory.CreateDirectory(cacheRoot);
                var entryLength = fileSystem.GetFileLength(imageEntry);
                var cached = Path.Combine(cacheRoot, CacheFileName(sourcePath, imageEntry, entryLength));
                var checksumPath = cached + ".sha256";
                var imageHash = TryReuseCache(cached, checksumPath, entryLength, progress, cancellationToken);
                if (imageHash == null)
                {
                    var partial = cached + ".partial-" + Guid.NewGuid().ToString("N");
                    _temporaryFiles.Add(partial);
                    try
                    {
                        using (var input = fileSystem.OpenFile(imageEntry, FileMode.Open, FileAccess.Read))
                        using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                   1024 * 1024, FileOptions.SequentialScan))
                            imageHash = CopyWithProgress(input, output, progress, cancellationToken);
                        if (new FileInfo(partial).Length != entryLength)
                            throw new ESDInstallerException("ErrorIsoMount", "The installation image was not extracted completely.");
                        TryDelete(checksumPath);
                        if (File.Exists(cached)) File.Delete(cached);
                        File.Move(partial, cached);
                        _temporaryFiles.Remove(partial);
                        File.WriteAllText(checksumPath, imageHash);
                    }
                    catch
                    {
                        TryDelete(partial);
                        _temporaryFiles.Remove(partial);
                        throw;
                    }
                }
                progress?.Invoke(100);
                var image = await InspectWimAsync(sourcePath, cached, WindowsImageKind.Iso, "ISO (direct read)", cancellationToken)
                    .ConfigureAwait(false);
                return image with { ResolvedImageSha256 = imageHash };
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static DiscFileSystem OpenIsoFileSystem(Stream stream)
    {
        try
        {
            if (UdfReader.Detect(stream))
            {
                stream.Position = 0;
                return new UdfReader(stream);
            }
        }
        catch { stream.Position = 0; }

        try
        {
            stream.Position = 0;
            if (CDReader.Detect(stream))
            {
                stream.Position = 0;
                return new CDReader(stream, true, true);
            }
        }
        catch (Exception exception)
        { throw new ESDInstallerException("ErrorIsoMount", "The ISO/UDF filesystem could not be read.", exception); }
        throw new ESDInstallerException("ErrorIsoMount", "The file does not contain a readable ISO 9660 or UDF filesystem.");
    }

    private static string? FindEntry(DiscFileSystem fileSystem, string expected)
    {
        if (fileSystem.FileExists(expected)) return expected;
        var current = string.Empty;
        foreach (var part in expected.Split('\\'))
        {
            var entries = fileSystem.GetFileSystemEntries(current.Length == 0 ? @"\" : current);
            var match = entries.FirstOrDefault(entry => string.Equals(Path.GetFileName(entry), part,
                StringComparison.OrdinalIgnoreCase));
            if (match == null) return null;
            current = match;
        }
        return current;
    }

    private static bool Exists(DiscFileSystem fileSystem, string path) => FindEntry(fileSystem, path) != null;

    /// <summary>Copies (or only reads, when output is null) and returns the SHA-256 of the bytes read.</summary>
    internal static string CopyWithProgress(Stream input, Stream? output, Action<int>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 1024];
        long copied = 0;
        int read;
        using (var sha = SHA256.Create())
        {
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output?.Write(buffer, 0, read);
                sha.TransformBlock(buffer, 0, read, null, 0);
                copied += read;
                if (input.Length > 0) progress?.Invoke((int)Math.Min(99, copied * 100L / input.Length));
            }
            sha.TransformFinalBlock(buffer, 0, 0);
            output?.Flush();
            return Hex.Encode(sha.Hash);
        }
    }

    /// <summary>
    /// Different media can share a file name, size and timestamp. Include the full path and
    /// samples of the ISO's volume descriptors and tail so their caches do not collide.
    /// </summary>
    internal static string CacheFileName(string sourcePath, string imageEntry, long entryLength)
    {
        using (var iso = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var sha = SHA256.Create())
        {
            var header = Encoding.UTF8.GetBytes(string.Join("|", sourcePath.ToUpperInvariant(), iso.Length,
                File.GetLastWriteTimeUtc(sourcePath).Ticks, imageEntry.ToUpperInvariant(), entryLength));
            sha.TransformBlock(header, 0, header.Length, null, 0);
            var sample = new byte[1024 * 1024];
            foreach (var offset in new[] { 0L, Math.Max(0L, iso.Length - sample.Length) })
            {
                iso.Position = offset;
                var total = 0;
                int read;
                while (total < sample.Length && (read = iso.Read(sample, total, sample.Length - total)) > 0) total += read;
                sha.TransformBlock(sample, 0, total, null, 0);
            }
            sha.TransformFinalBlock(sample, 0, 0);
            var invalid = Path.GetInvalidFileNameChars();
            var name = new string(Path.GetFileNameWithoutExtension(sourcePath)
                .Select(c => invalid.Contains(c) ? '_' : c).Take(40).ToArray());
            return name + "-" + Hex.Encode(sha.Hash).Substring(0, 32) + Path.GetExtension(imageEntry);
        }
    }

    /// <summary>Returns the verified checksum of a reusable cache file, or null when it must be extracted again.</summary>
    internal static string? TryReuseCache(string cached, string checksumPath, long expectedLength, Action<int>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(cached) || !File.Exists(checksumPath) || new FileInfo(cached).Length != expectedLength) return null;
            var expected = File.ReadAllText(checksumPath).Trim();
            if (!Hex.IsSha256(expected)) return null;
            using (var input = new FileStream(cached, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
                       FileOptions.SequentialScan))
            {
                var actual = CopyWithProgress(input, null, progress, cancellationToken);
                return Hex.FixedTimeEquals(actual, expected) ? actual : null;
            }
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private async Task<WindowsImage> InspectWimAsync(string sourcePath, string imagePath, WindowsImageKind kind,
        string? isoDescription, CancellationToken cancellationToken)
    {
        var editions = await _wimService.ReadEditionsAsync(imagePath, cancellationToken).ConfigureAwait(false);
        var build = editions.Select(x => x.Build).Where(x => x > 0).DefaultIfEmpty(0).Max();
        var generation = GenerationFromBuild(build);
        var architectures = editions.Select(x => x.Architecture).Distinct().ToArray();
        var architecture = architectures.Length == 1 ? architectures[0] : CpuArchitecture.Unknown;
        var info = new FileInfo(sourcePath);
        return new WindowsImage(sourcePath, imagePath, kind, generation, DisplayVersion(generation, build),
            architecture, info.Length, info.LastWriteTimeUtc, isoDescription, editions,
            generation == WindowsGeneration.WindowsVista ? "LegacyNt6EngineUnavailable" : null);
    }

    public static WindowsGeneration GenerationFromBuild(int build)
    {
        if (build >= 22000) return WindowsGeneration.Windows11;
        if (build >= 10240) return WindowsGeneration.Windows10;
        if (build >= 9600) return WindowsGeneration.Windows81;
        if (build >= 9200) return WindowsGeneration.Windows8;
        if (build >= 7600) return WindowsGeneration.Windows7;
        if (build >= 6000) return WindowsGeneration.WindowsVista;
        return WindowsGeneration.Unknown;
    }

    private static string DisplayVersion(WindowsGeneration generation, int build)
    {
        switch (generation)
        {
            case WindowsGeneration.Windows11: return "Windows 11";
            case WindowsGeneration.Windows10: return "Windows 10";
            case WindowsGeneration.Windows81: return "Windows 8.1";
            case WindowsGeneration.Windows8: return "Windows 8";
            case WindowsGeneration.Windows7: return "Windows 7";
            case WindowsGeneration.WindowsVista: return "Windows Vista";
            default: return build > 0 ? "Windows (build " + build + ")" : "Windows";
        }
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles.ToArray()) TryDelete(path);
        _temporaryFiles.Clear();
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
