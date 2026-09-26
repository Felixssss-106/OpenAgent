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
    /// <see cref="IAgentProvider"/> registered afterwards.
    /// </summary>
    public static IServiceCollection AddOpenAgentProviders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<NativeAgentProvider>();
        services.AddSingleton<IAgentProvider>(provider =>
            provider.GetRequiredService<NativeAgentProvider>());
        services.AddSingleton<ProviderRegistry>(provider =>
            new ProviderRegistry(provider.GetServices<IAgentProvider>()));
        return services;
    }
}
