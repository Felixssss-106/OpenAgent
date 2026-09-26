using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenAgent.Windows.UI.Services;

namespace OpenAgent.Windows.UI.Views;

public sealed partial class ProvidersPage : Page
{
    public ProvidersPage()
    {
        InitializeComponent();
        Loaded += ProvidersPage_Loaded;
    }

    private void ProvidersPage_Loaded(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    /// <summary>
    /// Lists what the host can actually drive. This used to be a hard-coded card
    /// for the bundled agent, which hid every CLI the provider layer discovered.
    /// </summary>
    private async Task RefreshAsync()
    {
        IReadOnlyList<ProviderSummary> providers;
        try
        {
            providers = await AgentHost.Current.ProvidersAsync();
        }
        catch (Exception ex)
        {
            StatsText.Text = $"Provider 列表读取失败：{ex.Message}";
            return;
        }

        var online = UiBrushes.Get("StatusOnlineBrush", UiBrushes.Fallback.Online);
        var accent = UiBrushes.Get("AccentBrush", UiBrushes.Fallback.Accent);
        var secondary = UiBrushes.Get("TextSecondaryBrush", UiBrushes.Fallback.StatusText);

        ProviderList.ItemsSource = providers
            .Select(provider => new ProviderItem
            {
                Name = provider.DisplayName,
                StatusLabel = provider.IsDefault ? "内置 · 默认" : "已发现 · 可驱动",
                StatusBrush = online,
                StatusTextBrush = provider.IsDefault ? accent : secondary,
            })
            .ToList();

        StatsText.Text = providers.Count == 0
            ? "没有可用的 Provider"
            : $"{providers.Count} 个可用 Provider · API Key 存于系统凭据库";
    }
}

public sealed class ProviderItem
{
    public string Name { get; set; } = string.Empty;
    public string StatusLabel { get; internal set; } = string.Empty;
    public Brush StatusBrush { get; internal set; } = UiBrushes.FromHex(UiBrushes.Fallback.StatusNeutral);
    public Brush StatusTextBrush { get; internal set; } = UiBrushes.FromHex(UiBrushes.Fallback.StatusText);
}
