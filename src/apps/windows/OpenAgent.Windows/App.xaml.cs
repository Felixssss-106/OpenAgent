using System;
using System.IO;
using System.Threading;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenAgent.Agent;
using OpenAgent.Providers;
using OpenAgent.Security;
using OpenAgent.Transport;
using OpenAgent.Tools;
using OpenAgent.Windows.Native;
using OpenAgent.Windows.Services;
using OpenAgent.Windows.UI.Services;
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

    /// <summary>
    /// The composition root. Never null: before startup finishes it is an empty
    /// provider, so a failure to open the database degrades the shell instead of
    /// killing it before the window appears.
    /// </summary>
    public static IServiceProvider Services { get; private set; } =
        new ServiceCollection().BuildServiceProvider();

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

        BuildCompositionRoot();

        MainWindow = new MainWindow();
        MainWindow.Activate();

        ApplyTheme(ReadThemeSetting());

        RegisterCommandCenterHotkey(MainWindow);
        CreateTrayIcon();
    }

    /// <summary>
    /// Registers the whole non-UI stack once (spec section 165) and hands the UI
    /// library its <see cref="IAgentHost"/> so pages read real data.
    /// </summary>
    private static void BuildCompositionRoot()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddOpenAgent(options => options.PermissionMode = PermissionMode.AskBeforeActions);
        services.AddOpenAgentProviders();
        var transport = new UdpLanTransport(LanDiscoveryOptions.Default(), new LocalLoopbackTransport());
        services.AddSingleton<ITransport>(transport);

        Services = services.BuildServiceProvider();

        var tasks = Services.GetService<AgentTaskService>();
        var registry = Services.GetService<ToolRegistry>();
        var logger = Services.GetService<ILogger<App>>();
        transport.InboundMessage += (_, e) =>
        {
            // Best-effort visibility for cross-device commands (seed; Phase 6-7
            // will route these into the agent task pipeline with pairing/trust).
            logger?.LogInformation(
                "LAN inbound {Type} from {From} to {To}: {Text}",
                e.Message.Type, e.Message.From, e.Message.To, e.Message.Text);
        };
        if (tasks is not null && registry is not null)
        {
            AgentHost.Register(new AgentHostAdapter(tasks, registry, transport));
        }
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
            ContextFlyout = BuildTrayMenu(),
            MenuActivation = PopupActivationMode.RightClick,
        };
    }

    /// <summary>Open the shell / the command center, or quit for real.</summary>
    private MenuFlyout BuildTrayMenu()
    {
        var openWindow = new MenuFlyoutItem { Text = "打开主窗口" };
        openWindow.Click += (_, _) => ShowMainWindow();

        var openCenter = new MenuFlyoutItem { Text = "打开 Command Center" };
        openCenter.Click += (_, _) => ShowCommandCenter();

        var exit = new MenuFlyoutItem { Text = "退出" };
        exit.Click += (_, _) => ExitApplication();

        var menu = new MenuFlyout();
        menu.Items.Add(openWindow);
        menu.Items.Add(openCenter);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exit);
        return menu;
    }

    public void ShowMainWindow()
    {
        var window = MainWindow;
        if (window is null)
        {
            return;
        }

        // Closing hides the window (see MainWindow), so a tray "open" has to
        // put it back before activating it.
        window.AppWindow.Show();
        window.Activate();
    }

    /// <summary>Releases the hotkey and the tray icon before the process dies.</summary>
    public void ExitApplication()
    {
        if (_hotkey is not null)
        {
            _hotkey.Dispose();
            _hotkey = null;
        }

        if (_trayIcon is not null)
        {
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }

        Environment.Exit(0);
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
