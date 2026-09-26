using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Security;
using OpenAgent.Shared.Protocol;
using OpenAgent.Tools.Internal;

namespace OpenAgent.Tools.Files;

/// <summary>
/// Read-only directory listing (spec sections 38, 39). Entries pass through
/// <see cref="SecretRedactor"/> because file names routinely carry tokens.
/// </summary>
public sealed class FileListTool : ITool
{
    private const int DefaultLimit = 200;
    private const int MaxLimit = 1000;

    /// <summary>Upper bound on entries touched by one call, so a recursive walk cannot run away.</summary>
    private const int MaxScannedEntries = 20000;

    public ToolDefinition Definition { get; } = new()
    {
        Id = "file.list",
        Name = "List directory",
        Description = "Lists files and directories under a path.",
        InputSchemaJson = """
        {"type":"object","properties":{"path":{"type":"string"},"pattern":{"type":"string"},"recursive":{"type":"boolean"},"limit":{"type":"integer"}},"required":[]}
        """,
        Risk = RiskLevel.Safe,
        Permissions = new[] { "file.read" },
        Reversible = true,
        RequiresApproval = false,
    };

    public Task<ToolResult> ExecuteAsync(
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = ToolPath.Resolve(ToolArguments.ReadString(arguments, "path"), context);
        if (!resolved.IsAllowed)
        {
            return Task.FromResult(FileToolFailures.PathRejected(resolved.Reason));
        }

        var directory = resolved.NormalizedPath!;
        if (!Directory.Exists(directory))
        {
            return Task.FromResult(FileToolFailures.NotFound(directory));
        }

        var pattern = ToolArguments.ReadString(arguments, "pattern");
        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "*";
        }

        var recursive = ToolArguments.ReadBoolean(arguments, "recursive");
        var requestedLimit = ToolArguments.ReadInt32(arguments, "limit") ?? DefaultLimit;
        var limit = Math.Clamp(requestedLimit, 1, MaxLimit);

        var entries = new List<Dictionary<string, object?>>();
        var total = 0;

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
                ReturnSpecialDirectories = false,
            };

            foreach (var entry in Directory.EnumerateFileSystemEntries(directory, pattern, options))
            {
                cancellationToken.ThrowIfCancellationRequested();

                total++;
                if (entries.Count >= limit || total > MaxScannedEntries)
                {
                    continue;
                }

                entries.Add(Describe(entry));
            }
        }
        catch (Exception ex) when (FileToolFailures.IsIoFailure(ex))
        {
            return Task.FromResult(FileToolFailures.IoFailure(ex));
        }

        return Task.FromResult(ToolResult.Ok(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["path"] = directory,
            ["pattern"] = pattern,
            ["recursive"] = recursive,
            ["total"] = total,
            ["returned"] = entries.Count,
            ["truncated"] = total > entries.Count,
            ["entries"] = entries,
        })));
    }

    private static Dictionary<string, object?> Describe(string entry)
    {
        var name = Path.GetFileName(entry);
        var isDirectory = Directory.Exists(entry);

        long size = 0;
        DateTimeOffset lastWriteUtc = default;
        if (isDirectory)
        {
            var info = new DirectoryInfo(entry);
            lastWriteUtc = info.LastWriteTimeUtc;
        }
        else
        {
            var info = new FileInfo(entry);
            size = info.Length;
            lastWriteUtc = info.LastWriteTimeUtc;
        }

        return new Dictionary<string, object?>
        {
            ["name"] = SecretRedactor.Redact(name),
            ["path"] = SecretRedactor.Redact(entry),
            ["type"] = isDirectory ? "directory" : "file",
            ["sizeBytes"] = size,
            ["lastWriteUtc"] = lastWriteUtc,
        };
    }
}
