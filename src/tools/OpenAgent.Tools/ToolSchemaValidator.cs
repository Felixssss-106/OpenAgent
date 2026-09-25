using System.Text.Json;

namespace OpenAgent.Tools;

/// <summary>
/// Minimal JSON Schema validation for tool arguments. A model's tool call is
/// untrusted input: it is checked against the schema before the permission
/// engine even sees it (spec section 131).
/// </summary>
public static class ToolSchemaValidator
{
    public static ValidationResult Validate(JsonElement arguments, string schemaJson)
    {
        if (arguments.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined))
        {
            return ValidationResult.Fail("arguments must be a JSON object");
        }

        JsonDocument? schema = null;
        try
        {
            schema = JsonDocument.Parse(schemaJson);
        }
        catch (JsonException)
        {
            return ValidationResult.Fail("tool schema is not valid JSON");
        }

        using (schema)
        {
            var root = schema.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ValidationResult.Fail("tool schema root must be an object");
            }

            if (!root.TryGetProperty("properties", out var properties) ||
                properties.ValueKind != JsonValueKind.Object)
            {
                return ValidationResult.Ok;
            }

            foreach (var property in properties.EnumerateObject())
            {
                if (!arguments.TryGetProperty(property.Name, out var value))
                {
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.Object ||
                    !property.Value.TryGetProperty("type", out var typeNode) ||
                    typeNode.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (!MatchesType(value, typeNode.GetString()))
                {
                    return ValidationResult.Fail(
                        $"argument '{property.Name}' must be of type {typeNode.GetString()}");
                }
            }

            if (!root.TryGetProperty("required", out var required) ||
                required.ValueKind != JsonValueKind.Array)
            {
                return ValidationResult.Ok;
            }

            foreach (var item in required.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var name = item.GetString();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (!arguments.TryGetProperty(name, out var value) ||
                    value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    return ValidationResult.Fail($"missing required argument '{name}'");
                }
            }
        }

        return ValidationResult.Ok;
    }

    private static bool MatchesType(JsonElement value, string? type) => type switch
    {
        "string" => value.ValueKind == JsonValueKind.String,
        "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number &&
                     value.TryGetInt64(out _),
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "array" => value.ValueKind == JsonValueKind.Array,
        "object" => value.ValueKind == JsonValueKind.Object,
        _ => true,
    };
}

public readonly record struct ValidationResult(bool IsValid, string? Error)
{
    public static ValidationResult Ok => new(true, null);

    public static ValidationResult Fail(string error) => new(false, error);
}
