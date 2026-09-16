using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ESDInstaller.Core.Models;
using ESDInstaller.Core.Services;

namespace ESDInstaller.Services;

public sealed record WorkerResult(int ExitCode, string? LogPath, bool ElevationCancelled);

public sealed class WorkerClient
{
    public async Task<WorkerResult> ExecuteAsync(InstallationPlan plan, IProgress<ProgressMessage> progress,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var approvedBytes = ApprovedPlan.Serialize(plan);
        var approvedDigest = ApprovedPlan.Digest(approvedBytes);
        var localRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ESDInstaller");
        var tempDirectory = Path.Combine(localRoot, "Temp");
        var logDirectory = Path.Combine(localRoot, "Logs");
        Directory.CreateDirectory(tempDirectory);
        Directory.CreateDirectory(logDirectory);
        var requestId = Guid.NewGuid();
        var planPath = Path.Combine(tempDirectory, $"plan-{requestId:N}.json");
        var pipeName = $"ESDInstaller-{requestId:N}";
        await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var workerDirectory = Path.Combine(AppContext.BaseDirectory, "Worker");
        var workerPath = Path.Combine(workerDirectory, "ESDInstaller.Worker.exe");
        if (!File.Exists(workerPath))
        {
            workerDirectory = AppContext.BaseDirectory;
            workerPath = Path.Combine(workerDirectory, "ESDInstaller.Worker.exe");
        }
        if (!File.Exists(workerPath))
            throw new ESDInstallerException("ErrorWorkerMissing", workerPath);

        Process? worker = null;
        string? logPath = null;
        try
        {
            // A new file and a digest passed independently to the worker bind
            // elevation to these exact bytes, not a later file replacement.
            await using (var file = new FileStream(planPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             4096, FileOptions.Asynchronous))
                await file.WriteAsync(approvedBytes, cancellationToken).ConfigureAwait(false);
            var info = new ProcessStartInfo
            {
                FileName = workerPath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = workerDirectory
            };
            info.ArgumentList.Add("--plan");
            info.ArgumentList.Add(planPath);
            info.ArgumentList.Add("--plan-sha256");
            info.ArgumentList.Add(approvedDigest);
            info.ArgumentList.Add("--pipe");
            info.ArgumentList.Add(pipeName);
            info.ArgumentList.Add("--log-dir");
            info.ArgumentList.Add(logDirectory);

            try { worker = Process.Start(info); }
            catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
            {
                return new WorkerResult(1223, null, true);
            }
            if (worker is null) throw new ESDInstallerException("ErrorWorkerStart", workerPath);

            using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectionTimeout.CancelAfter(TimeSpan.FromSeconds(45));
            var connection = pipe.WaitForConnectionAsync(connectionTimeout.Token);
            var exited = worker.WaitForExitAsync(connectionTimeout.Token);
            // A worker that exits before connecting (for example, invalid arguments) reports its code at once.
            if (await Task.WhenAny(connection, exited).ConfigureAwait(false) == exited &&
                exited.IsCompletedSuccessfully && !connection.IsCompleted)
            {
                connectionTimeout.Cancel();
                try { await connection.ConfigureAwait(false); } catch (OperationCanceledException) { }
                return new WorkerResult(worker.ExitCode, null, false);
            }
            await connection.ConfigureAwait(false);
            // Only the worker started above may report progress; another local program could connect first.
            if (!PipeClientVerifier.IsClient(pipe, worker.Id))
                throw new ESDInstallerException("ErrorWorkerStart", "An unexpected program connected to the installation progress channel.");
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096, leaveOpen: true);
            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null) break;
                try
                {
                    var message = JsonSerializer.Deserialize<ProgressMessage>(line,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (message is null) continue;
                    logPath = message.LogPath ?? logPath;
                    progress.Report(message);
                }
                catch (JsonException) { }
            }
            // The worker closes its progress channel just before exiting; do not wait forever if it hangs.
            try { await worker.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromMinutes(2), cancellationToken).ConfigureAwait(false); }
            catch (TimeoutException)
            {
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
