using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OpenAgent.Windows.UI.Services;

/// <summary>
/// Key/value store for the shell's own preferences.
///
/// An unpackaged WinUI 3 app has no package identity, so
/// <c>ApplicationData.Current</c> throws on every access and
/// <c>LocalSettings</c> is unreachable — the theme and reasoning-effort pickers
/// saved into a catch block and came back at their default after a restart.
/// The app already owns one reliable directory, so the settings live beside it.
/// </summary>
public static class UiSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenAgent",
        "ui-settings.json");

    private static Dictionary<string, string>? _values;

    public static string? Get(string key)
    {
        return Load().TryGetValue(key, out var value) ? value : null;
    }

    public static void Set(string key, string value)
    {
        var values = Load();
        values[key] = value;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(values));
        }
        catch (Exception ex)
        {
            // A read-only or full profile means the preference is lost on
            // restart, not that the running session is wrong.
            System.Diagnostics.Debug.WriteLine($"UiSettings save failed: {ex.Message}");
        }
    }

    private static Dictionary<string, string> Load()
    {
        if (_values is not null)
        {
            return _values;
        }

        try
        {
            if (File.Exists(FilePath))
            {
                _values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath))
                    ?? new Dictionary<string, string>();
                return _values;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UiSettings load failed: {ex.Message}");
        }

        _values = new Dictionary<string, string>();
        return _values;
    }
}
