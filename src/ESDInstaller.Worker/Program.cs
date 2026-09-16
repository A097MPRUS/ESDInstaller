using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ESDInstaller.Core.Installation;
using ESDInstaller.Core.Models;
using ESDInstaller.Core.Services;

namespace ESDInstaller.Worker;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var arguments = ParseArguments(args);
        if (!arguments.TryGetValue("plan", out var planPath) ||
            !arguments.TryGetValue("plan-sha256", out var planDigest) ||
            !arguments.TryGetValue("pipe", out var pipeName) ||
            !arguments.TryGetValue("log-dir", out var logDirectory))
        {
            return 64;
        }

        if (!PrivilegeService.IsAdministrator()) return 740;
        if (!File.Exists(planPath) || new FileInfo(planPath).Length > 1024 * 1024) return 65;

        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        try { await pipe.ConnectAsync(30_000).ConfigureAwait(false); }
        catch { return 66; }
        await using var sender = new ProgressSender(pipe);

        InstallationLog? log = null;
        try
        {
            var plan = await ApprovedPlan.ReadAsync(planPath, planDigest).ConfigureAwait(false);
            log = new InstallationLog(logDirectory);
            sender.Send(new ProgressMessage(InstallationStage.Connecting, 0, null, "ProgressAdministratorGranted",
                string.Empty, DateTime.UtcNow, LogPath: log.Path));

            var processes = new ProcessRunner();
            var wim = new WimService();
            var disks = new DiskService(processes);
            var validator = new ExecutionPlanValidator(disks, wim);
            var context = new InstallationExecutionContext
            {
                Processes = processes,
                Validator = validator,
                Log = log,
                Report = sender.Send
            };
            IInstallationEngine engine = plan.Engine switch
            {
                InstallationEngineKind.ModernWindows => new ModernWindowsEngine(),
                InstallationEngineKind.LegacyNt6 => new LegacyNt6Engine(),
                InstallationEngineKind.LegacyXpNt5 => new LegacyXpNt5Engine(),
                _ => throw new ESDInstallerException("LegacyEngineUnavailable", plan.Engine.ToString())
            };
            await engine.ExecuteAsync(plan, context).ConfigureAwait(false);
            return 0;
        }
        catch (ESDInstallerException exception)
        {
            log?.Write("FATAL", $"{exception.MessageKey}: {exception.TechnicalDetail}");
            sender.Send(new ProgressMessage(InstallationStage.Failed, 0, null, exception.MessageKey,
                exception.TechnicalDetail, DateTime.UtcNow, true, log?.Path));
            return 2;
        }
        catch (Exception exception)
        {
            log?.Write("FATAL", exception.ToString());
            sender.Send(new ProgressMessage(InstallationStage.Failed, 0, null, "ErrorUnexpected",
                exception.Message, DateTime.UtcNow, true, log?.Path));
            return 1;
        }
        finally
        {
            log?.Dispose();
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index + 1 < args.Length; index += 2)
        {
            if (args[index].StartsWith("--", StringComparison.Ordinal))
                result[args[index][2..]] = args[index + 1];
        }
        return result;
    }

    private sealed class ProgressSender : IAsyncDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _gate = new();
        private bool _connected = true;

        public ProgressSender(Stream stream) => _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        public void Send(ProgressMessage message)
        {
            lock (_gate)
            {
                if (!_connected) return;
                try { _writer.WriteLine(JsonSerializer.Serialize(message)); }
                catch (IOException) { _connected = false; }
                catch (ObjectDisposedException) { _connected = false; }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try { await _writer.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }
}
