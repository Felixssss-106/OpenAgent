using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenAgent.Windows.UI.Services;

namespace OpenAgent.Windows.UI.Views;

public sealed partial class SettingsPage : Page
{
    private const string ThemeSettingKey = "ui.theme";
    private const string PermissionModeKey = "agent.permissionMode";
    private static readonly string[] ThemeOptions = { "跟随系统", "浅色", "深色" };

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        // The registry key is the truth about startup, not a cached setting: an
        // entry removed by hand must read 已关闭 on the next visit.
        StartupSetting.Value = StartupRegistrar.IsEnabled() ? "已开启" : "已关闭";
        PermissionSetting.Value = PermissionModeChoices.LabelFor(
            PermissionModeChoices.Normalize(AgentHost.Current.PermissionMode));

        ThemeSetting.Value = ReadTheme() switch
        {
            ElementTheme.Light => "浅色",
            ElementTheme.Dark => "深色",
            _ => "跟随系统",
        };

        try
        {
            var providers = await AgentHost.Current.ProvidersAsync();
            var defaultProvider = providers.FirstOrDefault(p => p.IsDefault) ?? providers.FirstOrDefault();
            if (defaultProvider is not null)
            {
                DefaultAgentSetting.Value = defaultProvider.DisplayName;
            }
        }
        catch
        {
            // The host is not wired (designer, tests): the static "OpenAgent Native"
            // text is still the honest answer, because that provider is always there.
        }
    }

    private void StartupSetting_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (StartupRegistrar.IsEnabled())
            {
                StartupRegistrar.Disable();
                StartupSetting.Value = "已关闭";
            }
            else
            {
                StartupRegistrar.Enable();
                StartupSetting.Value = "已开启";
            }
        }
        catch (Exception ex)
        {
            StartupSetting.Value = StartupRegistrar.IsEnabled() ? "已开启" : "已关闭";
            _ = new ContentDialog
            {
                Title = "开机启动",
                Content = $"写入启动项失败：{ex.Message}",
                CloseButtonText = "好",
                XamlRoot = XamlRoot,
            }.ShowAsync();
        }
    }

    private async void PermissionSetting_Click(object sender, RoutedEventArgs e)
    {
        var modes = PermissionModeChoices.Modes;
        var labels = modes.Select(PermissionModeChoices.LabelFor).ToList();
        var current = PermissionModeChoices.Normalize(AgentHost.Current.PermissionMode);

        var dialog = new ContentDialog
        {
            Title = "权限模式",
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot,
        };

        var panel = new StackPanel { Spacing = 8 };
        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = labels,
            SelectedIndex = modes.ToList().IndexOf(current),
        };
        panel.Children.Add(list);
        panel.Children.Add(new TextBlock
        {
            Text = "选择立即生效，并在下次启动时保持。",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
        });
        dialog.Content = panel;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary || list.SelectedIndex < 0)
            return;

        var mode = PermissionModeChoices.Normalize(modes[list.SelectedIndex]);
        AgentHost.Current.PermissionMode = mode;
        UiSettings.Set(PermissionModeKey, mode);
        PermissionSetting.Value = PermissionModeChoices.LabelFor(mode);
    }

    private async void ThemeSetting_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "主题",
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot,
        };

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = ThemeOptions,
            SelectedItem = ThemeSetting.Value,
        };
        dialog.Content = list;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary || list.SelectedItem is not string selected)
            return;

        var theme = selected switch
        {
            "浅色" => ElementTheme.Light,
            "深色" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        ThemeSetting.Value = selected;
        ApplyTheme(theme);
    }

    private void ApplyTheme(ElementTheme theme)
    {
        SaveTheme(theme);

        // The window root owns every page, so theming it themes the shell.
        if (XamlRoot?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }
    }

    private static ElementTheme ReadTheme()
    {
        return UiSettings.Get(ThemeSettingKey) switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private static void SaveTheme(ElementTheme theme)
    {
        UiSettings.Set(ThemeSettingKey, theme.ToString());
    }
}
