using Microsoft.Win32;

namespace OpenAgent.Windows.UI.Services;

/// <summary>
/// 开机启动 for an unpackaged WinUI 3 app: there is no package identity, so
/// <c>StartupTask</c> is unreachable and the only switch Windows honours is the
/// per-user Run key. The key itself is the single source of truth — the settings
/// row reads it back on every load, so an entry removed by hand shows 已关闭.
/// </summary>
public static class StartupRegistrar
{
    private const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OpenAgent";

    public static bool IsEnabled() => IsEnabled(DefaultRunKeyPath);

    public static void Enable() => Enable(DefaultRunKeyPath);

    public static void Disable() => Disable(DefaultRunKeyPath);

    /// <summary>Injectable key path: tests exercise the logic on a scratch key
    /// instead of the machine's real autorun entry.</summary>
    public static bool IsEnabled(string runKeyPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath);
        return key?.GetValue(ValueName) is string;
    }

    public static void Enable(string runKeyPath)
    {
        var exe = Environment.ProcessPath
            ?? throw new InvalidOperationException("executable path is unknown; cannot register startup");
        using var key = Registry.CurrentUser.CreateSubKey(runKeyPath);
        key.SetValue(ValueName, $"\"{exe}\"");
    }

    public static void Disable(string runKeyPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
