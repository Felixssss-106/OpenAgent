using OpenAgent.Security;

namespace OpenAgent.Tools;

/// <summary>What a tool is allowed to know about the call it is serving.</summary>
public sealed class ToolContext
{
    public string? TaskId { get; init; }

    public string? DeviceId { get; init; }

    public string? WorkingDirectory { get; init; }

    public PermissionMode PermissionMode { get; init; } = PermissionMode.AskBeforeActions;
}
