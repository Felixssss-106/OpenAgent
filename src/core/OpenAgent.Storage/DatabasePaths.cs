namespace OpenAgent.Storage;

/// <summary>
/// Everything lives under one user-owned directory, never in the install
/// directory (spec section 210).
/// </summary>
public sealed class DatabasePaths
{
    public DatabasePaths()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenAgent"))
    {
    }

    public DatabasePaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }

    public string DatabaseFile => Path.Combine(Root, "openagent.sqlite");

    public string LogsDirectory => Path.Combine(Root, "Logs");

    public string SessionsDirectory => Path.Combine(Root, "Sessions");

    public string PluginsDirectory => Path.Combine(Root, "Plugins");

    public string DownloadsDirectory => Path.Combine(Root, "Downloads");

    public string CacheDirectory => Path.Combine(Root, "Cache");

    public string BackupsDirectory => Path.Combine(Root, "Backups");

    public void EnsureCreated()
    {
        foreach (var directory in new[]
                 {
                     Root,
                     LogsDirectory,
                     SessionsDirectory,
                     PluginsDirectory,
                     DownloadsDirectory,
                     CacheDirectory,
                     BackupsDirectory,
                 })
        {
            Directory.CreateDirectory(directory);
        }
    }
}
