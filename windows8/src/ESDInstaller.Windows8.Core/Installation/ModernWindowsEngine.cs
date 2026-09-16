using ESDInstaller.Windows8.Core.Models;
using ESDInstaller.Windows8.Core.Services;

namespace ESDInstaller.Windows8.Core.Installation;

public sealed class ModernWindowsEngine : IInstallationEngine
{
    public InstallationEngineKind Kind => InstallationEngineKind.ModernWindows;
    // Generous limits: these commands normally finish in seconds, but must not hang forever.
    private static readonly TimeSpan BootTimeout = TimeSpan.FromMinutes(15), RegistryTimeout = TimeSpan.FromMinutes(5);

    public async Task ExecuteAsync(InstallationPlan plan, InstallationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!PrivilegeService.IsAdministrator())
            throw new ESDInstallerException("AdministratorRequired", "The installation worker is not elevated.");
        LogPlan(plan, context.Log);
        context.Progress(InstallationStage.Validating, 1, null, "ProgressValidatingPlan");
        // Keep both source files read-only, reject source/target overlap and
        // verify extracted ISO content before any formatting or boot operation.
        using var sourceLease = SourceImageLease.Open(plan, cancellationToken);
        await context.Validator.ValidateAsync(plan, cancellationToken).ConfigureAwait(false);

        context.Progress(InstallationStage.PreparingDestination, 4, null, "ProgressPreparingDestination");
        var destination = await context.DiskPart.FormatDestinationAsync(plan, context.Log, cancellationToken)
            .ConfigureAwait(false);
        context.Progress(InstallationStage.Formatting, 8, 100, "ProgressDestinationFormatted", destination.Root);

        context.Progress(InstallationStage.ApplyingImage, 9, 0, "ProgressApplyingImage");
        await context.Imaging.ApplyAsync(plan.Source.ImagePath, plan.Edition.Index, destination.Root,
            percent => context.Progress(InstallationStage.ApplyingImage, 9 + percent * 76 / 100, percent,
                "ProgressApplyingImage", percent + "%"),
            line => context.Log.Write("WIMLIB", line), cancellationToken).ConfigureAwait(false);

        if (plan.BypassWindows11Requirements)
        {
            context.Progress(InstallationStage.ApplyingImage, 86, null, "ProgressConfiguringWindows11Bypass");
            await ConfigureWindows11BypassAsync(plan, destination.Root, context, cancellationToken).ConfigureAwait(false);
        }

