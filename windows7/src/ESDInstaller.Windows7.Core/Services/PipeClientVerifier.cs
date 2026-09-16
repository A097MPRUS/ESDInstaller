using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ESDInstaller.Windows7.Core.Services;

/// <summary>Confirms that a named-pipe client is the process that was started, not another local program.</summary>
public static class PipeClientVerifier
{
    public static bool IsClient(NamedPipeServerStream pipe, int expectedProcessId) =>
        GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var clientProcessId) &&
        clientProcessId == (uint)expectedProcessId;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);
}
