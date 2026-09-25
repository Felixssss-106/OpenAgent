namespace OpenAgent.Security;

public enum PathRejection
{
    None = 0,
    Empty = 1,
    Invalid = 2,
    Traversal = 3,
    UncPath = 4,
    DevicePath = 5,
    BlockedRoot = 6,
    OutsideSandbox = 7,
}

public sealed record PathValidationResult(bool IsAllowed, string? NormalizedPath, PathRejection Reason)
{
    public static PathValidationResult Ok(string normalized) =>
        new(true, normalized, PathRejection.None);

    public static PathValidationResult Reject(PathRejection reason) =>
        new(false, null, reason);

    public string Describe() => Reason switch
    {
        PathRejection.None => "ok",
        PathRejection.Empty => "路径为空",
        PathRejection.Invalid => "路径格式无效",
        PathRejection.Traversal => "路径包含上级目录跳转（..）",
        PathRejection.UncPath => "不允许访问网络共享路径",
        PathRejection.DevicePath => "不允许设备路径",
        PathRejection.BlockedRoot => "该目录是受保护的系统目录",
        PathRejection.OutsideSandbox => "该路径不在允许目录内",
        _ => "路径不被允许",
    };
}

/// <summary>
/// Path gate every file tool must pass before touching the disk
/// (spec sections 39, 40, 241). Normalises first, then classifies — a string
/// blacklist alone is not a security boundary (spec section 42).
/// </summary>
public static class PathPolicy
{
    public static PathValidationResult Validate(string? path, PathPolicyOptions? options = null)
    {
        var opts = options ?? PathPolicyOptions.Default;

        if (string.IsNullOrWhiteSpace(path))
        {
            return PathValidationResult.Reject(PathRejection.Empty);
        }

        var trimmed = path.Trim().Trim('"');

        if (trimmed.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            trimmed.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return PathValidationResult.Reject(PathRejection.DevicePath);
        }

        if (trimmed.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return PathValidationResult.Reject(PathRejection.UncPath);
        }

        // Reject ".." explicitly instead of silently resolving it — an Agent
        // asking for ..\..\Windows is not a path we want to guess about.
        var segments = trimmed.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Array.IndexOf(segments, "..") >= 0)
        {
            return PathValidationResult.Reject(PathRejection.Traversal);
        }

        string full;
        try
        {
            full = Path.GetFullPath(trimmed);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PathValidationResult.Reject(PathRejection.Invalid);
        }

        foreach (var root in opts.BlockedRoots)
        {
            if (IsUnder(full, root))
            {
                return PathValidationResult.Reject(PathRejection.BlockedRoot);
            }
        }

        if (opts.AllowedRoots.Count > 0)
        {
            var inside = false;
            foreach (var root in opts.AllowedRoots)
            {
                if (IsUnder(full, root))
                {
                    inside = true;
                    break;
                }
            }

            if (!inside)
            {
                return PathValidationResult.Reject(PathRejection.OutsideSandbox);
            }
        }

        return PathValidationResult.Ok(full);
    }

    private static bool IsUnder(string candidate, string root)
    {
        if (string.IsNullOrEmpty(root))
        {
            return false;
        }

        var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        return a.Equals(b, StringComparison.OrdinalIgnoreCase)
            || a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class PathPolicyOptions
{
    public IReadOnlyList<string> BlockedRoots { get; init; } = DefaultBlockedRoots();

    /// <summary>Empty means no folder sandbox (spec section 241 is opt-in).</summary>
    public IReadOnlyList<string> AllowedRoots { get; init; } = Array.Empty<string>();

    public static PathPolicyOptions Default { get; } = new();

    private static List<string> DefaultBlockedRoots()
    {
        var roots = new List<string>();
        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.Windows,
                     Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.ProgramFilesX86,
                     Environment.SpecialFolder.CommonApplicationData,
                     Environment.SpecialFolder.System,
                     Environment.SpecialFolder.SystemX86,
                 })
        {
            var value = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(value))
            {
                roots.Add(value);
            }
        }

        return roots;
    }
}
