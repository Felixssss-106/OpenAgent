using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Shared.Protocol;
using OpenAgent.Tools.Internal;

namespace OpenAgent.Tools.Files;

/// <summary>
/// Moves a file or a directory (spec section 38). Medium risk: the source
/// disappears and the destination may be replaced.
/// </summary>
public sealed class FileMoveTool : ITool
{
    public ToolDefinition Definition { get; } = new()
    {
        Id = "file.move",
        Name = "Move file or directory",
        Description = "Moves a file or directory to a new location.",
        InputSchemaJson = """
        {"type":"object","properties":{"source":{"type":"string"},"destination":{"type":"string"},"overwrite":{"type":"boolean"}},"required":["source","destination"]}
        """,
        Risk = RiskLevel.Medium,
        Permissions = new[] { "file.write" },
        Reversible = false,
        RequiresApproval = true,
    };

    public Task<ToolResult> ExecuteAsync(
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rawSource = ToolArguments.ReadString(arguments, "source");
        if (string.IsNullOrWhiteSpace(rawSource))
        {
            return Task.FromResult(FileToolFailures.MissingArgument("source"));
        }

        var rawDestination = ToolArguments.ReadString(arguments, "destination");
        if (string.IsNullOrWhiteSpace(rawDestination))
        {
            return Task.FromResult(FileToolFailures.MissingArgument("destination"));
        }

        var source = ToolPath.Resolve(rawSource, context);
        if (!source.IsAllowed)
        {
            return Task.FromResult(FileToolFailures.PathRejected(source.Reason));
        }

        var destination = ToolPath.Resolve(rawDestination, context);
        if (!destination.IsAllowed)
        {
            return Task.FromResult(FileToolFailures.PathRejected(destination.Reason));
        }

        var sourcePath = source.NormalizedPath!;
        var destinationPath = destination.NormalizedPath!;
        var overwrite = ToolArguments.ReadBoolean(arguments, "overwrite");

        var sourceIsDirectory = Directory.Exists(sourcePath);
        if (!sourceIsDirectory && !File.Exists(sourcePath))
        {
            return Task.FromResult(FileToolFailures.NotFound(sourcePath));
        }

        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(FileToolFailures.InvalidArgument("源路径与目标路径相同"));
        }

        if (sourceIsDirectory && IsSameOrChild(destinationPath, sourcePath))
        {
            return Task.FromResult(FileToolFailures.Unsupported("不能把目录移动到它自己或其子目录中"));
        }

        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
        {
            if (!overwrite)
            {
                return Task.FromResult(FileToolFailures.AlreadyExists(destinationPath));
            }

            if (Directory.Exists(destinationPath))
            {
                return Task.FromResult(FileToolFailures.Unsupported("目标目录已存在，覆盖目录不被允许"));
            }
        }

        try
        {
            if (sourceIsDirectory)
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                File.Move(sourcePath, destinationPath, overwrite);
            }
        }
        catch (Exception ex) when (FileToolFailures.IsIoFailure(ex))
        {
            return Task.FromResult(FileToolFailures.IoFailure(ex));
        }

        return Task.FromResult(ToolResult.Ok(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["source"] = sourcePath,
            ["destination"] = destinationPath,
            ["kind"] = sourceIsDirectory ? "directory" : "file",
            ["overwrite"] = overwrite,
        })));
    }

    private static bool IsSameOrChild(string candidate, string root)
    {
        var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        return a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
               a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
