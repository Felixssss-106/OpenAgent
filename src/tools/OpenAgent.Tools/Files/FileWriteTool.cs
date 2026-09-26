using System.Text;
using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Shared.Protocol;
using OpenAgent.Tools.Internal;

namespace OpenAgent.Tools.Files;

/// <summary>
/// Writes a text file. Low risk, but it overwrites what was there and
/// OpenAgent keeps no backup, so the approval card must say it cannot be
/// undone (spec sections 18, 19, 39).
/// </summary>
public sealed class FileWriteTool : ITool
{
    /// <summary>Guard against a model pasting an enormous blob into a file.</summary>
    private const int MaxContentChars = 4 * 1024 * 1024;

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public ToolDefinition Definition { get; } = new()
    {
        Id = "file.write",
        Name = "Write text file",
        Description = "Writes or appends UTF-8 text to a file.",
        InputSchemaJson = """
        {"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"},"append":{"type":"boolean"},"createDirectories":{"type":"boolean"}},"required":["path","content"]}
        """,
        Risk = RiskLevel.Low,
        Permissions = new[] { "file.write" },
        Reversible = false,
        RequiresApproval = true,
    };

    public async Task<ToolResult> ExecuteAsync(
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rawPath = ToolArguments.ReadString(arguments, "path");
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return FileToolFailures.MissingArgument("path");
        }

        var content = ToolArguments.ReadString(arguments, "content");
        if (content is null)
        {
            return FileToolFailures.MissingArgument("content");
        }

        if (content.Length > MaxContentChars)
        {
            return FileToolFailures.TooLarge($"内容超过 {MaxContentChars} 字符上限");
        }

        var resolved = ToolPath.Resolve(rawPath, context);
        if (!resolved.IsAllowed)
        {
            return FileToolFailures.PathRejected(resolved.Reason);
        }

        var path = resolved.NormalizedPath!;
        if (Directory.Exists(path))
        {
            return FileToolFailures.WrongKind("目标是目录，不能写入");
        }

        var append = ToolArguments.ReadBoolean(arguments, "append");
        var createDirectories = ToolArguments.ReadBoolean(arguments, "createDirectories");
        var existed = File.Exists(path);
        var mode = append ? FileMode.Append : FileMode.Create;

        try
        {
            if (createDirectories)
            {
                var parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                {
                    Directory.CreateDirectory(parent);
                }
            }

            var stream = new FileStream(path, mode, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                var writer = new StreamWriter(stream, Utf8WithoutBom);
                await using (writer.ConfigureAwait(false))
                {
                    await writer.WriteAsync(content).ConfigureAwait(false);
                }
            }

            return ToolResult.Ok(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["path"] = path,
                ["bytes"] = Utf8WithoutBom.GetByteCount(content),
                ["append"] = append,
                ["created"] = !existed,
            }));
        }
        catch (Exception ex) when (FileToolFailures.IsIoFailure(ex))
        {
            return FileToolFailures.IoFailure(ex);
        }
    }
}
