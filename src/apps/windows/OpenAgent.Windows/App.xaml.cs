using System;
using System.IO;
using System.Threading;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenAgent.Windows.Native;
using WinRT.Interop;

namespace OpenAgent.Windows;

public partial class App : Application
{
    /// <summary>Guarantees a single shell instance; a second launch exits.</summary>
    private static Mutex? s_instanceMutex;

    private HotkeyManager? _hotkey;
    private CommandCenterWindow? _commandCenter;
    private TaskbarIcon? _trayIcon;

    public App()
    {
        InitializeComponent();
    }

    /// <summary>The main shell, so pages can reach it without walking the tree.</summary>
    public static MainWindow? MainWindow { get; private set; }

    /// <summary>Alt+Space as designed, or the fallback that actually registered.</summary>
    public string HotkeyLabel
    {
        get;
        private set;
    } = "Alt + Space";

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        s_instanceMutex = new Mutex(initiallyOwned: true, "OpenAgent.Shell.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            // Already running. Without IPC the best we can do is bail out
            // instead of opening a second, unpaired shell.
            Environment.Exit(0);
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Activate();

        ApplyTheme(ReadThemeSetting());

        RegisterCommandCenterHotkey(MainWindow);
        CreateTrayIcon();
    }

    /// <summary>
    /// The shell lives in the tray: closing the window should not kill the
    /// agent, and the tray is how a background app stays reachable.
    /// </summary>
    private void CreateTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "tray.png");
        if (!File.Exists(iconPath))
            return;

        var openCommand = new XamlUICommand();
        openCommand.ExecuteRequested += (_, _) => ShowCommandCenter();

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "OpenAgent",
            IconSource = new BitmapImage(new Uri(iconPath)),
            DoubleClickCommand = openCommand,
        };
    }

    private void RegisterCommandCenterHotkey(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        _hotkey = new HotkeyManager(hwnd);
        _hotkey.Pressed += (_, _) => ShowCommandCenter();

        if (_hotkey.TryRegister(NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, NativeMethods.VK_SPACE))
        {
            HotkeyLabel = "Alt + Space";
        }
        else if (_hotkey.TryRegister(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, NativeMethods.VK_SPACE))
        {
            // Alt+Space is the window system menu on most machines, so this is
            // the usual landing spot.
            HotkeyLabel = "Ctrl + Alt + Space";
        }
        else
        {
            HotkeyLabel = "不可用";
        }
    }

    public void ShowCommandCenter()
    {
        if (_commandCenter is not null)
        {
            _commandCenter.Activate();
            return;
        }

        _commandCenter = new CommandCenterWindow();
        _commandCenter.Closed += (_, _) => _commandCenter = null;
        _commandCenter.Activate();
    }

    // --- Theme -------------------------------------------------------------

    private const string ThemeSettingKey = "ui.theme";

    public static ElementTheme ReadThemeSetting()
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
            // Unpackaged apps sometimes have no local settings container.
        }

        return ElementTheme.Default;
    }

    public static void ApplyTheme(ElementTheme theme)
    {
        try
        {
            global::Windows.Storage.ApplicationData.Current.LocalSettings.Values[ThemeSettingKey] = theme.ToString();
        }
        catch
        {
            // Non-fatal: the theme still applies for this session.
        }

        if (MainWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }
    }
}
