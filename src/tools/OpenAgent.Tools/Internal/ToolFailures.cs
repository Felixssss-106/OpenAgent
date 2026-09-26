using OpenAgent.Core.Error;
using OpenAgent.Security;
using OpenAgent.Shared.Protocol;

namespace OpenAgent.Tools.Internal;

/// <summary>
/// Coded failures for the file tools (spec section 235). A tool never lets an
/// <see cref="IOException"/> escape to the Agent: every failure is converted
/// into a stable error code plus a redacted message.
/// </summary>
internal static class FileToolFailures
{
    internal static ToolResult MissingArgument(string name) =>
        ToolResult.Fail(ErrorCodes.ToolError(2), $"缺少参数 {name}");

    internal static ToolResult InvalidArgument(string message) =>
        ToolResult.Fail(ErrorCodes.ToolError(2), message);

    internal static ToolResult PathRejected(PathRejection reason) =>
        ToolResult.Fail(
            ErrorCodes.FileError(1),
            $"路径不被允许：{PathValidationResult.Reject(reason).Describe()}");

    internal static ToolResult NotFound(string path) =>
        ToolResult.Fail(ErrorCodes.FileError(2), $"路径不存在：{SecretRedactor.Redact(path)}");

    internal static ToolResult AlreadyExists(string path) =>
        ToolResult.Fail(ErrorCodes.FileError(3), $"目标已存在：{SecretRedactor.Redact(path)}");

    internal static ToolResult TooLarge(string message) =>
        ToolResult.Fail(ErrorCodes.FileError(5), message);

    internal static ToolResult WrongKind(string message) =>
        ToolResult.Fail(ErrorCodes.FileError(6), message);

    internal static ToolResult DirectoryNotEmpty(string path) =>
        ToolResult.Fail(ErrorCodes.FileError(7), $"目录非空：{SecretRedactor.Redact(path)}");

    internal static ToolResult Unsupported(string message) =>
        ToolResult.Fail(ErrorCodes.FileError(8), message);

    internal static ToolResult IoFailure(Exception ex) =>
        ToolResult.Fail(ErrorCodes.FileError(4), $"文件操作失败：{SecretRedactor.Redact(ex.Message)}");

    /// <summary>The exception set a file tool is expected to survive.</summary>
    internal static bool IsIoFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException;
}
