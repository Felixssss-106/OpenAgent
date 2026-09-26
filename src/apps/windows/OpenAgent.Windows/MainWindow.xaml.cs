using System;
using System.Linq;
using System.Reflection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenAgent.Core.Tasks;
using OpenAgent.Windows.UI.Controls;
using OpenAgent.Windows.UI.Services;using OpenAgent.Windows.UI.Views;

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
        SearchBox.KeyDown += SearchBox_KeyDown;
        Select("agent");
        ContentFrame.Navigate(typeof(AgentPage));
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

        if (Content is FrameworkElement root)
        {
            UiBrushes.Context = root;
            root.ActualThemeChanged += (_, _) => ApplyCaptionColors();
        }

        ApplyCaptionColors();

        // The shell lives in the tray, so the close button hides it.
        appWindow.Closing += AppWindow_Closing;
    }

    /// <summary>
    /// The artboards draw no caption buttons, so the real ones have to disappear
    /// into the 44px strip: transparent background, glyph in the current text
    /// colour. Left alone they paint an opaque black block over the top-right
    /// corner of the client area in either theme.
    /// </summary>
    private void ApplyCaptionColors()
    {
        var light = Content is FrameworkElement root && root.ActualTheme == ElementTheme.Light;
        var bar = AppWindow.TitleBar;

        var glyph = light
            ? global::Windows.UI.Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A)
            : global::Windows.UI.Color.FromArgb(0xFF, 0xE8, 0xE8, 0xEA);
        var hover = light
            ? global::Windows.UI.Color.FromArgb(0x14, 0x1A, 0x1A, 0x1A)
            : global::Windows.UI.Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF);
        var pressed = light
            ? global::Windows.UI.Color.FromArgb(0x24, 0x1A, 0x1A, 0x1A)
            : global::Windows.UI.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);

        var transparent = global::Windows.UI.Color.FromArgb(0, 0, 0, 0);
        bar.ButtonBackgroundColor = transparent;
        bar.ButtonInactiveBackgroundColor = transparent;
        bar.ButtonForegroundColor = glyph;
        bar.ButtonHoverForegroundColor = glyph;
        bar.ButtonPressedForegroundColor = glyph;
        bar.ButtonInactiveForegroundColor = light
            ? global::Windows.UI.Color.FromArgb(0x8A, 0x1A, 0x1A, 0x1A)
            : global::Windows.UI.Color.FromArgb(0x8A, 0xE8, 0xE8, 0xEA);
        bar.ButtonHoverBackgroundColor = hover;
        bar.ButtonPressedBackgroundColor = pressed;
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
            "agent" => typeof(AgentPage),
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

    /// <summary>
    /// Brings the Agent surface forward and types a command into it without
    /// running it, so a command written elsewhere is still confirmed here.
    /// </summary>
    public void ShowAgentPrompt(string? prompt)
    {
        NavigateTo("agent");
        if (string.IsNullOrWhiteSpace(prompt) || ContentFrame.Content is not AgentPage page)
        {
            return;
        }

        page.Prefill(prompt.Trim());
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
