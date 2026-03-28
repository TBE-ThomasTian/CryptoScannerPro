using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CryptoScanner.Views;

namespace CryptoScanner;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            // Show welcome/disclaimer dialog on first paint
            mainWindow.Opened += async (_, _) =>
            {
                var welcome = new WelcomeWindow();
                await welcome.ShowDialog(mainWindow);

                if (!welcome.Accepted)
                {
                    desktop.Shutdown();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
