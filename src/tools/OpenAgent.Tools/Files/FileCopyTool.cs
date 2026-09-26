using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Shared.Protocol;
using OpenAgent.Tools.Internal;

namespace OpenAgent.Tools.Files;

/// <summary>
/// Copies a file, or a directory tree when <c>recursive</c> is set
/// (spec sections 38, 39).
/// </summary>
public sealed class FileCopyTool : ITool
{
    /// <summary>Upper bound on entries one copy call may create.</summary>
    private const int MaxCopiedEntries = 10000;

    public ToolDefinition Definition { get; } = new()
    {
        Id = "file.copy",
        Name = "Copy file or directory",
        Description = "Copies a file, or a directory tree when recursive is true.",
        InputSchemaJson = """
        {"type":"object","properties":{"source":{"type":"string"},"destination":{"type":"string"},"overwrite":{"type":"boolean"},"recursive":{"type":"boolean"}},"required":["source","destination"]}
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
        var recursive = ToolArguments.ReadBoolean(arguments, "recursive");

        var sourceIsDirectory = Directory.Exists(sourcePath);
        if (!sourceIsDirectory && !File.Exists(sourcePath))
        {
            return Task.FromResult(FileToolFailures.NotFound(sourcePath));
        }

        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(FileToolFailures.InvalidArgument("源路径与目标路径相同"));
        }

        var copiedFiles = 0;

        try
        {
            if (sourceIsDirectory)
            {
                if (!recursive)
                {
                    return Task.FromResult(FileToolFailures.Unsupported("复制目录需要 recursive=true"));
                }

                if (File.Exists(destinationPath))
                {
                    return Task.FromResult(FileToolFailures.AlreadyExists(destinationPath));
                }

                CopyDirectory(sourcePath, destinationPath, overwrite, cancellationToken, ref copiedFiles);
            }
            else
            {
                File.Copy(sourcePath, destinationPath, overwrite);
                copiedFiles = 1;
            }
        }
        catch (Exception ex) when (FileToolFailures.IsIoFailure(ex))
        {
            return Task.FromResult(FileToolFailures.IoFailure(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(FileToolFailures.TooLarge($"复制内容过多：{ex.Message}"));
        }

        return Task.FromResult(ToolResult.Ok(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["source"] = sourcePath,
            ["destination"] = destinationPath,
            ["kind"] = sourceIsDirectory ? "directory" : "file",
            ["copiedFiles"] = copiedFiles,
            ["overwrite"] = overwrite,
        })));
    }

    private static void CopyDirectory(
        string source,
        string destination,
        bool overwrite,
        CancellationToken cancellationToken,
        ref int copiedFiles)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (copiedFiles >= MaxCopiedEntries)
            {
                throw new InvalidOperationException($"超过 {MaxCopiedEntries} 个文件的上限");
            }

            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite);
            copiedFiles++;
        }

        foreach (var child in Directory.EnumerateDirectories(source))
        {
            cancellationToken.ThrowIfCancellationRequested();

            CopyDirectory(child, Path.Combine(destination, Path.GetFileName(child)), overwrite, cancellationToken,
                ref copiedFiles);
        }
    }
}
