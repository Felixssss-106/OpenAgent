using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OpenAgent.Windows.UI.Views;

public sealed partial class DevicesPage : Page
{
    public DevicesPage()
    {
        InitializeComponent();
        Loaded += DevicesPage_Loaded;
    }

    private void DevicesPage_Loaded(object sender, RoutedEventArgs e)
    {
        var items = new List<DeviceItem>
        {
            new()
            {
                Name = Environment.MachineName,
                Tag = "本机",
                SystemInfo = $"Windows {Environment.OSVersion.Version.Major} · OpenAgent 0.4.2 · 直连",
                Metrics = "CPU 23% · 内存 51% · 延迟 32 ms",
            },
            new()
            {
                Name = "Pixel 9",
                Tag = "已信任",
                SystemInfo = "Android 15 · OpenAgent 0.4.2",
                Metrics = "电量 78%",
            },
        };

        DeviceList.ItemsSource = items;
    }
}

public class DeviceItem
{
    public string Name { get; set; } = "";
    public string Tag { get; set; } = "";
    public string SystemInfo { get; set; } = "";
    public string Metrics { get; set; } = "";
}
