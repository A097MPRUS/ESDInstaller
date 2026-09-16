using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json;
using ESDInstaller.Windows8.Core.Models;
using ESDInstaller.Windows8.Core.Services;

internal static class Program
{
    private static int _failures;
    private static int Main()
    {
        Test("monthly review: execution and destination safety", MonthlyReviewTests.RunSafetyChecks);
        Test("monthly review: updater stability and verification", () => MonthlyReviewTests.RunUpdateChecks().GetAwaiter().GetResult());
        Test("approved plan binding", LegacyReliabilityTests.RunApprovedPlanChecks);
        Test("installation image location and lock", LegacyReliabilityTests.RunSourceProtectionChecks);
        Test("ISO image cache identity and integrity", LegacyReliabilityTests.RunImageCacheChecks);
        Test("command-line quoting", TestQuoting);
        Test("semantic version comparison", TestSemanticVersion);
        Test("semantic version edge cases", MonthlyReviewTests.RunVersionChecks);
        Test("command time limits and worker identity", () => MonthlyReviewTests.RunSupervisionChecks().GetAwaiter().GetResult());
        Test("WIM metadata indexes", TestMetadata);
        Test("protected partition policy", TestProtectedPartition);
        Test("active MBR target classification", TestActiveMbrTargetClassification);
        Test("Windows 11 bypass keeps disk safety", TestCompatibility);
        Test("plan fingerprint", TestPlanFingerprint);
        Test("worker plan JSON round-trip", TestPlanJsonRoundTrip);
        Test("native WIM library architecture", TestNativeWimLibraryArchitecture);
        Test("native WIM library initialization", WimLibNative.EnsureInitialized);
        Test("localization key parity", TestLocalization);
        Test("read-only disk enumeration", TestDiskEnumeration);
        Console.WriteLine(_failures == 0 ? "All Windows 8/8.1 compatibility tests passed." : _failures + " test(s) failed.");
        return _failures == 0 ? 0 : 1;
    }

