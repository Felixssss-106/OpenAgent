using OpenAgent.Core.Error;
using OpenAgent.Security;
using OpenAgent.Shared.Protocol;

namespace OpenAgent.Tools.Internal;

/// <summary>
/// Processes OpenAgent must never terminate: killing any of them reboots or
/// breaks the machine, no matter which permission mode is active
/// (spec sections 42, 240).
/// </summary>
internal static class ProtectedProcesses
{
    /// <summary>Process ids below this belong to the kernel / system.</summary>
    private const int FirstUserProcessId = 5;

    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "system",
        "system idle process",
        "registry",
        "memory compression",
        "smss",
        "csrss",
        "wininit",
        "winlogon",
        "services",
        "lsass",
        "lsaiso",
        "svchost",
    };

    internal static bool IsProtected(int processId, string? name)
    {
        if (processId < FirstUserProcessId || processId == Environment.ProcessId)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(name) && Names.Contains(name);
    }
}

/// <summary>Coded failures shared by the process and application tools.</summary>
internal static class ProcessToolFailures
{
    internal static ToolResult MissingTarget() =>
        ToolResult.Fail(ErrorCodes.ToolError(2), "需要 processId 或 name");

    internal static ToolResult InvalidArgument(string message) =>
        ToolResult.Fail(ErrorCodes.ToolError(2), message);

    internal static ToolResult Protected(string label) =>
        ToolResult.Fail(ErrorCodes.ToolError(10), $"拒绝操作系统保护进程：{SecretRedactor.Redact(label)}");

    internal static ToolResult NotFound(string label) =>
        ToolResult.Fail(ErrorCodes.ToolError(6), $"进程不存在：{SecretRedactor.Redact(label)}");

    internal static ToolResult Failed(string label, string detail) =>
        ToolResult.Fail(
            ErrorCodes.ToolError(7),
            $"操作失败：{SecretRedactor.Redact(label)}（{SecretRedactor.Redact(detail)}）");
}
