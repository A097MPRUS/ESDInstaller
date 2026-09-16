using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ESDInstaller.Windows7.Core.Services;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Elapsed)
{
    public bool Succeeded => ExitCode == 0;
}

public sealed class ProcessRunner
{
    private static readonly Regex DismProgressRegex = new Regex(
        @"(?<value>\d{1,3}(?:[\.,]\d+)?)\s*%", RegexOptions.Compiled);

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
            Arguments = JoinArguments(arguments),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (environment != null)
        {
            foreach (var pair in environment)
                info.EnvironmentVariables[pair.Key] = pair.Value ?? string.Empty;
        }

        using (var process = new Process { StartInfo = info, EnableRaisingEvents = true })
        {
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var watch = Stopwatch.StartNew();
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            process.OutputDataReceived += delegate(object? sender, DataReceivedEventArgs eventArgs)
            {
                if (eventArgs.Data == null) return;
                lock (stdout) stdout.AppendLine(eventArgs.Data);
                output?.Invoke(eventArgs.Data, false);
                var match = DismProgressRegex.Match(eventArgs.Data);
                double value;
                if (match.Success && double.TryParse(match.Groups["value"].Value.Replace(',', '.'),
                        NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                    progress?.Invoke(Clamp((int)Math.Round(value), 0, 100));
            };
            process.ErrorDataReceived += delegate(object? sender, DataReceivedEventArgs eventArgs)
            {
                if (eventArgs.Data == null) return;
                lock (stderr) stderr.AppendLine(eventArgs.Data);
                output?.Invoke(eventArgs.Data, true);
            };
            process.Exited += delegate { exited.TrySetResult(process.ExitCode); };

            if (!process.Start())
                throw new InvalidOperationException("Could not start " + Path.GetFileName(executable) + ".");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            using (var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (timeout.HasValue) limit.CancelAfter(timeout.Value);
                using (limit.Token.Register(delegate
                       {
                           exited.TrySetCanceled();
                           try { if (!process.HasExited) process.Kill(); } catch { }
                       }))
                {
                    try { await exited.Task.ConfigureAwait(false); }
                    catch (TaskCanceledException)
                    {
                        process.WaitForExit(5000);
                        if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                        throw new TimeoutException(Path.GetFileName(executable) + " did not finish within " + timeout + " and was stopped.");
                    }
                }
            }

            // A helper process that inherited the output pipes must not block completion forever.
            process.WaitForExit(10000);
            watch.Stop();
            return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), watch.Elapsed);
        }
    }

    public Task<ProcessResult> RunPowerShellAsync(
        string script,
        IReadOnlyDictionary<string, string?>? environment = null,
        Action<string, bool>? output = null,
        CancellationToken cancellationToken = default)
    {
        var wrapped = "$ProgressPreference='SilentlyContinue'\r\n" +
                      "$VerbosePreference='SilentlyContinue'\r\n" +
                      "try { & {\r\n" + script + "\r\n} } catch { " +
                      "[Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrapped));
        return RunAsync(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded },
            environment, output, null, cancellationToken);
    }

    public static string QuoteArgument(string value)
    {
        if (value.Length > 0 && value.All(c => !char.IsWhiteSpace(c) && c != '"')) return value;
        var builder = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { slashes++; continue; }
            if (character == '"')
            {
                builder.Append('\\', slashes * 2 + 1).Append('"');
                slashes = 0;
                continue;
            }
            builder.Append('\\', slashes).Append(character);
            slashes = 0;
        }
        builder.Append('\\', slashes * 2).Append('"');
        return builder.ToString();
    }

    private static string JoinArguments(IEnumerable<string> arguments) =>
        string.Join(" ", arguments.Select(QuoteArgument));

    internal static int Clamp(int value, int minimum, int maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;
}
