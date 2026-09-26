using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenAgent.Core.Domain;
using OpenAgent.Windows.UI.Services;

namespace OpenAgent.Windows.UI.Views;

/// <summary>
/// Reads the real task log through <see cref="IAgentHost"/>. The page never
/// touches a repository: UI → agent service → storage (spec sections 35, 165).
/// </summary>
public sealed partial class TasksPage : Page
{
    public TasksPage()
    {
        InitializeComponent();
        Loaded += TasksPage_Loaded;
    }

    private void TasksPage_Loaded(object sender, RoutedEventArgs e)
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IReadOnlyList<AgentTask> tasks;
        try
        {
            tasks = await AgentHost.Current.RecentTasksAsync(50);
        }
        catch (Exception ex)
        {
            StatsText.Text = $"任务列表读取失败：{ex.Message}";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var items = tasks
            .Select(task => new TaskItem
            {
                TaskId = task.Id,
                Name = string.IsNullOrWhiteSpace(task.Prompt) ? "(空指令)" : task.Prompt,
                StatusLabel = string.IsNullOrWhiteSpace(task.Error)
                    ? TaskViewMapper.StatusLabel(task.Status)
                    : $"{TaskViewMapper.StatusLabel(task.Status)} · {task.Error}",
                DurationText = TaskViewMapper.DurationText(task, now),
                StatusBrush = UiBrushes.Get(TaskViewMapper.StatusBrushKey(task.Status), "#FF8A8F98"),
                StatusTextBrush = UiBrushes.Get(TaskViewMapper.StatusTextBrushKey(task.Status), "#FF7D8185"),
            })
            .ToList();

        TaskList.ItemsSource = items;
        StatsText.Text = TaskViewMapper.SummaryLine(tasks);
    }

    private async void TaskList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not TaskItem item)
        {
            return;
        }

        var events = await AgentHost.Current.TaskEventsAsync(item.TaskId);
        var body = events.Count == 0
            ? "（该任务还没有事件记录）"
            : string.Join(
                Environment.NewLine,
                events.Select(record =>
                    $"[{record.TimestampUtc.ToLocalTime():HH:mm:ss}] {record.Kind} · {record.Text}"));

        var dialog = new ContentDialog
        {
            Title = item.Name,
            CloseButtonText = "关闭",
            XamlRoot = XamlRoot,
            Content = new ScrollViewer
            {
                MaxHeight = 320,
                Content = new TextBlock
                {
                    Text = body,
                    Style = (Style)Application.Current.Resources["TypeCode"],
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                },
            },
        };

        await dialog.ShowAsync();
    }
}

public class TaskItem
{
    public string TaskId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;
    public Brush StatusBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
    public Brush StatusTextBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
}
