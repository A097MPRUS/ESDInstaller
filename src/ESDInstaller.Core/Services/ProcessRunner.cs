using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ESDInstaller.Core.Services;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Elapsed)
{
    public bool Succeeded => ExitCode == 0;
}

public sealed class ProcessRunner
{
    private static readonly Regex DismProgressRegex = new(@"(?<value>\d{1,3}(?:[\.,]\d+)?)\s*%", RegexOptions.Compiled);

    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string?>? environment = null,
        Action<string, bool>? output = null,
        Action<int>? progress = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                info.Environment[pair.Key] = pair.Value ?? string.Empty;
            }
        }

        using var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var watch = Stopwatch.StartNew();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null) return;
            lock (stdout) stdout.AppendLine(eventArgs.Data);
            output?.Invoke(eventArgs.Data, false);
            var match = DismProgressRegex.Match(eventArgs.Data);
            if (match.Success && double.TryParse(match.Groups["value"].Value.Replace(',', '.'),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                progress?.Invoke(Math.Clamp((int)Math.Round(value), 0, 100));
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null) return;
            lock (stderr) stderr.AppendLine(eventArgs.Data);
            output?.Invoke(eventArgs.Data, true);
        };

        cancellationToken.ThrowIfCancellationRequested();
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start {Path.GetFileName(executable)}.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout.HasValue) limit.CancelAfter(timeout.Value);
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); } catch { }
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"{Path.GetFileName(executable)} did not finish within {timeout} and was stopped.");
        }

        watch.Stop();
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), watch.Elapsed);
    }

    public Task<ProcessResult> RunPowerShellAsync(
        string script,
        IReadOnlyDictionary<string, string?>? environment = null,
        Action<string, bool>? output = null,
        CancellationToken cancellationToken = default)
    {
        var wrappedScript = $$"""
            $ProgressPreference = 'SilentlyContinue'
            $VerbosePreference = 'SilentlyContinue'
            [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            try {
              & {
            {{script}}
              }
            }
            catch {
              [Console]::Error.WriteLine($_.Exception.Message)
              exit 1
            }
            """;
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrappedScript));
        return RunAsync(
            Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded],
            environment,
            output,
            cancellationToken: cancellationToken);
    }
}
