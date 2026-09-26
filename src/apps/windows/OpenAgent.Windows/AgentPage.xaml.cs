using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using OpenAgent.Agent;
using OpenAgent.Core;
using OpenAgent.Providers;
using OpenAgent.Core.Domain;
using OpenAgent.Core.Error;
using OpenAgent.Core.Tasks;
using OpenAgent.Security;
using OpenAgent.Shared.Protocol;
using OpenAgent.Tools;
using OpenAgent.Windows.UI.Services;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace OpenAgent.Windows;

/// <summary>
/// The Agent surface: 起始页, 对话态 and 审批态 in one place, exactly where the
/// artboards put them. Submitting really runs the chain — task → permission
/// engine → tool executor → event stream (spec sections 97, 132, 155) — and the
/// approval card blocks until the user answers or it expires.
/// </summary>
public sealed partial class AgentPage : Page
{
    private const int ResultPreviewLength = 240;
    private const string ProviderId = NativeAgentProvider.ProviderId;
    private static readonly TimeSpan ApprovalLifetime = TimeSpan.FromMinutes(10);

    private readonly AgentTaskService? _tasks;
    private readonly ToolExecutor? _executor;
    private readonly ApprovalService? _approvals;
    private readonly ToolRegistry? _registry;
    private readonly PermissionMode _permissionMode;

    private TaskCompletionSource<bool>? _approvalWait;
    private ApprovalRecord? _pendingApproval;
    private DispatcherQueueTimer? _approvalTimer;
    private DispatcherQueueTimer? _longPressTimer;
    private ProgressBar? _longPressProgress;
    private int _longPressTicks;
    private bool _useLongPress;
    private bool _planEditRequested;
    private bool _busy;

    public AgentPage()
    {
        InitializeComponent();

        // M-17: high and critical approvals are confirmed by a long press, so the
        // press and release handlers are wired once and gated per approval.
        ApproveButton.PointerPressed += OnApprovePointerPressed;
        ApproveButton.PointerReleased += OnApprovePointerReleased;
        ApproveButton.PointerCanceled += OnApprovePointerReleased;
        ApproveButton.PointerCaptureLost += OnApprovePointerReleased;

        var services = App.Services;
        _tasks = services.GetService<AgentTaskService>();
        _executor = services.GetService<ToolExecutor>();
        _approvals = services.GetService<ApprovalService>();
        _registry = services.GetService<ToolRegistry>();
        _permissionMode = services.GetService<OpenAgentOptions>()?.PermissionMode
                          ?? PermissionMode.AskBeforeActions;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateGreeting();
        DeviceNameText.Text = $"{Environment.MachineName} 直连";
        DevicePillText.Text = $"{Environment.MachineName} · {WindowsLabel()}";
        MetaText.Text = "已连接 · 直连";

        // The hint next to the send button is the shortcut that summons this
        // surface, so it shows whichever one actually registered.
        ComposerHint.Text = App.Current is App shell ? shell.HotkeyLabel : "Alt + Space";

        UpdateEffortLabel();
    }

    private static string WindowsLabel() =>
        Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10";

    // --- 思考强度 ----------------------------------------------------------

    private const string EffortSettingKey = "agent.reasoning-effort";
    private static readonly string[] EffortNames = { "关闭", "低", "中", "高" };

    private int _effortLevel = ReadEffort();

    private void UpdateEffortLabel()
    {
        EffortLabel.Text = $"思考强度 · {EffortNames[_effortLevel]}";
        EffortFlyoutValue.Text = EffortNames[_effortLevel];
        EffortSlider.Value = _effortLevel;
    }

    private void EffortButton_Click(object sender, RoutedEventArgs e) =>
        EffortFlyout.ShowAt(EffortButton);

    private void OnEffortChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        var level = (int)Math.Round(e.NewValue);
        if (level == _effortLevel)
        {
            return;
        }

