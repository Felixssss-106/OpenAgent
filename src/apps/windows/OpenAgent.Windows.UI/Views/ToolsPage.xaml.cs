using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace OpenAgent.Windows.UI.Views;

public sealed partial class ToolsPage : Page
{
    public ToolsPage()
    {
        InitializeComponent();
        Loaded += ToolsPage_Loaded;
    }

    private void ToolsPage_Loaded(object sender, RoutedEventArgs e)
    {
        var resources = Application.Current.Resources;
        var safeBg = (Brush)resources["RiskSafeBgBrush"];
        var safeFg = (Brush)resources["RiskSafeBrush"];
        var lowBg = (Brush)resources["RiskLowBgBrush"];
        var lowFg = (Brush)resources["RiskLowBrush"];

        var items = new List<ToolItem>
        {
            new()
            {
                Name = "system.get_info",
                Description = "读取系统信息 · CPU、内存、进程工作集",
                Glyph = "\uE770",
                RiskLabel = "只读",
                RiskBackground = safeBg,
                RiskForeground = safeFg,
            },
            new()
            {
                Name = "process.list",
                Description = "列出正在运行的进程 · 按内存排序",
                Glyph = "\uE9D5",
                RiskLabel = "只读",
                RiskBackground = safeBg,
                RiskForeground = safeFg,
            },
            new()
            {
                Name = "app.launch",
                Description = "启动应用程序 · 拒绝系统目录",
                Glyph = "\uE7AD",
                RiskLabel = "低风险",
                RiskBackground = lowBg,
                RiskForeground = lowFg,
            },
        };

        ToolList.ItemsSource = items;
        StatsText.Text = $"{items.Count} 个内置工具 · 全部来自本地注册表";
    }
}

public class ToolItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Glyph { get; set; } = "";
    public string RiskLabel { get; set; } = "";
    public Brush RiskBackground { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
    public Brush RiskForeground { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
}
