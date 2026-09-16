using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using ESDInstaller.Windows8.Core.Models;
using ESDInstaller.Windows8.Core.Services;

namespace ESDInstaller.Windows8.Services;

public sealed record WorkerResult(int ExitCode, string? LogPath, bool ElevationCancelled);

public sealed class WorkerClient
{
    public async Task<WorkerResult> ExecuteAsync(InstallationPlan plan, IProgress<ProgressMessage> progress,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var approvedBytes = ApprovedPlan.Serialize(plan);
        var approvedDigest = ApprovedPlan.Digest(approvedBytes);
        var localRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ESDInstallerWindows8");
        var temp = Path.Combine(localRoot, "Temp");
        var logs = Path.Combine(localRoot, "Logs");
        Directory.CreateDirectory(temp); Directory.CreateDirectory(logs);
        var requestId = Guid.NewGuid().ToString("N");
        var planPath = Path.Combine(temp, "plan-" + requestId + ".json");
        var pipeName = "ESDInstallerWindows8-" + requestId;
        using (var pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                   PipeOptions.Asynchronous))
        {
            var workerDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Worker");
            var workerPath = Path.Combine(workerDirectory, "ESDInstaller.Windows8.Worker.exe");
            if (!File.Exists(workerPath)) throw new ESDInstallerException("ErrorWorkerMissing", workerPath);
            Process? worker = null;
            string? logPath = null;
            try
            {
                // A new file and a digest passed independently to the worker bind
                // elevation to these exact bytes, not a later file replacement.
                using (var file = new FileStream(planPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    file.Write(approvedBytes, 0, approvedBytes.Length);
                var info = new ProcessStartInfo
                {
                    FileName = workerPath, UseShellExecute = true, Verb = "runas", WorkingDirectory = workerDirectory,
                    Arguments = "--plan " + ProcessRunner.QuoteArgument(planPath) + " --plan-sha256 " + approvedDigest + " --pipe " +
                                ProcessRunner.QuoteArgument(pipeName) + " --log-dir " + ProcessRunner.QuoteArgument(logs)
                };
                try { worker = Process.Start(info); }
                catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
                { return new WorkerResult(1223, null, true); }
                if (worker == null) throw new ESDInstallerException("ErrorWorkerStart", workerPath);
                var connection = pipe.WaitForConnectionAsync();
                var exited = Task.Run(() => worker.WaitForExit());
                var timeout = Task.Delay(TimeSpan.FromSeconds(45), cancellationToken);
                var first = await Task.WhenAny(connection, exited, timeout).ConfigureAwait(false);
                // A worker that exits before connecting (for example, invalid arguments) reports its code at once.
                if (first == exited && !connection.IsCompleted) return new WorkerResult(worker.ExitCode, null, false);
                if (first == timeout)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new ESDInstallerException("ErrorWorkerStart", "The elevated worker did not connect.");
                }
                await connection.ConfigureAwait(false);
                // Only the worker started above may report progress; another local program could connect first.
                if (!PipeClientVerifier.IsClient(pipe, worker.Id))
                    throw new ESDInstallerException("ErrorWorkerStart", "An unexpected program connected to the installation progress channel.");
                using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true))
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) break;
                        try
                        {
                            var message = JsonConvert.DeserializeObject<ProgressMessage>(line);
                            if (message == null) continue;
                            logPath = message.LogPath ?? logPath;
                            progress.Report(message);
                        }
                        catch (JsonException) { }
                    }
                }
                // The worker closes its progress channel just before exiting; do not wait forever if it hangs.
                if (await Task.WhenAny(exited, Task.Delay(TimeSpan.FromMinutes(2), cancellationToken)).ConfigureAwait(false) != exited)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new ESDInstallerException("ErrorUnexpected", "The installation worker stopped reporting progress but did not exit. Check the log before restarting.");
                }
                return new WorkerResult(worker.ExitCode, logPath, false);
            }
            finally
            {
                worker?.Dispose();
                try { File.Delete(planPath); } catch { }
            }
        }
    }
}
