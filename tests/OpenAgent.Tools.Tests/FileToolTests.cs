using System.Text;
using System.Text.Json;
using OpenAgent.Security;
using OpenAgent.Tools;
using OpenAgent.Tools.Files;

namespace OpenAgent.Tools.Tests;

/// <summary>
/// File tools: happy paths, argument validation and the path gate
/// (spec sections 39, 40). Every test runs inside a throw-away directory.
/// </summary>
public sealed class FileToolTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "openagent-file-tests",
        Guid.NewGuid().ToString("N"));

    private readonly ToolContext _context;

    public FileToolTests()
    {
        Directory.CreateDirectory(_root);
        _context = new ToolContext
        {
            PermissionMode = PermissionMode.AskBeforeActions,
            WorkingDirectory = _root,
        };
    }

    public void Dispose() => TryDeleteDirectory(_root);

    [Fact]
    public async Task List_returns_the_files_it_just_saw_written()
    {
        await WriteAsync("a.txt", "alpha");
        await WriteAsync("b.txt", "beta");

        var result = await new FileListTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(_root)}\"}}"), CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        var data = result.Data!.Value;
        Assert.Equal(2, data.GetProperty("total").GetInt32());

        var names = data.GetProperty("entries")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("a.txt", names);
        Assert.Contains("b.txt", names);
    }

    [Fact]
    public async Task List_is_limited_and_reports_truncation()
    {
        await WriteAsync("a.txt", "1");
        await WriteAsync("b.txt", "2");
        await WriteAsync("c.txt", "3");

        var result = await new FileListTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(_root)}\",\"limit\":2}}"), CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        var data = result.Data!.Value;
        Assert.Equal(2, data.GetProperty("returned").GetInt32());
        Assert.Equal(3, data.GetProperty("total").GetInt32());
        Assert.True(data.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task List_accepts_a_relative_path_from_the_working_directory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));

        var result = await new FileListTool().ExecuteAsync(
            _context, Args("{\"path\":\"sub\"}"), CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
    }

    [Fact]
    public async Task Every_file_tool_refuses_a_traversal_path()
    {
        var args = Args($"{{\"path\":\"{Json(@"..\..\Windows\System32\drivers\etc\hosts")}\"}}");

        await AssertRejectedAsync(new FileListTool(), args);
        await AssertRejectedAsync(new FileReadTool(), args);
        await AssertRejectedAsync(new FileDeleteTool(), args);
    }

    [Fact]
    public async Task Every_file_tool_refuses_a_unc_path()
    {
        var args = Args("{\"path\":\"\\\\\\\\server\\\\share\\\\file.txt\"}");

        await AssertRejectedAsync(new FileListTool(), args);
        await AssertRejectedAsync(new FileReadTool(), args);
        await AssertRejectedAsync(new FileDeleteTool(), args);
    }

    [Fact]
    public async Task Read_returns_redacted_content()
    {
        var path = Path.Combine(_root, "secret.txt");
        await File.WriteAllTextAsync(path, "api_key = sk-abcdefgh12345678\nhello", Encoding.UTF8);

        var result = await new FileReadTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(path)}\"}}"), CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        var data = result.Data!.Value;
        Assert.Equal("hello", data.GetProperty("content").GetString()!.Split('\n').Last().Trim());
        Assert.Contains("***", data.GetProperty("content").GetString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-abcdefgh12345678", data.GetProperty("content").GetString()!,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_rejects_binary_content()
    {
        var path = Path.Combine(_root, "blob.bin");
        await File.WriteAllBytesAsync(path, new byte[] { 0x41, 0x00, 0x42, 0x00 });

        var result = await new FileReadTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(path)}\"}}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(OpenAgent.Core.Error.ErrorCodes.FileError(6), result.Error!.Code);
    }

    [Fact]
    public async Task Read_reports_truncation_when_the_file_is_larger_than_max_bytes()
    {
        var path = Path.Combine(_root, "long.txt");
        await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(new string('x', 5000)));

        var result = await new FileReadTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(path)}\",\"maxBytes\":10}}"), CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        var data = result.Data!.Value;
        Assert.True(data.GetProperty("truncated").GetBoolean());
        Assert.Equal(10, data.GetProperty("readBytes").GetInt32());
    }

    [Fact]
    public async Task Write_creates_a_file_and_append_extends_it()
    {
        var path = Path.Combine(_root, "note.txt");

        var first = await new FileWriteTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(path)}\",\"content\":\"one\"}}"), CancellationToken.None);
        Assert.True(first.Success, first.Error?.Message);
        Assert.True(first.Data!.Value.GetProperty("created").GetBoolean());

        var second = await new FileWriteTool().ExecuteAsync(
            _context,
            Args($"{{\"path\":\"{Json(path)}\",\"content\":\"two\",\"append\":true}}"),
            CancellationToken.None);
        Assert.True(second.Success, second.Error?.Message);

        Assert.Equal("onetwo", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Write_can_create_missing_directories()
    {
        var path = Path.Combine(_root, "nested", "deeper", "note.txt");

        var result = await new FileWriteTool().ExecuteAsync(
            _context,
            Args($"{{\"path\":\"{Json(path)}\",\"content\":\"x\",\"createDirectories\":true}}"),
            CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Write_requires_content()
    {
        var result = await new FileWriteTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(Path.Combine(_root, "x.txt"))}\"}}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(OpenAgent.Core.Error.ErrorCodes.ToolError(2), result.Error!.Code);
    }

    [Fact]
    public async Task Write_refuses_a_system_directory()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var path = Path.Combine(windows, "openagent-should-not-exist.txt");

        var result = await new FileWriteTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(path)}\",\"content\":\"x\"}}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(OpenAgent.Core.Error.ErrorCodes.FileError(1), result.Error!.Code);
    }

    [Fact]
    public async Task Move_relocates_a_file()
    {
        var source = Path.Combine(_root, "source.txt");
        var destination = Path.Combine(_root, "moved.txt");
        await File.WriteAllTextAsync(source, "payload", Encoding.UTF8);

        var result = await new FileMoveTool().ExecuteAsync(
            _context,
            Args($"{{\"source\":\"{Json(source)}\",\"destination\":\"{Json(destination)}\"}}"),
            CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        Assert.False(File.Exists(source));
        Assert.Equal("payload", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task Move_refuses_to_overwrite_without_permission()
    {
        var source = Path.Combine(_root, "s.txt");
        var destination = Path.Combine(_root, "d.txt");
        await File.WriteAllTextAsync(source, "s", Encoding.UTF8);
        await File.WriteAllTextAsync(destination, "d", Encoding.UTF8);

        var result = await new FileMoveTool().ExecuteAsync(
            _context,
            Args($"{{\"source\":\"{Json(source)}\",\"destination\":\"{Json(destination)}\"}}"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(OpenAgent.Core.Error.ErrorCodes.FileError(3), result.Error!.Code);
        Assert.Equal("s", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task Move_fails_when_the_source_is_missing()
    {
        var result = await new FileMoveTool().ExecuteAsync(
            _context,
            Args($"{{\"source\":\"{Json(Path.Combine(_root, "nope.txt"))}\",\"destination\":\"{Json(Path.Combine(_root, "d.txt"))}\"}}"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(OpenAgent.Core.Error.ErrorCodes.FileError(2), result.Error!.Code);
    }

    [Fact]
    public async Task Copy_duplicates_a_file()
    {
        var source = Path.Combine(_root, "origin.txt");
        var destination = Path.Combine(_root, "copy.txt");
        await File.WriteAllTextAsync(source, "payload", Encoding.UTF8);

        var result = await new FileCopyTool().ExecuteAsync(
            _context,
            Args($"{{\"source\":\"{Json(source)}\",\"destination\":\"{Json(destination)}\"}}"),
            CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("payload", await File.ReadAllTextAsync(destination));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task Copy_needs_the_recursive_flag_for_a_directory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "tree"));
        await File.WriteAllTextAsync(Path.Combine(_root, "tree", "leaf.txt"), "leaf", Encoding.UTF8);

        var plain = await new FileCopyTool().ExecuteAsync(
            _context,
            Args($"{{\"source\":\"{Json(Path.Combine(_root, "tree"))}\",\"destination\":\"{Json(Path.Combine(_root, "tree-copy"))}\"}}"),
            CancellationToken.None);
        Assert.False(plain.Success);
        Assert.Equal(OpenAgent.Core.Error.ErrorCodes.FileError(8), plain.Error!.Code);

        var recursive = await new FileCopyTool().ExecuteAsync(
            _context,
            Args($"{{\"source\":\"{Json(Path.Combine(_root, "tree"))}\",\"destination\":\"{Json(Path.Combine(_root, "tree-copy"))}\",\"recursive\":true}}"),
            CancellationToken.None);
        Assert.True(recursive.Success, recursive.Error?.Message);
        Assert.Equal("leaf", await File.ReadAllTextAsync(Path.Combine(_root, "tree-copy", "leaf.txt")));
    }

    [Fact]
    public async Task Delete_removes_a_file()
    {
        var path = Path.Combine(_root, "doomed.txt");
        await File.WriteAllTextAsync(path, "bye", Encoding.UTF8);

        var result = await new FileDeleteTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(path)}\"}}"), CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Delete_protects_a_populated_directory()
    {
        var directory = Path.Combine(_root, "populated");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "keep.txt"), "keep", Encoding.UTF8);

        var refused = await new FileDeleteTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(directory)}\"}}"), CancellationToken.None);
        Assert.False(refused.Success);
        Assert.Equal(OpenAgent.Core.Error.ErrorCodes.FileError(7), refused.Error!.Code);
        Assert.True(File.Exists(Path.Combine(directory, "keep.txt")));

        var forced = await new FileDeleteTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(directory)}\",\"recursive\":true}}"), CancellationToken.None);
        Assert.True(forced.Success, forced.Error?.Message);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task Delete_fails_cleanly_for_a_missing_path()
    {
        var result = await new FileDeleteTool().ExecuteAsync(
            _context, Args($"{{\"path\":\"{Json(Path.Combine(_root, "ghost.txt"))}\"}}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(OpenAgent.Core.Error.ErrorCodes.FileError(2), result.Error!.Code);
    }

    [Fact]
    public async Task Delete_refuses_a_device_path()
    {
        var result = await new FileDeleteTool().ExecuteAsync(
            _context, Args("{\"path\":\"\\\\\\\\?\\\\C:\\\\Windows\"}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(OpenAgent.Core.Error.ErrorCodes.FileError(1), result.Error!.Code);
    }

    private async Task WriteAsync(string name, string content)
    {
        var path = Path.Combine(_root, name);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8);
    }

    private async Task AssertRejectedAsync(ITool tool, JsonElement args)
    {
        var result = await tool.ExecuteAsync(_context, args, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(OpenAgent.Core.Error.ErrorCodes.FileError(1), result.Error!.Code);
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>Embeds a Windows path in JSON: forward slashes, so no escaping is needed.</summary>
    private static string Json(string value) => value.Replace('\\', '/');

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory must never fail a test run.
        }
    }
}
