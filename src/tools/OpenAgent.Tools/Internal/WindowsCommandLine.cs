using System.Collections.Generic;
using System.Text;

namespace OpenAgent.Tools.Internal;

/// <summary>
/// Splits an arguments tail the way the Windows CRT does (CommandLineToArgvW
/// rules), so app.launch's single "arguments" string becomes explicit argv
/// entries handed to the target through ProcessStartInfo.ArgumentList. The
/// target's command line is rebuilt by the OS from that list — quotes and
/// backslashes in the string can no longer reshape anything after the split.
/// </summary>
public static class WindowsCommandLine
{
    public static string[] Split(string? line)
    {
        var args = new List<string>();
        if (string.IsNullOrEmpty(line))
        {
            return Array.Empty<string>();
        }

        var current = new StringBuilder();
        var inQuotes = false;
        var backslashes = 0;
        var hasToken = false;

        foreach (var ch in line)
        {
            if (ch == '\\')
            {
                backslashes++;
                continue;
            }

            if (ch == '"')
            {
                // 2n backslashes + quote => n backslashes, quote toggles quoting;
                // 2n+1 backslashes + quote => n backslashes and a literal quote.
                current.Append('\\', backslashes / 2);
                if (backslashes % 2 == 0)
                {
                    inQuotes = !inQuotes;
                }
                else
                {
                    current.Append('"');
                }

                backslashes = 0;
                hasToken = true;
                continue;
            }

            // Backslashes before anything but a quote are literal.
            current.Append('\\', backslashes);
            backslashes = 0;

            if ((ch == ' ' || ch == '\t') && !inQuotes)
            {
                if (hasToken || current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }

                continue;
            }

            current.Append(ch);
            hasToken = true;
        }

        current.Append('\\', backslashes);
        if (hasToken || current.Length > 0)
        {
            args.Add(current.ToString());
        }

        return args.ToArray();
    }
}
