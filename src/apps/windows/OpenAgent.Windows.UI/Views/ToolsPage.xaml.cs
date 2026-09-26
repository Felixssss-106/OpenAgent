using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenAgent.Windows.UI.Services;

namespace OpenAgent.Windows.UI.Views;

/// <summary>
/// Enumerates the tool catalogue through <see cref="IAgentHost"/>: one list,
/// whatever registered the tools (core, plugin, MCP, provider) — spec section 126.
/// </summary>
public sealed partial class ToolsPage : Page
{
    public ToolsPage()
    {
        InitializeComponent();
        Loaded += ToolsPage_Loaded;
    }

    private void ToolsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IReadOnlyList<ToolSummary> tools;
        try
        {
            tools = await AgentHost.Current.ToolsAsync();
        }
        catch (Exception ex)
        {
            StatsText.Text = $"工具列表读取失败：{ex.Message}";
            return;
        }

        var items = tools
            .Select(tool => new ToolItem
            {
                Name = tool.Id,
                Description = Describe(tool),
                Glyph = ToolViewMapper.Glyph(tool.Id),
                RiskLabel = $"{ToolViewMapper.RiskLabel(tool.Risk)} · {(tool.Reversible ? "可撤销" : "不可撤销")}",
                RiskBackground = UiBrushes.Get(ToolViewMapper.RiskBackgroundKey(tool.Risk), UiBrushes.Fallback.ChipBackground),
                RiskForeground = UiBrushes.Get(ToolViewMapper.RiskBrushKey(tool.Risk), UiBrushes.Fallback.ChipForeground),
            })
            .ToList();

        ToolGroups.ItemsSource = items
            .GroupBy(item => Family(item.Name))
            .OrderBy(group => FamilyOrder(group.Key))
            .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new ToolGroup { Title = FamilyTitle(group.Key), Items = group.ToList() })
            .ToList();

        StatsText.Text = items.Count == 0
            ? "没有已注册的工具"
            : $"{items.Count} 个可用工具 · {items.Count(i => i.RiskLabel.StartsWith("高", StringComparison.Ordinal))} 个需要批准";
    }

    /// <summary>The part of a tool id before the first dot: file, app, system…</summary>
    private static string Family(string toolId)
    {
        var dot = toolId.IndexOf('.');
        return dot <= 0 ? "other" : toolId[..dot];
    }

    private static string FamilyTitle(string family) => family switch
    {
        "file" => "文件",
        "app" => "应用",
        "process" => "进程",
        "screen" => "屏幕",
        "system" => "系统",
        _ => "其他",
    };

    private static int FamilyOrder(string family) => family switch
    {
        "file" => 0,
        "app" => 1,
        "process" => 2,
        "screen" => 3,
        "system" => 4,
        _ => 5,
    };

    private static string Describe(ToolSummary tool)
    {
        var description = string.IsNullOrWhiteSpace(tool.Description)
            ? tool.DisplayName
            : tool.Description;

        var reversible = tool.Reversible ? "可逆" : "不可逆";
        return $"{description} · {reversible}";
    }
}

public class ToolItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Glyph { get; set; } = string.Empty;
    public string RiskLabel { get; set; } = string.Empty;
    public Brush RiskBackground { get; set; } = UiBrushes.FromHex(UiBrushes.Fallback.StatusNeutral);
    public Brush RiskForeground { get; set; } = UiBrushes.FromHex(UiBrushes.Fallback.StatusNeutral);
}

/// <summary>One card on the page: a family heading and the tools under it.</summary>
public sealed class ToolGroup
{
    public string Title { get; set; } = string.Empty;
    public List<ToolItem> Items { get; set; } = new();
}