        context.Progress(InstallationStage.InstallingBootFiles, 88, null, "ProgressInstallingBootFiles");
        var boot = await context.DiskPart.AcquireBootAccessAsync(plan, context.Log, cancellationToken).ConfigureAwait(false);
        try
        {
            await InstallBootFilesAsync(plan, destination.Root, boot.Root, context, cancellationToken).ConfigureAwait(false);
            context.Progress(InstallationStage.Verifying, 96, null, "ProgressVerifyingInstallation");
            VerifyFiles(plan, destination.Root, boot.Root);
        }
        finally { await context.DiskPart.ReleaseBootAccessAsync(plan, boot, context.Log).ConfigureAwait(false); }
        if (plan.RequestOneTimeBoot)
            context.Log.Write("WARNING", "One-time boot was not changed because the new BCD object could not be proven safely.");
        context.Progress(InstallationStage.Completed, 100, 100, "ProgressInstallationCompleted", context.Log.Path);
    }

    private static async Task InstallBootFilesAsync(InstallationPlan plan, string destinationRoot, string bootRoot,
        InstallationExecutionContext context, CancellationToken cancellationToken)
    {
        var bcdboot = Path.Combine(Environment.SystemDirectory, "bcdboot.exe");
        var arguments = new List<string>
        {
            Path.Combine(destinationRoot, "Windows"), "/s", bootRoot.TrimEnd('\\')
        };
        var help = await context.Processes.RunAsync(bcdboot, new[] { "/?" }, cancellationToken: cancellationToken, timeout: RegistryTimeout)
            .ConfigureAwait(false);
        if ((help.StandardOutput + help.StandardError).IndexOf("/f", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            arguments.Add("/f");
            arguments.Add(plan.FirmwareMode == FirmwareMode.Uefi ? "UEFI" : "BIOS");
        }
        if (plan.PreserveCurrentBootDefault) arguments.Add("/d");
        arguments.Add("/v");
        context.Log.Write("COMMAND", "bcdboot " + string.Join(" ", arguments.Select(ProcessRunner.QuoteArgument)));
        var result = await context.Processes.RunAsync(bcdboot, arguments,
            output: (line, error) => context.Log.Write(error ? "BCDBOOT-STDERR" : "BCDBOOT", line),
            cancellationToken: cancellationToken, timeout: BootTimeout).ConfigureAwait(false);
        context.Log.Write("RESULT", "BCDBoot exit code " + result.ExitCode + "; elapsed " + result.Elapsed);
        if (!result.Succeeded)
            throw new ESDInstallerException("ErrorBcdBoot", "BCDBoot exited with code " + result.ExitCode + ". " + result.StandardError.Trim());
    }

    private static async Task ConfigureWindows11BypassAsync(InstallationPlan plan, string destinationRoot,
        InstallationExecutionContext context, CancellationToken cancellationToken)
    {
        if (plan.Generation != WindowsGeneration.Windows11)
            throw new ESDInstallerException("ValidationPlanUnreadable", "The bypass is only valid for Windows 11.");
        var hive = Path.Combine(destinationRoot, "Windows", "System32", "Config", "SYSTEM");
        if (!File.Exists(hive)) throw new ESDInstallerException("ErrorWindows11Bypass", "Offline SYSTEM hive not found: " + hive);
        var reg = Path.Combine(Environment.SystemDirectory, "reg.exe");
        var mount = "HKLM\\ESDInstaller_" + plan.PlanId.ToString("N");
        var loaded = false;
        try
        {
            await RunRegistryAsync(reg, new[] { "load", mount, hive }, context, cancellationToken).ConfigureAwait(false);
            loaded = true;
            var lab = mount + @"\Setup\LabConfig";
            foreach (var name in new[] { "BypassCPUCheck", "BypassTPMCheck", "BypassRAMCheck", "BypassSecureBootCheck", "BypassStorageCheck" })
                await RunRegistryAsync(reg, new[] { "add", lab, "/v", name, "/t", "REG_DWORD", "/d", "1", "/f" }, context, cancellationToken).ConfigureAwait(false);
            await RunRegistryAsync(reg, new[] { "add", mount + @"\Setup\MoSetup", "/v", "AllowUpgradesWithUnsupportedTPMOrCPU", "/t", "REG_DWORD", "/d", "1", "/f" }, context, cancellationToken).ConfigureAwait(false);
            var verify = await RunRegistryAsync(reg, new[] { "query", lab, "/s" }, context, cancellationToken).ConfigureAwait(false);
            foreach (var name in new[] { "BypassCPUCheck", "BypassTPMCheck", "BypassRAMCheck", "BypassSecureBootCheck", "BypassStorageCheck" })
                if (verify.StandardOutput.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                    throw new ESDInstallerException("ErrorWindows11Bypass", "Offline registry verification failed for " + name + ".");
        }
        finally
        {
            if (loaded)
            {
                var unload = await context.Processes.RunAsync(reg, new[] { "unload", mount },
                    output: (line, error) => context.Log.Write(error ? "REG-STDERR" : "REG", line),
                    cancellationToken: CancellationToken.None, timeout: RegistryTimeout).ConfigureAwait(false);
                if (!unload.Succeeded) throw new ESDInstallerException("ErrorWindows11Bypass", "The offline hive could not be unloaded.");
            }
        }
    }

    private static async Task<ProcessResult> RunRegistryAsync(string reg, IReadOnlyList<string> arguments,
        InstallationExecutionContext context, CancellationToken cancellationToken)
    {
        context.Log.Write("COMMAND", "reg " + string.Join(" ", arguments.Select(ProcessRunner.QuoteArgument)));
        var result = await context.Processes.RunAsync(reg, arguments,
            output: (line, error) => context.Log.Write(error ? "REG-STDERR" : "REG", line),
            cancellationToken: cancellationToken, timeout: RegistryTimeout).ConfigureAwait(false);
        if (!result.Succeeded) throw new ESDInstallerException("ErrorWindows11Bypass", "REG exited with code " + result.ExitCode + ".");
        return result;
    }

    private static void VerifyFiles(InstallationPlan plan, string destinationRoot, string bootRoot)
    {
        foreach (var path in new[]
        {
            Path.Combine(destinationRoot, "Windows", "System32", "Config", "SYSTEM"),
            Path.Combine(destinationRoot, "Windows", "System32", "winload.exe")
        }) if (!File.Exists(path)) throw new ESDInstallerException("ErrorDeploymentVerification", path);

        if (plan.FirmwareMode == FirmwareMode.Uefi)
        {
            var manager = Path.Combine(bootRoot, "EFI", "Microsoft", "Boot", "bootmgfw.efi");
            if (!File.Exists(manager)) throw new ESDInstallerException("ErrorBootVerification", manager);
        }
        else
        {
            var bcd = Path.Combine(bootRoot, "Boot", "BCD");
            if (!File.Exists(bcd)) throw new ESDInstallerException("ErrorBootVerification", bcd);
        }
    }

    private static void LogPlan(InstallationPlan plan, InstallationLog log)
    {
        log.Write("PLAN", "ID=" + plan.PlanId + "; fingerprint=" + plan.ConfirmationFingerprint);
        log.Write("SOURCE", plan.Source.SourcePath + "; image=" + plan.Source.ImagePath + "; index=" + plan.Edition.Index + "; build=" + plan.Edition.Build);
        log.Write("TARGET", "Disk " + plan.DestinationDisk.DiskNumber + ": " + plan.DestinationDisk.Model + "; UniqueId=" + plan.DestinationDisk.UniqueId + "; Serial=" + plan.DestinationDisk.SerialNumber + "; Size=" + plan.DestinationDisk.SizeBytes);
        log.Write("TARGET", "Partition " + plan.DestinationPartition.PartitionNumber + "; Offset=" + plan.DestinationPartition.OffsetBytes + "; Length=" + plan.DestinationPartition.LengthBytes);
        log.Write("BOOT", "Partition " + plan.BootPartition.PartitionNumber + "; Firmware=" + plan.FirmwareMode + "; Scheme=" + plan.PartitionScheme);
    }
}
