using System;
using System.Linq;
using System.Reflection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenAgent.Windows.UI.Services;
using OpenAgent.Windows.UI.Views;

namespace OpenAgent.Windows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        NavView.SelectionChanged += NavView_SelectionChanged;

        VersionText.Text = $"OpenAgent {ProductVersion()}";

        NavView.Loaded += (_, _) => RefreshBadges();

        // MenuItems[0] is the "状态" header, so pick the first real item.
        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
    }

    /// <summary>
    /// Read from the assembly rather than a literal: the sidebar used to carry a
    /// version copied out of a design mockup, so it never changed on release.
    /// </summary>
    private static string ProductVersion()
    {
        var info = typeof(MainWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(info))
        {
            return "dev";
        }

        var plus = info.IndexOf('+');
        return plus >= 0 ? info[..plus] : info;
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
        RefreshBadges();
    }

    /// <summary>
    /// Sidebar counters read the same host the pages do. They were literals copied
    /// from a design mockup, which left a "4" badge sitting above an empty task list.
    /// A failed read hides the badge rather than showing a number it cannot back up.
    /// </summary>
    private async void RefreshBadges()
    {
        try
        {
            var tasks = await AgentHost.Current.RecentTasksAsync(50);
            var devices = await AgentHost.Current.DevicesAsync();
            SetCount(TaskCount, tasks.Count);
            SetCount(DeviceCount, devices.Count);
        }
        catch (Exception)
        {
            TaskCount.Visibility = Visibility.Collapsed;
            DeviceCount.Visibility = Visibility.Collapsed;
        }
    }

    private static void SetCount(TextBlock counter, int count)
    {
        counter.Text = count.ToString();
        counter.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
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
