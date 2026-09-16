using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ESDInstaller.Windows7.Core.Installation;
using ESDInstaller.Windows7.Core.Models;
using ESDInstaller.Windows7.Core.Services;

namespace ESDInstaller.Windows7.Worker;

internal static class Program
{
    private static int Main(string[] args) => RunAsync(args).GetAwaiter().GetResult();

    private static async Task<int> RunAsync(string[] args)
    {
        var arguments = ParseArguments(args);
        string? planPath, planDigest, pipeName, logDirectory;
        if (!arguments.TryGetValue("plan", out planPath) || !arguments.TryGetValue("plan-sha256", out planDigest) ||
            !arguments.TryGetValue("pipe", out pipeName) ||
            !arguments.TryGetValue("log-dir", out logDirectory)) return 64;
        if (!PrivilegeService.IsAdministrator()) return 740;
        if (!File.Exists(planPath) || new FileInfo(planPath).Length > 1024 * 1024) return 65;

        using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous))
        {
            try { await pipe.ConnectAsync(30000).ConfigureAwait(false); }
            catch { return 66; }
            using (var sender = new ProgressSender(pipe))
            {
                InstallationLog? log = null;
                try
                {
                    var plan = ApprovedPlan.Read(planPath, planDigest);
                    log = new InstallationLog(logDirectory);
                    sender.Send(new ProgressMessage(InstallationStage.Connecting, 0, null,
                        "ProgressAdministratorGranted", "", DateTime.UtcNow, false, log.Path));
                    var processes = new ProcessRunner();
                    var wim = new WimService();
                    var disks = new DiskService();
                    var context = new InstallationExecutionContext
                    {
                        Processes = processes,
                        Validator = new ExecutionPlanValidator(disks, wim),
                        Log = log,
                        Report = sender.Send,
                        Imaging = new WimDeploymentService(),
                        DiskPart = new DiskPartService(processes, disks)
                    };
                    IInstallationEngine engine;
                    switch (plan.Engine)
                    {
                        case InstallationEngineKind.ModernWindows: engine = new ModernWindowsEngine(); break;
                        case InstallationEngineKind.LegacyNt6: engine = new LegacyNt6Engine(); break;
                        case InstallationEngineKind.LegacyXpNt5: engine = new LegacyXpNt5Engine(); break;
                        default: throw new ESDInstallerException("LegacyEngineUnavailable", plan.Engine.ToString());
                    }
                    await engine.ExecuteAsync(plan, context).ConfigureAwait(false);
                    return 0;
                }
                catch (ESDInstallerException exception)
                {
                    log?.Write("FATAL", exception.MessageKey + ": " + exception.Detail);
                    sender.Send(new ProgressMessage(InstallationStage.Failed, 0, null, exception.MessageKey,
                        exception.Detail, DateTime.UtcNow, true, log?.Path));
                    return 2;
                }
                catch (Exception exception)
                {
                    log?.Write("FATAL", exception.ToString());
                    sender.Send(new ProgressMessage(InstallationStage.Failed, 0, null, "ErrorUnexpected",
                        exception.Message, DateTime.UtcNow, true, log?.Path));
                    return 1;
                }
                finally { log?.Dispose(); }
            }
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index + 1 < args.Length; index += 2)
            if (args[index].StartsWith("--", StringComparison.Ordinal)) result[args[index].Substring(2)] = args[index + 1];
        return result;
    }

    private sealed class ProgressSender : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _sync = new object();
        private bool _connected = true;
        public ProgressSender(Stream stream) => _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        public void Send(ProgressMessage message)
        {
            lock (_sync)
            {
                if (!_connected) return;
                try { _writer.WriteLine(JsonSerializer.Serialize(message)); }
                catch (IOException) { _connected = false; }
                catch (ObjectDisposedException) { _connected = false; }
            }
        }
        public void Dispose() { try { _writer.Dispose(); } catch { } }
    }
}
