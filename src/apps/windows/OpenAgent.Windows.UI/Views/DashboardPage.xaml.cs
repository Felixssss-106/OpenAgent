using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenAgent.Windows.UI.Services;

namespace OpenAgent.Windows.UI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += DashboardPage_Loaded;
    }

    private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateGreeting();
        DeviceNameText.Text = $"{Environment.MachineName} 直连";
    }

    private void UpdateGreeting()
    {
        var hour = DateTime.Now.Hour;
        var greeting = hour switch
        {
            >= 5 and < 12 => "Good morning",
            >= 12 and < 18 => "Good afternoon",
            >= 18 and < 22 => "Good evening",
            _ => "Good night"
        };
        GreetingText.Text = greeting;
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var text = CommandInput.Text?.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        // Hand it to the Command Center, which owns the real execution pipeline.
        // This used to clear the box and drop the command on the floor.
        AgentHost.Current.RequestCommandCenter(text);
        CommandInput.Text = string.Empty;
    }
}
