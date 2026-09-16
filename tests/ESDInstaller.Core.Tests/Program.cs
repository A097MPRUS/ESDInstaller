using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ESDInstaller.Core.Models;
using ESDInstaller.Core.Services;

var failures = new List<string>();

void Check(bool condition, string name)
{
    if (condition) Console.WriteLine($"PASS  {name}");
    else { Console.WriteLine($"FAIL  {name}"); failures.Add(name); }
}

Dictionary<string, string> LoadResources(string file) => XDocument.Load(file).Descendants("data")
    .Where(element => element.Attribute("name") is not null)
    .ToDictionary(element => element.Attribute("name")!.Value,
        element => element.Element("value")?.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

string PlaceholderSignature(string value) => string.Join("|", Regex.Matches(value, @"\{\d+\}")
    .Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal));

try
{
    await ModernReliabilityTests.RunAsync();
    MonthlyReviewTests.RunSafetyChecks();
    Check(true, "Monthly review: plan identity, firmware and destination safety");
    await MonthlyReviewTests.RunUpdateChecks();
    Check(true, "Monthly review: update scheduling, offline errors, isolated downloads and verification");
    MonthlyReviewTests.RunVersionChecks();
    Check(true, "Semantic version edge cases");
    await MonthlyReviewTests.RunSupervisionChecks();
    Check(true, "Command time limits and worker pipe identity");
    Check(SemanticVersion.Parse("1.10.0").CompareTo(SemanticVersion.Parse("1.9.0")) > 0,
        "Semantic version numeric comparison");
    Check(SemanticVersion.Parse("2.0.0").CompareTo(SemanticVersion.Parse("2.0.0-rc.1")) > 0,
        "Semantic version prerelease comparison");
    Check(SemanticVersion.Parse("1.0.0-beta.11").CompareTo(SemanticVersion.Parse("1.0.0-beta.2")) > 0,
        "Semantic version prerelease identifier comparison");

    const string metadata = "\uFEFF" + """
        <WIM><IMAGE INDEX="3"><NAME>Windows 11 Pro</NAME><DESCRIPTION>Windows 11 Pro</DESCRIPTION><TOTALBYTES>15123456789</TOTALBYTES><WINDOWS><ARCH>9</ARCH><VERSION><MAJOR>10</MAJOR><MINOR>0</MINOR><BUILD>26100</BUILD><SPBUILD>1</SPBUILD></VERSION></WINDOWS></IMAGE></WIM>
        """;
    var edition = WimService.ParseEdition(metadata, 1);
    Check(edition.Index == 3 && edition.Name == "Windows 11 Pro", "WIM metadata index and name");
    Check(edition.Architecture == CpuArchitecture.X64 && edition.Build == 26100, "WIM architecture and build");
    Check(ImageService.GenerationFromBuild(26100) == WindowsGeneration.Windows11, "Windows 11 build detection");
    Check(ImageService.GenerationFromBuild(7601) == WindowsGeneration.Windows7, "Windows 7 build detection");

    var scratchPath = WimService.EnsureScratchDirectory();
    Check(Directory.Exists(scratchPath), "Writable WIMGAPI scratch directory");

    var resourceRoot = Path.Combine(Directory.GetCurrentDirectory(), "src", "ESDInstaller.App", "Strings");
    var resourceFiles = Directory.GetFiles(resourceRoot, "Resources.resw", SearchOption.AllDirectories);
    var englishResources = LoadResources(Path.Combine(resourceRoot, "en-US", "Resources.resw"));
    Check(resourceFiles.Length == 42, "All 42 application languages are present");
    Check(resourceFiles.All(file =>
    {
        var translated = LoadResources(file);
        return translated.Count == englishResources.Count && englishResources.All(entry =>
            translated.TryGetValue(entry.Key, out var value) && !string.IsNullOrWhiteSpace(value) &&
            !value.Contains("[[[KEY", StringComparison.Ordinal) && !value.Contains("ZXTERM", StringComparison.Ordinal) &&
            !value.Contains("ZXPH", StringComparison.Ordinal) &&
            PlaceholderSignature(entry.Value) == PlaceholderSignature(value));
    }), "Every application language has complete keys and placeholders");
    var selectorSource = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ESDInstaller.App", "MainWindow.xaml.cs"));
    var settingsSource = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ESDInstaller.App", "Services", "SettingsService.cs"));
    var newLanguageCodes = new[] { "nb", "fi", "sv", "mn", "hy", "kk", "ba", "tt", "crh", "ab", "os",
        "ar", "he", "fa", "af", "hu", "pt", "cs", "ug-Cyrl", "tr", "th", "ko", "ja", "ka", "az",
        "zh-Hant", "nn", "ky", "it", "ro", "is" };
    Check(newLanguageCodes.All(code => selectorSource.Contains($"Tag = \"{code}\"", StringComparison.Ordinal)),
        "All 42 languages are registered in the application selector");
    Check(new[] { "nb-NO", "fi-FI", "sv-SE", "mn-MN", "hy-AM", "kk-KZ", "ba-RU", "tt-RU", "crh-Latn", "ab-GE", "os-GE",
        "ar-SA", "he-IL", "fa-IR", "af-ZA", "hu-HU", "pt-PT", "cs-CZ", "ug-Cyrl-CN", "tr-TR", "th-TH",
        "ko-KR", "ja-JP", "ka-GE", "az-Latn-AZ", "zh-TW", "nn-NO", "ky-KG", "it-IT", "ro-RO", "is-IS" }
        .All(culture => settingsSource.Contains($"\"{culture}\"", StringComparison.Ordinal)),
        "System-language mapping includes all new cultures");

    var destination = new PartitionInfo(1, 3, 1024 * 1024, 100L * 1024 * 1024 * 1024, 'W', "Target", "NTFS",
        "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "Basic", "ebd0a0a2-b9e5-4433-87c0-68b6b72699c7", 0,
        PartitionRole.BasicData, false, false, false, false, false, false, false, false, ["W:\\"]);
    var efi = new PartitionInfo(1, 1, 1024 * 1024, 260L * 1024 * 1024, null, "", "FAT32",
        "11111111-2222-3333-4444-555555555555", "System", "c12a7328-f81f-11d2-ba4b-00a0c93ec93b", 0,
        PartitionRole.EfiSystem, false, false, false, false, false, false, false, false, []);
    var disk = new DiskInfo(1, "Test SSD", "Test SSD", "SERIAL-1", "UNIQUE-1", "path", "NVMe",
        256L * 1024 * 1024 * 1024, PartitionScheme.Gpt, false, false, false, false, [efi, destination]);
    var imageEdition = new WindowsImageEdition(1, "Windows 11 Pro", "Windows 11 Pro", CpuArchitecture.X64, 26100,
        new Version(10, 0, 26100, 1), 15L * 1024 * 1024 * 1024);
    var image = new WindowsImage("C:\\test.iso", "D:\\sources\\install.wim", WindowsImageKind.Iso,
        WindowsGeneration.Windows11, "Windows 11", CpuArchitecture.X64, 5L * 1024 * 1024 * 1024,
        DateTime.UnixEpoch, "D:\\", [imageEdition]);
    var host = new CompatibilitySnapshot(FirmwareMode.Uefi, CpuArchitecture.X64, true, true, true, true,
        16L * 1024 * 1024 * 1024);
    var compatibility = new CompatibilityService(new ProcessRunner());
    var validation = compatibility.CheckImageCompatibility(image, imageEdition, disk, destination, efi, host);
    Check(validation.IsValid, "Valid Windows 11 UEFI/GPT plan");

    var unsupportedHost = host with
    {
        TpmPresent = false,
        TpmReady = false,
        SecureBootCapable = false,
        SecureBootEnabled = false,
        PhysicalMemoryBytes = 2L * 1024 * 1024 * 1024
    };
    var unsupportedBlocked = compatibility.CheckImageCompatibility(image, imageEdition, disk, destination, efi,
        unsupportedHost);
    Check(!unsupportedBlocked.IsValid && unsupportedBlocked.Errors.Any(issue => issue.Code == "win11-tpm"),
        "Unsupported Windows 11 hardware is blocked by default");
    var unsupportedBypassed = compatibility.CheckImageCompatibility(image, imageEdition, disk, destination, efi,
        unsupportedHost, bypassWindows11Requirements: true);
    Check(unsupportedBypassed.IsValid && unsupportedBypassed.Warnings.Any(issue => issue.Code == "win11-bypass") &&
          unsupportedBypassed.Warnings.Any(issue => issue.Code == "win11-memory"),
        "Advanced Windows 11 hardware bypass converts policy gates to warnings");

    var protectedDestination = destination with { IsCurrentWindows = true };
    var blocked = compatibility.CheckImageCompatibility(image, imageEdition, disk, protectedDestination, efi, host);
    Check(!blocked.IsValid && blocked.Errors.Any(issue => issue.Code == "protected-target"), "Current Windows partition is blocked");
    var protectedWithBypass = compatibility.CheckImageCompatibility(image, imageEdition, disk,
        protectedDestination, efi, unsupportedHost, bypassWindows11Requirements: true);
    Check(!protectedWithBypass.IsValid && protectedWithBypass.Errors.Any(issue => issue.Code == "protected-target"),
        "Windows 11 hardware bypass cannot bypass partition protection");
    var extendedContainer = destination with
    {
        PartitionNumber = 0,
        DriveLetter = null,
        FileSystem = string.Empty,
        Role = PartitionRole.Unknown
    };
    var unsupportedTarget = compatibility.CheckImageCompatibility(image, imageEdition, disk,
        extendedContainer, efi, host);
    Check(extendedContainer.IsProtected && !unsupportedTarget.IsValid &&
          unsupportedTarget.Errors.Any(issue => issue.Code == "unsupported-target"),
        "Unallocated and MBR extended-container targets are blocked before execution");

    var state = new SessionState
    {
        Image = image,
        Edition = imageEdition,
        DestinationDisk = disk,
        DestinationPartition = destination,
        BootPartition = efi,
        Compatibility = host,
        AdvancedMode = true,
        BypassWindows11Requirements = true
    };
    var plan = new InstallationPlanFactory().Create(state);
    var roundTrip = JsonSerializer.Deserialize<InstallationPlan>(JsonSerializer.Serialize(plan));
    Check(roundTrip?.DestinationPartition.OffsetBytes == destination.OffsetBytes && roundTrip.ConfirmationFingerprint.Length == 12,
        "Immutable plan JSON round-trip and fingerprint");
    Check(roundTrip?.BypassWindows11Requirements == true &&
          roundTrip.Operations.Any(operation => operation.Id == "win11-bypass"),
        "Windows 11 bypass is explicit in the immutable plan");

    var biosBoot = efi with
    {
        LengthBytes = 50L * 1024 * 1024,
        FileSystem = "NTFS",
        GptType = string.Empty,
        MbrType = 0x07,
        Role = PartitionRole.BasicData,
        IsActive = true
    };
    var biosDisk = disk with { PartitionScheme = PartitionScheme.Mbr, Partitions = [biosBoot, destination] };
    var windows10Edition = imageEdition with { Name = "Windows 10 Pro", Build = 19045 };
    var windows10Image = image with
    {
        Generation = WindowsGeneration.Windows10,
        DisplayVersion = "Windows 10",
        Editions = [windows10Edition]
    };
    var biosHost = host with { FirmwareMode = FirmwareMode.Bios, SecureBootCapable = false, SecureBootEnabled = false };
    var biosValidation = compatibility.CheckImageCompatibility(windows10Image, windows10Edition, biosDisk,
        destination, biosBoot, biosHost);
    var biosPlan = new InstallationPlanFactory().Create(new SessionState
    {
        Image = windows10Image,
        Edition = windows10Edition,
        DestinationDisk = biosDisk,
        DestinationPartition = destination,
        BootPartition = biosBoot,
        Compatibility = biosHost
    });
    Check(biosValidation.IsValid && biosPlan.FirmwareMode == FirmwareMode.Bios &&
          biosPlan.BootPartition.LengthBytes == 50L * 1024 * 1024,
        "Windows 10 BIOS/MBR review plan can be created");

    var bootPageXaml = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ESDInstaller.App", "Views", "BootPage.xaml"));
    var coordinatorSource = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ESDInstaller.App", "Services", "WizardCoordinator.cs"));
    Check(bootPageXaml.Contains("x:Name=\"NextButton\"", StringComparison.Ordinal) &&
          bootPageXaml.Contains("Grid.Column=\"2\"", StringComparison.Ordinal),
        "Review button has an isolated footer column");
    Check(coordinatorSource.Contains("Review navigation failed", StringComparison.Ordinal),
        "Review navigation failures are surfaced and logged");
    var appProject = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ESDInstaller.App", "ESDInstaller.App.csproj"));
    var installerBuilder = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "installer", "build-installer.ps1"));
    Check(appProject.Contains("IncludeSelfContainedWorkerInPublish", StringComparison.Ordinal) &&
          appProject.Contains("Worker\\ESDInstaller.Worker.exe", StringComparison.Ordinal),
        "Self-contained elevated worker is included in publish output");
    Check(installerBuilder.Contains("worker failed its startup smoke test", StringComparison.OrdinalIgnoreCase),
        "Installer build verifies the packaged elevated worker can start");

    if (OperatingSystem.IsWindows())
    {
        var runner = new ProcessRunner();
        var disks = await new DiskService(runner).GetDisksAsync();
        Check(disks.Count > 0, "Read-only physical disk enumeration");
        Check(disks.All(item => item.SizeBytes > 0 && item.Partitions.Count > 0), "Disk geometry is populated");
        var storageSizesResult = await runner.RunPowerShellAsync(
            "Get-Disk | ForEach-Object { Write-Output (\"{0}:{1}\" -f $_.Number, $_.Size) }");
        var storageSizes = storageSizesResult.StandardOutput.Split(Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2 && int.TryParse(parts[0], out _) && long.TryParse(parts[1], out _))
            .ToDictionary(parts => int.Parse(parts[0]), parts => long.Parse(parts[1]));
        Check(storageSizesResult.Succeeded && disks.All(item => storageSizes.TryGetValue(item.Number, out var size) && size == item.SizeBytes),
            "Planned disk size uses the same Storage API as execution validation");
        var current = disks.SelectMany(item => item.Partitions).FirstOrDefault(item => item.IsCurrentWindows);
        Check(current is not null && current.IsProtected, "Running Windows partition is detected and protected");
        var actualHost = await new CompatibilityService(runner).InspectHostAsync();
        Check(actualHost.FirmwareMode != FirmwareMode.Unknown && actualHost.PhysicalMemoryBytes > 0,
            "Firmware and physical memory inspection");
        var readableError = await runner.RunPowerShellAsync("throw 'Readable PowerShell failure'");
        Check(!readableError.Succeeded && readableError.StandardError.Contains("Readable PowerShell failure") &&
              !readableError.StandardError.Contains("CLIXML", StringComparison.OrdinalIgnoreCase),
            "PowerShell failures are readable and do not expose CLIXML");

        var integrationImages = Environment.GetEnvironmentVariable("ESDINSTALLER_TEST_IMAGES")?
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        foreach (var integrationImage in integrationImages)
        {
            await using var imageService = new ImageService(runner, new WimService());
            var inspected = await imageService.InspectAsync(integrationImage);
            Check(inspected.Editions.Count > 0,
                $"Inspect {Path.GetFileName(integrationImage)} ({inspected.Editions.Count} edition(s))");
        }
    }
}
catch (Exception exception)
{
    Console.WriteLine(exception);
    failures.Add("Unhandled test exception");
}

if (failures.Count == 0)
{
    Console.WriteLine("ALL TESTS PASSED");
    return 0;
}

Console.WriteLine($"{failures.Count} TEST(S) FAILED: {string.Join(", ", failures)}");
return 1;
