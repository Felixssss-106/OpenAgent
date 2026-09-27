using Microsoft.Win32;
using OpenAgent.Windows.UI.Services;
using Xunit;

namespace OpenAgent.Windows.UI.Tests;

/// <summary>
/// The startup toggle's contract, exercised on a scratch key so the machine's
/// real autorun entry is never touched by a test run.
/// </summary>
public sealed class StartupRegistrarTests : IDisposable
{
    private const string ScratchKey = @"Software\OpenAgent\Tests\StartupRegistrarTests";

    public void Dispose()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\OpenAgent\Tests", writable: true);
        key?.DeleteSubKeyTree("StartupRegistrarTests", throwOnMissingSubKey: false);
    }

    [Fact]
    public void IsEnabled_is_false_when_the_key_does_not_exist()
    {
        Clean();
        Assert.False(StartupRegistrar.IsEnabled(ScratchKey));
    }

    [Fact]
    public void Enable_writes_a_quoted_executable_path_and_IsEnabled_reads_it_back()
    {
        Clean();
        StartupRegistrar.Enable(ScratchKey);

        Assert.True(StartupRegistrar.IsEnabled(ScratchKey));
        using var key = Registry.CurrentUser.OpenSubKey(ScratchKey);
        var value = Assert.IsType<string>(key!.GetValue("OpenAgent"));
        Assert.Equal($"\"{Environment.ProcessPath}\"", value);
    }

    [Fact]
    public void Disable_removes_the_entry()
    {
        Clean();
        StartupRegistrar.Enable(ScratchKey);
        StartupRegistrar.Disable(ScratchKey);

        Assert.False(StartupRegistrar.IsEnabled(ScratchKey));
    }

    [Fact]
    public void Disable_on_an_entry_that_is_not_there_does_not_throw()
    {
        Clean();
        StartupRegistrar.Disable(ScratchKey);
    }

    private static void Clean()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\OpenAgent\Tests", writable: true);
        key?.DeleteSubKeyTree("StartupRegistrarTests", throwOnMissingSubKey: false);
    }
}
