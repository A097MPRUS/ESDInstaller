using System.Security.Cryptography;
using System.Text.Json;
using ESDInstaller.Windows7.Core.Models;

namespace ESDInstaller.Windows7.Core.Services;

/// <summary>Binds the elevated worker to the exact plan bytes the user approved.</summary>
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

    public static string Digest(byte[] bytes) => Hex.Sha256(bytes);

    public static InstallationPlan Read(string path, string expectedDigest)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
        {
            if (stream.Length <= 0 || stream.Length > MaximumBytes) throw Invalid("Invalid plan file size.");
            var bytes = new byte[(int)stream.Length];
            var total = 0;
            while (total < bytes.Length)
            {
                var read = stream.Read(bytes, total, bytes.Length - total);
                if (read == 0) throw Invalid("The plan file was truncated.");
                total += read;
            }
            return Verify(bytes, expectedDigest);
        }
    }

    internal static InstallationPlan Verify(byte[] bytes, string? expectedDigest)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumBytes || expectedDigest == null || !Hex.IsSha256(expectedDigest))
            throw Invalid("The approved plan could not be verified.");
        if (!Hex.FixedTimeEquals(Hex.Sha256(bytes), expectedDigest))
            throw Invalid("The installation instructions changed after approval. Review the installation again.");
        try
        {
            var plan = JsonSerializer.Deserialize<InstallationPlan>(bytes) ?? throw Invalid("The plan is empty.");
            ExecutionPlanValidator.ValidatePlanStructure(plan);
            return plan;
        }
        catch (JsonException) { throw Invalid("The plan could not be read."); }
    }

    private static ESDInstallerException Invalid(string detail) => new ESDInstallerException("ValidationPlanUnreadable", detail);
}

internal static class Hex
{
    public static string Sha256(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return Encode(sha.ComputeHash(bytes));
    }

    public static string Encode(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");

    public static bool IsSha256(string value) =>
        value.Length == 64 && value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));

    /// <summary>Case-insensitive comparison whose time does not depend on where the values differ.</summary>
    public static bool FixedTimeEquals(string left, string right)
    {
        if (left.Length != right.Length) return false;
        var difference = 0;
        for (var i = 0; i < left.Length; i++) difference |= char.ToUpperInvariant(left[i]) ^ char.ToUpperInvariant(right[i]);
        return difference == 0;
    }
}
