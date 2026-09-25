using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenAgent.Shared.Protocol;

/// <summary>
/// Standardised tool result (spec section 274). <see cref="Data"/> is written
/// only when the call succeeded, <see cref="Error"/> only when it failed.
/// </summary>
public sealed class ToolResult
{
    public bool Success { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Data { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolError? Error { get; set; }

    public string? Reversible { get; set; }

    public static ToolResult Ok(JsonElement? data = null, string? undo = null) => new()
    {
        Success = true,
        Data = data,
        Reversible = undo,
    };

    public static ToolResult Fail(string code, string message) => new()
    {
        Success = false,
        Error = new ToolError(code, message),
    };
}

public sealed class ToolError
{
    public ToolError()
    {
    }

    public ToolError(string code, string message)
    {
        Code = code;
        Message = message;
    }

    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
