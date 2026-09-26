using System;
using System.Collections.Generic;
using OpenAgent.Core.Domain;
using OpenAgent.Core.Tasks;
using OpenAgent.Windows.UI.Services;
using Xunit;

namespace OpenAgent.Windows.UI.Tests;

public sealed class TaskViewMapperTests
{
    [Theory]
    [InlineData(AgentTaskStatus.Queued, "排队中")]
    [InlineData(AgentTaskStatus.Planning, "规划中")]
    [InlineData(AgentTaskStatus.WaitingApproval, "等待批准")]
    [InlineData(AgentTaskStatus.Running, "运行中")]
    [InlineData(AgentTaskStatus.Paused, "已暂停")]
    [InlineData(AgentTaskStatus.Completed, "已完成")]
    [InlineData(AgentTaskStatus.Failed, "失败")]
    [InlineData(AgentTaskStatus.Cancelled, "已取消")]
    public void StatusLabel_returns_expected_string(AgentTaskStatus status, string expected)
    {
        Assert.Equal(expected, TaskViewMapper.StatusLabel(status));
    }

    [Fact]
    public void StatusBrushKey_running_is_online() =>
        Assert.Equal("StatusOnlineBrush", TaskViewMapper.StatusBrushKey(AgentTaskStatus.Running));

    [Fact]
    public void StatusBrushKey_waiting_approval_is_pending() =>
        Assert.Equal("StatusPendingBrush", TaskViewMapper.StatusBrushKey(AgentTaskStatus.WaitingApproval));

    [Fact]
    public void StatusBrushKey_failed_is_error() =>
        Assert.Equal("StatusErrorBrush", TaskViewMapper.StatusBrushKey(AgentTaskStatus.Failed));

    [Fact]
    public void StatusBrushKey_queued_is_offline() =>
        Assert.Equal("StatusOfflineBrush", TaskViewMapper.StatusBrushKey(AgentTaskStatus.Queued));

    [Fact]
    public void DurationText_under_one_minute_shows_seconds()
    {
        var task = new AgentTask
        {
            CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMilliseconds(500);
        Assert.Equal("0.5s", TaskViewMapper.DurationText(task, now));
    }

    [Fact]
    public void DurationText_completed_task_shows_fixed_duration()
    {
        var task = new AgentTask
        {
            CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CompletedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 1, 30, TimeSpan.Zero),
        };
        var now = task.CompletedAtUtc.Value.AddMinutes(10);
        Assert.Equal("1m 30s", TaskViewMapper.DurationText(task, now));
    }

    [Fact]
    public void SummaryLine_empty_list_shows_prompt() =>
        Assert.Equal("暂无任务 · 用 Alt + Space 发起第一条指令", TaskViewMapper.SummaryLine(Array.Empty<AgentTask>()));

    [Fact]
    public void SummaryLine_mixed_statuses()
    {
        var tasks = new List<AgentTask>
        {
            new() { Status = AgentTaskStatus.Running },
            new() { Status = AgentTaskStatus.WaitingApproval },
            new() { Status = AgentTaskStatus.Completed },
            new() { Status = AgentTaskStatus.Failed },
        };
        var summary = TaskViewMapper.SummaryLine(tasks);
        Assert.Contains("1 进行中", summary);
        Assert.Contains("1 等待批准", summary);
        Assert.Contains("1 已完成", summary);
        Assert.Contains("1 失败", summary);
    }
}
