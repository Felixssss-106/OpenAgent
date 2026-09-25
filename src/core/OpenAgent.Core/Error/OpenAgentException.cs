namespace OpenAgent.Core.Error;

/// <summary>
/// The only exception type the retry policy and the UI error translator reason
/// about. Everything else is treated as unexpected and surfaces as-is.
/// </summary>
public sealed class OpenAgentException : Exception
{
    public OpenAgentException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public OpenAgentException(string code, string message, Exception inner)
        : base(message, inner)
    {
        Code = code;
    }

    public string Code { get; }

    public int? Family => ErrorCodes.FamilyOf(Code);

    public static OpenAgentException PermissionDenied(string message) =>
        new(ErrorCodes.PermissionError(1), message);

    public static OpenAgentException ToolFailed(string message) =>
        new(ErrorCodes.ToolError(1), message);

    public static OpenAgentException InvalidArguments(string message) =>
        new(ErrorCodes.ToolError(2), message);

    public static OpenAgentException Network(string message) =>
        new(ErrorCodes.NetworkError(1), message);
}
