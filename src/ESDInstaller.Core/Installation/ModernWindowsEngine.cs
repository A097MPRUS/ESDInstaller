using System.Text.Json;
using ESDInstaller.Core.Models;
using ESDInstaller.Core.Services;

namespace ESDInstaller.Core.Installation;

public sealed class ModernWindowsEngine : IInstallationEngine
{
    // Generous limits: these commands normally finish in seconds, but must not hang forever.
    private static readonly TimeSpan BootTimeout = TimeSpan.FromMinutes(15), RegistryTimeout = TimeSpan.FromMinutes(5);

    public InstallationEngineKind Kind => InstallationEngineKind.ModernWindows;

    public async Task ExecuteAsync(InstallationPlan plan, InstallationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!PrivilegeService.IsAdministrator())
            throw new ESDInstallerException("AdministratorRequired", "The elevated worker is not running as administrator.");

        context.Log.Write("PLAN", $"Plan ID: {plan.PlanId}; fingerprint: {plan.ConfirmationFingerprint}");
        context.Log.Write("SOURCE", $"{plan.Source.SourcePath}; image: {plan.Source.ImagePath}; index: {plan.Edition.Index}; build: {plan.Edition.Build}; architecture: {plan.Edition.Architecture}");
        context.Log.Write("TARGET", $"Disk {plan.DestinationDisk.DiskNumber}: {plan.DestinationDisk.Model}; UniqueId={plan.DestinationDisk.UniqueId}; Serial={plan.DestinationDisk.SerialNumber}; Size={plan.DestinationDisk.SizeBytes}");
        context.Log.Write("TARGET", $"Partition {plan.DestinationPartition.PartitionNumber}; Offset={plan.DestinationPartition.OffsetBytes}; Length={plan.DestinationPartition.LengthBytes}; Guid={plan.DestinationPartition.PartitionGuid}");
        context.Log.Write("BOOT", $"Partition {plan.BootPartition.PartitionNumber}; Firmware={plan.FirmwareMode}; Scheme={plan.PartitionScheme}");
        context.Log.Write("COMPATIBILITY", $"Windows 11 unsupported-hardware bypass: {plan.BypassWindows11Requirements}");

        context.Progress(InstallationStage.Validating, 1, null, "ProgressValidatingPlan");
        // Keep both source files read-only and reject source/target overlap
        // before any formatting or boot operation.
        using var sourceLease = SourceImageLease.Open(plan);
        await context.Validator.ValidateAsync(plan, cancellationToken).ConfigureAwait(false);
        context.Progress(InstallationStage.PreparingDestination, 4, null, "ProgressPreparingDestination");

        var destinationRoot = await PrepareAndFormatDestinationAsync(plan, context, cancellationToken).ConfigureAwait(false);
        context.Progress(InstallationStage.Formatting, 8, 100, "ProgressDestinationFormatted", destinationRoot);

        context.Progress(InstallationStage.ApplyingImage, 9, 0, "ProgressApplyingImage");
        var dism = Path.Combine(Environment.SystemDirectory, "dism.exe");
        var dismArguments = new[]
        {
            "/English",
            "/Apply-Image",
            $"/ImageFile:{plan.Source.ImagePath}",
            $"/Index:{plan.Edition.Index}",
            $"/ApplyDir:{destinationRoot}",
            "/CheckIntegrity",
            "/Verify"
        };
        context.Log.Write("COMMAND", FormatCommand(dism, dismArguments));
        var apply = await context.Processes.RunAsync(dism, dismArguments,
            output: (line, isError) => context.Log.Write(isError ? "DISM-STDERR" : "DISM", line),
            progress: percent => context.Progress(InstallationStage.ApplyingImage, 9 + (percent * 76 / 100), percent,
                "ProgressApplyingImage", $"{percent}%"), cancellationToken: cancellationToken).ConfigureAwait(false);
        context.Log.Write("RESULT", $"DISM exit code {apply.ExitCode}; elapsed {apply.Elapsed}");
        if (!apply.Succeeded)
            throw new ESDInstallerException("ErrorDismApply", $"DISM exited with code {apply.ExitCode}. {apply.StandardError}".Trim());

        if (plan.BypassWindows11Requirements)
        {
            context.Progress(InstallationStage.ApplyingImage, 86, null,
                "ProgressConfiguringWindows11Bypass");
            await ConfigureWindows11BypassAsync(plan, destinationRoot, context, cancellationToken)
                .ConfigureAwait(false);
        }

