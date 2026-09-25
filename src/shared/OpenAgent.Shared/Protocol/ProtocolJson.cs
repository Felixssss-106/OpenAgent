using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenAgent.Shared.Protocol;

/// <summary>
/// One serializer configuration for the whole product so Windows and any future
/// Kotlin/other client agree on the wire shape.
/// </summary>
public static class ProtocolJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = false,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
