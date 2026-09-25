using System.Text.Json;
using OpenAgent.Shared.Protocol;

namespace OpenAgent.Tools;

/// <summary>
/// The only way an AI reaches the operating system (spec section 132): the model
/// emits a structured call, OpenAgent validates and executes it. Never the
/// reverse.
/// </summary>
public interface ITool
{
    ToolDefinition Definition { get; }

    Task<ToolResult> ExecuteAsync(
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken);
}
