using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using OpenAgent.Agent;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using OpenAgent.Core;
using OpenAgent.Core.Domain;
using OpenAgent.Core.Error;
using OpenAgent.Core.Tasks;
using OpenAgent.Security;
using OpenAgent.Shared.Protocol;
using OpenAgent.Tools;
using OpenAgent.Windows.UI.Services;
using Windows.Graphics;
using WinRT.Interop;

namespace OpenAgent.Windows;

/// <summary>
/// Alt+Space overlay. One input, the agent's steps, and an approval card when
/// the permission engine asks for one. Submission really runs: task → tool
/// executor → event stream (spec sections 97, 132, 155). Motion follows
/// <c>design/motion.md</c>: M-02 enter/exit, M-14 approval expand, M-17 long
/// press for high/critical risk, M-24 checkmark draw, all gated by
/// <see cref="Motion.Enabled"/>.
/// </summary>
public sealed partial class CommandCenterWindow : Window
{
    private const int WindowWidth = 720;
    private const int WindowHeight = 480;
    private const int ResultPreviewLength = 240;

    /// <summary>Matches <see cref="ApprovalService"/>'s default lifetime.</summary>
    private static readonly TimeSpan ApprovalLifetime = TimeSpan.FromMinutes(10);

    private const string ProviderId = "openagent.native";

    private readonly AgentTaskService? _tasks;
    private readonly ToolExecutor? _executor;
    private readonly ApprovalService? _approvals;
    private readonly ToolRegistry? _registry;
    private readonly PermissionMode _permissionMode;

    private TaskCompletionSource<bool>? _approvalWait;
    private ApprovalRecord? _pendingApproval;
    private DispatcherQueueTimer? _approvalTimer;
    private bool _busy;

    private AppWindow? _appWindow;

    // --- Motion / approval interaction state --------------------------------

    private bool _isClosing;
    private bool _useLongPress;
    private DispatcherQueueTimer? _longPressTimer;
    private ProgressBar? _longPressProgress;
    private int _longPressTicks;

    public CommandCenterWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        // Window is not a FrameworkElement, so the open animation hangs off
        // the root grid's Loaded instead.
        Activated += OnActivated;
        Closed += OnClosed;

        // M-17: high/critical approvals are confirmed by a long press, not a
        // tap, so the press/release handlers are wired once here and gated by
        // _useLongPress per approval.
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

    private void ConfigureWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var id = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(id);

