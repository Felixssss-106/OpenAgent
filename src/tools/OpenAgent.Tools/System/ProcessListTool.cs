using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Shared.Protocol;

namespace OpenAgent.Tools.System;

/// <summary>
/// Read-only process listing, heaviest first (spec sections 38, 150).
/// </summary>
public sealed class ProcessListTool : ITool
{
    private const int MaxLimit = 100;

    public ToolDefinition Definition { get; } = new()
    {
        Id = "process.list",
        Name = "List processes",
        Description = "Lists running processes ordered by memory usage.",
        InputSchemaJson = """
        {"type":"object","properties":{"limit":{"type":"integer"}},"required":[]}
        """,
        Risk = RiskLevel.Safe,
        Permissions = new[] { "process.read" },
        Reversible = true,
        RequiresApproval = false,
    };

    public Task<ToolResult> ExecuteAsync(
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var limit = 20;
        if (arguments.TryGetProperty("limit", out var limitNode) && limitNode.TryGetInt32(out var requested))
        {
            limit = Math.Clamp(requested, 1, MaxLimit);
        }

        var processes = new List<Dictionary<string, object?>>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                processes.Add(new Dictionary<string, object?>
                {
                    ["id"] = process.Id,
                    ["name"] = process.ProcessName,
                    ["memoryBytes"] = process.WorkingSet64,
                });
            }
            catch (InvalidOperationException)
            {
                // The process exited between enumeration and inspection.
            }
            catch (Win32Exception)
            {
                // Access denied for a protected process — skip it, do not fail.
            }
            finally
            {
                process.Dispose();
            }
        }

        var top = processes
            .OrderByDescending(p => (long)(p["memoryBytes"] ?? 0L))
            .Take(limit)
            .ToArray();

        return Task.FromResult(ToolResult.Ok(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["total"] = processes.Count,
            ["returned"] = top.Length,
            ["processes"] = top,
        })));
    }
}
