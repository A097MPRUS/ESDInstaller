using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using ESDInstaller.Core.Models;

namespace ESDInstaller.Core.Services;

/// <summary>Read-only source handles and physical-volume checks, held through deployment.</summary>
public sealed class SourceImageLease : IDisposable
{
    private readonly List<FileStream> _files = new();

    public static SourceImageLease Open(InstallationPlan plan)
    {
        ExecutionPlanValidator.ValidatePlanStructure(plan);
        var lease = new SourceImageLease();
        try
        {
            foreach (var path in new[] { plan.Source.SourcePath, plan.Source.ImagePath }
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                // Resolve the opened file, not a drive-letter prefix: junctions
                // and directory mount points may lead to a different volume.
                var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                lease._files.Add(file);
                var extents = GetExtents(file.SafeFileHandle);
                EnsureSeparate(extents, plan.DestinationPartition, path);
            }
            return lease;
        }
        catch (Exception exception)
        {
            lease.Dispose();
            if (exception is ESDInstallerException) throw;
            throw new ESDInstallerException("ErrorImageOpen",
                "The location of the installation image could not be verified. Use a local copy on a different volume. " +
                exception.Message, exception);
        }
    }

    internal static void EnsureSeparate(IReadOnlyList<SourceExtent> extents, PartitionIdentity target, string path)
    {
        foreach (var extent in extents)
        {
            if (extent.DiskNumber == target.DiskNumber &&
                extent.Offset < checked(target.OffsetBytes + target.LengthBytes) &&
                target.OffsetBytes < checked(extent.Offset + extent.Length))
                throw new ESDInstallerException("ValidationProtectedPartition",
                    "The installation image is on the partition selected for erasing. Move it to another volume first. " + path);
        }
    }

    internal static IReadOnlyList<SourceExtent> GetExtents(SafeFileHandle file)
    {
        var name = new StringBuilder(512);
        var length = GetFinalPathNameByHandle(file, name, (uint)name.Capacity, 1);
        if (length >= name.Capacity)
        {
            if (length > 32768) throw new IOException("The source path is too long.");
            name = new StringBuilder(checked((int)length + 1));
            length = GetFinalPathNameByHandle(file, name, (uint)name.Capacity, 1);
        }
        if (length == 0 || length >= name.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
        var finalPath = name.ToString();
        var end = finalPath.IndexOf("}\\", StringComparison.Ordinal);
        if (!finalPath.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase) || end < 0)
            throw new IOException("The source does not have a verifiable local volume.");
        var volumeRoot = finalPath[..(end + 2)];
        // Real or mounted optical media cannot be the writable destination.
        if (GetDriveType(volumeRoot) == 5) return Array.Empty<SourceExtent>();
        using var volume = CreateFile(volumeRoot.TrimEnd('\\'), 0, 7, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (volume.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var buffer = new byte[64 * 1024];
        if (!DeviceIoControl(volume, 0x00560000, IntPtr.Zero, 0, buffer, buffer.Length, out var returned, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return ParseExtents(buffer, returned);
    }

    internal static IReadOnlyList<SourceExtent> ParseExtents(byte[] buffer, int returned)
    {
        // Native VOLUME_DISK_EXTENTS: DWORD count, alignment, DISK_EXTENT[].
        // DISK_EXTENT: DWORD disk, alignment, LARGE_INTEGER offset and length.
        const int start = 8, stride = 24;
        if (returned < start || returned > buffer.Length) throw new IOException("Invalid volume extent data.");
        var count = BitConverter.ToUInt32(buffer, 0);
        if (count == 0 || count > (returned - start) / stride) throw new IOException("Incomplete volume extent data.");
        var result = new List<SourceExtent>();
        for (var i = 0; i < count; i++)
        {
            var offset = start + i * stride;
            var disk = BitConverter.ToUInt32(buffer, offset);
            var position = BitConverter.ToInt64(buffer, offset + 8);
            var size = BitConverter.ToInt64(buffer, offset + 16);
            if (disk > int.MaxValue || position < 0 || size <= 0 || position > long.MaxValue - size)
                throw new IOException("Invalid physical source location.");
            result.Add(new SourceExtent((int)disk, position, size));
        }
        return result;
    }

    public void Dispose()
    {
        foreach (var file in _files) file.Dispose();
        _files.Clear();
    }

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
    [DllImport("kernel32.dll", EntryPoint = "GetDriveTypeW", CharSet = CharSet.Unicode)]
    private static extern uint GetDriveType(string root);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint code, IntPtr input, int inputSize,
        [Out] byte[] output, int outputSize, out int returned, IntPtr overlapped);
}

internal sealed record SourceExtent(int DiskNumber, long Offset, long Length);