        _appWindow.IsShownInSwitchers = false;
        _appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        // Content paints its own rounded surface, so the system chrome is dropped.
        _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        CenterOnDisplay();
    }

    private void CenterOnDisplay()
    {
        if (_appWindow is null)
            return;

        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary)?.WorkArea;
        if (area is null)
            return;

        var x = area.Value.X + (area.Value.Width - WindowWidth) / 2;
        var y = area.Value.Y + (int)(area.Value.Height * 0.28);
        _appWindow.Move(new PointInt32(x, y));
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        PlayEnterAnimation();
        InputBox.Focus(FocusState.Programmatic);
    }

    // --- Motion: M-02 enter / exit -------------------------------------------

    /// <summary>
    /// M-02: scale 0.97 → 1 and opacity 0 → 1 over 320ms with an ease-out-expo
    /// approximation. Reduced-motion users get the final values with no
    /// storyboard (motion.md section 1, prefers-reduced-motion).
    /// </summary>
    private void PlayEnterAnimation()
    {
        PanelScale.ScaleX = 0.97;
        PanelScale.ScaleY = 0.97;
        Panel.Opacity = 0;

        if (!Motion.Enabled)
        {
            PanelScale.ScaleX = 1;
            PanelScale.ScaleY = 1;
            Panel.Opacity = 1;
            return;
        }

        var board = new Storyboard();
        var ease = new ExponentialEase { EasingMode = EasingMode.EaseOut };

        board.Children.Add(Anim(PanelScale, "ScaleX", 0.97, 1, Motion.Layout, ease));
        board.Children.Add(Anim(PanelScale, "ScaleY", 0.97, 1, Motion.Layout, ease));
        board.Children.Add(Anim(Panel, "Opacity", 0, 1, Motion.Layout, ease));
        board.Begin();
    }

    /// <summary>
    /// M-02 exit: scale 1 → 0.98 and opacity 1 → 0 over 240ms with an ease-in
    /// approximation. The window only actually closes once the animation has
    /// finished (or immediately, under reduced-motion).
    /// </summary>
    private void PlayExitAnimation(Action onCompleted)
    {
        if (!Motion.Enabled)
        {
            onCompleted();
            return;
        }

        var board = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        board.Children.Add(Anim(PanelScale, "ScaleX", 1, 0.98, Motion.Exit, ease));
        board.Children.Add(Anim(PanelScale, "ScaleY", 1, 0.98, Motion.Exit, ease));
        board.Children.Add(Anim(Panel, "Opacity", 1, 0, Motion.Exit, ease));

        board.Completed += (_, _) => onCompleted();
        board.Begin();
    }

    /// <summary>
    /// Routes every close path (Esc, deactivation, tray) through the exit
    /// animation so the panel never vanishes mid-motion.
    /// </summary>
    private void BeginClose()
    {
        if (_isClosing)
            return;

        _isClosing = true;
        StopApprovalTimer();
        StopLongPressTimer();
        PlayExitAnimation(() => Close());
    }

    private static DoubleAnimation Anim(
        DependencyObject target,
        string property,
        double from,
        double to,
        int milliseconds,
        EasingFunctionBase easing)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(milliseconds)),
            EasingFunction = easing,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    // --- Motion: M-14 approval card expand ----------------------------------

    /// <summary>
    /// M-14: the card scales 0.92 → 1 and fades 0 → 1 over 380ms with an
    /// ease-out-quart approximation. No overshoot — high/critical approvals
    /// must not bounce (motion.md section 6).
    /// </summary>
    private void PlayApprovalExpand()
    {
        ApprovalCardScale.ScaleX = 0.92;
        ApprovalCardScale.ScaleY = 0.92;
        ApprovalCard.Opacity = 0;
        ApprovalCard.Visibility = Visibility.Visible;

        if (!Motion.Enabled)
        {
            ApprovalCardScale.ScaleX = 1;
            ApprovalCardScale.ScaleY = 1;
            ApprovalCard.Opacity = 1;
            return;
        }

        var board = new Storyboard();
        var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };

        board.Children.Add(Anim(ApprovalCardScale, "ScaleX", 0.92, 1, 380, ease));
        board.Children.Add(Anim(ApprovalCardScale, "ScaleY", 0.92, 1, 380, ease));
        board.Children.Add(Anim(ApprovalCard, "Opacity", 0, 1, 380, ease));
        board.Begin();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            BeginClose();
        }
    }

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Escape)
        {
            BeginClose();
            return;
        }

        if (e.Key == global::Windows.System.VirtualKey.Enter)
        {
            Submit();
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => Submit();

    private async void Submit()
    {
        var text = InputBox.Text?.Trim();
        if (string.IsNullOrEmpty(text) || _busy)
            return;

        InputBox.Text = string.Empty;

        // First submission clears the placeholder hint.
        if (HintText is not null && HintText.Visibility == Visibility.Visible)
        {
            HintText.Visibility = Visibility.Collapsed;
        }

        _busy = true;
        SendButton.IsEnabled = false;
        try
        {
            await RunAsync(text);
        }
        catch (Exception ex)
        {
            AddStep($"{ErrorCodes.AgentError(9)} {ex.Message}", StepKind.Error);
        }
        finally
        {
            _busy = false;
            SendButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// The real chain: create a task, plan one tool call, execute it, stream the
    /// outcome into the step list (spec sections 97, 132).
    /// </summary>
    private async Task RunAsync(string prompt)
    {
        if (_tasks is null || _executor is null || _registry is null || _approvals is null)
        {
            AddStep($"{ErrorCodes.AgentError(1)} 本地 Agent 未就绪：组合根没有启动。", StepKind.Error);
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
            AddStep(
                $"{ErrorCodes.ToolError(3)} 没有匹配到可执行的本地工具：{prompt}",
                StepKind.Error);
            return;
        }

        var task = await _tasks.CreateAsync(prompt, ProviderId);
        await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Assistant, prompt);
        await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Running);
        AddStep($"任务已创建 · {plan.Rationale}", StepKind.Info);

        using var document = JsonDocument.Parse(plan.ArgumentsJson);
        var request = new ToolExecutionRequest(
            plan.ToolId,
            document.RootElement.Clone(),
            task.Id,
            PermissionMode: _permissionMode,
            ProviderId: ProviderId);

        var outcome = await _executor.ExecuteAsync(request, _tasks.TokenFor(task.Id));

        if (!outcome.Executed && outcome.Decision == PermissionDecision.RequireApproval)
        {
            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.WaitingApproval);

            var gate = await GateAsync(task.Id, plan.ToolId, tools, plan);
            if (!gate.Approved)
            {
                var denied = $"{ErrorCodes.PermissionError(1)} 未获批准，操作已终止";
                await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Error, denied);
                await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Failed, denied);
                AddStep(denied, StepKind.Error);
                _tasks.Release(task.Id);
                return;
            }

            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Running);
            request = request with { ApprovalId = gate.ApprovalId };
            outcome = await _executor.ExecuteAsync(request, _tasks.TokenFor(task.Id));
        }

        if (outcome.Result.Success)
        {
            var summary = Summarize(outcome.Result);
            await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Tool, $"{plan.ToolId} → {summary}");
            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Completed);
            AddCompletionStep($"{plan.ToolId} 完成 · {summary}");
        }
        else
        {
            var code = outcome.Result.Error?.Code ?? ErrorCodes.ToolError(9);
            var message = outcome.Result.Error?.Message ?? "工具执行失败";
            var failure = $"{code} {message}";
            await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Error, failure);
            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Failed, failure);
            AddStep(failure, StepKind.Error);
        }

        _tasks.Release(task.Id);
    }

    /// <summary>
    /// Asks the approval service for a record, shows it in the overlay, and
    /// waits for the user. Expiry is explicit: the card says how long is left and
    /// a timeout is reported as a failed approval, never as silence
    /// (spec sections 98, 109).
    /// </summary>
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

    private async Task<(bool Approved, string? ApprovalId)> ShowApprovalCardAsync(
        ApprovalRecord approval,
        string displayName,
        RiskLevel risk,
        CommandPlan plan)
    {
        _pendingApproval = approval;

        ApprovalTitle.Text = $"需要批准 · {displayName}";
        ApprovalRiskText.Text = ToolViewMapper.RiskLabel(risk);
        ApprovalRiskChip.Background = UiBrushes.Get(ToolViewMapper.RiskBackgroundKey(risk), "#FFF0F2F5");
        ApprovalRiskText.Foreground = UiBrushes.Get(ToolViewMapper.RiskBrushKey(risk), "#FF52565A");
        ApprovalArgsText.Text = plan.ArgumentsJson;
        ApprovalNoteText.Text = approval.Reversible
            ? "该操作可逆。"
            : "该操作不可撤销，请确认参数后再批准。";

        // M-17: high/critical risk replaces the tap-to-approve button with a
        // long-press (1200ms) so a stray click cannot authorise file.delete or
        // process.terminate. Lower risk keeps the instant button.
        _useLongPress = risk >= RiskLevel.High;
        if (_useLongPress)
        {
            BuildLongPressButtonContent();
        }
        else
        {
            ApproveButton.Content = "批准";
        }

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

        return (approved, approval.Id);
    }

    /// <summary>
    /// Swaps the approve button's content for a label plus a thin progress bar
    /// that fills over the long-press window.
    /// </summary>
    private void BuildLongPressButtonContent()
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

        var label = new TextBlock
        {
            Text = "长按批准",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var grid = new Grid();
        grid.Children.Add(label);
        grid.Children.Add(_longPressProgress);
        ApproveButton.Content = grid;
    }

    private void OnApprovePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_useLongPress)
            return;

        _longPressTicks = 0;
        if (_longPressProgress is not null)
            _longPressProgress.Value = 0;

        _longPressTimer = DispatcherQueue.CreateTimer();
        _longPressTimer.Interval = TimeSpan.FromMilliseconds(50);
        _longPressTimer.Tick += (_, _) =>
        {
            _longPressTicks++;
            if (_longPressProgress is not null)
                _longPressProgress.Value = Math.Min(_longPressTicks * 50, Motion.LongPress);

            if (_longPressTicks * 50 >= Motion.LongPress)
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
            return;

        StopLongPressTimer();
        if (_longPressProgress is not null)
            _longPressProgress.Value = 0;
    }

    private void StopLongPressTimer()
    {
        if (_longPressTimer is null)
            return;

        _longPressTimer.Stop();
        _longPressTimer = null;
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
        AddStep(ExpiredText(), StepKind.Error);
        _approvalWait?.TrySetResult(false);
    }

    private void UpdateCountdown(DateTimeOffset expiresAt)
    {
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        ApprovalCountdown.Text =
            $"剩余 {(int)remaining.TotalMinutes:00}:{remaining.Seconds:00} · 超时自动拒绝";
    }

    private static string ExpiredText() =>
        $"{ErrorCodes.PermissionError(4)} 审批已过期（{ApprovalLifetime.TotalMinutes:0} 分钟有效），请重新发起指令。";

    private async void ApproveButton_Click(object sender, RoutedEventArgs e)
    {
        // Long-press approvals resolve from the pointer timer, not the click.
        if (_useLongPress)
            return;

        await ResolveApprovalAsync(true);
    }

    private async void RejectButton_Click(object sender, RoutedEventArgs e) =>
        await ResolveApprovalAsync(false);

    private async Task ResolveApprovalAsync(bool approved)
    {
        var record = _pendingApproval;
        var wait = _approvalWait;
        if (record is null || wait is null)
            return;

        if (_approvals is not null &&
            !await _approvals.ResolveAsync(record.Id, approved, "windows.shell"))
        {
            // Already resolved, or expired while the card was open.
            StopApprovalTimer();
            AddStep(ExpiredText(), StepKind.Error);
            wait.TrySetResult(false);
            return;
        }

        wait.TrySetResult(approved);
    }

    private void StopApprovalTimer()
    {
        if (_approvalTimer is null)
            return;

        _approvalTimer.Stop();
        _approvalTimer = null;
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        StopApprovalTimer();
        StopLongPressTimer();
        _approvalWait?.TrySetResult(false);
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

    private enum StepKind
    {
        Info,
        Error,
    }

    private void AddStep(string text, StepKind kind = StepKind.Info)
    {
        var isError = kind == StepKind.Error;

        var dot = new Ellipse
        {
            Width = 6,
            Height = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = UiBrushes.Get(
                isError ? TaskViewMapper.StatusErrorBrushKey : "AccentBrush",
                isError ? "#FFE0484A" : "#FF5B8DEF"),
        };

        var label = new TextBlock
        {
            Text = text,
            Style = (Style)Application.Current.Resources["TypeBody"],
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (isError)
        {
            label.Foreground = UiBrushes.Get(TaskViewMapper.StatusErrorBrushKey, "#FFE0484A");
        }

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
        };
        row.Children.Add(dot);
        row.Children.Add(label);

        StepList.Children.Add(row);
    }

    /// <summary>
    /// M-24: a completed tool draws its checkmark rather than dropping one in.
    /// The Path starts fully dashed (invisible) and StrokeDashOffset animates
    /// to 0 over 480ms so the stroke appears to be drawn along the path.
    /// </summary>
    private void AddCompletionStep(string text)
    {
        var figure = new PathFigure
        {
            StartPoint = new global::Windows.Foundation.Point(3, 10),
            IsClosed = false,
        };
        figure.Segments.Add(new LineSegment { Point = new global::Windows.Foundation.Point(7, 14) });
        figure.Segments.Add(new LineSegment { Point = new global::Windows.Foundation.Point(15, 5) });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        var check = new Path
        {
            Data = geometry,
            Stroke = UiBrushes.Get("AccentBrush", "#FF5B8DEF"),
            StrokeThickness = 2,
            Width = 18,
            Height = 18,
            StrokeDashArray = new DoubleCollection { 20, 20 },
            StrokeDashOffset = 20,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var label = new TextBlock
        {
            Text = text,
            Style = (Style)Application.Current.Resources["TypeBody"],
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
        };
        row.Children.Add(check);
        row.Children.Add(label);

        StepList.Children.Add(row);

        if (Motion.Enabled)
        {
            var board = new Storyboard();
            board.Children.Add(Anim(
                check,
                "StrokeDashOffset",
                20,
                0,
                Motion.Enter,
                new ExponentialEase { EasingMode = EasingMode.EaseOut }));
            board.Begin();
        }
        else
        {
            check.StrokeDashOffset = 0;
        }
    }
}
