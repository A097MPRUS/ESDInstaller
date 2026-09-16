using System.Security.Cryptography;
using System.Text.Json;
using ESDInstaller.Core.Models;

namespace ESDInstaller.Core.Services;

public static class ApprovedPlan
{
    public const int MaximumBytes = 1024 * 1024;

    public static byte[] Serialize(InstallationPlan plan)
    {
        ExecutionPlanValidator.ValidatePlanStructure(plan);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(plan);
        if (bytes.Length > MaximumBytes) throw Invalid("The approved plan is too large.");
        return bytes;
    }

    public static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public static async Task<InstallationPlan> ReadAsync(string path, string expectedDigest,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length <= 0 || stream.Length > MaximumBytes) throw Invalid("Invalid plan file size.");
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return Verify(bytes, expectedDigest);
    }

    internal static InstallationPlan Verify(byte[] bytes, string expectedDigest)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumBytes || expectedDigest?.Length != 64)
            throw Invalid("The approved plan could not be verified.");
        byte[] expected;
        try { expected = Convert.FromHexString(expectedDigest); }
        catch (FormatException) { throw Invalid("Invalid approved-plan digest."); }
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), expected))
            throw Invalid("The installation instructions changed after approval. Review the installation again.");
        try
        {
            var plan = JsonSerializer.Deserialize<InstallationPlan>(bytes)
                ?? throw Invalid("The plan is empty.");
            ExecutionPlanValidator.ValidatePlanStructure(plan);
            return plan;
        }
        catch (JsonException) { throw Invalid("The plan could not be read."); }
    }

    private static ESDInstallerException Invalid(string detail) => new("ValidationPlanUnreadable", detail);
}
