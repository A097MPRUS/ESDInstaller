using System;
using System.Collections.Generic;
using System.Linq;

namespace ESDInstaller.Windows7.Core.Services;

public sealed class SemanticVersion : IComparable<SemanticVersion>
{
    private SemanticVersion(int major, int minor, int patch, IReadOnlyList<string> preRelease)
    {
        Major = major; Minor = minor; Patch = patch; PreRelease = preRelease;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public IReadOnlyList<string> PreRelease { get; }

    public static SemanticVersion Parse(string value)
    {
        SemanticVersion version;
        if (!TryParse(value, out version)) throw new FormatException("'" + value + "' is not a valid semantic version.");
        return version;
    }

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value!.Trim();
        if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text.Substring(1);
        var buildIndex = text.IndexOf('+');
        if (buildIndex >= 0)
        {
            // Build metadata is ignored for precedence but must still be well formed.
            if (!text.Substring(buildIndex + 1).Split('.').All(IsIdentifier)) return false;
            text = text.Substring(0, buildIndex);
        }
        var preRelease = new string[0];
        var preReleaseIndex = text.IndexOf('-');
        if (preReleaseIndex >= 0)
        {
            preRelease = text.Substring(preReleaseIndex + 1).Split('.');
            text = text.Substring(0, preReleaseIndex);
            if (!preRelease.All(part => IsIdentifier(part) && (!IsNumeric(part) || part == "0" || part[0] != '0')))
                return false;
        }
        var parts = text.Split('.');
        int major, minor, patch;
        if (parts.Length != 3 || !TryPart(parts[0], out major) || !TryPart(parts[1], out minor) || !TryPart(parts[2], out patch)) return false;
        version = new SemanticVersion(major, minor, patch, preRelease);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other == null) return 1;
        var result = Major.CompareTo(other.Major); if (result != 0) return result;
        result = Minor.CompareTo(other.Minor); if (result != 0) return result;
        result = Patch.CompareTo(other.Patch); if (result != 0) return result;
        if (PreRelease.Count == 0) return other.PreRelease.Count == 0 ? 0 : 1;
        if (other.PreRelease.Count == 0) return -1;
        for (var index = 0; index < Math.Min(PreRelease.Count, other.PreRelease.Count); index++)
        {
            var left = PreRelease[index];
            var right = other.PreRelease[index];
            var leftNumeric = IsNumeric(left);
            var rightNumeric = IsNumeric(right);
            // Numeric identifiers have no leading zeros, so length then digits orders any size exactly.
            if (leftNumeric && rightNumeric)
                result = left.Length != right.Length ? left.Length.CompareTo(right.Length) : string.CompareOrdinal(left, right);
            else if (leftNumeric != rightNumeric) result = leftNumeric ? -1 : 1;
            else result = string.CompareOrdinal(left, right);
            if (result != 0) return Math.Sign(result);
        }
        return PreRelease.Count.CompareTo(other.PreRelease.Count);
    }

    public override string ToString() => Major + "." + Minor + "." + Patch +
        (PreRelease.Count == 0 ? string.Empty : "-" + string.Join(".", PreRelease));

    private static bool IsNumeric(string value) => value.Length > 0 && value.All(c => c >= '0' && c <= '9');

    private static bool IsIdentifier(string value) => value.Length > 0 &&
        value.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '-');

    private static bool TryPart(string value, out int part)
    {
        part = 0;
        return IsNumeric(value) && (value == "0" || value[0] != '0') &&
               int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out part);
    }
}
