using OpenAgent.Core;

namespace OpenAgent.Providers;

/// <summary>
/// Flags describing what an agent CLI can actually do. No provider supports
/// every flag, so the capability table is queried before an adapter picks an
/// execution mode (spec section 23).
/// </summary>
[Flags]
public enum AgentCapabilities
{
    None = 0,
    Streaming = 1 << 0,
    ToolCalling = 1 << 1,
    Approval = 1 << 2,
    SessionResume = 1 << 3,
    ImageInput = 1 << 4,
    InteractiveTerminal = 1 << 5,
    JsonOutput = 1 << 6,
    StructuredOutput = 1 << 7,
    Mcp = 1 << 8,
}

/// <summary>
/// The minimal tool description a provider needs to plan a call. The full
/// <see cref="OpenAgent.Tools.ToolDefinition"/> stays in the tool layer; the
/// provider only reads the fields that influence routing (spec section 126).
/// </summary>
public sealed record ProviderToolInfo(
    string Id,
    string DisplayName,
    string Description,
    RiskLevel Risk,
    bool Reversible);

public enum AgentHealth
{
    Healthy,
    Degraded,
    Unavailable,
}

public enum ProviderStepKind
{
    Thought,
    ToolCall,
    ToolResult,
    Error,
}

/// <summary>One emitted step from a provider turn (spec section 132).</summary>
public sealed record ProviderStep(ProviderStepKind Kind, string Text, string? ToolId = null, string? ArgumentsJson = null);

/// <summary>An open conversation with a provider. Native keeps these in memory.</summary>
public sealed record AgentSession(string Id, string TaskId, DateTimeOffset CreatedAtUtc);

/// <summary>
/// An agent CLI adapter. Native is the default; external CLIs (Codex, Claude
/// Code, OpenCode, Pi, Gemini) are written when their CLI is detected and
/// driven — never hardcoded (spec sections 22, 23, 27).
/// </summary>
public interface IAgentProvider
{
    string Id { get; }
    string DisplayName { get; }
    AgentCapabilities Capabilities { get; }

    Task<AgentSession> CreateSessionAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns one prompt into a batch of steps. Native returns the plan and the
    /// tool call together; a streaming provider can later split this across
    /// an event stream (spec section 132).
    /// </summary>
    Task<IReadOnlyList<ProviderStep>> SendPromptAsync(
        AgentSession session,
        string prompt,
        IReadOnlyList<ProviderToolInfo> tools,
        CancellationToken cancellationToken = default);

    Task StopAsync(AgentSession session, CancellationToken cancellationToken = default);

    Task ResumeAsync(AgentSession session, CancellationToken cancellationToken = default);

    Task<AgentHealth> HealthCheckAsync(CancellationToken cancellationToken = default);
}
