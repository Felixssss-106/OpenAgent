using System.Collections.Generic;

namespace OpenAgent.Providers;

/// <summary>
/// The set of agent providers available to the host. Native is always present
/// as the default (spec section 22); external CLIs are added here once their
/// adapter is written. Discovery (<see cref="CliDiscovery"/>) fills a separate
/// list so the UI can show "detected, not driven" without pretending an
/// adapter exists.
/// </summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IAgentProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    public ProviderRegistry(IEnumerable<IAgentProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        foreach (var provider in providers)
        {
            Register(provider);
        }

        // The default is mandatory: if DI was asked for providers before Native
        // was registered, install it now so the shell always has a working
        // agent (spec section 22: "OpenAgent 自带 Agent 必须作为默认 Agent").
        if (!_providers.ContainsKey(DefaultProviderId))
        {
            Register(new NativeAgentProvider());
        }
    }

    /// <summary>Stable id of the default provider (<see cref="NativeAgentProvider"/>).</summary>
    public static string DefaultProviderId => NativeAgentProvider.ProviderId;

    public void Register(IAgentProvider provider) =>
        _providers[provider.Id] = provider;

    public IAgentProvider Default => _providers[DefaultProviderId];

    public IReadOnlyCollection<IAgentProvider> All => _providers.Values;

    public IAgentProvider? Get(string id) =>
        _providers.TryGetValue(id, out var provider) ? provider : null;
}
