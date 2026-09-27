using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Core.Error;
using OpenAgent.Security;
using OpenAgent.Shared.Protocol;

namespace OpenAgent.Tools.System;

/// <summary>
/// Starts an application. Low risk, but it has a side effect, so the default
/// permission mode asks first (spec sections 38, 43, 44).
/// </summary>
public sealed class AppLaunchTool : ITool
{
    public ToolDefinition Definition { get; } = new()
    {
        Id = "app.launch",
        Name = "Launch application",
        Description = "Starts an application by full path, or by name resolved through PATH.",
        InputSchemaJson = """
        {"type":"object","properties":{"target":{"type":"string"},"arguments":{"type":"string"}},"required":["target"]}
        """,
        Risk = RiskLevel.Low,
        Permissions = new[] { "process.write" },
        Reversible = false,
        RequiresApproval = true,
    };

    public Task<ToolResult> ExecuteAsync(
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetProperty("target", out var targetNode) ||
            targetNode.ValueKind != JsonValueKind.String)
        {
            return Task.FromResult(ToolResult.Fail(
                ErrorCodes.ToolError(2), "缺少参数 target"));
        }

        var target = targetNode.GetString() ?? string.Empty;
        var processArguments = arguments.TryGetProperty("arguments", out var argNode) &&
                               argNode.ValueKind == JsonValueKind.String
            ? argNode.GetString()
            : null;

        var resolved = Resolve(target);
        if (resolved.IsFailure)
        {
            return Task.FromResult(ToolResult.Fail(ErrorCodes.ToolError(3), resolved.Error!));
        }

        try
        {
            // The resolved value is a real file path, so shell execution adds
            // nothing but risk: arguments go through the CRT-style split into an
            // explicit argv list (Internal.WindowsCommandLine), and the OS builds
            // the target's command line from it — no string can reshape the line
            // after the split (spec section 43).
            var startInfo = new ProcessStartInfo(resolved.Value!)
            {
                UseShellExecute = false,
            };

            if (!string.IsNullOrWhiteSpace(processArguments))
            {
            foreach (var arg in Internal.WindowsCommandLine.Split(processArguments))
            {
                startInfo.ArgumentList.Add(arg);
            }
            }

            if (!string.IsNullOrWhiteSpace(context.WorkingDirectory))
            {
                startInfo.WorkingDirectory = context.WorkingDirectory;
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Task.FromResult(ToolResult.Fail(
                    ErrorCodes.ToolError(4), $"无法启动：{resolved.Value}"));
            }

            return Task.FromResult(ToolResult.Ok(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["processId"] = process.Id,
                ["name"] = process.ProcessName,
                ["path"] = resolved.Value,
            })));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return Task.FromResult(ToolResult.Fail(ErrorCodes.ToolError(5), $"启动失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// Resolution is explicit on purpose — no shell, no string concatenation into
    /// a command line (spec section 43).
    /// </summary>
    private static (bool IsFailure, string? Value, string? Error) Resolve(string target)
    {
        if (Path.IsPathRooted(target))
        {
            var check = PathPolicy.Validate(target);
            if (!check.IsAllowed)
            {
                return (true, null, check.Describe());
            }

            return File.Exists(check.NormalizedPath!)
                ? (false, check.NormalizedPath, null)
                : (true, null, "文件不存在");
        }

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), target);
            var check = PathPolicy.Validate(candidate);
            if (!check.IsAllowed || !File.Exists(check.NormalizedPath!))
            {
                continue;
            }

            return (false, check.NormalizedPath, null);
        }

        return (true, null, $"找不到应用程序：{target}");
    }
}
