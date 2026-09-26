using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenAgent.Windows.UI.Services;

namespace OpenAgent.Windows.UI.Views;

/// <summary>
/// Reads real devices from the transport through <see cref="IAgentHost"/>. No
/// sample data — the local machine registers itself via the loopback transport
/// (spec sections 60-65); LAN/Android devices appear here once their adapters
/// are wired (Phase 6-7).
/// </summary>
public sealed partial class DevicesPage : Page
{
    public DevicesPage()
    {
        InitializeComponent();
        Loaded += DevicesPage_Loaded;
    }

    private void DevicesPage_Loaded(object sender, RoutedEventArgs e)
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IReadOnlyList<DeviceSummary> devices;
        try
        {
            devices = await AgentHost.Current.DevicesAsync();
        }
        catch (Exception ex)
        {
            StatsText.Text = $"设备列表读取失败：{ex.Message}";
            DeviceList.ItemsSource = new List<DeviceItem>
            {
                new()
                {
                    Name = "设备列表读取失败",
                    Tag = "错误",
                    SystemInfo = ex.Message,
                    Metrics = string.Empty,
                },
            };
            return;
        }

        DeviceList.ItemsSource = devices
            .Select(device => new DeviceItem
            {
                Name = device.Name,
                Tag = device.Tag,
                SystemInfo = device.SystemInfo,
                Metrics = device.Metrics,
            })
            .ToList();

        StatsText.Text = devices.Count == 0
            ? "没有已发现的设备"
            : $"{devices.Count} 台设备 · {devices.Count(device => device.Tag == "在线")} 台在线";
    }
}

public class DeviceItem
{
    public string Name { get; set; } = "";
    public string Tag { get; set; } = "";
    public string SystemInfo { get; set; } = "";
    public string Metrics { get; set; } = "";
}
