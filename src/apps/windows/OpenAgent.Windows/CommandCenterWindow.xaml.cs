using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;
using WinRT.Interop;

namespace OpenAgent.Windows;

/// <summary>
/// Alt+Space overlay. A single-purpose surface: one input, the agent's steps,
/// and nothing else. Opens with a short scale+fade and closes on Esc or
/// focus loss.
/// </summary>
public sealed partial class CommandCenterWindow : Window
{
    private const int WindowWidth = 720;
    private const int WindowHeight = 480;

    public CommandCenterWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        // Window is not a FrameworkElement, so the open animation hangs off
        // the root grid's Loaded instead.
        Activated += OnActivated;
    }

    private AppWindow? _appWindow;

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
        PlayOpenAnimation();
        InputBox.Focus(FocusState.Programmatic);
    }

    private void PlayOpenAnimation()
    {
        var storyboard = new Storyboard();

        var scaleX = new DoubleAnimation
        {
            From = 0.96,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(scaleX, PanelScale);
        Storyboard.SetTargetProperty(scaleX, "ScaleX");

        var scaleY = new DoubleAnimation
        {
            From = 0.96,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(scaleY, PanelScale);
        Storyboard.SetTargetProperty(scaleY, "ScaleY");

        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(140)),
        };
        Storyboard.SetTarget(fade, Panel);
        Storyboard.SetTargetProperty(fade, "Opacity");

        storyboard.Children.Add(scaleX);
        storyboard.Children.Add(scaleY);
        storyboard.Children.Add(fade);
        storyboard.Begin();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            Close();
        }
    }

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Escape)
        {
            Close();
            return;
        }

        if (e.Key == global::Windows.System.VirtualKey.Enter)
        {
            Submit();
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => Submit();

    private void Submit()
    {
        var text = InputBox.Text?.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        // First submission clears the placeholder hint.
        if (HintText is not null && HintText.Visibility == Visibility.Visible)
        {
            HintText.Visibility = Visibility.Collapsed;
        }

        AddStep(text);
        InputBox.Text = string.Empty;
    }

    private void AddStep(string text)
    {
        var dot = new Ellipse
        {
            Width = 6,
            Height = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentBrush"],
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
        row.Children.Add(dot);
        row.Children.Add(label);

        StepList.Children.Add(row);
    }
}
