using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Shared.Protocol;
using OpenAgent.Tools.Internal;

namespace OpenAgent.Tools.System;

/// <summary>
/// Closes an application: asks its main window to close first, and only kills
/// it when the caller explicitly set <c>force</c> (spec sections 38, 44).
/// Medium risk — closing an application discards unsaved work.
/// </summary>
public sealed class AppCloseTool : ITool
{
    private const int DefaultTimeoutMs = 5000;

    private const int MaxTimeoutMs = 30000;

    private const int MaxNameMatches = 50;

    public ToolDefinition Definition { get; } = new()
    {
        Id = "app.close",
        Name = "Close application",
        Description = "Closes an application by process id or name, optionally forcing it.",
        InputSchemaJson = """
        {"type":"object","properties":{"processId":{"type":"integer"},"name":{"type":"string"},"force":{"type":"boolean"},"timeoutMs":{"type":"integer"}},"required":[]}
        """,
        Risk = RiskLevel.Medium,
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
        var timeoutMs = ToolArguments.ReadInt32(arguments, "timeoutMs") ?? DefaultTimeoutMs;
        timeoutMs = Math.Clamp(timeoutMs, 0, MaxTimeoutMs);

        var results = new List<Dictionary<string, object?>>();

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

                try
                {
                    if (ProtectedProcesses.IsProtected(id, processName))
                    {
                        return Task.FromResult(ProcessToolFailures.Protected($"{processName} ({id})"));
                    }

                    var graceful = process.CloseMainWindow();
                    if (graceful)
                    {
                        process.WaitForExit(timeoutMs);
                    }

                    var forceKilled = false;
                    if (!process.HasExited && force)
                    {
                        process.Kill(entireProcessTree: true);
                        forceKilled = true;
                        process.WaitForExit(Math.Min(timeoutMs, 2000));
                    }

                    results.Add(new Dictionary<string, object?>
                    {
                        ["processId"] = id,
                        ["name"] = processName,
                        ["exited"] = process.HasExited,
                        ["graceful"] = graceful,
                        ["forceKilled"] = forceKilled,
                    });
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
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

        if (results.Count == 0)
        {
            return Task.FromResult(ProcessToolFailures.NotFound(name ?? processId?.ToString() ?? "?"));
        }

        var closed = results.Count(r => (bool)(r["exited"] ?? false));

        return Task.FromResult(ToolResult.Ok(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["requested"] = results.Count,
            ["closed"] = closed,
            ["results"] = results,
        })));
    }

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
                // Already gone — reported as "not found", not as a failure.
                return Array.Empty<Process>();
            }
        }

        return Process.GetProcessesByName(name).Take(MaxNameMatches).ToArray();
    }
}
