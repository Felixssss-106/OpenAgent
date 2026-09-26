using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace OpenAgent.Windows.UI.Controls;

public sealed partial class SettingRow : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingRow), new PropertyMetadata("", OnTitleChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(SettingRow), new PropertyMetadata("", OnValueChanged));

    public static readonly DependencyProperty ValueForegroundProperty =
        DependencyProperty.Register(nameof(ValueForeground), typeof(Brush), typeof(SettingRow), new PropertyMetadata(null, OnValueForegroundChanged));

    public SettingRow()
    {
        InitializeComponent();

        // A row nobody listens to is a label, not a control: Tab should not walk the five
        // of them. What it does not stop is the focus adorner WinUI draws when the window
        // is activated with nothing focused yet — see scripts/capture-window.ps1, which
        // neutralises that transient state before shooting, because no artboard draws it.
        Loaded += (_, _) => RootButton.IsTabStop = Click is not null;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush ValueForeground
    {
        get => (Brush)GetValue(ValueForegroundProperty);
        set => SetValue(ValueForegroundProperty, value);
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingRow row && row.TitleBlock != null)
            row.TitleBlock.Text = (string)e.NewValue;
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingRow row && row.ValueBlock != null)
            row.ValueBlock.Text = (string)e.NewValue;
    }

    private static void OnValueForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingRow row && row.ValueBlock != null && e.NewValue is Brush brush)
            row.ValueBlock.Foreground = brush;
    }

    public event RoutedEventHandler? Click;

    private void RootButton_Click(object sender, RoutedEventArgs e)
    {
        Click?.Invoke(this, e);
    }
}
