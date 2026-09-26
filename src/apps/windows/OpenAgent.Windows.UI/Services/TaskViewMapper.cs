using System.Collections.Generic;
using System.Globalization;
using OpenAgent.Core.Domain;
using OpenAgent.Core.Tasks;

namespace OpenAgent.Windows.UI.Services;

/// <summary>
/// Pure mapping from the task domain to view strings. No WinUI types, so the
/// mapping is unit-testable without a XAML root (spec sections 35, 155).
/// </summary>
public static class TaskViewMapper
{
    public const string StatusOnlineBrushKey = "StatusOnlineBrush";
    public const string StatusPendingBrushKey = "StatusPendingBrush";
    public const string StatusOfflineBrushKey = "StatusOfflineBrush";
    public const string StatusErrorBrushKey = "StatusErrorBrush";

    public static string StatusLabel(AgentTaskStatus status) => status switch
    {
        AgentTaskStatus.Queued => "排队中",
        AgentTaskStatus.Planning => "规划中",
        AgentTaskStatus.WaitingApproval => "等待批准",
        AgentTaskStatus.Running => "运行中",
        AgentTaskStatus.Paused => "已暂停",
        AgentTaskStatus.Completed => "已完成",
        AgentTaskStatus.Failed => "失败",
        AgentTaskStatus.Cancelled => "已取消",
        _ => "未知",
    };

    /// <summary>The status dot colour.</summary>
    public static string StatusBrushKey(AgentTaskStatus status) => status switch
    {
        AgentTaskStatus.Running => StatusOnlineBrushKey,
        AgentTaskStatus.Planning => StatusOnlineBrushKey,
        AgentTaskStatus.WaitingApproval => StatusPendingBrushKey,
        AgentTaskStatus.Paused => StatusPendingBrushKey,
        AgentTaskStatus.Failed => StatusErrorBrushKey,
        _ => StatusOfflineBrushKey,
    };

    /// <summary>Terminal states read as muted text rather than a loud colour.</summary>
    public static string StatusTextBrushKey(AgentTaskStatus status) =>
        AgentTaskStatuses.IsTerminal(status) || status == AgentTaskStatus.Queued
            ? StatusTextMutedBrushKey
            : StatusBrushKey(status);

    public const string StatusTextMutedBrushKey = "TextTertiaryBrush";

    /// <summary>Wall-clock length of a task; running tasks are measured against now.</summary>
    public static string DurationText(AgentTask task, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(task);

        var end = task.CompletedAtUtc ?? now;
        var span = end - task.CreatedAtUtc;
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        if (span.TotalMinutes < 1.0)
        {
            return span.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }

        if (span.TotalHours < 1.0)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}m {1:00}s",
                (int)span.TotalMinutes,
                span.Seconds);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}h {1:00}m",
            (int)span.TotalHours,
            span.Minutes);
    }

    /// <summary>"2 运行中 · 1 等待批准 · 5 已完成" style summary line.</summary>
    public static string SummaryLine(IReadOnlyList<AgentTask> tasks)
    {
        if (tasks is null || tasks.Count == 0)
        {
            return "暂无任务 · 用 Alt + Space 发起第一条指令";
        }

        var running = 0;
        var waiting = 0;
        var completed = 0;
        var failed = 0;
        var other = 0;

        foreach (var task in tasks)
        {
            switch (task.Status)
            {
                case AgentTaskStatus.Queued:
                case AgentTaskStatus.Planning:
                case AgentTaskStatus.Running:
                case AgentTaskStatus.Paused:
                    running++;
                    break;
                case AgentTaskStatus.WaitingApproval:
                    waiting++;
                    break;
                case AgentTaskStatus.Completed:
                    completed++;
                    break;
                case AgentTaskStatus.Failed:
                    failed++;
                    break;
                default:
                    other++;
                    break;
            }
        }

        var parts = new List<string>(4);
        if (running > 0)
        {
            parts.Add($"{running} 进行中");
        }

        if (waiting > 0)
        {
            parts.Add($"{waiting} 等待批准");
        }

        if (completed > 0)
        {
            parts.Add($"{completed} 已完成");
        }

        if (failed > 0)
        {
            parts.Add($"{failed} 失败");
        }

        if (other > 0)
        {
            parts.Add($"{other} 已结束");
        }

        return string.Join(" · ", parts);
    }
}
