using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CryptoScanner.ViewModels;

namespace CryptoScanner.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainWindowViewModel();
        DataContext = _vm;

        var editor = this.FindControl<StrategyEditorControl>("StrategyEditor");
        var fitBtn = this.FindControl<Button>("FitToViewBtn");
        var delBtn = this.FindControl<Button>("DeleteBlockBtn");
        var portfolioChart = this.FindControl<PortfolioChartControl>("PortfolioChart");
        var resetPortfolioChartBtn = this.FindControl<Button>("ResetPortfolioChartBtn");
        var exportPortfolioPdfBtn = this.FindControl<Button>("ExportPortfolioPdfBtn");

        // Wire "Zentrieren" button
        if (fitBtn != null && editor != null)
            fitBtn.Click += (_, _) => editor.FitToView();

        if (editor != null && _vm != null)
            editor.BlockDoubleClicked += block => _vm.OpenStrategyBlockEditor(block);

        if (portfolioChart != null && resetPortfolioChartBtn != null)
            resetPortfolioChartBtn.Click += (_, _) => portfolioChart.ResetView();

        if (exportPortfolioPdfBtn != null && _vm != null)
        {
            exportPortfolioPdfBtn.Click += async (_, _) =>
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Depotliste als PDF speichern",
                    SuggestedFileName = $"CryptoScanner-Depot-{DateTime.Now:yyyyMMdd-HHmmss}.pdf",
                    DefaultExtension = "pdf",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("PDF-Datei")
                        {
                            Patterns = new[] { "*.pdf" }
                        }
                    }
                });

                if (file != null)
                    _vm.ExportPortfolioPdf(file.Path.LocalPath);
            };
        }

        // Wire "Loeschen" button — deletes the selected block
        if (delBtn != null && editor != null)
        {
            delBtn.Click += (_, _) =>
            {
                if (editor.SelectedBlockId.HasValue)
                    editor.DeleteBlock(editor.SelectedBlockId.Value);
            };
        }

        // Auto-fit when switching to Strategy tab or loading a new strategy
        if (_vm != null && editor != null)
        {
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.SelectedTabIndex) && _vm.SelectedTabIndex == 3)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => editor.FitToView(),
                        Avalonia.Threading.DispatcherPriority.Loaded);
                }

                if (e.PropertyName == nameof(MainWindowViewModel.CurrentStrategy) && _vm.SelectedTabIndex == 3)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => editor.FitToView(),
                        Avalonia.Threading.DispatcherPriority.Loaded);
                }
            };
        }
    }
}
