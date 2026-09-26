using System;
using System.Linq;
using System.Reflection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenAgent.Core.Tasks;
using OpenAgent.Windows.UI.Controls;
using OpenAgent.Windows.UI.Services;
using OpenAgent.Windows.UI.Views;

namespace OpenAgent.Windows;

public partial class MainWindow : Window
{
    private NavRow[] _navRows = Array.Empty<NavRow>();

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();

        _navRows = [NavAgent, NavTasks, NavDevices, NavTools, NavProviders, NavPlugins, NavSettings];

        VersionText.Text = $"OpenAgent {ProductVersion()}";

        Root.Loaded += (_, _) => RefreshCounts();
        // A page's first control otherwise takes focus on navigation and paints a
        // focus ring the artboards never show; taking focus on the page itself
        // keeps the keyboard walk intact because Tab still moves into the content.
        ContentFrame.Navigated += (_, _) =>
        {
            if (ContentFrame.Content is Control control)
            {
                _ = control.Focus(FocusState.Programmatic);
            }
        };
        SearchBox.KeyDown += SearchBox_KeyDown;
        Select("agent");
        ContentFrame.Navigate(typeof(DashboardPage));
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
    /// Frameless shell: the 44px caption strip across the top is the drag region,
    /// so the window keeps its caption buttons but loses the stock title bar.
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
        RefreshCounts();
    }

    private void Nav_Invoked(object sender, RoutedEventArgs e)
    {
        if (sender is NavRow row)
        {
            NavigateTo(row.Key);
        }
    }

    /// <summary>
    /// Selects a sidebar row and shows its page. Also the entry point for the
    /// <c>--page</c> startup switch the visual comparison script drives with.
    /// </summary>
    public void NavigateTo(string key)
    {
        if (string.IsNullOrEmpty(key) || !_navRows.Any(row => row.Key == key))
        {
            return;
        }

        Select(key);

        var page = key switch
        {
            "agent" => typeof(DashboardPage),
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

    private void Select(string key)
    {
        foreach (var row in _navRows)
        {
            row.IsSelected = row.Key == key;
        }
    }

    /// <summary>
    /// Ctrl+K focuses the search field and Enter there asks the agent — the field
    /// is an entry point to a command, not a filter over a catalogue.
    /// </summary>
    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.K &&
            (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
                global::Windows.System.VirtualKey.Control) & global::Windows.UI.Core.CoreVirtualKeyStates.Down)
            == global::Windows.UI.Core.CoreVirtualKeyStates.Down)
        {
            _ = SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
            return;
        }

        if (e.Key == global::Windows.System.VirtualKey.Enter && SearchBox.Text.Trim().Length > 0)
        {
            AgentHost.Current.RequestCommandCenter(SearchBox.Text.Trim());
            SearchBox.Text = string.Empty;
            e.Handled = true;
        }
    }

    /// <summary>
    /// Sidebar counters read the same host the pages do. They were literals copied
    /// from a design mockup, which left a count sitting above an empty list. A
    /// count that cannot be read stays hidden rather than showing a number it
    /// cannot back up.
    /// </summary>
    private async void RefreshCounts()
    {
        try
        {
            var tasks = await AgentHost.Current.RecentTasksAsync(50);
            var devices = await AgentHost.Current.DevicesAsync();
            var tools = await AgentHost.Current.ToolsAsync();
            var providers = await AgentHost.Current.ProvidersAsync();

            NavAgent.Count = Count(tasks.Count(t => !AgentTaskStatuses.IsTerminal(t.Status)));
            NavTasks.Count = Count(tasks.Count);
            NavDevices.Count = Count(devices.Count);
            NavTools.Count = Count(tools.Count);
            NavProviders.Count = Count(providers.Count);
        }
        catch (Exception)
        {
            NavAgent.Count = string.Empty;
            NavTasks.Count = string.Empty;
            NavDevices.Count = string.Empty;
            NavTools.Count = string.Empty;
            NavProviders.Count = string.Empty;
        }
    }

    private static string Count(int value) => value > 0 ? value.ToString() : string.Empty;
}
