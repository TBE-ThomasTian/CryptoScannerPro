using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CryptoScanner.Views;

public partial class WelcomeWindow : Window
{
    public bool Accepted { get; private set; }

    public WelcomeWindow()
    {
        InitializeComponent();
    }

    private void OnContinueClick(object? sender, RoutedEventArgs e)
    {
        Accepted = true;
        Close();
    }
}
