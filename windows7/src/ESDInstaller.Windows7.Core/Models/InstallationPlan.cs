using System.Text.Json.Serialization;

namespace ESDInstaller.Windows7.Core.Models;

public sealed record DiskIdentity(int DiskNumber, string UniqueId, string SerialNumber, string Model,
    long SizeBytes, PartitionScheme PartitionScheme);

public sealed record PartitionIdentity(int DiskNumber, int PartitionNumber, long OffsetBytes, long LengthBytes,
    string PartitionGuid, char? DriveLetter, string VolumeLabel, string FileSystem, PartitionRole Role);

public sealed record SourceIdentity(string SourcePath, string ImagePath, WindowsImageKind Kind,
    long SourceSizeBytes, DateTime SourceLastWriteUtc, string? ImageSha256 = null);

public sealed record PlannedOperation(string Id, string DescriptionKey, bool Destructive);

public sealed record InstallationPlan(
    Guid PlanId,
    DateTime CreatedUtc,
    SourceIdentity Source,
    WindowsImageEdition Edition,
    WindowsGeneration Generation,
    InstallationEngineKind Engine,
    DiskIdentity DestinationDisk,
    PartitionIdentity DestinationPartition,
    PartitionIdentity BootPartition,
    FirmwareMode FirmwareMode,
    PartitionScheme PartitionScheme,
    bool FormatDestination,
    bool BypassWindows11Requirements,
    bool PreserveCurrentBootDefault,
    bool RequestOneTimeBoot,
    IReadOnlyList<PlannedOperation> Operations,
    string ConfirmationFingerprint)
{
    [JsonIgnore]
    public string DestinationRoot => DestinationPartition.DriveLetter.HasValue
        ? $"{DestinationPartition.DriveLetter.Value}:\\" : string.Empty;
}

public sealed record PlanIssue(PlanSeverity Severity, string Code, string MessageKey, string Detail);

public sealed record PlanValidationResult(IReadOnlyList<PlanIssue> Issues)
{
    public bool IsValid => Issues.All(issue => issue.Severity != PlanSeverity.Error);
    public IEnumerable<PlanIssue> Errors => Issues.Where(issue => issue.Severity == PlanSeverity.Error);
    public IEnumerable<PlanIssue> Warnings => Issues.Where(issue => issue.Severity == PlanSeverity.Warning);
}

public sealed record CompatibilitySnapshot(FirmwareMode FirmwareMode, CpuArchitecture HostArchitecture,
    bool TpmPresent, bool TpmReady, bool SecureBootCapable, bool SecureBootEnabled, long PhysicalMemoryBytes);

public sealed record ProgressMessage(InstallationStage Stage, int OverallPercent, int? OperationPercent,
    string MessageKey, string Detail, DateTime TimestampUtc, bool IsError = false, string? LogPath = null);
