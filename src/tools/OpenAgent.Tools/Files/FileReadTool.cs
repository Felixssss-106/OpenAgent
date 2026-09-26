using System.Text;
using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Security;
using OpenAgent.Shared.Protocol;
using OpenAgent.Tools.Internal;

namespace OpenAgent.Tools.Files;

/// <summary>
/// Reads a UTF-8 text file, capped so a 4&nbsp;GB log cannot be streamed into a
/// model context (spec sections 39, 150).
/// </summary>
public sealed class FileReadTool : ITool
{
    private const int DefaultMaxBytes = 262144;

    private const int HardMaxBytes = 1048576;

    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    public ToolDefinition Definition { get; } = new()
    {
        Id = "file.read",
        Name = "Read text file",
        Description = "Reads a UTF-8 text file, truncated at maxBytes.",
        InputSchemaJson = """
        {"type":"object","properties":{"path":{"type":"string"},"maxBytes":{"type":"integer"}},"required":["path"]}
        """,
        Risk = RiskLevel.Safe,
        Permissions = new[] { "file.read" },
        Reversible = true,
        RequiresApproval = false,
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

        var resolved = ToolPath.Resolve(rawPath, context);
        if (!resolved.IsAllowed)
        {
            return FileToolFailures.PathRejected(resolved.Reason);
        }

        var path = resolved.NormalizedPath!;
        if (Directory.Exists(path))
        {
            return FileToolFailures.WrongKind("目标是目录，不是文件");
        }

        if (!File.Exists(path))
        {
            return FileToolFailures.NotFound(path);
        }

        var requestedMaxBytes = ToolArguments.ReadInt32(arguments, "maxBytes") ?? DefaultMaxBytes;
        var maxBytes = Math.Clamp(requestedMaxBytes, 1, HardMaxBytes);

        long fileLength;
        try
        {
            fileLength = new FileInfo(path).Length;
        }
        catch (Exception ex) when (FileToolFailures.IsIoFailure(ex))
        {
            return FileToolFailures.IoFailure(ex);
        }

        try
        {
            // maxBytes + 1 so a file that is exactly at the limit still reports
            // truncation correctly.
            var buffer = new byte[maxBytes + 1];
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 81920,
                useAsync: true);

            var read = await ReadUpToAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
            var truncated = read > maxBytes || stream.Position < fileLength;
            var slice = buffer.AsSpan(0, Math.Min(read, maxBytes));

            if (slice.IndexOf((byte)0) >= 0)
            {
                return FileToolFailures.WrongKind("不是文本文件（内容包含二进制字节）");
            }

            var bomLength = slice.StartsWith(Utf8Bom) ? Utf8Bom.Length : 0;
            var content = Encoding.UTF8.GetString(slice[bomLength..]);

            return ToolResult.Ok(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["path"] = path,
                ["sizeBytes"] = fileLength,
                ["readBytes"] = slice.Length - bomLength,
                ["truncated"] = truncated,
                ["encoding"] = "utf-8",
                ["content"] = SecretRedactor.Redact(content),
            }));
        }
        catch (Exception ex) when (FileToolFailures.IsIoFailure(ex))
        {
            return FileToolFailures.IoFailure(ex);
        }
    }

    /// <summary>Fills <paramref name="buffer"/> unless the stream ends first.</summary>
    private static async Task<int> ReadUpToAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
