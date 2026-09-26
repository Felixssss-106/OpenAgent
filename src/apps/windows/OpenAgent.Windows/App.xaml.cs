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

        // Two switches exist for the screenshot comparison in scripts/ui-shot.ps1:
        // open one page directly, and render it in a chosen theme without writing
        // that choice to the settings store. Anything else is left alone.
        var theme = ReadThemeSetting();
        foreach (var argument in Environment.GetCommandLineArgs())
        {
            if (argument.StartsWith("--page=", StringComparison.OrdinalIgnoreCase))
            {
                MainWindow.NavigateTo(argument["--page=".Length..]);
            }
            else if (string.Equals(argument, "--theme=light", StringComparison.OrdinalIgnoreCase))
            {
                theme = ElementTheme.Light;
            }
            else if (string.Equals(argument, "--theme=dark", StringComparison.OrdinalIgnoreCase))
            {
                theme = ElementTheme.Dark;
            }
        }

        ApplyTheme(theme);

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
        var providers = Services.GetService<ProviderRegistry>();
        var logger = Services.GetService<ILogger<App>>();
        transport.InboundMessage += (_, e) =>
        {
            // Best-effort visibility for cross-device commands (seed; Phase 6-7
            // will route these into the agent task pipeline with pairing/trust).
            logger?.LogInformation(
                "LAN inbound {Type} from {From} to {To}: {Text}",
                e.Message.Type, e.Message.From, e.Message.To, e.Message.Text);
        };
        if (tasks is not null && registry is not null && providers is not null)
        {
            AgentHost.Register(new AgentHostAdapter(tasks, registry, transport, providers));
        }
    }

    /// <summary>
    /// The shell lives in the tray: closing the window should not kill the
    /// agent, and the tray is how a background app stays reachable.
    /// </summary>
    private void CreateTrayIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "tray.ico");
            if (!File.Exists(iconPath))
                return;

            var openCommand = new XamlUICommand();
            openCommand.ExecuteRequested += (_, _) => ShowCommandCenter();

            // A real .ico rather than IconSource + PNG: H.NotifyIcon converts an
            // ImageSource through GDI, which throws on a PNG and leaves the tray empty.
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "OpenAgent",
                Icon = new global::System.Drawing.Icon(iconPath),
                DoubleClickCommand = openCommand,
                ContextFlyout = BuildTrayMenu(),
                MenuActivation = PopupActivationMode.RightClick,
            };
        }
        catch (Exception ex)
        {
            // Losing the tray must never cost the shell its window.
            _trayIcon = null;
            System.Diagnostics.Debug.WriteLine($"Tray icon unavailable: {ex.Message}");
        }
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

    /// <summary>
    /// Alt+Space raises the Agent surface. It used to open a separate overlay
    /// window with its own copy of the conversation UI; the artboards keep one
    /// Agent screen inside the shell, so the hotkey now goes there.
    /// </summary>
    public void ShowCommandCenter(string? prefill = null)
    {
        if (MainWindow is not Windows.MainWindow shell)
        {
            return;
        }

        shell.ShowFromTray();
        shell.ShowAgentPrompt(prefill);
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

        // Only the root element is themed. Setting Application.RequestedTheme at
        // runtime — or even before the first window — fails the XAML engine over on
        // this Windows App SDK (fail-fast 0xc000027b, combase RPC_E_CALL_REJECTED),
        // so brushes fetched in code resolve their palette through the themed tree.
        // See UiBrushes.Context.
        if (MainWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }
    }
}
