using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Shared.Protocol;

namespace OpenAgent.Tools.System;

/// <summary>
/// Read-only machine snapshot used by the dashboard and by the Agent when the
/// user asks "why is my PC slow?" (spec sections 38, 150).
/// </summary>
public sealed class SystemGetInfoTool : ITool
{
    public ToolDefinition Definition { get; } = new()
    {
        Id = "system.get_info",
        Name = "System information",
        Description = "Reads OS version, processor count, memory and the OpenAgent process footprint.",
        InputSchemaJson = """{"type":"object","properties":{},"required":[]}""",
        Risk = RiskLevel.Safe,
        Permissions = new[] { "system.read" },
        Reversible = true,
        RequiresApproval = false,
    };

    public Task<ToolResult> ExecuteAsync(
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var info = new Dictionary<string, object?>
        {
            ["os"] = RuntimeInformation.OSDescription,
            ["machineName"] = Environment.MachineName,
            ["processorCount"] = Environment.ProcessorCount,
            ["availableMemoryBytes"] = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            ["appVersion"] = typeof(SystemGetInfoTool).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            ["currentTimeUtc"] = DateTimeOffset.UtcNow,
            ["uptimeSeconds"] = Environment.TickCount64 / 1000,
        };

        using var self = Process.GetCurrentProcess();
        info["processWorkingSetBytes"] = self.WorkingSet64;

        try
        {
            var cpu = self.TotalProcessorTime;
            var elapsed = DateTimeOffset.UtcNow - self.StartTime.ToUniversalTime();
            info["processCpuPercent"] = elapsed.TotalSeconds > 0 && Environment.ProcessorCount > 0
                ? Math.Round(cpu.TotalSeconds / elapsed.TotalSeconds / Environment.ProcessorCount * 100, 1)
                : 0.0;
        }
        catch (Exception)
        {
            // Process CPU time is unavailable in some sandboxes; the rest of the
            // snapshot is still valid, so report it as unknown rather than failing.
            info["processCpuPercent"] = null;
        }

        return Task.FromResult(ToolResult.Ok(JsonSerializer.SerializeToElement(info)));
    }
}
