using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace OpenAgent.Windows.UI.Views;

public sealed partial class TasksPage : Page
{
    public TasksPage()
    {
        InitializeComponent();
        Loaded += TasksPage_Loaded;
    }

    private void TasksPage_Loaded(object sender, RoutedEventArgs e)
    {
        var items = new List<TaskItem>
        {
            new()
            {
                Name = "整理下载文件",
                StatusLabel = "运行中",
                DurationText = "00:04",
                StatusBrush = (SolidColorBrush)Application.Current.Resources["StatusOnlineBrush"],
                StatusTextBrush = (SolidColorBrush)Application.Current.Resources["StatusOnlineBrush"],
            },
            new()
            {
                Name = "删除重复文件",
                StatusLabel = "等待批准",
                DurationText = "—",
                StatusBrush = (SolidColorBrush)Application.Current.Resources["RiskMediumBrush"],
                StatusTextBrush = (SolidColorBrush)Application.Current.Resources["RiskMediumBrush"],
            },
            new()
            {
                Name = "分析 OpenAgent 项目",
                StatusLabel = "已完成",
                DurationText = "2m 14s",
                StatusBrush = (SolidColorBrush)Application.Current.Resources["TextQuaternaryBrush"],
                StatusTextBrush = (SolidColorBrush)Application.Current.Resources["TextTertiaryBrush"],
            },
            new()
            {
                Name = "查看系统占用",
                StatusLabel = "已完成",
                DurationText = "3s",
                StatusBrush = (SolidColorBrush)Application.Current.Resources["TextQuaternaryBrush"],
                StatusTextBrush = (SolidColorBrush)Application.Current.Resources["TextTertiaryBrush"],
            },
            new()
            {
                Name = "修复单元测试",
                StatusLabel = "失败 · 权限不足",
                DurationText = "18s",
                StatusBrush = (SolidColorBrush)Application.Current.Resources["StatusErrorBrush"],
                StatusTextBrush = (SolidColorBrush)Application.Current.Resources["StatusErrorBrush"],
            },
        };

        TaskList.ItemsSource = items;

        int running = 0, pending = 0, completed = 0;
        foreach (var item in items)
        {
            if (item.StatusLabel == "运行中") running++;
            else if (item.StatusLabel == "等待批准") pending++;
            else if (item.StatusLabel == "已完成") completed++;
        }
        StatsText.Text = $"{running} 运行中 · {pending} 等待批准 · {completed} 已完成";
    }

    private void TaskList_ItemClick(object sender, ItemClickEventArgs e)
    {
        // TODO: Navigate to task detail
    }
}

public class TaskItem
{
    public string Name { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string DurationText { get; set; } = "";
    public Brush StatusBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
    public Brush StatusTextBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
}
