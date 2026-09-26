using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace OpenAgent.Windows.UI.Views;

public sealed partial class ProvidersPage : Page
{
    public ProvidersPage()
    {
        InitializeComponent();
        Loaded += ProvidersPage_Loaded;
    }

    private void ProvidersPage_Loaded(object sender, RoutedEventArgs e)
    {
        var online = (Brush)Application.Current.Resources["StatusOnlineBrush"];

        ProviderList.ItemsSource = new List<ProviderItem>
        {
            new()
            {
                Name = "OpenAgent Native",
                Model = "本地规则引擎 · 无需联网",
                StatusLabel = "默认 · 在线",
                StatusBrush = online,
            },
        };
    }
}

public class ProviderItem
{
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public Brush StatusBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
}
