using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAgent.Core.Events;
using OpenAgent.Core.Retry;
using OpenAgent.Core.Time;
using OpenAgent.Security;
using OpenAgent.Storage;
using OpenAgent.Storage.Repositories;
using OpenAgent.Tools;
using OpenAgent.Tools.System;

namespace OpenAgent.Agent;

public sealed class OpenAgentOptions
{
    /// <summary>Null means <c>%LOCALAPPDATA%\OpenAgent</c>.</summary>
    public string? DatabaseRoot { get; set; }

    public PermissionMode PermissionMode { get; set; } = PermissionMode.AskBeforeActions;

    public PermissionOptions Permissions { get; set; } = new();
}

/// <summary>
/// One composition root for the non-UI layers, so the host (and the tests) get
/// exactly the same wiring instead of a duplicate copy (spec section 165).
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddOpenAgent(
        this IServiceCollection services,
        Action<OpenAgentOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new OpenAgentOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IUtcClock, SystemUtcClock>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<IRetryPolicy, ExponentialBackoffRetryPolicy>();

        services.AddSingleton(new DatabasePaths(options.DatabaseRoot ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenAgent")));

        services.AddSingleton(provider =>
        {
            var database = new SqliteDatabase(
                provider.GetRequiredService<DatabasePaths>(),
                provider.GetRequiredService<ILogger<SqliteDatabase>>());
            database.Initialize();
            return database;
        });

        services.AddSingleton<PermissionService>(provider =>
            new PermissionService(provider.GetRequiredService<OpenAgentOptions>().Permissions));

        services.AddSingleton<TaskRepository>();
        services.AddSingleton<TaskEventRepository>();
        services.AddSingleton<ApprovalRepository>();
        services.AddSingleton<AuditLogRepository>();
        services.AddSingleton<DeviceRepository>();
        services.AddSingleton<SettingsRepository>();

        services.AddSingleton<ITool, SystemGetInfoTool>();
        services.AddSingleton<ITool, ProcessListTool>();
        services.AddSingleton<ITool, AppLaunchTool>();

        services.AddSingleton(provider => new ToolRegistry(provider.GetServices<ITool>()));

        services.AddSingleton<ApprovalService>();
        services.AddSingleton<ToolExecutor>();
        services.AddSingleton<AgentTaskService>();

        return services;
    }
}