        context.Progress(InstallationStage.InstallingBootFiles, 88, null, "ProgressInstallingBootFiles");
        var bootAccess = await AcquireBootAccessAsync(plan, context, cancellationToken).ConfigureAwait(false);
        try
        {
            var bcdboot = Path.Combine(Environment.SystemDirectory, "bcdboot.exe");
            var windowsPath = Path.Combine(destinationRoot, "Windows");
            var firmware = plan.FirmwareMode == FirmwareMode.Uefi ? "UEFI" : "BIOS";
            var bootArguments = new List<string> { windowsPath, "/s", bootAccess.Root.TrimEnd('\\'), "/f", firmware };
            if (plan.PreserveCurrentBootDefault) bootArguments.Add("/d");
            bootArguments.Add("/v");
            context.Log.Write("COMMAND", FormatCommand(bcdboot, bootArguments));
            var bootResult = await context.Processes.RunAsync(bcdboot, bootArguments,
                output: (line, isError) => context.Log.Write(isError ? "BCDBOOT-STDERR" : "BCDBOOT", line),
                cancellationToken: cancellationToken, timeout: BootTimeout).ConfigureAwait(false);
            context.Log.Write("RESULT", $"BCDBoot exit code {bootResult.ExitCode}; elapsed {bootResult.Elapsed}");
            if (!bootResult.Succeeded)
                throw new ESDInstallerException("ErrorBcdBoot", $"BCDBoot exited with code {bootResult.ExitCode}. {bootResult.StandardError}".Trim());

            context.Progress(InstallationStage.Verifying, 96, null, "ProgressVerifyingInstallation");
            VerifyFiles(plan, destinationRoot, bootAccess.Root);
        }
        finally
        {
            await ReleaseBootAccessAsync(plan, bootAccess, context).ConfigureAwait(false);
        }

        if (plan.RequestOneTimeBoot)
            context.Log.Write("WARNING", "One-time boot was requested but was not changed because a new BCD identifier could not be proven safely.");

