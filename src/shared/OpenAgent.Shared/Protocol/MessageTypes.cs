namespace OpenAgent.Shared.Protocol;

/// <summary>
/// Envelope <c>type</c> values (spec section 66).
/// </summary>
public static class MessageTypes
{
    public const string Hello = "hello";
    public const string HelloAck = "hello.ack";

    public const string PairRequest = "pair.request";
    public const string PairConfirm = "pair.confirm";
    public const string PairReject = "pair.reject";

    public const string DeviceStatus = "device.status";
    public const string DeviceUpdate = "device.update";

    public const string CommandRequest = "command.request";
    public const string CommandAccepted = "command.accepted";
    public const string CommandStarted = "command.started";
    public const string CommandOutput = "command.output";
    public const string CommandCompleted = "command.completed";
    public const string CommandFailed = "command.failed";
    public const string CommandCancelled = "command.cancelled";

    public const string ApprovalRequest = "approval.request";
    public const string ApprovalResponse = "approval.response";

    public const string TaskCreate = "task.create";
    public const string TaskUpdate = "task.update";
    public const string TaskCompleted = "task.completed";
    public const string TaskFailed = "task.failed";

    public const string FileOffer = "file.offer";
    public const string FileAccept = "file.accept";
    public const string FileChunk = "file.chunk";
    public const string FileProgress = "file.progress";
    public const string FileComplete = "file.complete";
    public const string FileFailed = "file.failed";

    public const string ScreenshotRequest = "screenshot.request";
    public const string ScreenshotResponse = "screenshot.response";

    public const string ClipboardRequest = "clipboard.request";
    public const string ClipboardResponse = "clipboard.response";

    public const string Ping = "ping";
    public const string Pong = "pong";

    public const string Error = "error";
}
