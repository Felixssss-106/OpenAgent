using System.Linq;
using Microsoft.UI.Windowing;
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
        ConfigureWindow();
        NavView.SelectionChanged += NavView_SelectionChanged;

        // MenuItems[0] is the "状态" header, so pick the first real item.
        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
    }

    /// <summary>
    /// Frameless shell: the sidebar header doubles as the drag region, so the
    /// window keeps its caption buttons but loses the stock title bar.
    /// </summary>
    private void ConfigureWindow()
    {
        var appWindow = AppWindow;
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        SetTitleBar(AppTitleBar);

        // The shell lives in the tray, so the close button hides it.
        appWindow.Closing += AppWindow_Closing;
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        sender.Hide();
    }

    public void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
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