        _effortLevel = level;
        SaveEffort(level);
        UpdateEffortLabel();
    }

    private static void SaveEffort(int level)
    {
        try
        {
            global::Windows.Storage.ApplicationData.Current.LocalSettings.Values[EffortSettingKey] = level;
        }
        catch
        {
            // Unpackaged apps sometimes have no local settings container.
        }
    }

    private static int ReadEffort()
    {
        try
        {
            var values = global::Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue(EffortSettingKey, out var raw) &&
                raw is int stored &&
                stored >= 0 &&
                stored < EffortNames.Length)
            {
                return stored;
            }
        }
        catch
        {
            // Same: the default level still applies.
        }

        return 2;
    }

    private void UpdateGreeting()
    {
        var hour = DateTime.Now.Hour;
        var greeting = hour switch
        {
            >= 5 and < 12 => "Good morning",
            >= 12 and < 18 => "Good afternoon",
            >= 18 and < 22 => "Good evening",
            _ => "Good night",
        };

        // The artboards force two lines with "Good" alone on the first.
        var space = greeting.IndexOf(' ');
        GreetingLine1.Text = greeting[..space];
        GreetingLine2.Text = greeting[(space + 1)..];
    }

    /// <summary>
    /// Types a command written elsewhere into the bar without running it, so the
    /// user still sees and confirms exactly what is about to execute.
    /// </summary>
    public void Prefill(string prompt)
    {
        EnterConversation();
        CommandInput.Text = prompt;
        CommandInput.Select(prompt.Length, 0);
        _ = CommandInput.Focus(FocusState.Pointer);
    }

    private void CommandInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter)
        {
            Submit();
            e.Handled = true;
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => Submit();

    private async void Submit()
    {
        var text = CommandInput.Text?.Trim();
        if (string.IsNullOrEmpty(text) || _busy)
        {
            return;
        }

        CommandInput.Text = string.Empty;
        EnterConversation();
        AddUserTurn(text);

        _busy = true;
        SendButton.IsEnabled = false;
        try
        {
            await RunAsync(text);
        }
        catch (Exception ex)
        {
            AddLine($"{ErrorCodes.AgentError(9)} {ex.Message}", error: true);
        }
        finally
        {
            _busy = false;
            SendButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// 起始页 gives way to 对话态: the greeting is dropped, the conversation takes
    /// the column, and the meta row fades in with the composer (design/motion.md).
    /// </summary>
    private void EnterConversation()
    {
        if (StartState.Visibility == Visibility.Collapsed)
        {
            return;
        }

        StartState.Visibility = Visibility.Collapsed;
        ChatScroll.Visibility = Visibility.Visible;
        MetaRow.Visibility = Visibility.Visible;
    }

    private async Task RunAsync(string prompt)
    {
        if (_tasks is null || _executor is null || _registry is null || _approvals is null)
        {
            AddLine($"{ErrorCodes.AgentError(1)} 本地 Agent 未就绪：组合根没有启动。", error: true);
            return;
        }

        var tools = _registry.All()
            .Select(definition => new ToolSummary(
                definition.Id,
                definition.Name,
                definition.Description,
                definition.Risk,
                definition.Reversible,
                definition.Permissions))
            .ToArray();

        var plan = CommandPlanner.Plan(prompt, tools);
        if (plan is null)
        {
            AddLine($"{ErrorCodes.ToolError(3)} 没有匹配到可执行的本地工具：{prompt}", error: true);
            return;
        }

        var task = await _tasks.CreateAsync(prompt, ProviderId);
        await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Assistant, prompt);
        await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Running);

        using var document = JsonDocument.Parse(plan.ArgumentsJson);
        var request = new ToolExecutionRequest(
            plan.ToolId,
            document.RootElement.Clone(),
            task.Id,
            PermissionMode: _permissionMode,
            ProviderId: ProviderId);

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var outcome = await _executor.ExecuteAsync(request, _tasks.TokenFor(task.Id));
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);

        if (!outcome.Executed && outcome.Decision == PermissionDecision.RequireApproval)
        {
            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.WaitingApproval);

            var gate = await GateAsync(task.Id, plan.ToolId, tools, plan);
            if (!gate.Approved)
            {
                var denied = $"{ErrorCodes.PermissionError(1)} 未获批准，操作已终止";
                await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Error, denied);
                await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Failed, denied);

                if (_planEditRequested)
                {
                    // 修改计划 is a rejection with a follow-up: the prompt goes back
                    // into the bar so it can be edited rather than retyped.
                    _planEditRequested = false;
                    AddLine("已取消，指令已退回输入框。");
                    CommandInput.Text = prompt;
                    _ = CommandInput.Focus(FocusState.Programmatic);
                }
                else
                {
                    AddLine(denied, error: true);
                }

                _tasks.Release(task.Id);
                return;
            }

            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Running);
            request = request with { ApprovalId = gate.ApprovalId };
            started = System.Diagnostics.Stopwatch.GetTimestamp();
            outcome = await _executor.ExecuteAsync(request, _tasks.TokenFor(task.Id));
            elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
        }

        if (outcome.Result.Success)
        {
            var summary = Summarize(outcome.Result);
            await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Tool, $"{plan.ToolId} → {summary}");
            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Completed);
            AddToolCall(plan.ToolId, plan.ArgumentsJson, summary, failed: false, elapsed);
        }
        else
        {
            var code = outcome.Result.Error?.Code ?? ErrorCodes.ToolError(9);
            var message = outcome.Result.Error?.Message ?? "工具执行失败";
            await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Error, $"{code} {message}");
            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Failed, $"{code} {message}");
            AddToolCall(plan.ToolId, plan.ArgumentsJson, $"{code} {message}", failed: true, elapsed);
        }

        _tasks.Release(task.Id);
    }

    private async Task<(bool Approved, string? ApprovalId)> GateAsync(
        string taskId,
        string toolId,
        IReadOnlyList<ToolSummary> tools,
        CommandPlan plan)
    {
        var tool = tools.FirstOrDefault(
            candidate => string.Equals(candidate.Id, toolId, StringComparison.OrdinalIgnoreCase));
        var risk = tool?.Risk ?? RiskLevel.Medium;
        var displayName = tool?.DisplayName ?? toolId;

        var approval = await _approvals!.RequestAsync(
            taskId,
            toolId,
            risk,
            tool?.Reversible ?? false,
            $"{displayName} 请求执行",
            plan.ArgumentsJson);

        await _tasks!.AppendEventAsync(
            taskId,
            TaskEventKinds.Approval,
            $"{toolId} 等待批准 · {plan.ArgumentsJson}");

        return await ShowApprovalCardAsync(approval, displayName, risk, plan);
    }

    /// <summary>
    /// Shows the card and waits. Expiry is explicit: the card says how long is
    /// left and a timeout is reported as a denied approval, never as silence.
    /// </summary>
    private async Task<(bool Approved, string? ApprovalId)> ShowApprovalCardAsync(
        ApprovalRecord approval,
        string displayName,
        RiskLevel risk,
        CommandPlan plan)
    {
        _pendingApproval = approval;

        // Artboard 05 titles the card with what is about to happen ("移动 35 个
        // 文件"), not with the tool's registry name, so the plan's own rationale
        // wins whenever the planner produced one.
        ApprovalTitle.Text = string.IsNullOrWhiteSpace(plan.Rationale)
            ? displayName
            : plan.Rationale;
        ApprovalArgsText.Text = plan.ArgumentsJson;
        ApprovalReversibleText.Text = approval.Reversible ? "可撤销" : "不可撤销";
        ApprovalNoteText.Text = approval.Reversible
            ? "该操作可逆。"
            : "该操作不可撤销，请确认参数后再批准。";
        ApprovalRiskText.Text = ToolViewMapper.RiskLabel(risk);
        // Artboard 05 keeps every fact in the same grey; the risk is carried by
        // its own word, and the long-press gate below is the actual warning.

        // M-17: high and critical risk require a 1200ms press, so a stray click
        // cannot authorise file.delete or process.terminate.
        _useLongPress = risk >= RiskLevel.High;
        ApproveButton.Content = _useLongPress ? LongPressContent() : "批准";

        Fade(ComposerBlock, visible: false);
        ApprovalCard.Visibility = Visibility.Visible;
        PlayApprovalExpand();

        _approvalWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _approvalTimer = DispatcherQueue.CreateTimer();
        _approvalTimer.Interval = TimeSpan.FromSeconds(1);
        _approvalTimer.Tick += (_, _) => TickApproval(approval.ExpiresAtUtc);
        _approvalTimer.Start();
        UpdateCountdown(approval.ExpiresAtUtc);

        var approved = await _approvalWait.Task;

        StopApprovalTimer();
        StopLongPressTimer();
        _pendingApproval = null;
        _useLongPress = false;
        _longPressProgress = null;
        ApprovalCard.Visibility = Visibility.Collapsed;
        Fade(ComposerBlock, visible: true);

        return (approved, approval.Id);
    }

    private UIElement LongPressContent()
    {
        _longPressProgress = new ProgressBar
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = 3,
            Minimum = 0,
            Maximum = Motion.LongPress,
            Value = 0,
            Opacity = 0.7,
            CornerRadius = new CornerRadius(2),
        };

        var grid = new Grid();
        grid.Children.Add(new TextBlock
        {
            Text = "长按批准",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        grid.Children.Add(_longPressProgress);
        return grid;
    }

    private void OnApprovePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_useLongPress)
        {
            return;
        }

        _longPressTicks = 0;
        if (_longPressProgress is not null)
        {
            _longPressProgress.Value = 0;
        }

        _longPressTimer = DispatcherQueue.CreateTimer();
        _longPressTimer.Interval = TimeSpan.FromMilliseconds(50);
        _longPressTimer.Tick += (_, _) =>
        {
            _longPressTicks++;
            if (_longPressProgress is not null)
            {
                _longPressProgress.Value = LongPressCounter.ProgressMs(_longPressTicks);
            }

            if (LongPressCounter.IsComplete(_longPressTicks))
            {
                StopLongPressTimer();
                _ = ResolveApprovalAsync(true);
            }
        };
        _longPressTimer.Start();
    }

    private void OnApprovePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_useLongPress)
        {
            return;
        }

        StopLongPressTimer();
        if (_longPressProgress is not null)
        {
            _longPressProgress.Value = 0;
        }
    }

    private void PlanButton_Click(object sender, RoutedEventArgs e)
    {
        _planEditRequested = true;
        _ = ResolveApprovalAsync(false);
    }

    private void TickApproval(DateTimeOffset expiresAt)
    {
        if (DateTimeOffset.UtcNow < expiresAt)
        {
            UpdateCountdown(expiresAt);
            return;
        }

        StopApprovalTimer();
        _approvals?.ExpireStale();
        AddLine(ExpiredText(), error: true);
        _approvalWait?.TrySetResult(false);
    }

    private void UpdateCountdown(DateTimeOffset expiresAt)
    {
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        ApprovalCountdown.Text = $"剩余 {(int)remaining.TotalMinutes:00}:{remaining.Seconds:00} · 超时自动拒绝";
    }

    private static string ExpiredText() =>
        $"{ErrorCodes.PermissionError(4)} 审批已过期（{ApprovalLifetime.TotalMinutes:0} 分钟有效），请重新发起指令。";

    private async void ApproveButton_Click(object sender, RoutedEventArgs e)
    {
        // Long-press approvals resolve from the pointer timer, not the click.
        if (_useLongPress)
        {
            return;
        }

        await ResolveApprovalAsync(true);
    }

    private async void RejectButton_Click(object sender, RoutedEventArgs e) =>
        await ResolveApprovalAsync(false);

    private async Task ResolveApprovalAsync(bool approved)
    {
        var record = _pendingApproval;
        var wait = _approvalWait;
        if (record is null || wait is null)
        {
            return;
        }

        if (_approvals is not null &&
            !await _approvals.ResolveAsync(record.Id, approved, "windows.shell"))
        {
            StopApprovalTimer();
            AddLine(ExpiredText(), error: true);
            wait.TrySetResult(false);
            return;
        }

        wait.TrySetResult(approved);
    }

    private void StopApprovalTimer()
    {
        if (_approvalTimer is null)
        {
            return;
        }

        _approvalTimer.Stop();
        _approvalTimer = null;
    }

    private void StopLongPressTimer()
    {
        if (_longPressTimer is null)
        {
            return;
        }

        _longPressTimer.Stop();
        _longPressTimer = null;
    }

    private static string Summarize(ToolResult result)
    {
        if (result.Data is null)
        {
            return "完成";
        }

        var text = result.Data.Value.ToString();
        return text.Length <= ResultPreviewLength
            ? text
            : string.Concat(text.AsSpan(0, ResultPreviewLength), "…");
    }

    // ---- rendering, in the artboards' vocabulary --------------------------

    private void AddUserTurn(string prompt)
    {
        ChatColumn.Children.Add(Line("你", 11, "TextQuaternaryBrush", weight: FontWeights.SemiBold));
        ChatColumn.Children.Add(Line(prompt, 20, "TextPrimaryBrush", weight: FontWeights.SemiBold, top: 4));
        ChatColumn.Children.Add(Line(
            $"AGENT · {ProviderDisplayName()} · {DateTime.Now:HH:mm}",
            11,
            "TextQuaternaryBrush",
            weight: FontWeights.SemiBold,
            top: 20));
        ScrollToBottom();
    }

    private string ProviderDisplayName()
    {
        var provider = App.Services.GetService<ProviderRegistry>()?.Default;
        return provider is null ? "OpenAgent Native" : provider.DisplayName;
    }

    /// <summary>
    /// Codex-style tool call as artboards 03/04 draw it: a disclosure row naming
    /// the tool with its duration right-aligned, the call indented under it, and
    /// the output in a full-width sunken block. The artboards deliberately keep
    /// this unframed — a capsule here was rejected.
    /// </summary>
    private void AddToolCall(
        string toolId, string arguments, string output, bool failed, TimeSpan elapsed)
    {
        var block = new StackPanel { Margin = new Thickness(0, 20, 0, 0), Spacing = 6 };

        block.Children.Add(DisclosureRow(
            failed ? $"{toolId} 失败" : $"已使用 {toolId} 运行了命令",
            $"{elapsed.TotalSeconds:0.0}s",
            failed ? "StatusErrorBrush" : "TextTertiaryBrush"));
        block.Children.Add(DisclosureRow(
            $"{toolId}  {Collapse(arguments)}",
            string.Empty,
            "TextSecondaryBrush",
            left: 21));

        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(Line("纯文本", 11, "TextQuaternaryBrush", width: 0));
        foreach (var row in output.Split('\n'))
        {
            body.Children.Add(Line(row, 12, "TextSecondaryBrush", width: 0));
        }

        block.Children.Add(new Border
        {
            Background = UiBrushes.Get("BgSunkenBrush", UiBrushes.Fallback.ChipBackground),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20, 16, 20, 16),
            Margin = new Thickness(0, 4, 0, 0),
            Child = body,
        });

        ChatColumn.Children.Add(block);
        ScrollToBottom();
    }

    private static Grid DisclosureRow(string text, string trailing, string brushKey, double left = 0)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Margin = new Thickness(left, 0, 0, 0);

        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        head.Children.Add(new FontIcon
        {
            Glyph = "",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = UiBrushes.Get("TextQuaternaryBrush", UiBrushes.Fallback.Quaternary),
        });
        head.Children.Add(Line(text, 13, brushKey, width: 0));
        Grid.SetColumn(head, 0);
        row.Children.Add(head);

        if (trailing.Length > 0)
        {
            var duration = Line(trailing, 12, "TextQuaternaryBrush", width: 0);
            duration.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(duration, 1);
            row.Children.Add(duration);
        }

        return row;
    }

    private void AddLine(string text, bool error = false)
    {
        ChatColumn.Children.Add(Line(
            text,
            13,
            error ? "StatusErrorBrush" : "TextSecondaryBrush",
            top: 12));
        ScrollToBottom();
    }

    private static string Collapse(string json)
    {
        var flattened = json.Replace("\n", string.Empty).Replace("  ", " ");
        return flattened.Length <= 120 ? flattened : string.Concat(flattened.AsSpan(0, 120), "…");
    }

    private static TextBlock Line(
        string text,
        double size,
        string brushKey,
        double top = 0,
        double left = 0,
        global::Windows.UI.Text.FontWeight? weight = null,
        double width = 760) => new()
        {
            Text = text,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = width > 0 ? width : double.PositiveInfinity,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(left, top, 0, 0),
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = UiBrushes.Get(brushKey, UiBrushes.Fallback.StatusText),
        };

    private void ScrollToBottom() =>
        _ = DispatcherQueue.TryEnqueue(() => ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null));

    private static void Fade(UIElement target, bool visible)
    {
        if (!Motion.Enabled)
        {
            target.Opacity = visible ? 1 : 0;
            if (!visible)
            {
                target.IsHitTestVisible = false;
            }

            return;
        }

        var board = new Storyboard();
        board.Children.Add(Anim(target, "Opacity", visible ? 0 : 1, visible ? 1 : 0, Motion.State, null));
        board.Completed += (_, _) =>
        {
            if (!visible)
            {
                target.IsHitTestVisible = false;
            }
        };
        if (visible)
        {
            target.IsHitTestVisible = true;
        }

        board.Begin();
    }

    /// <summary>M-14: the card opens from its bottom edge rather than appearing.</summary>
    private void PlayApprovalExpand()
    {
        if (!Motion.Enabled)
        {
            return;
        }

        var board = new Storyboard();
        board.Children.Add(Anim(ApprovalCard, "Opacity", 0, 1, Motion.Enter, null));
        board.Children.Add(Anim(ApprovalCardShift, "Y", 12, 0, Motion.Enter, new ExponentialEase
        {
            EasingMode = EasingMode.EaseOut,
        }));
        board.Begin();
    }

    private static DoubleAnimation Anim(
        DependencyObject target,
        string path,
        double from,
        double to,
        int durationMs,
        EasingFunctionBase? easing)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EasingFunction = easing,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, path);
        return animation;
    }
}
