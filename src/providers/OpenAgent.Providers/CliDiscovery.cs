using System.Diagnostics;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

namespace OpenAgent.Providers;

/// <summary>A recognized external agent CLI and where it was found.</summary>
public sealed record DiscoveredCli(string Id, string Executable, string? Version, string Path);

/// <summary>
/// Scans the machine for known agent CLIs (Codex, Claude Code, OpenCode, Pi,
/// Gemini). Discovery only — never installs, never hardcodes flags. The spec
/// (sections 24–26) says: find, identify, verify; driving happens in each
/// CLI's own adapter, written when its CLI is actually present.
/// </summary>
public static class CliDiscovery
{
    /// <summary>
    /// The known CLI table: provider id → executable names to look up. Keeping
    /// the table here means the discovery loop and its tests share one source.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> KnownClis = new Dictionary<string, string[]>
    {
        ["codex"] = new[] { "codex", "codex.exe" },
        ["claude"] = new[] { "claude", "claude.exe" },
        ["opencode"] = new[] { "opencode", "opencode.exe" },
        ["pi"] = new[] { "pi", "pi.exe" },
        ["gemini"] = new[] { "gemini", "gemini.exe" },
    };

    /// <summary>
    /// Walks PATH (<c>where.exe</c>) for each known CLI and, when one is found,
    /// asks it for <c>--version</c>. Nothing is installed; no CLI is driven
    /// beyond a version probe (spec section 25, 26).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<DiscoveredCli> Scan(CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // where.exe is Windows-only; other platforms get nothing here and
            // their own discovery path later (spec section 25 lists Windows
            // sources only for now).
            return Array.Empty<DiscoveredCli>();
        }

        var found = new List<DiscoveredCli>();
        foreach (var (id, executables) in KnownClis)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            foreach (var exe in executables)
            {
                var path = Locate(exe);
                if (path is null)
                {
                    continue;
                }

                var version = ProbeVersion(path, cancellationToken);
                found.Add(new DiscoveredCli(id, exe, version, path));
                break;
            }
        }

        return found;
    }

    /// <summary>
    /// Parses the first version-looking token out of a <c>--version</c> line.
    /// Exported so the parse path is unit-testable without spawning a process.
    /// </summary>
    public static string? ParseVersion(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        // Lines like "codex 0.1.2" or "claude 1.4.0 (commit abc)". Take the
        // first token that looks like major.minor[.patch].
        foreach (var token in output.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Contains('.'))
            {
                var candidate = token.Trim('v', 'V', '(', ')');
                var parts = candidate.Split('.');
                if (parts.Length >= 2 && parts.All(p => p.Length > 0 && p.All(char.IsDigit)))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string? Locate(string exe)
    {
        try
        {
            var info = new ProcessStartInfo("where.exe")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var name in new[] { exe })
            {
                info.ArgumentList.Add(name);
            }

            using var process = Process.Start(info);
            if (process is null)
            {
                return null;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);
            if (process.ExitCode != 0)
            {
                return null;
            }

            var first = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return first.Length == 0 ? null : first[0].Trim();
        }
        catch
        {
            // where.exe missing or the exe is not on PATH — not an error here.
            return null;
        }
    }

    private static string? ProbeVersion(string path, CancellationToken cancellationToken)
    {
        try
        {
            var info = new ProcessStartInfo(path)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var flag in new[] { "--version" })
            {
                info.ArgumentList.Add(flag);
            }

            using var process = Process.Start(info);
            if (process is null)
            {
                return null;
            }

            // A version probe must not hang the discovery loop forever.
            if (!process.WaitForExit(3000))
            {
                try { process.Kill(); } catch { }
                return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            return ParseVersion(stdout);
        }
        catch
        {
            // The CLI exists but --version is unsupported or fails — still
            // discovered, just with an unknown version (spec section 26).
            return null;
        }
    }
}
