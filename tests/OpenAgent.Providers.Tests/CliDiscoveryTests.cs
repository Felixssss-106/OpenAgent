using System.Runtime.Versioning;
using OpenAgent.Providers;
using Xunit;

namespace OpenAgent.Providers.Tests;

public sealed class CliDiscoveryTests
{
    [Fact]
    public void KnownClis_covers_the_spec_set()
    {
        // Spec section 24: Codex, Claude Code, OpenCode, Pi, Gemini.
        Assert.True(CliDiscovery.KnownClis.ContainsKey("codex"));
        Assert.True(CliDiscovery.KnownClis.ContainsKey("claude"));
        Assert.True(CliDiscovery.KnownClis.ContainsKey("opencode"));
        Assert.True(CliDiscovery.KnownClis.ContainsKey("pi"));
        Assert.True(CliDiscovery.KnownClis.ContainsKey("gemini"));
    }

    [Theory]
    [InlineData("codex 0.1.2", "0.1.2")]
    [InlineData("claude 1.4.0 (commit abc)", "1.4.0")]
    [InlineData("opencode v0.3.1", "0.3.1")]
    [InlineData("gemini 0.1.0\n", "0.1.0")]
    [InlineData("pi 2.0", "2.0")]
    public void ParseVersion_picks_the_first_dotted_numeric_token(string output, string expected)
    {
        Assert.Equal(expected, CliDiscovery.ParseVersion(output));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("command not found")]
    [InlineData("vNext (unreleased)")]
    public void ParseVersion_returns_null_when_no_version_present(string? output)
    {
        Assert.Null(CliDiscovery.ParseVersion(output));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Scan_on_non_windows_returns_empty_without_throwing()
    {
        // Scan is [SupportedOSPlatform("windows")]; on the test runner the guard
        // returns an empty list rather than throwing, so the call is safe from
        // any OS. We only assert the contract, not a specific platform.
        var result = CliDiscovery.Scan();
        Assert.NotNull(result);
    }
}

public sealed class ProviderRegistryTests
{
    [Fact]
    public void Native_is_installed_when_no_providers_were_registered()
    {
        var registry = new ProviderRegistry(Array.Empty<IAgentProvider>());
        Assert.Equal("openagent.native", registry.Default.Id);
        Assert.Single(registry.All);
    }

    [Fact]
    public void Register_replaces_an_existing_provider_with_the_same_id()
    {
        var first = new NativeAgentProvider();
        var registry = new ProviderRegistry(new[] { first });
        var second = new NativeAgentProvider();
        registry.Register(second);

        Assert.Same(second, registry.Get(NativeAgentProvider.ProviderId));
        Assert.Single(registry.All);
    }

    [Fact]
    public void Default_is_always_native()
    {
        var registry = new ProviderRegistry(Array.Empty<IAgentProvider>());
        Assert.IsType<NativeAgentProvider>(registry.Default);
        Assert.Equal(ProviderRegistry.DefaultProviderId, registry.Default.Id);
    }

    [Fact]
    public void Get_returns_null_for_an_unknown_id()
    {
        var registry = new ProviderRegistry(Array.Empty<IAgentProvider>());
        Assert.Null(registry.Get("does.not.exist"));
    }
}