        context.Progress(InstallationStage.Completed, 100, 100, "ProgressInstallationCompleted", context.Log.Path);
    }

    private static async Task ConfigureWindows11BypassAsync(InstallationPlan plan, string destinationRoot,
        InstallationExecutionContext context, CancellationToken cancellationToken)
    {
        if (plan.Generation != WindowsGeneration.Windows11)
            throw new ESDInstallerException("ValidationPlanUnreadable", "Unsupported-hardware bypass requires a Windows 11 image.");

        var systemHive = Path.Combine(destinationRoot, "Windows", "System32", "Config", "SYSTEM");
        if (!File.Exists(systemHive))
            throw new ESDInstallerException("ErrorWindows11Bypass", $"Offline SYSTEM hive was not found: {systemHive}");

        var reg = Path.Combine(Environment.SystemDirectory, "reg.exe");
        var mountName = $"ESDInstaller_{plan.PlanId:N}";
        var mountRoot = $@"HKLM\{mountName}";
        var loaded = false;
        try
        {
            await RunRegistryCommandAsync(reg, new[] { "load", mountRoot, systemHive }, context,
                cancellationToken).ConfigureAwait(false);
            loaded = true;

            var labConfig = $@"{mountRoot}\Setup\LabConfig";
            foreach (var valueName in new[]
                     {
                         "BypassCPUCheck", "BypassTPMCheck", "BypassRAMCheck",
                         "BypassSecureBootCheck", "BypassStorageCheck"
                     })
            {
                await RunRegistryCommandAsync(reg,
                    new[] { "add", labConfig, "/v", valueName, "/t", "REG_DWORD", "/d", "1", "/f" },
                    context, cancellationToken).ConfigureAwait(false);
            }

            var moSetup = $@"{mountRoot}\Setup\MoSetup";
            await RunRegistryCommandAsync(reg,
                new[] { "add", moSetup, "/v", "AllowUpgradesWithUnsupportedTPMOrCPU", "/t", "REG_DWORD", "/d", "1", "/f" },
                context, cancellationToken).ConfigureAwait(false);

            var verification = await RunRegistryCommandAsync(reg,
                new[] { "query", labConfig, "/s" }, context, cancellationToken).ConfigureAwait(false);
            foreach (var valueName in new[]
                     {
                         "BypassCPUCheck", "BypassTPMCheck", "BypassRAMCheck",
                         "BypassSecureBootCheck", "BypassStorageCheck"
                     })
            {
                if (!verification.StandardOutput.Contains(valueName, StringComparison.OrdinalIgnoreCase))
                    throw new ESDInstallerException("ErrorWindows11Bypass", $"Offline registry verification failed for {valueName}.");
            }
            context.Log.Write("VERIFY", "Windows 11 unsupported-hardware bypass values were written and read back from the offline SYSTEM hive.");
        }
        finally
        {
            if (loaded)
            {
                var unload = await context.Processes.RunAsync(reg, new[] { "unload", mountRoot },
                    output: (line, isError) => context.Log.Write(isError ? "REG-STDERR" : "REG", line),
                    cancellationToken: CancellationToken.None, timeout: RegistryTimeout).ConfigureAwait(false);
                context.Log.Write("RESULT", $"REG unload exit code {unload.ExitCode}; elapsed {unload.Elapsed}");
                if (!unload.Succeeded)
                    throw new ESDInstallerException("ErrorWindows11Bypass", $"The offline SYSTEM registry hive could not be unloaded. {unload.StandardError}".Trim());
            }
        }
    }

    private static async Task<ProcessResult> RunRegistryCommandAsync(string reg, IReadOnlyList<string> arguments,
        InstallationExecutionContext context, CancellationToken cancellationToken)
    {
        context.Log.Write("COMMAND", FormatCommand(reg, arguments));
        var result = await context.Processes.RunAsync(reg, arguments,
            output: (line, isError) => context.Log.Write(isError ? "REG-STDERR" : "REG", line),
            cancellationToken: cancellationToken, timeout: RegistryTimeout).ConfigureAwait(false);
        context.Log.Write("RESULT", $"REG exit code {result.ExitCode}; elapsed {result.Elapsed}");
        if (!result.Succeeded)
            throw new ESDInstallerException("ErrorWindows11Bypass",
                $"REG exited with code {result.ExitCode}. {result.StandardError}".Trim());
        return result;
    }

    private static async Task<string> PrepareAndFormatDestinationAsync(InstallationPlan plan,
        InstallationExecutionContext context, CancellationToken cancellationToken)
    {
        const string script = """
            $ErrorActionPreference = 'Stop'
            $diskNumber = [int]$env:WD_DISK_NUMBER
            $partitionNumber = [int]$env:WD_PARTITION_NUMBER
            $disk = Get-Disk -Number $diskNumber -ErrorAction Stop
            $wmiDisk = Get-CimInstance Win32_DiskDrive -Filter "Index=$diskNumber" -ErrorAction Stop
            $actualUnique = ([string]$wmiDisk.PNPDeviceID).Trim()
            $actualSerial = ([string]$wmiDisk.SerialNumber).Trim()
            if ($env:WD_DISK_UNIQUE -and $actualUnique -ne $env:WD_DISK_UNIQUE) { throw 'Disk unique ID changed.' }
            if ($env:WD_DISK_SERIAL -and $actualSerial -ne $env:WD_DISK_SERIAL) { throw 'Disk serial number changed.' }
            $expectedSize = [long]$env:WD_DISK_SIZE
            $actualSize = [long]$disk.Size
            if ($actualSize -ne $expectedSize) { throw "Disk size changed. Expected $expectedSize bytes; found $actualSize bytes." }
            if ($disk.IsBoot -and $env:WD_ALLOW_BOOT_DISK -ne 'true') { throw 'Refusing an unexpected boot disk.' }
            if ($disk.IsReadOnly -or $disk.IsOffline) { throw 'Destination disk is read-only or offline.' }
            $partition = Get-Partition -DiskNumber $diskNumber -PartitionNumber $partitionNumber -ErrorAction Stop
            if ([long]$partition.Offset -ne [long]$env:WD_PARTITION_OFFSET -or [long]$partition.Size -ne [long]$env:WD_PARTITION_LENGTH) { throw 'Partition geometry changed.' }
            $actualGuid = ([string]$partition.Guid).Trim().Trim('{','}').ToLowerInvariant()
            if ($env:WD_PARTITION_GUID -and $actualGuid -ne $env:WD_PARTITION_GUID) { throw 'Partition GUID changed.' }
            $systemPartition = $null
            try { $systemPartition = Get-Partition -DriveLetter $env:SystemDrive.TrimEnd(':') -ErrorAction Stop } catch {}
            if ($systemPartition -and $systemPartition.DiskNumber -eq $diskNumber -and $systemPartition.PartitionNumber -eq $partitionNumber) { throw 'Refusing to format the running Windows partition.' }
            if ($partition.IsBoot -or $partition.IsSystem -or $partition.IsReadOnly -or $partition.IsOffline) { throw 'Refusing to format a protected partition.' }
            $partition | Format-Volume -FileSystem NTFS -NewFileSystemLabel 'Windows' -Force -Confirm:$false -ErrorAction Stop | Out-Null
            $partition = Get-Partition -DiskNumber $diskNumber -PartitionNumber $partitionNumber
            $added = $false
            if (-not $partition.DriveLetter) {
              $partition | Add-PartitionAccessPath -AssignDriveLetter -ErrorAction Stop
              $partition = Get-Partition -DiskNumber $diskNumber -PartitionNumber $partitionNumber
              $added = $true
            }
            if (-not $partition.DriveLetter) { throw 'No drive letter is available for the destination partition.' }
            [pscustomobject]@{ Root="$($partition.DriveLetter):\"; AddedDriveLetter=$added } | ConvertTo-Json -Compress
            """;
        var environment = PlanEnvironment(plan, plan.DestinationPartition);
        // Installing onto the disk that currently boots the machine is the supported scenario for this tool,
        // so the boot-disk guard in the script is always allowed here. The protection that matters is per
        // partition: ValidatePlanStructure requires the boot partition to be on this same disk, the script
        // refuses the partition running Windows, and IsProtected/IsBitLocker reject the system, reserved,
        // recovery and encrypted partitions.
        environment["WD_ALLOW_BOOT_DISK"] = "true";
        context.Log.Write("COMMAND", "PowerShell Storage: revalidate exact disk/partition identity; Format-Volume NTFS; assign access path if required");
        var result = await context.Processes.RunPowerShellAsync(script, environment,
            output: (line, isError) => context.Log.Write(isError ? "STORAGE-STDERR" : "STORAGE", line),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        context.Log.Write("RESULT", $"Format operation exit code {result.ExitCode}; elapsed {result.Elapsed}");
        if (!result.Succeeded) throw new ESDInstallerException("ErrorFormatDestination", result.StandardError.Trim());
        var info = DeserializeJson<AccessResult>(result.StandardOutput);
        if (info is null || string.IsNullOrWhiteSpace(info.Root))
            throw new ESDInstallerException("ErrorFormatDestination", "The formatting operation did not return an access path.");
        return info.Root;
    }

    private static async Task<AccessResult> AcquireBootAccessAsync(InstallationPlan plan,
        InstallationExecutionContext context, CancellationToken cancellationToken)
    {
        const string script = """
            $ErrorActionPreference = 'Stop'
            $diskNumber = [int]$env:WD_DISK_NUMBER
            $disk = Get-Disk -Number $diskNumber -ErrorAction Stop
            $wmiDisk = Get-CimInstance Win32_DiskDrive -Filter "Index=$diskNumber" -ErrorAction Stop
            if ($env:WD_DISK_UNIQUE -and ([string]$wmiDisk.PNPDeviceID).Trim() -ne $env:WD_DISK_UNIQUE) { throw 'Boot disk unique ID changed.' }
            if ($env:WD_DISK_SERIAL -and ([string]$wmiDisk.SerialNumber).Trim() -ne $env:WD_DISK_SERIAL) { throw 'Boot disk serial number changed.' }
            $partition = Get-Partition -DiskNumber ([int]$env:WD_DISK_NUMBER) -PartitionNumber ([int]$env:WD_PARTITION_NUMBER) -ErrorAction Stop
            if ([long]$partition.Offset -ne [long]$env:WD_PARTITION_OFFSET -or [long]$partition.Size -ne [long]$env:WD_PARTITION_LENGTH) { throw 'Boot partition geometry changed.' }
            $actualGuid = ([string]$partition.Guid).Trim().Trim('{','}').ToLowerInvariant()
            if ($env:WD_PARTITION_GUID -and $actualGuid -ne $env:WD_PARTITION_GUID) { throw 'Boot partition GUID changed.' }
            $added = $false
            if (-not $partition.DriveLetter) {
              $partition | Add-PartitionAccessPath -AssignDriveLetter
              $partition = Get-Partition -DiskNumber ([int]$env:WD_DISK_NUMBER) -PartitionNumber ([int]$env:WD_PARTITION_NUMBER)
              $added = $true
            }
            if (-not $partition.DriveLetter) { throw 'No drive letter is available for the boot partition.' }
            [pscustomobject]@{ Root="$($partition.DriveLetter):\"; AddedDriveLetter=$added } | ConvertTo-Json -Compress
            """;
        var environment = PlanEnvironment(plan, plan.BootPartition);
        context.Log.Write("COMMAND", "PowerShell Storage: revalidate exact boot partition identity; assign temporary access path if required");
        var result = await context.Processes.RunPowerShellAsync(script, environment,
            output: (line, isError) => context.Log.Write(isError ? "BOOT-STORAGE-STDERR" : "BOOT-STORAGE", line),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded) throw new ESDInstallerException("ErrorBootPartitionAccess", result.StandardError.Trim());
        return DeserializeJson<AccessResult>(result.StandardOutput)
               ?? throw new ESDInstallerException("ErrorBootPartitionAccess", "No boot access path was returned.");
    }

    private static async Task ReleaseBootAccessAsync(InstallationPlan plan, AccessResult access,
        InstallationExecutionContext context)
    {
        if (!access.AddedDriveLetter || string.IsNullOrWhiteSpace(access.Root)) return;
        const string script = """
            $ErrorActionPreference = 'Continue'
            $partition = Get-Partition -DiskNumber ([int]$env:WD_DISK_NUMBER) -PartitionNumber ([int]$env:WD_PARTITION_NUMBER) -ErrorAction SilentlyContinue
            if ($partition -and [long]$partition.Offset -eq [long]$env:WD_PARTITION_OFFSET -and [long]$partition.Size -eq [long]$env:WD_PARTITION_LENGTH) {
              $partition | Remove-PartitionAccessPath -AccessPath $env:WD_ACCESS_PATH -Confirm:$false -ErrorAction SilentlyContinue
            }
            """;
        var environment = PlanEnvironment(plan, plan.BootPartition);
        environment["WD_ACCESS_PATH"] = access.Root;
        var result = await context.Processes.RunPowerShellAsync(script, environment).ConfigureAwait(false);
        context.Log.Write("RESULT", $"Temporary boot access path removal exit code {result.ExitCode}");
    }

    private static void VerifyFiles(InstallationPlan plan, string destinationRoot, string bootRoot)
    {
        var requiredDestinationFiles = new[]
        {
            Path.Combine(destinationRoot, "Windows", "System32", "Config", "SYSTEM"),
            Path.Combine(destinationRoot, "Windows", "System32", "winload.exe")
        };
        foreach (var path in requiredDestinationFiles)
        {
            if (!File.Exists(path)) throw new ESDInstallerException("ErrorDeploymentVerification", path);
        }

        if (plan.FirmwareMode == FirmwareMode.Uefi)
        {
            var manager = Path.Combine(bootRoot, "EFI", "Microsoft", "Boot", "bootmgfw.efi");
            var fallbackName = plan.Edition.Architecture switch
            {
                CpuArchitecture.Arm64 => "bootaa64.efi",
                CpuArchitecture.X86 => "bootia32.efi",
                _ => "bootx64.efi"
            };
            var fallback = Path.Combine(bootRoot, "EFI", "Boot", fallbackName);
            if (!File.Exists(manager)) throw new ESDInstallerException("ErrorBootVerification", manager);
            if (!File.Exists(fallback)) throw new ESDInstallerException("ErrorBootFallbackVerification", fallback);
        }
        else
        {
            var bcd = Path.Combine(bootRoot, "Boot", "BCD");
            if (!File.Exists(bcd)) throw new ESDInstallerException("ErrorBootVerification", bcd);
        }
    }

    /// <summary>
    /// Environment for one validation script. The partition values come from <paramref name="partition"/>;
    /// the disk values come from the destination disk, because ValidatePlanStructure requires the boot
    /// partition to be on that same disk, so both refer to the same physical device.
    /// </summary>
    private static Dictionary<string, string?> PlanEnvironment(InstallationPlan plan, PartitionIdentity partition) => new()
    {
        ["WD_DISK_NUMBER"] = partition.DiskNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["WD_PARTITION_NUMBER"] = partition.PartitionNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["WD_PARTITION_OFFSET"] = partition.OffsetBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["WD_PARTITION_LENGTH"] = partition.LengthBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["WD_PARTITION_GUID"] = partition.PartitionGuid.Trim().Trim('{', '}').ToLowerInvariant(),
        ["WD_DISK_UNIQUE"] = plan.DestinationDisk.UniqueId.Trim(),
        ["WD_DISK_SERIAL"] = plan.DestinationDisk.SerialNumber.Trim(),
        ["WD_DISK_SIZE"] = plan.DestinationDisk.SizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };

    private static string FormatCommand(string executable, IEnumerable<string> arguments) =>
        $"{Path.GetFileName(executable)} {string.Join(' ', arguments.Select(argument => argument.Contains(' ') ? $"\"{argument}\"" : argument))}";

    private static T? DeserializeJson<T>(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end < start) return default;
        return JsonSerializer.Deserialize<T>(output[start..(end + 1)],
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private sealed record AccessResult(string Root, bool AddedDriveLetter);
}
