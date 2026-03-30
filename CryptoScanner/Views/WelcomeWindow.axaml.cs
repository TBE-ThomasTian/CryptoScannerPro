using Avalonia.Controls;
using Avalonia.Interactivity;
using CryptoScanner.Services;

namespace CryptoScanner.Views;

public partial class WelcomeWindow : Window
{
    public bool Accepted { get; private set; }

    public WelcomeWindow()
    {
        InitializeComponent();

        var combo = this.FindControl<ComboBox>("LanguageComboBox");
        if (combo != null)
        {
            combo.SelectedIndex = Loc.Language == "en" ? 1 : 0;
            combo.SelectionChanged += (_, _) =>
            {
                var selected = (combo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                Loc.Language = selected == "EN" ? "en" : "de";
            };
        }
    }

    private void OnContinueClick(object? sender, RoutedEventArgs e)
    {
        Accepted = true;
        Close();
    }
}
