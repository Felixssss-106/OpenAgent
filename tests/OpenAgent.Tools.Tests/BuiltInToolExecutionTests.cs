using System.Text.Json;
using OpenAgent.Security;
using OpenAgent.Tools;
using OpenAgent.Tools.System;

namespace OpenAgent.Tools.Tests;

public sealed class BuiltInToolExecutionTests
{
    private static readonly ToolContext Context = new()
    {
        PermissionMode = PermissionMode.AskBeforeActions,
    };

    [Fact]
    public async Task System_info_returns_real_machine_data()
    {
        var tool = new SystemGetInfoTool();

        var result = await tool.ExecuteAsync(Context, JsonDocument.Parse("{}").RootElement, CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        var data = result.Data!.Value;
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("os").GetString()));
        Assert.True(data.GetProperty("processorCount").GetInt32() > 0);
        Assert.True(data.GetProperty("availableMemoryBytes").GetInt64() > 0);
        Assert.Equal(Environment.MachineName, data.GetProperty("machineName").GetString());
    }

    [Fact]
    public async Task Process_list_returns_running_processes_sorted_by_memory()
    {
        var tool = new ProcessListTool();
        var args = JsonDocument.Parse("{\"limit\":5}").RootElement;

        var result = await tool.ExecuteAsync(Context, args, CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        var data = result.Data!.Value;
        Assert.True(data.GetProperty("total").GetInt32() > 0);

        var processes = data.GetProperty("processes").EnumerateArray().ToArray();
        Assert.NotEmpty(processes);
        Assert.True(processes.Length <= 5);

        var memories = processes.Select(p => p.GetProperty("memoryBytes").GetInt64()).ToArray();
        Assert.Equal(memories.OrderByDescending(m => m), memories);
    }

    [Fact]
    public async Task Launching_a_missing_application_fails_cleanly()
    {
        var tool = new AppLaunchTool();
        var args = JsonDocument.Parse("{\"target\":\"definitely-not-a-real-app-xyz\"}").RootElement;

        var result = await tool.ExecuteAsync(Context, args, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error!.Code));
    }

    [Fact]
    public async Task Launching_refuses_a_system_directory_target()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(windows))
        {
            return;
        }

        var tool = new AppLaunchTool();
        var target = JsonSerializer.SerializeToElement(new { target = Path.Combine(windows, "System32", "notepad.exe") });

        var result = await tool.ExecuteAsync(Context, target, CancellationToken.None);

        Assert.False(result.Success);
    }
}
