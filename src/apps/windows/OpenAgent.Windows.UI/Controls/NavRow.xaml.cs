using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using OpenAgent.Windows.UI.Services;

namespace OpenAgent.Windows.UI.Controls;

/// <summary>
/// One sidebar / tab-bar row, drawn the way the Pixso frames draw it: a 36px
/// capsule whose icon, label and count all shift to the accent colour when the
/// row is selected, on a tab-highlight pill.
/// </summary>
public sealed partial class NavRow : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(NavRow), new PropertyMetadata("", OnLabelChanged));

    /// <summary>Routing key the shell maps to a page. Not shown anywhere.</summary>
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.Register(nameof(Key), typeof(string), typeof(NavRow), new PropertyMetadata(""));

    public static readonly DependencyProperty CountProperty =
        DependencyProperty.Register(nameof(Count), typeof(string), typeof(NavRow), new PropertyMetadata("", OnCountChanged));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(IconElement), typeof(NavRow), new PropertyMetadata(null, OnIconChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(NavRow), new PropertyMetadata(false, OnIsSelectedChanged));

    public NavRow()
    {
        InitializeComponent();
        ApplyState();
    }

    /// <summary>Raised when the row is activated. Carries no selection logic: the
    /// shell owns which row is selected.</summary>
    public event RoutedEventHandler? Invoked;

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Key
    {
        get => (string)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <summary>Quiet trailing number, or empty to hide the slot entirely.</summary>
    public string Count
    {
        get => (string)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public IconElement? Icon
    {
        get => (IconElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var row = (NavRow)d;
        row.LabelBlock.Text = (string)e.NewValue;
        AutomationProperties.SetName(row.RootButton, (string)e.NewValue);
    }

    private static void OnCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var row = (NavRow)d;
        var text = (string)e.NewValue;
        row.CountBlock.Text = text;
        row.CountBlock.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var row = (NavRow)d;
        row.IconHost.Content = e.NewValue;
        row.ApplyState();
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NavRow)d).ApplyState();

    private void ApplyState()
    {
        var selected = IsSelected;
        RootButton.Background = selected
            ? UiBrushes.Get("TabHighlightBrush", "#FFFFFFFF")
            : UiBrushes.FromHex(UiBrushes.Fallback.Transparent);

        var accent = UiBrushes.Get("AccentBrush", UiBrushes.Fallback.Accent);
        if (Icon is not null)
        {
            Icon.Foreground = selected
                ? accent
                : UiBrushes.Get("IconMutedBrush", UiBrushes.Fallback.StatusNeutral);
        }

        LabelBlock.Foreground = selected
            ? accent
            : UiBrushes.Get("TextSecondaryBrush", UiBrushes.Fallback.StatusText);
        CountBlock.Foreground = selected
            ? accent
            : UiBrushes.Get("TextQuaternaryBrush", UiBrushes.Fallback.Quaternary);
    }

    private void RootButton_Click(object sender, RoutedEventArgs e) => Invoked?.Invoke(this, e);
}
