namespace OpenAgent.Core.Domain;

/// <summary>
/// Output stream kinds the UI must render differently instead of dumping
/// everything into a chat bubble (spec section 155).
/// </summary>
public static class TaskEventKinds
{
    public const string Assistant = "assistant";
    public const string Tool = "tool";
    public const string System = "system";
    public const string Error = "error";
    public const string Approval = "approval";
}

public sealed class TaskEventRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string TaskId { get; set; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; set; }

    public string Kind { get; set; } = TaskEventKinds.System;

    public string Text { get; set; } = string.Empty;

    /// <summary>Raw payload kept out of the database when large (spec section 208).</summary>
    public string? DataJson { get; set; }
}
