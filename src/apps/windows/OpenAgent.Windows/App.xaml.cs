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
using OpenAgent.Storage.Repositories;
using OpenAgent.Transport;
using OpenAgent.Transport.Pairing;
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
    /// --data=&lt;dir&gt; points the whole store at a throwaway directory. It exists for
    /// scripts/ui-state-verify.sh: the approval card is only capturable from a known
    /// starting state, and every earlier verification run had left tasks and
    /// conversations behind in %LOCALAPPDATA%\OpenAgent. Null keeps the default.
    /// </summary>
    private static string? CommandLineDataRoot()
    {
        foreach (var argument in Environment.GetCommandLineArgs())
        {
            if (!argument.StartsWith("--data=", StringComparison.OrdinalIgnoreCase)) continue;
            var path = argument["--data=".Length..];
            if (!string.IsNullOrWhiteSpace(path)) return path;
        }
        return null;
    }

    /// <summary>
    /// Registers the whole non-UI stack once (spec section 165) and hands the UI
    /// library its <see cref="IAgentHost"/> so pages read real data.
    /// </summary>
    private static void BuildCompositionRoot()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddOpenAgent(options =>
        {
            // The settings page persists the pick; unknown values fall back to the
            // install default — never to something more permissive (spec 239).
            options.PermissionMode = Enum.TryParse<PermissionMode>(
                PermissionModeChoices.Normalize(UiSettings.Get("agent.permissionMode")),
                out var mode)
                ? mode
                : PermissionMode.AskBeforeActions;
            options.DatabaseRoot = CommandLineDataRoot();
        });
        services.AddOpenAgentProviders();
        services.AddSingleton<IPairingStore, PairingStore>();
        var transport = new UdpLanTransport(LanDiscoveryOptions.Default(), new LocalLoopbackTransport());
        services.AddSingleton<ITransport>(transport);

        Services = services.BuildServiceProvider();

        var tasks = Services.GetService<AgentTaskService>();
        var registry = Services.GetService<ToolRegistry>();
        var providers = Services.GetService<ProviderRegistry>();
        var executor = Services.GetService<ToolExecutor>();
        var logger = Services.GetService<ILogger<App>>();

        // v2 pairing: identity on first run, PIN dialog on a request, encrypted
        // traffic afterwards (docs/protocol.md §4).
        var pairing = new PairingService(
            Services.GetRequiredService<IPairingStore>(),
            transport,
            transport.SelfId);
        pairing.EnsureIdentity();
        pairing.PinRequested += (_, pin) => ShowPairingDialog(pin, pairing);

        transport.InboundMessage += (_, e) =>
        {
            var envelope = e.Message;

            // Pairing traffic is consumed by the state machine; everything else
            // is application payload. A command runs only for a paired peer and
            // only in sealed form (docs/protocol.md §4.4).
            if (envelope.Type is LanMessageType.PairRequest or LanMessageType.PairConfirm
                or LanMessageType.PairChallenge or LanMessageType.PairComplete)
            {
                _ = pairing.HandleAsync(envelope);
                return;
            }

            if (envelope.Type == LanMessageType.Command && remoteCommands is not null)
            {
                var key = pairing.KeyFor(envelope.From);
                if (key is null)
                {
                    _ = remoteCommands.RejectUnpairedAsync(envelope);
                    return;
                }

                if (envelope.Cipher is null
                    || !EnvelopeCrypto.TryOpen(envelope, key, out var opened)
                    || DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - opened.Ts > ReplayWindowMs)
                {
                    logger?.LogWarning("dropped undecryptable or stale command from {From}", envelope.From);
                    return;
                }

                _ = remoteCommands.HandleAsync(opened);
                return;
            }

            logger?.LogInformation(
                "LAN inbound {Type} from {From} to {To}: {Text}",
                envelope.Type, envelope.From, envelope.To, envelope.Text);
        };
        if (tasks is not null && registry is not null && providers is not null && executor is not null)
        {
            var options = Services.GetRequiredService<OpenAgentOptions>();
            remoteCommands = new RemoteCommandService(
                tasks,
                executor,
                options,
                transport,
                transport.SelfId,
                pairing,
                GateRemoteApprovalAsync,
                Services.GetRequiredService<ILogger<RemoteCommandService>>());
            AgentHost.Register(new AgentHostAdapter(
                tasks, registry, transport, providers, options));
        }
    }

    private static RemoteCommandService? remoteCommands;

    private const long ReplayWindowMs = 5 * 60_000;

    /// <summary>
    /// The PIN a LAN peer must reproduce on its own screen (docs/protocol.md
    /// §4.2). Accepting answers the pair_challenge; declining tells the state
    /// machine to forget the request — the phone stays unpaired either way.
    /// </summary>
    private static void ShowPairingDialog(string pin, PairingService pairing)
    {
        var window = MainWindow;
        if (window?.Content?.XamlRoot is null)
        {
            pairing.DeclinePairing();
            return;
        }

        window.DispatcherQueue.TryEnqueue(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = "配对请求",
                Content = $"收到新的配对请求。\n\n在手机上输入此配对码：\n{pin}",
                PrimaryButtonText = "同意配对",
                CloseButtonText = "拒绝",
                XamlRoot = window.Content.XamlRoot,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await pairing.AcceptPairingAsync();
            }
            else
            {
                pairing.DeclinePairing();
            }
        });
    }

    /// <summary>
    /// Marshals a LAN approval onto the window's dispatcher: the phone's 批准
    /// flow shows the same card a typed prompt does, and the waiting remote run
    /// resumes when the card resolves.
    /// </summary>
    private static Task<(bool Approved, string? ApprovalId)> GateRemoteApprovalAsync(
        RemoteApprovalRequest request)
    {
        var completion = new TaskCompletionSource<(bool Approved, string? ApprovalId)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var window = MainWindow;
        if (window is null)
        {
            completion.SetResult((false, null));
            return completion.Task;
        }

        window.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                completion.SetResult(await window.GateRemoteApprovalAsync(request));
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        return completion.Task;
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
        return UiSettings.Get(ThemeSettingKey) switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    /// <summary>
    /// Themes the tree only. Persisting is the settings page's job: this also runs
    /// for the <c>--theme=</c> screenshot switch, and a screenshot must not rewrite
    /// what the user chose.
    /// </summary>
    public static void ApplyTheme(ElementTheme theme)
    {
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
