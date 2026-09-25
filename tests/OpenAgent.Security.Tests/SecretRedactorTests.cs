namespace OpenAgent.Security.Tests;

public sealed class SecretRedactorTests
{
    [Theory]
    [InlineData("API_KEY=sk-abcdef123456", "API_KEY")]
    [InlineData("api_key: sk-abcdef123456", "api_key")]
    [InlineData("Authorization=Bearer aaa.bbb.ccc", "Authorization")]
    public void Sensitive_keys_are_masked(string input, string keyPresent)
    {
        var redacted = SecretRedactor.Redact(input);

        Assert.Contains(keyPresent, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-abcdef123456", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("aaa.bbb.ccc", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_sensitive_keys_are_left_alone()
    {
        var redacted = SecretRedactor.Redact("path=D:/tmp;name=report");

        Assert.Equal("path=D:/tmp;name=report", redacted);
    }

    [Fact]
    public void Loose_api_keys_are_masked_even_without_a_key_name() =>
        Assert.DoesNotContain("sk-livekey12345678", SecretRedactor.Redact("using sk-livekey12345678 now"), StringComparison.Ordinal);

    [Fact]
    public void Summaries_are_truncated()
    {
        var summary = SecretRedactor.Summary(new string('x', 500), 240);

        Assert.Equal(241, summary.Length);
    }

    [Fact]
    public void Empty_input_is_safe()
    {
        Assert.Equal(string.Empty, SecretRedactor.Redact(null));
        Assert.Equal(string.Empty, SecretRedactor.Redact(string.Empty));
    }
}
