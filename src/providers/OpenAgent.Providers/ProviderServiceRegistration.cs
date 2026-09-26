using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace OpenAgent.Providers;

/// <summary>
/// Provider-layer DI. Lives here (not in <c>OpenAgent.Agent</c>) so the agent
/// layer never takes a dependency on the provider layer — the host calls both
/// <c>AddOpenAgent</c> and <c>AddOpenAgentProviders</c> and stays the only
/// project that knows about both (spec section 165).
/// </summary>
public static class ProviderServiceRegistration
{
    /// <summary>
    /// Registers the Native provider as the default and the
    /// <see cref="ProviderRegistry"/> that aggregates every
    /// <see cref="IAgentProvider"/> registered afterwards. Any agent CLI found on
    /// PATH via <see cref="CliDiscovery"/> becomes a real provider here, so
    /// installing Codex / Claude Code / OpenCode makes it selectable with no shell
    /// change (spec sections 24-27).
    /// </summary>
    public static IServiceCollection AddOpenAgentProviders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<NativeAgentProvider>();
        services.AddSingleton<IAgentProvider>(provider =>
            provider.GetRequiredService<NativeAgentProvider>());

        services.AddSingleton<IProcessRunner, RealProcessRunner>();

        if (OperatingSystem.IsWindows())
        {
            foreach (var cli in CliDiscovery.Scan())
            {
                var profile = CliInvocationProfiles.For(cli.Id);
                if (profile is null)
                {
                    continue;
                }

                services.AddSingleton<IAgentProvider>(sp =>
                    new CliAgentProvider(profile, sp.GetRequiredService<IProcessRunner>()));
            }
        }

        services.AddSingleton<ProviderRegistry>(provider =>
            new ProviderRegistry(provider.GetServices<IAgentProvider>()));
        return services;
    }
}