    private static void Test(string name, Action action)
    {
        try { action(); Console.WriteLine("PASS  " + name); }
        catch (Exception exception) { _failures++; Console.WriteLine("FAIL  " + name + ": " + exception.Message); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void TestQuoting()
    {
        Require(ProcessRunner.QuoteArgument("plain") == "plain", "plain argument changed");
        Require(ProcessRunner.QuoteArgument("C:\\image file.wim") == "\"C:\\image file.wim\"", "space quoting failed");
        Require(ProcessRunner.QuoteArgument("") == "\"\"", "empty argument failed");
    }
    private static void TestSemanticVersion()
    {
        Require(SemanticVersion.Parse("1.10.0").CompareTo(SemanticVersion.Parse("1.9.0")) > 0,
            "1.10.0 was not newer than 1.9.0");
        Require(SemanticVersion.Parse("2.0.0").CompareTo(SemanticVersion.Parse("2.0.0-rc.1")) > 0,
            "a stable release was not newer than its prerelease");
        Require(SemanticVersion.Parse("1.0.0-beta.11").CompareTo(SemanticVersion.Parse("1.0.0-beta.2")) > 0,
            "numeric prerelease identifiers were compared as text");
    }
    private static void TestMetadata()
    {
        var xml = "<WIM><IMAGE INDEX='1'><NAME>Home</NAME><TOTALBYTES>10</TOTALBYTES><WINDOWS><ARCH>9</ARCH><VERSION><MAJOR>10</MAJOR><MINOR>0</MINOR><BUILD>19045</BUILD><SPBUILD>0</SPBUILD></VERSION></WINDOWS></IMAGE><IMAGE INDEX='2'><DISPLAYNAME>Pro</DISPLAYNAME><WINDOWS><ARCH>9</ARCH><VERSION><MAJOR>10</MAJOR><MINOR>0</MINOR><BUILD>22631</BUILD><SPBUILD>0</SPBUILD></VERSION></WINDOWS></IMAGE></WIM>";
        var editions = WimService.ParseEditions(xml);
        Require(editions.Count == 2 && editions[1].Index == 2 && editions[1].Build == 22631, "indexes were not parsed independently");
    }
    private static void TestProtectedPartition()
    {
        var current = Partition(0, 3, PartitionRole.BasicData, true, false);
        var recovery = Partition(0, 4, PartitionRole.Recovery, false, false);
        var normal = Partition(1, 2, PartitionRole.BasicData, false, false);
        Require(current.IsProtected && recovery.IsProtected && !normal.IsProtected, "partition protection classification failed");
    }
    private static void TestActiveMbrTargetClassification()
    {
        Require(!DiskService.ResolveSystemFlag(true, false, true, false, true, PartitionRole.BasicData),
            "an active ordinary data volume was incorrectly treated as the system partition");
        Require(DiskService.ResolveSystemFlag(true, true, true, false, false, PartitionRole.BasicData),
            "the Storage Management system flag was not protected");
        Require(DiskService.ResolveSystemFlag(false, false, true, false, false, PartitionRole.BasicData),
            "the legacy no-letter active system partition fallback was not protected");
        Require(!DiskService.ResolveSystemFlag(false, false, true, false, true, PartitionRole.BasicData),
            "the legacy fallback rejected an active drive-letter target");
        Require(DiskService.RoleFromMetadata(null, 0x07, "Installable File System", "NTFS", false) == PartitionRole.BasicData,
            "an ordinary MBR NTFS partition was not classified as basic data");
        Require(DiskService.RoleFromMetadata(null, 0, "GPT: System", "", true) == PartitionRole.EfiSystem,
            "the legacy EFI fallback was not protected");
    }
    private static void TestCompatibility()
    {
        var service = new CompatibilityService();
        var target = Partition(1, 2, PartitionRole.BasicData, false, false);
        var boot = Partition(1, 1, PartitionRole.BasicData, false, true);
        var disk = new DiskInfo(1, "Test", "Test", "SERIAL", "ID", @"\\.\PHYSICALDRIVE1", "SATA",
            128L << 30, PartitionScheme.Mbr, false, false, false, false, new[] { boot, target });
        var image = new WindowsImage("x", "x", WindowsImageKind.Wim, WindowsGeneration.Windows11, "Windows 11",
            CpuArchitecture.X64, 1, DateTime.UtcNow, null, Array.Empty<WindowsImageEdition>());
        var edition = new WindowsImageEdition(1, "Pro", "Pro", CpuArchitecture.X64, 22631, new Version(10,0,22631), 0);
        var host = new CompatibilitySnapshot(FirmwareMode.Bios, CpuArchitecture.X64, false, false, false, false, 2L << 30);
        Require(!service.CheckImageCompatibility(image, edition, disk, target, boot, host, false).IsValid,
            "requirements were silently bypassed");
        Require(service.CheckImageCompatibility(image, edition, disk, target, boot, host, true).IsValid,
            "explicit bypass did not demote hardware-only checks");
        var protectedTarget = Partition(1, 3, PartitionRole.Recovery, false, false);
        Require(!service.CheckImageCompatibility(image, edition, disk, protectedTarget, boot, host, true).IsValid,
            "bypass disabled disk safety");
    }
    private static void TestPlanFingerprint()
    {
        var target = Partition(1, 2, PartitionRole.BasicData, false, false); var boot = Partition(1, 1, PartitionRole.BasicData, false, true);
        var image = new WindowsImage("C:\\image.wim", "C:\\image.wim", WindowsImageKind.Wim, WindowsGeneration.Windows7,
            "Windows 7", CpuArchitecture.X64, 5, new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), null,
            new[] { new WindowsImageEdition(1, "Professional", "Professional", CpuArchitecture.X64, 7601, new Version(6,1,7601), 1) });
        var disk = new DiskInfo(1, "Disk", "Disk", "S", "U", "P", "SATA", 100L << 30, PartitionScheme.Mbr,
            false, false, false, false, new[] { boot, target });
        var session = new SessionState { Image = image, Edition = image.Editions[0], DestinationDisk = disk,
            DestinationPartition = target, BootPartition = boot, Compatibility = new CompatibilitySnapshot(FirmwareMode.Bios, CpuArchitecture.X64, false, false, false, false, 8L << 30) };
        var factory = new InstallationPlanFactory();
        Require(factory.Create(session).ConfirmationFingerprint == factory.Create(session).ConfirmationFingerprint,
            "same immutable identifiers produced different fingerprints");
    }
    private static void TestPlanJsonRoundTrip()
    {
        var plan = new InstallationPlan(Guid.NewGuid(), DateTime.UtcNow,
            new SourceIdentity(@"C:\media.iso", @"C:\cache\install.wim", WindowsImageKind.Iso, 1234, DateTime.UtcNow),
            new WindowsImageEdition(6, "Windows 11 Pro", "Professional", CpuArchitecture.X64, 26100, new Version(10, 0, 26100), 42),
            WindowsGeneration.Windows11, InstallationEngineKind.ModernWindows,
            new DiskIdentity(2, "DISK-ID", "SERIAL", "Test SSD", 256L << 30, PartitionScheme.Gpt),
            new PartitionIdentity(2, 3, 200L << 20, 100L << 30, "PART-ID", 'W', "Windows", "NTFS", PartitionRole.BasicData),
            new PartitionIdentity(2, 1, 1L << 20, 100L << 20, "EFI-ID", null, "", "FAT32", PartitionRole.EfiSystem),
            FirmwareMode.Uefi, PartitionScheme.Gpt, true, false, true, false,
            new[] { new PlannedOperation("validate", "OperationValidatePlan", false) }, "ABCDEF123456");
        var json = JsonConvert.SerializeObject(plan, Formatting.Indented);
        var restored = JsonConvert.DeserializeObject<InstallationPlan>(json);
        Require(restored != null && restored.PlanId == plan.PlanId && restored.DestinationDisk.UniqueId == "DISK-ID" &&
                restored.DestinationPartition.OffsetBytes == plan.DestinationPartition.OffsetBytes &&
                restored.Operations.Count == 1, "immutable installation plan did not survive worker serialization");
    }
    private static void TestNativeWimLibraryArchitecture()
    {
        var root = AppDomain.CurrentDomain.BaseDirectory;
        var x86 = Path.Combine(root, "x86", "libwim-15.dll");
        var x64 = Path.Combine(root, "x64", "libwim-15.dll");
        Require(File.Exists(x86), "x86 libwim-15.dll was not copied");
        Require(File.Exists(x64), "x64 libwim-15.dll was not copied");
        Require(WimLibNative.ReadPeMachine(x86) == 0x014c, "x86 folder does not contain an x86 DLL");
        Require(WimLibNative.ReadPeMachine(x64) == 0x8664, "x64 folder does not contain an AMD64 DLL");
    }
    private static void TestLocalization()
    {
        var root = FindRepositoryRoot(); var strings = Path.Combine(root, "windows8", "src", "ESDInstaller.Windows8.App", "Strings");
        var english = Keys(Path.Combine(strings, "en-US", "Resources.resw"));
        var englishValues = Values(Path.Combine(strings, "en-US", "Resources.resw"));
        foreach (var file in Directory.GetFiles(strings, "Resources.resw", SearchOption.AllDirectories))
        {
            var missing = english.Except(Keys(file)).ToArray();
            Require(missing.Length == 0, Path.GetDirectoryName(file) + " is missing " + missing.Length + " keys");
            var translated = Values(file);
            Require(translated.Count == englishValues.Count && englishValues.All(item =>
                    translated.ContainsKey(item.Key) && !string.IsNullOrWhiteSpace(translated[item.Key]) &&
                    translated[item.Key].IndexOf("[[[KEY", StringComparison.Ordinal) < 0 &&
                    translated[item.Key].IndexOf("ZXTERM", StringComparison.Ordinal) < 0 &&
                    translated[item.Key].IndexOf("ZXPH", StringComparison.Ordinal) < 0 &&
                    Placeholders(item.Value) == Placeholders(translated[item.Key])),
                Path.GetDirectoryName(file) + " has incomplete values or placeholders");
        }
        Require(Directory.GetFiles(strings, "Resources.resw", SearchOption.AllDirectories).Length == 42, "all 42 languages are not present");
        var selector = File.ReadAllText(Path.Combine(root, "windows8", "src", "ESDInstaller.Windows8.App", "SettingsWindow.xaml.cs"));
        var settings = File.ReadAllText(Path.Combine(root, "windows8", "src", "ESDInstaller.Windows8.App", "Services", "SettingsService.cs"));
        Require(new[] { "nb", "fi", "sv", "mn", "hy", "kk", "ba", "tt", "crh", "ab", "os", "ar", "he", "fa", "af", "hu", "pt", "cs", "ug-Cyrl", "tr", "th", "ko", "ja", "ka", "az", "zh-Hant", "nn", "ky", "it", "ro", "is" }.All(code => selector.IndexOf("\"" + code + "\"", StringComparison.Ordinal) >= 0),
            "all new languages are registered in the selector");
        Require(new[] { "nb-NO", "fi-FI", "sv-SE", "mn-MN", "hy-AM", "kk-KZ", "ba-RU", "tt-RU", "crh-Latn", "ab-GE", "os-GE", "ar-SA", "he-IL", "fa-IR", "af-ZA", "hu-HU", "pt-PT", "cs-CZ", "ug-Cyrl-CN", "tr-TR", "th-TH", "ko-KR", "ja-JP", "ka-GE", "az-Latn-AZ", "zh-TW", "nn-NO", "ky-KG", "it-IT", "ro-RO", "is-IS" }.All(culture => settings.IndexOf("\"" + culture + "\"", StringComparison.Ordinal) >= 0),
            "all new cultures are registered for system-language detection");
        var destinationPage = File.ReadAllText(Path.Combine(root, "windows8", "src", "ESDInstaller.Windows8.App", "Views", "DestinationPage.xaml"));
        Require(destinationPage.IndexOf("x:Name=\"SelectionText\" Grid.Column=\"1\"", StringComparison.Ordinal) >= 0 &&
                destinationPage.IndexOf("x:Name=\"Next\" Grid.Column=\"2\"", StringComparison.Ordinal) >= 0,
            "destination footer controls are not isolated in separate grid columns");
        var appRoot = Directory.GetParent(strings)!.FullName;
        var missingXamlKeys = Directory.GetFiles(appRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(file => file.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0 &&
                           file.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"\{loc:Loc\s+([^},\s]+)").Cast<Match>().Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase).Where(key => !english.Contains(key)).ToArray();
        Require(missingXamlKeys.Length == 0, "XAML references missing localization keys: " + string.Join(", ", missingXamlKeys));
    }
    private static void TestDiskEnumeration()
    {
        var disks = new DiskService().GetDisksAsync().GetAwaiter().GetResult();
        Require(disks.Count > 0, "no physical disks returned");
        Require(disks.All(x => x.SizeBytes > 0), "invalid disk size returned");
    }
    private static HashSet<string> Keys(string path) => new HashSet<string>(XDocument.Load(path).Descendants("data").Select(x => x.Attribute("name")?.Value).Where(x => x != null)!);
    private static Dictionary<string, string> Values(string path) => XDocument.Load(path).Descendants("data")
        .Where(x => x.Attribute("name") != null).ToDictionary(x => x.Attribute("name")!.Value, x => x.Element("value")?.Value ?? "");
    private static string Placeholders(string value) => string.Join("|", Regex.Matches(value, @"\{\d+\}").Cast<Match>().Select(x => x.Value).OrderBy(x => x));
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "windows8"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
    private static PartitionInfo Partition(int disk, int number, PartitionRole role, bool current, bool active) =>
        new PartitionInfo(disk, number, number * 1048576L, 80L << 30, number == 2 ? 'D' : (char?)null,
            "", role == PartitionRole.EfiSystem ? "FAT32" : "NTFS", "", role.ToString(), "", 0, role,
            active, current, false, current, false, false, false, false, Array.Empty<string>());
}
