using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenAgent.Windows.UI.Views;

namespace OpenAgent.Windows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        NavView.SelectionChanged += NavView_SelectionChanged;

        // MenuItems[0] is the "状态" header, so pick the first real item.
        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag?.ToString();
        var page = tag switch
        {
            "dashboard" => typeof(DashboardPage),
            "tasks" => typeof(TasksPage),
            "devices" => typeof(DevicesPage),
            "tools" => typeof(ToolsPage),
            "providers" => typeof(ProvidersPage),
            "plugins" => typeof(PluginsPage),
            "settings" => typeof(SettingsPage),
            _ => null,
        };

        if (page is not null)
        {
            ContentFrame.Navigate(page);
        }
    }
}
