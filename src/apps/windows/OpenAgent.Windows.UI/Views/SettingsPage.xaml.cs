using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OpenAgent.Windows.UI.Views;

public sealed partial class SettingsPage : Page
{
    private const string ThemeSettingKey = "ui.theme";
    private static readonly string[] ThemeOptions = { "跟随系统", "浅色", "深色" };

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ThemeSetting.Value = ReadTheme() switch
        {
            ElementTheme.Light => "浅色",
            ElementTheme.Dark => "深色",
            _ => "跟随系统",
        };
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
        try
        {
            var values = global::Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue(ThemeSettingKey, out var raw) && raw is string name)
            {
                return name switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default,
                };
            }
        }
        catch
        {
            // No local settings container when unpackaged; default is fine.
        }

        return ElementTheme.Default;
    }

    private static void SaveTheme(ElementTheme theme)
    {
        try
        {
            global::Windows.Storage.ApplicationData.Current.LocalSettings.Values[ThemeSettingKey] = theme.ToString();
        }
        catch
        {
            // Non-fatal: the theme still applies for this session.
        }
    }
}
