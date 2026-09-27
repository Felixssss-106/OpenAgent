using OpenAgent.Tools.Internal;
using Xunit;

namespace OpenAgent.Tools.Tests;

/// <summary>
/// The CRT-style split app.launch uses before handing a tail to
/// ProcessStartInfo.ArgumentList — it must agree with CommandLineToArgvW or the
/// target sees a different argv than the caller reasoned about.
/// </summary>
public sealed class WindowsCommandLineTests
{
    [Fact]
    public void Null_and_blank_produce_no_arguments()
    {
        Assert.Empty(WindowsCommandLine.Split(null));
        Assert.Empty(WindowsCommandLine.Split(""));
        Assert.Empty(WindowsCommandLine.Split("   "));
    }

    [Fact]
    public void Plain_words_split_on_whitespace() =>
        Assert.Equal(new[] { "/t", "60", "/nobreak" }, WindowsCommandLine.Split("/t 60 /nobreak"));

    [Fact]
    public void Quoted_whitespace_stays_one_argument() =>
        Assert.Equal(new[] { "a b", "c" }, WindowsCommandLine.Split("\"a b\" c"));

    [Fact]
    public void Empty_quotes_produce_an_empty_argument() =>
        Assert.Equal(new[] { "", "x" }, WindowsCommandLine.Split("\"\" x"));

    [Fact]
    public void Backslash_quote_rules_match_the_CRT()
    {
        // a\"b -> a"b : one backslash + quote is a literal quote.
        Assert.Equal(new[] { "a\"b" }, WindowsCommandLine.Split("a\\\"b"));

        // a\\"b -> a\<quoted b> : two backslashes are literal, quote toggles.
        Assert.Equal(new[] { "a\\b" }, WindowsCommandLine.Split("a\\\\\"b"));

        // \\\\path\with\slashes passes through untouched.
        Assert.Equal(new[] { "C:\\Windows\\notepad.exe" }, WindowsCommandLine.Split("C:\\Windows\\notepad.exe"));
    }

    [Fact]
    public void Trailing_backslash_is_preserved() =>
        Assert.Equal(new[] { "C:\\Temp\\" }, WindowsCommandLine.Split("C:\\Temp\\"));
}
