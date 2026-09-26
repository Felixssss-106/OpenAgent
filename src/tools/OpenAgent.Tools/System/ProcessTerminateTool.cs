using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Shared.Protocol;
using OpenAgent.Tools.Internal;

namespace OpenAgent.Tools.System;

/// <summary>
/// Terminates a process. High risk and irreversible — unsaved work dies with
/// the process, so this is never auto-approved (spec sections 18, 42).
/// </summary>
public sealed class ProcessTerminateTool : ITool
{
    private const int MaxNameMatches = 50;

    public ToolDefinition Definition { get; } = new()
    {
        Id = "process.terminate",
        Name = "Terminate process",
        Description = "Terminates a process by id or by name. Unsaved work is lost.",
        InputSchemaJson = """
        {"type":"object","properties":{"processId":{"type":"integer"},"name":{"type":"string"},"force":{"type":"boolean"}},"required":[]}
        """,
        Risk = RiskLevel.High,
        Permissions = new[] { "process.write" },
        Reversible = false,
        RequiresApproval = true,
    };

    public Task<ToolResult> ExecuteAsync(
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var processId = ToolArguments.ReadInt32(arguments, "processId");
        var name = ToolArguments.ReadString(arguments, "name");

        if (processId is null && string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(ProcessToolFailures.MissingTarget());
        }

        if (processId is <= 0)
        {
            return Task.FromResult(ProcessToolFailures.InvalidArgument("processId 必须为正整数"));
        }

        var force = ToolArguments.ReadBoolean(arguments, "force");
        var terminated = new List<Dictionary<string, object?>>();

        try
        {
            foreach (var process in Resolve(processId, name))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var id = process.Id;
                string processName;
                try
                {
                    processName = process.ProcessName;
                }
                catch (InvalidOperationException)
                {
                    process.Dispose();
                    continue;
                }

                if (ProtectedProcesses.IsProtected(id, processName))
                {
                    process.Dispose();
                    return Task.FromResult(ProcessToolFailures.Protected($"{processName} ({id})"));
                }

                try
                {
                    if (force)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    else
                    {
                        process.Kill();
                    }

                    terminated.Add(new Dictionary<string, object?>
                    {
                        ["processId"] = id,
                        ["name"] = processName,
                        ["entireProcessTree"] = force,
                    });
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    process.Dispose();
                    return Task.FromResult(ProcessToolFailures.Failed($"{processName} ({id})", ex.Message));
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Task.FromResult(ProcessToolFailures.Failed(name ?? processId?.ToString() ?? "?", ex.Message));
        }

        if (terminated.Count == 0)
        {
            return Task.FromResult(ProcessToolFailures.NotFound(name ?? processId?.ToString() ?? "?"));
        }

        return Task.FromResult(ToolResult.Ok(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["terminated"] = terminated,
            ["count"] = terminated.Count,
        })));
    }

    /// <summary>
    /// Resolution is explicit: an id selects exactly one process, a name
    /// selects the processes Windows reports for it (spec section 43).
    /// </summary>
    private static IEnumerable<Process> Resolve(int? processId, string? name)
    {
        if (processId is not null)
        {
            try
            {
                return new[] { Process.GetProcessById(processId.Value) };
            }
            catch (ArgumentException)
            {
                // The process exited between the model's call and ours — that is
                // "not found", not a failure to terminate.
                return Array.Empty<Process>();
            }
        }

        return Process.GetProcessesByName(name)
            .Take(MaxNameMatches)
            .ToArray();
    }
}
