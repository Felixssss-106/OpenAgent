using System.Diagnostics;
using System.Text.Json;
using OpenAgent.Core.Error;
using OpenAgent.Security;
using OpenAgent.Tools;
using OpenAgent.Tools.System;

namespace OpenAgent.Tools.Tests;

/// <summary>
/// Process and application tools: the success paths run against a child process
/// the test starts and then removes (spec sections 38, 42, 43).
/// </summary>
public sealed class ProcessAndAppToolTests
{
    private static readonly ToolContext Context = new()
    {
        PermissionMode = PermissionMode.FullAccess,
    };

    [Fact]
    public async Task Terminate_requires_a_target()
    {
        var result = await new ProcessTerminateTool().ExecuteAsync(
            Context, Args("{}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.ToolError(2), result.Error!.Code);
    }

    [Fact]
    public async Task Terminate_refuses_the_current_process()
    {
        var result = await new ProcessTerminateTool().ExecuteAsync(
            Context, Args($"{{\"processId\":{Environment.ProcessId}}}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.ToolError(10), result.Error!.Code);
    }

    [Fact]
    public async Task Terminate_refuses_a_protected_system_process()
    {
        var result = await new ProcessTerminateTool().ExecuteAsync(
            Context, Args("{\"name\":\"csrss\"}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.ToolError(10), result.Error!.Code);
    }

    [Fact]
    public async Task Terminate_reports_a_missing_process()
    {
        var result = await new ProcessTerminateTool().ExecuteAsync(
            Context, Args("{\"processId\":2147483000}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.ToolError(6), result.Error!.Code);
    }

    [Fact]
    public async Task Terminate_stops_a_running_process()
    {
        using var child = StartSleeper();
        if (child is null)
        {
            // No sleeper helper on this machine: the refusal tests above still
            // cover the safety-critical behaviour.
            return;
        }

        var result = await new ProcessTerminateTool().ExecuteAsync(
            Context, Args($"{{\"processId\":{child.Id}}}"), CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(1, result.Data!.Value.GetProperty("count").GetInt32());
        Assert.True(child.HasExited);
    }

    [Fact]
    public async Task Close_requires_a_target()
    {
        var result = await new AppCloseTool().ExecuteAsync(Context, Args("{}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.ToolError(2), result.Error!.Code);
    }

    [Fact]
    public async Task Close_refuses_a_protected_system_process()
    {
        var result = await new AppCloseTool().ExecuteAsync(
            Context, Args("{\"name\":\"csrss\"}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.ToolError(10), result.Error!.Code);
    }

    [Fact]
    public async Task Close_force_terminates_a_process_without_a_window()
    {
        using var child = StartSleeper();
        if (child is null)
        {
            return;
        }

        var result = await new AppCloseTool().ExecuteAsync(
            Context, Args($"{{\"processId\":{child.Id},\"force\":true}}"), CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(1, result.Data!.Value.GetProperty("closed").GetInt32());
        Assert.True(child.HasExited);
    }

    [Fact]
    public async Task Close_reports_a_missing_process()
    {
        var result = await new AppCloseTool().ExecuteAsync(
            Context, Args("{\"name\":\"definitely-not-a-real-process-xyz\"}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.ToolError(6), result.Error!.Code);
    }

    /// <summary>Starts a 60 second sleeper, or null when the helper is unavailable.</summary>
    private static Process? StartSleeper()
    {
        var helper = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "timeout.exe");

        if (!File.Exists(helper))
        {
            return null;
        }

        return Process.Start(new ProcessStartInfo(helper, "/t 60 /nobreak")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;
}
