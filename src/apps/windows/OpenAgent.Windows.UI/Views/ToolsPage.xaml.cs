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
                RiskLabel = ToolViewMapper.RiskLabel(tool.Risk),
                RiskBackground = UiBrushes.Get(ToolViewMapper.RiskBackgroundKey(tool.Risk), UiBrushes.Fallback.ChipBackground),
                RiskForeground = UiBrushes.Get(ToolViewMapper.RiskBrushKey(tool.Risk), UiBrushes.Fallback.ChipForeground),
            })
            .ToList();

        ToolList.ItemsSource = items;
        StatsText.Text = items.Count == 0
            ? "没有已注册的工具"
            : $"{items.Count} 个工具 · 全部来自本地工具注册表";
    }

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
