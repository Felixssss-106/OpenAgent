using System.Globalization;
using System.Text.Json;

namespace OpenAgent.Providers;

/// <summary>
/// The default, credential-free provider. It routes a prompt to a single tool
/// call with deterministic keyword matching — the same logic the shell's
/// <c>CommandPlanner</c> used, now living in the provider layer where the spec
/// wants it (sections 22, 23). No external CLI, no model call, no network: this
/// is the floor that always works before any adapter is detected.
/// </summary>
public sealed class NativeAgentProvider : IAgentProvider
{
    /// <summary>Stable id used by the registry and the UI (spec section 22).</summary>
    public const string ProviderId = "openagent.native";

    private static readonly string[] LaunchKeywords = { "打开", "启动", "运行", "launch", "open", "start" };
    private static readonly string[] ProcessKeywords = { "进程", "process", "tasklist" };
    private static readonly string[] SystemKeywords = { "系统", "内存", "内存占用", "cpu", "性能", "配置", "system", "info" };

    /// <summary>Plan arguments are shown to the user, so 记事本 has to stay
    /// 记事本 rather than turning into \u8BB0\u4E8B\u672C on the approval card.</summary>
    private static readonly JsonSerializerOptions ArgumentsEncoding = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Id => ProviderId;
    public string DisplayName => "OpenAgent Native Agent";

    // Native is deterministic and local: it calls tools, supports approval,
    // can resume a session and emits structured output, but it does not stream
    // or take images and has no interactive terminal or MCP bridge.
    public AgentCapabilities Capabilities =>
        AgentCapabilities.ToolCalling
        | AgentCapabilities.Approval
        | AgentCapabilities.SessionResume
        | AgentCapabilities.StructuredOutput;

    public Task<AgentSession> CreateSessionAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var session = new AgentSession(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), taskId, DateTimeOffset.UtcNow);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<ProviderStep>> SendPromptAsync(
        AgentSession session,
        string prompt,
        IReadOnlyList<ProviderToolInfo> tools,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(tools);

        var plan = Plan(prompt, tools);
        if (plan is null)
        {
            return Task.FromResult<IReadOnlyList<ProviderStep>>(new[]
            {
                new ProviderStep(ProviderStepKind.Error, "没有匹配到可执行的本地工具"),
            });
        }

        return Task.FromResult<IReadOnlyList<ProviderStep>>(new[]
        {
            new ProviderStep(ProviderStepKind.Thought, plan.Rationale),
            new ProviderStep(ProviderStepKind.ToolCall, plan.ToolId, plan.ToolId, plan.ArgumentsJson),
        });
    }

    public Task StopAsync(AgentSession session, CancellationToken cancellationToken = default)
    {
        // Native sessions are in-memory and stop with the caller; there is no
        // child process to signal (spec section 23).
        return Task.CompletedTask;
    }

    public Task ResumeAsync(AgentSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        // Nothing to reload: the session record carries everything Native needs.
        return Task.CompletedTask;
    }

    public Task<AgentHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        // Native is always available — it depends on nothing external.
        return Task.FromResult(AgentHealth.Healthy);
    }

    /// <summary>
    /// Pure routing helper, exported so the registry tests and the shell can
    /// exercise it without a live session. Null means "no match".
    /// </summary>
    public static ProviderPlan? Plan(string? prompt, IReadOnlyList<ProviderToolInfo> tools)
    {
        if (string.IsNullOrWhiteSpace(prompt) || tools is null || tools.Count == 0)
        {
            return null;
        }

        var text = prompt.Trim();

        if (ContainsAny(text, LaunchKeywords))
        {
            var launchTool = Prefer(tools, "app.launch", "launch", "app");
            var target = ExtractTarget(text);
            if (launchTool is not null && target.Length > 0)
            {
                return new ProviderPlan(
                    launchTool.Id,
                    JsonSerializer.Serialize(new Dictionary<string, string> { ["target"] = target }, ArgumentsEncoding),
                    $"启动 {target}");
            }
        }

        if (ContainsAny(text, ProcessKeywords))
        {
            var processTool = Prefer(tools, "process.list", "process");
            if (processTool is not null)
            {
                return new ProviderPlan(processTool.Id, "{}", "列出运行中的进程");
            }
        }

        if (ContainsAny(text, SystemKeywords))
        {
            var systemTool = Prefer(tools, "system.get_info", "system");
            if (systemTool is not null)
            {
                return new ProviderPlan(systemTool.Id, "{}", "读取系统信息");
            }
        }

        return null;
    }

    /// <summary>Text following the first launch keyword, with trailing punctuation removed.</summary>
    public static string ExtractTarget(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        foreach (var keyword in LaunchKeywords)
        {
            var index = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var rest = text[(index + keyword.Length)..].Trim();
            // Iteratively strip punctuation and whitespace from both ends —
            // removing one may expose the other (e.g. `"spotify" 的` →
            // `spotify" ` after 的 is removed, then `"` and space are stripped).
            while (true)
            {
                var prev = rest;
                rest = rest.Trim('。', '.', '，', ',', '；', ';', '！', '!', '？', '?', '"', '\'', '“', '”', '的').Trim();
                if (rest.Length == prev.Length)
                {
                    break;
                }
            }
            if (rest.Length > 0)
            {
                return rest;
            }
        }

        return string.Empty;
    }

    private static ProviderToolInfo? Prefer(IReadOnlyList<ProviderToolInfo> tools, params string[] preferences)
    {
        foreach (var preference in preferences)
        {
            foreach (var tool in tools)
            {
                if (tool.Id.Contains(preference, StringComparison.OrdinalIgnoreCase))
                {
                    return tool;
                }
            }
        }

        return null;
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>One interpreted command from the Native provider.</summary>
public sealed record ProviderPlan(string ToolId, string ArgumentsJson, string Rationale);
