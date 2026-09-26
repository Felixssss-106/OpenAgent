using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Shared.Protocol;
using OpenAgent.Tools.Internal;

namespace OpenAgent.Tools.Files;

/// <summary>
/// Deletes a file or a directory tree. High risk and irreversible: it is on the
/// never-auto-approve list, so every permission mode asks first
/// (spec sections 18, 42, 99).
/// </summary>
public sealed class FileDeleteTool : ITool
{
    public ToolDefinition Definition { get; } = new()
    {
        Id = "file.delete",
        Name = "Delete file or directory",
        Description = "Permanently deletes a file, or a directory tree when recursive is true.",
        InputSchemaJson = """
        {"type":"object","properties":{"path":{"type":"string"},"recursive":{"type":"boolean"}},"required":["path"]}
        """,
        Risk = RiskLevel.High,
        Permissions = new[] { "file.delete" },
        Reversible = false,
        RequiresApproval = true,
    };

    public Task<ToolResult> ExecuteAsync(
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rawPath = ToolArguments.ReadString(arguments, "path");
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return Task.FromResult(FileToolFailures.MissingArgument("path"));
        }

        var resolved = ToolPath.Resolve(rawPath, context);
        if (!resolved.IsAllowed)
        {
            return Task.FromResult(FileToolFailures.PathRejected(resolved.Reason));
        }

        var path = resolved.NormalizedPath!;
        var recursive = ToolArguments.ReadBoolean(arguments, "recursive");

        var isDirectory = Directory.Exists(path);
        if (!isDirectory && !File.Exists(path))
        {
            return Task.FromResult(FileToolFailures.NotFound(path));
        }

        try
        {
            if (isDirectory)
            {
                // An explicit non-recursive delete must never silently remove a
                // populated directory — that is the classic data-loss bug.
                if (!recursive && Directory.EnumerateFileSystemEntries(path).Any())
                {
                    return Task.FromResult(FileToolFailures.DirectoryNotEmpty(path));
                }

                Directory.Delete(path, recursive);
            }
            else
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (FileToolFailures.IsIoFailure(ex))
        {
            return Task.FromResult(FileToolFailures.IoFailure(ex));
        }

        return Task.FromResult(ToolResult.Ok(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["path"] = path,
            ["kind"] = isDirectory ? "directory" : "file",
            ["recursive"] = recursive,
            ["deleted"] = true,
        })));
    }
}
