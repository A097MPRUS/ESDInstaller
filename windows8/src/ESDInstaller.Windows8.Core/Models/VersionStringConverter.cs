using System;
using Newtonsoft.Json;

namespace ESDInstaller.Windows8.Core.Models;

/// <summary>
/// Reads and writes <see cref="Version"/> as a string such as "10.0.19045".
/// Newtonsoft's default object form records Revision as -1 for a version without a revision, and
/// <c>Version</c> rejects that on the way back in ("Version's parameters must be greater than or equal to
/// zero"), so a plan containing an edition version could not be read by the elevated worker. Writing the
/// string form also matches what the Windows 7 edition produces, keeping the approved plan equivalent
/// across both editions instead of dropping the field.
/// </summary>
internal sealed class VersionStringConverter : JsonConverter<Version>
{
    public override void WriteJson(JsonWriter writer, Version? value, JsonSerializer serializer) =>
        writer.WriteValue(value?.ToString());

    public override Version? ReadJson(JsonReader reader, Type objectType, Version? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.Value is not string text || text.Length == 0) return null;
        try
        {
            return new Version(text);
        }
        catch (Exception exception) when (exception is FormatException || exception is ArgumentException ||
                                         exception is OverflowException)
        {
            throw new JsonSerializationException("The version value '" + text + "' could not be read.", exception);
        }
    }
}
