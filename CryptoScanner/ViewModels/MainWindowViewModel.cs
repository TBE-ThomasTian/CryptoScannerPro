using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoScanner.Models;
using CryptoScanner.Services;

namespace CryptoScanner.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly KrakenApiService _krakenApi;
    private readonly TechnicalAnalysisService _technicalAnalysis;
    private readonly ScoringService _scoringService;
    private readonly PortfolioService _portfolioService;
    private readonly AiTradingService _aiTradingService;
    private readonly SecureStorageService _secureStorage;
    private readonly StrategyExecutionService _strategyService;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _autoRefreshCts;

    private List<CryptoCoin> _allCoins = new();

    public ObservableCollection<CryptoCoin> Coins { get; } = new();

    // ── Tab navigation ─────────────────────────────────────────
    [ObservableProperty] private int _selectedTabIndex;  // 0=Scanner, 1=Depot, 2=KI-Trading, 3=Strategie

    public bool IsTabScanner => SelectedTabIndex == 0;
    public bool IsTabDepot => SelectedTabIndex == 1;
    public bool IsTabAi => SelectedTabIndex == 2;
    public bool IsTabStrategy => SelectedTabIndex == 3;

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsTabScanner));
        OnPropertyChanged(nameof(IsTabDepot));
        OnPropertyChanged(nameof(IsTabAi));
        OnPropertyChanged(nameof(IsTabStrategy));
    }

    // ── Scanner state ──────────────────────────────────────────
    [ObservableProperty] private CryptoCoin? _selectedCoin;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isAutoRefreshEnabled;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedFilter = "Alle";
    [ObservableProperty] private int _scanProgress;
    [ObservableProperty] private int _scanTotal;
    [ObservableProperty] private string _statusText = "Bereit";
    [ObservableProperty] private string _lastUpdateTime = "Nie";
    [ObservableProperty] private bool _showDetailPanel;
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private string _selectedSort = "Name";
    [ObservableProperty] private bool _sortAscending = true;
    [ObservableProperty] private string _selectedInterval = "2 Min";
    [ObservableProperty] private string _selectedTimeframe = "7 Tage";
    [ObservableProperty] private string _selectedCurrency = "USD";

    // ── Chart indicator toggles ─────────────────────────────────
    [ObservableProperty] private bool _showCandlesticks;
    [ObservableProperty] private bool _showSma20 = true;
    [ObservableProperty] private bool _showSma50 = true;
    [ObservableProperty] private bool _showEma12;
    [ObservableProperty] private bool _showEma26;
    [ObservableProperty] private bool _showBollingerBands;
    [ObservableProperty] private bool _showHelpDialog;

    // ── Depot starting balance input ─────────────────────────────
    [ObservableProperty] private string _startingBalanceInput = "10000";
    [ObservableProperty] private string _portfolioFeeRateInput = "0,26";
    [ObservableProperty] private string _selectedPortfolioSort = "G/V %";
    [ObservableProperty] private bool _portfolioSortAscending;

    public bool CanEditStartingBalance => Portfolio.Positions.Count == 0 && Portfolio.TransactionHistory.Count == 0;

    // ── Buy/Sell dialog state ──────────────────────────────────
    [ObservableProperty] private bool _showBuyDialog;
    [ObservableProperty] private bool _showSellDialog;
    [ObservableProperty] private bool _showStrategyBlockEditor;
    [ObservableProperty] private CryptoCoin? _tradeCoin;
    [ObservableProperty] private string _tradeAmount = "";
    [ObservableProperty] private string _tradeMessage = string.Empty;
    [ObservableProperty] private PortfolioPosition? _sellPosition;
    [ObservableProperty] private bool _tradeByValue;  // false=coin amount, true=currency value
    [ObservableProperty] private string _tradeValueInput = "";
    [ObservableProperty] private StrategyBlock? _editingStrategyBlock;
    [ObservableProperty] private string _strategyBlockEditorOperator = "";
    [ObservableProperty] private string _strategyBlockEditorValue = "";
    [ObservableProperty] private string _strategyBlockEditorActionAmount = "";
    [ObservableProperty] private string _strategyBlockEditorConditionPreset = "";
    [ObservableProperty] private string _strategyBlockEditorAlarmText = "";

    public string TradePreview
    {
        get
        {
            if (TradeCoin == null || TradeCoin.CurrentPrice <= 0) return "";
            var sym = TradeCoin.CurrencySymbol;
            var feeRate = _portfolioService.TradingFeeRate;
            if (TradeByValue)
            {
                if (decimal.TryParse(TradeValueInput?.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var val) && val > 0)
                {
                    var fee = val * feeRate;
                    return $"= {val / TradeCoin.CurrentPrice:N6} {TradeCoin.DisplayName} | Gebuehr {sym}{fee:N2}";
                }
            }
            else
            {
                if (decimal.TryParse(TradeAmount?.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var amt) && amt > 0)
                {
                    var gross = amt * TradeCoin.CurrentPrice;
                    var fee = gross * feeRate;
                    if (ShowBuyDialog)
                        return $"= {sym}{gross:N2} + Gebuehr {sym}{fee:N2} = {sym}{gross + fee:N2}";
                    if (ShowSellDialog)
                        return $"= {sym}{gross:N2} - Gebuehr {sym}{fee:N2} = {sym}{gross - fee:N2}";
                }
            }
            return "";
        }
    }

    public string TradeMaxInfo
    {
        get
        {
            if (ShowBuyDialog && TradeCoin != null)
                return $"Max inkl. {PortfolioFeeRateText} Gebuehr: {Portfolio.CurrencySymbol}{Portfolio.Balance:N2}";
            if (ShowSellDialog && SellPosition != null)
                return $"Max: {SellPosition.Amount:N6} {SellPosition.Symbol}";
            return "";
        }
    }

    public string PortfolioFeeRateText => _portfolioService.TradingFeeRatePercent;
    public string PortfolioFeeInfo => $"Paper-Gebuehr: {PortfolioFeeRateText} pro Kauf/Verkauf (Kraken-aehnlich)";
    public bool IsEditingConditionBlock => EditingStrategyBlock?.Type == BlockType.Condition;
    public bool IsEditingActionBlock => EditingStrategyBlock?.Type == BlockType.ActionBuy || EditingStrategyBlock?.Type == BlockType.ActionSell;
    public bool IsEditingAlarmBlock => EditingStrategyBlock?.Type == BlockType.ActionAlarm;
    public string StrategyBlockEditorTitle => EditingStrategyBlock == null
        ? "Block bearbeiten"
        : $"{EditingStrategyBlock.Title} bearbeiten";

    partial void OnTradeAmountChanged(string value) => OnPropertyChanged(nameof(TradePreview));
    partial void OnTradeValueInputChanged(string value) => OnPropertyChanged(nameof(TradePreview));
    partial void OnTradeByValueChanged(bool value) => OnPropertyChanged(nameof(TradePreview));
    partial void OnEditingStrategyBlockChanged(StrategyBlock? value)
    {
        OnPropertyChanged(nameof(IsEditingConditionBlock));
        OnPropertyChanged(nameof(IsEditingActionBlock));
        OnPropertyChanged(nameof(IsEditingAlarmBlock));
        OnPropertyChanged(nameof(StrategyBlockEditorTitle));
    }
    partial void OnPortfolioFeeRateInputChanged(string value)
    {
        if (!decimal.TryParse(value?.Replace(",", "."),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var percent))
            return;

        _portfolioService.SetTradingFeeRate(percent / 100m);
        OnPropertyChanged(nameof(PortfolioFeeRateText));
        OnPropertyChanged(nameof(PortfolioFeeInfo));
        OnPropertyChanged(nameof(TradeMaxInfo));
        OnPropertyChanged(nameof(TradePreview));
    }

    // ── AI Trading state ───────────────────────────────────────
    [ObservableProperty] private string _aiApiKey = string.Empty;
    [ObservableProperty] private string _aiApiUrl = "https://api.openai.com/v1/chat/completions";
    [ObservableProperty] private string _aiProvider = "ChatGPT";
    [ObservableProperty] private bool _isAiAnalyzing;
    [ObservableProperty] private bool _aiAutoTrading;
    [ObservableProperty] private string _aiStatusText = string.Empty;
    public ObservableCollection<TradeOrder> AiRecommendations { get; } = new();
    public ObservableCollection<string> AiLog { get; } = new();

    // ── Strategy state ───────────────────────────────────────
    [ObservableProperty] private TradingStrategy _currentStrategy = new();
    [ObservableProperty] private string _strategyStatusText = string.Empty;
    [ObservableProperty] private bool _strategyAutoApplyToPaperPortfolio;
    [ObservableProperty] private bool _strategyRequirePositiveDayChange = true;
    [ObservableProperty] private bool _strategyRequireAboveSma50 = true;
    [ObservableProperty] private bool _strategyRequirePositiveMacd = true;
    [ObservableProperty] private bool _strategyPreferTopScoreBuys = true;
    [ObservableProperty] private bool _strategyRequireMinScore40 = true;
    [ObservableProperty] private bool _strategyRequireAboveAverageVolume;
    [ObservableProperty] private bool _strategySkipExistingPositions = true;
    [ObservableProperty] private string _selectedStrategyFilterPreset = "Ausgewogen";
    [ObservableProperty] private bool _isOptimizingStrategy;
    public ObservableCollection<TradeOrder> StrategyResults { get; } = new();
    public ObservableCollection<string> StrategyLog { get; } = new();
    public ObservableCollection<StrategyOptimizationResult> StrategyOptimizationResults { get; } = new();
    public ObservableCollection<string> SavedStrategyNames { get; } = new();
    public ObservableCollection<PortfolioPosition> SortedPortfolioPositions { get; } = new();
    public ObservableCollection<string> StrategyFilterPresets { get; } = new()
    { "Konservativ", "Ausgewogen", "Aggressiv" };

    // ── Portfolio ──────────────────────────────────────────────
    public Portfolio Portfolio => _portfolioService.Portfolio;
    [ObservableProperty] private List<PortfolioSnapshot>? _portfolioSnapshots;

    // ── Options ────────────────────────────────────────────────
    public ObservableCollection<string> FilterOptions { get; } = new()
    { "Alle", "Starker Kauf", "Kauf", "Halten", "Verkauf", "Starker Verkauf" };
    public ObservableCollection<string> SortOptions { get; } = new()
    { "Name", "Preis", "24h%", "Score" };
    public ObservableCollection<string> IntervalOptions { get; } = new()
    { "1 Min", "2 Min", "5 Min", "10 Min", "15 Min", "30 Min" };
    public ObservableCollection<string> TimeframeOptions { get; } = new()
    { "24h", "7 Tage", "1 Monat", "3 Monate", "1 Jahr" };
    public ObservableCollection<string> CurrencyOptions { get; } = new()
    { "USD", "EUR" };
    public ObservableCollection<string> LanguageOptions { get; } = new()
    { "DE", "EN" };
    public ObservableCollection<string> PortfolioSortOptions { get; } = new()
    { "G/V %", "G/V", "Wert", "Investiert", "Coin", "Menge" };

    [ObservableProperty] private string _selectedLanguage = Loc.Language == "en" ? "EN" : "DE";

    partial void OnSelectedLanguageChanged(string value)
    {
        Loc.Language = value == "EN" ? "en" : "de";
    }
    partial void OnSelectedStrategyFilterPresetChanged(string value) => ApplyStrategyFilterPreset(value);
    partial void OnSelectedPortfolioSortChanged(string value) => RefreshPortfolioPositions();
    partial void OnPortfolioSortAscendingChanged(bool value) => RefreshPortfolioPositions();
    public ObservableCollection<string> AiProviderOptions { get; } = new()
    { "ChatGPT", "Claude" };

    private static int ParseIntervalMinutes(string interval) => interval switch
    {
        "1 Min" => 1, "2 Min" => 2, "5 Min" => 5,
        "10 Min" => 10, "15 Min" => 15, "30 Min" => 30, _ => 2
    };

    private static (int Interval, int Days) ParseTimeframe(string tf) => tf switch
    {
        "24h" => (60, 1),
        "7 Tage" => (60, 7),
        "1 Monat" => (240, 30),
        "3 Monate" => (1440, 90),
        "1 Jahr" => (1440, 365),
        _ => (60, 7)
    };

    public MainWindowViewModel()
    {
        _krakenApi = new KrakenApiService();
        _technicalAnalysis = new TechnicalAnalysisService();
        _scoringService = new ScoringService();
        _portfolioService = new PortfolioService();
        _aiTradingService = new AiTradingService();
        _secureStorage = new SecureStorageService();
        _strategyService = new StrategyExecutionService();

        // Load preset strategy names first, then saved ones
        foreach (var (name, _) in StrategyPresets.All)
            SavedStrategyNames.Add(name);
        foreach (var name in _strategyService.ListSavedStrategies())
            if (!SavedStrategyNames.Contains(name))
                SavedStrategyNames.Add(name);

        // Initialize with RSI Momentum preset as the default template
        _currentStrategy = StrategyPresets.RsiMomentum();
        ApplyStrategyFilterPreset(SelectedStrategyFilterPreset);
        PortfolioFeeRateInput = (_portfolioService.TradingFeeRate * 100m).ToString("N2");
        RefreshPortfolioPositions();

        // Load persisted API key (decrypted)
        var savedKey = _secureStorage.LoadApiKey();
        if (!string.IsNullOrEmpty(savedKey))
            _aiApiKey = savedKey;
    }

    // ── Property change handlers ───────────────────────────────
    partial void OnSearchTextChanged(string value) => RefreshCoinsOnUiThread();
    partial void OnSelectedFilterChanged(string value) => RefreshCoinsOnUiThread();
    partial void OnSelectedSortChanged(string value) => RefreshCoinsOnUiThread();
    partial void OnSortAscendingChanged(bool value) => RefreshCoinsOnUiThread();
    partial void OnSelectedTimeframeChanged(string value)
    {
        if (!IsScanning && _allCoins.Count > 0)
            Dispatcher.UIThread.Post(() => ScanCommand.Execute(null));
    }

    partial void OnSelectedCoinChanged(CryptoCoin? value)
    {
        ShowDetailPanel = value != null;
        // Fetch order book when a coin is selected (lazy load)
        if (value != null && value.OrderBook == null)
            _ = FetchOrderBookAsync(value);
    }

    private async Task FetchOrderBookAsync(CryptoCoin coin)
    {
        try
        {
            var ob = await _krakenApi.GetOrderBookAsync(coin.PairName, 50);
            coin.OrderBook = ob;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[MainWindowViewModel] Order book load failed for {coin.PairName}: {ex}");
        }
    }

    partial void OnIsAutoRefreshEnabledChanged(bool value)
    {
        if (value) StartAutoRefresh(); else StopAutoRefresh();
    }

    partial void OnSelectedIntervalChanged(string value)
    {
        if (IsAutoRefreshEnabled) { StopAutoRefresh(); StartAutoRefresh(); }
    }

    partial void OnAiApiKeyChanged(string value)
    {
        // Persist encrypted API key whenever it changes
        _secureStorage.SaveApiKey(value);
    }

    partial void OnAiProviderChanged(string value)
    {
        AiApiUrl = value == "Claude"
            ? "https://api.anthropic.com/v1/messages"
            : "https://api.openai.com/v1/chat/completions";
    }

    // ── Auto refresh ───────────────────────────────────────────
    private void StartAutoRefresh()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts = new CancellationTokenSource();
        var ct = _autoRefreshCts.Token;
        var minutes = ParseIntervalMinutes(SelectedInterval);

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(minutes), ct);
                if (!ct.IsCancellationRequested && !IsScanning)
                    await Dispatcher.UIThread.InvokeAsync(() => ScanCommand.Execute(null));
            }
        }, ct);
    }

    private void StopAutoRefresh()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts = null;
    }

    // ── Coin list management ───────────────────────────────────
    private void RefreshCoinsOnUiThread()
    {
        if (Dispatcher.UIThread.CheckAccess()) RepopulateCoins();
        else Dispatcher.UIThread.Post(RepopulateCoins);
    }

    private void RepopulateCoins()
    {
        var filtered = _allCoins.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim().ToUpperInvariant();
            filtered = filtered.Where(c =>
                c.DisplayName.ToUpperInvariant().Contains(search) ||
                c.PairName.ToUpperInvariant().Contains(search));
        }

        if (!string.IsNullOrEmpty(SelectedFilter) && SelectedFilter != "Alle")
        {
            filtered = SelectedFilter switch
            {
                "Starker Kauf" => filtered.Where(c => c.Signal == SignalType.StarkerKauf),
                "Kauf" => filtered.Where(c => c.Signal == SignalType.Kauf),
                "Halten" => filtered.Where(c => c.Signal == SignalType.Halten),
                "Verkauf" => filtered.Where(c => c.Signal == SignalType.Verkauf),
                "Starker Verkauf" => filtered.Where(c => c.Signal == SignalType.StarkerVerkauf),
                _ => filtered
            };
        }

        filtered = (SelectedSort ?? "Name") switch
        {
            "Preis" => SortAscending ? filtered.OrderBy(c => c.CurrentPrice) : filtered.OrderByDescending(c => c.CurrentPrice),
            "24h%" => SortAscending ? filtered.OrderBy(c => c.Change24hPercent) : filtered.OrderByDescending(c => c.Change24hPercent),
            "Score" => SortAscending ? filtered.OrderBy(c => c.CompositeScore) : filtered.OrderByDescending(c => c.CompositeScore),
            _ => SortAscending ? filtered.OrderBy(c => c.DisplayName) : filtered.OrderByDescending(c => c.DisplayName)
        };

        var list = filtered.ToList();
        Coins.Clear();
        foreach (var coin in list) Coins.Add(coin);
    }

    // ═══════════════════════════════════════════════════════════
    // SCANNER
    // ═══════════════════════════════════════════════════════════
    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning) return;
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var previouslySelectedPair = SelectedCoin?.PairName;

        IsScanning = true;
        ErrorText = string.Empty;
        StatusText = "Lade Handelspaare...";

        try
        {
            var currency = SelectedCurrency ?? "USD";
            var currencySymbol = currency == "EUR" ? "\u20AC" : "$";
            var pairs = await _krakenApi.GetTradingPairsAsync(currency, ct);

            if (pairs.Count == 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() => StatusText = $"Keine {currency}-Handelspaare gefunden.");
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ScanTotal = pairs.Count;
                ScanProgress = 0;
                StatusText = $"Lade Preisdaten ({pairs.Count} Paare)...";
            });

            var pairNames = pairs.Select(p => p.PairName).ToList();
            var allTickers = await _krakenApi.GetAllTickerDataAsync(pairNames, ct);

            var coins = new List<CryptoCoin>();
            foreach (var pair in pairs)
            {
                var coin = new CryptoCoin
                {
                    PairName = pair.PairName,
                    DisplayName = pair.Base,
                    BaseCurrency = pair.Base,
                    QuoteCurrency = pair.Quote,
                    CurrencySymbol = currencySymbol,
                };
                if (allTickers.TryGetValue(pair.PairName, out var ticker))
                {
                    coin.CurrentPrice = ticker.LastPrice;
                    coin.Change24hPercent = ticker.Change24hPercent;
                    coin.High24h = ticker.High24h;
                    coin.Low24h = ticker.Low24h;
                    coin.Volume24h = ticker.Volume24h;
                }
                coins.Add(coin);
            }

            _allCoins = coins;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RepopulateCoins();
                StatusText = $"{coins.Count} Coins geladen. Analysiere OHLC-Daten...";
            });

            var (ohlcInterval, ohlcDays) = ParseTimeframe(SelectedTimeframe);
            var sincestamp = DateTimeOffset.UtcNow.AddDays(-ohlcDays).ToUnixTimeSeconds();
            var timeframe = SelectedTimeframe;
            int completed = 0, errors = 0;

            var tasks = coins.Select(coin => Task.Run(async () =>
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    var ohlcData = await _krakenApi.GetOhlcDataAsync(coin.PairName, ohlcInterval, sincestamp, ct);
                    if (ohlcData.Count >= 14)
                    {
                        coin.OhlcData = ohlcData;
                        var indicators = _technicalAnalysis.Calculate(ohlcData, coin.CurrentPrice);
                        indicators.PeriodLabel = timeframe;
                        coin.Indicators = indicators;
                        var (score, signal) = _scoringService.CalculateScore(indicators);
                        coin.CompositeScore = score;
                        coin.Signal = signal;
                    }
                    else { coin.Signal = SignalType.Halten; coin.ErrorMessage = "Nicht genug Daten"; }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    coin.ErrorMessage = $"Fehler: {ex.Message}";
                    coin.Signal = SignalType.Halten;
                    Interlocked.Increment(ref errors);
                }
                finally
                {
                    coin.IsLoading = false;
                    var n = Interlocked.Increment(ref completed);
                    if (n % 5 == 0 || n == coins.Count)
                        Dispatcher.UIThread.Post(() => { ScanProgress = n; StatusText = $"Analysiere... ({n}/{coins.Count})"; });
                }
            }, CancellationToken.None)).ToList();

            await Task.WhenAll(tasks);
            if (ct.IsCancellationRequested) return;

            // Update portfolio prices with latest data
            _portfolioService.UpdatePrices(coins);

            var errCount = errors;
            var coinCount = coins.Count;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                EvaluateStrategyOnCoins();
                RepopulateCoins();
                if (!string.IsNullOrWhiteSpace(previouslySelectedPair))
                {
                    var restoredCoin = Coins.FirstOrDefault(c => c.PairName == previouslySelectedPair)
                        ?? _allCoins.FirstOrDefault(c => c.PairName == previouslySelectedPair);
                    if (restoredCoin != null)
                        SelectedCoin = restoredCoin;
                }
                LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");
                StatusText = $"Scan abgeschlossen: {coinCount} Coins gefunden" + (errCount > 0 ? $" ({errCount} Fehler)" : "");
                OnPropertyChanged(nameof(Portfolio));
                PortfolioSnapshots = Portfolio.ValueHistory.ToList();
                RefreshPortfolioPositions();

                // AI auto-trading after scan
                if (AiAutoTrading && _allCoins.Count > 0)
                    _ = RunAiAnalysisAsync();

                // Run active strategy after scan
                if (CurrentStrategy.IsActive && _allCoins.Count > 0)
                {
                    TestStrategy();
                    if (StrategyAutoApplyToPaperPortfolio)
                        ApplyStrategyToPaperPortfolio();
                }
            });
        }
        catch (OperationCanceledException) { Dispatcher.UIThread.Post(() => StatusText = "Scan abgebrochen"); }
        catch (Exception ex) { Dispatcher.UIThread.Post(() => { ErrorText = ex.Message; StatusText = "Scan fehlgeschlagen"; }); }
        finally { Dispatcher.UIThread.Post(() => IsScanning = false); }
    }

    [RelayCommand] private void StopScan() => _scanCts?.Cancel();
    [RelayCommand] private void CloseDetail() { SelectedCoin = null; ShowDetailPanel = false; }
    [RelayCommand] private void ToggleSortDirection() => SortAscending = !SortAscending;
    [RelayCommand] private void OpenHelp() => ShowHelpDialog = true;
    [RelayCommand] private void CloseHelp() => ShowHelpDialog = false;

    [RelayCommand]
    private void SelectTab(string? index)
    {
        if (int.TryParse(index, out var i))
            SelectedTabIndex = i;
    }

    // ═══════════════════════════════════════════════════════════
    // PAPER TRADING - BUY / SELL
    // ═══════════════════════════════════════════════════════════
    [RelayCommand]
    private void OpenBuyDialog(CryptoCoin? coin)
    {
        if (coin == null) return;
        TradeCoin = coin;
        TradeAmount = ""; TradeValueInput = ""; TradeByValue = false;
        TradeMessage = string.Empty;
        ShowBuyDialog = true;
        OnPropertyChanged(nameof(TradeMaxInfo));
        OnPropertyChanged(nameof(TradePreview));
    }

    [RelayCommand]
    private void OpenSellDialog(PortfolioPosition? position)
    {
        if (position == null) return;
        SellPosition = position;
        var coin = _allCoins.FirstOrDefault(c => c.DisplayName == position.Symbol);
        TradeCoin = coin;
        TradeAmount = ""; TradeValueInput = ""; TradeByValue = false;
        TradeMessage = string.Empty;
        ShowSellDialog = true;
        OnPropertyChanged(nameof(TradeMaxInfo));
        OnPropertyChanged(nameof(TradePreview));
    }

    [RelayCommand]
    private void FillMaxTrade()
    {
        if (ShowBuyDialog && TradeCoin != null && TradeCoin.CurrentPrice > 0)
        {
            if (TradeByValue)
                TradeValueInput = (Portfolio.Balance / (1m + _portfolioService.TradingFeeRate)).ToString("N2");
            else
                TradeAmount = (Portfolio.Balance / (TradeCoin.CurrentPrice * (1m + _portfolioService.TradingFeeRate))).ToString("N6");
        }
        else if (ShowSellDialog && SellPosition != null)
        {
            TradeAmount = SellPosition.Amount.ToString("N6");
        }
    }

    [RelayCommand]
    private void ExecuteBuy()
    {
        if (TradeCoin == null || TradeCoin.CurrentPrice <= 0) return;

        decimal amount;
        if (TradeByValue)
        {
            if (!decimal.TryParse(TradeValueInput?.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var val) || val <= 0)
            { TradeMessage = "Bitte gueltigen Betrag eingeben."; return; }
            amount = val / TradeCoin.CurrentPrice;
        }
        else
        {
            if (!decimal.TryParse(TradeAmount?.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out amount) || amount <= 0)
            { TradeMessage = "Bitte gueltige Menge eingeben."; return; }
        }

        var (success, message) = _portfolioService.Buy(
            TradeCoin.DisplayName, TradeCoin.DisplayName, amount, TradeCoin.CurrentPrice);
        TradeMessage = message;

        if (success)
        {
            OnPropertyChanged(nameof(Portfolio));
            PortfolioSnapshots = Portfolio.ValueHistory.ToList();
            RefreshPortfolioPositions();
            OnPropertyChanged(nameof(CanEditStartingBalance));
            ShowBuyDialog = false;
            StatusText = message;
        }
    }

    [RelayCommand]
    private void ExecuteSell()
    {
        if (SellPosition == null) return;
        if (!decimal.TryParse(TradeAmount?.Replace(",", "."),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            TradeMessage = "Bitte gueltige Menge eingeben.";
            return;
        }

        var price = SellPosition.CurrentPrice;
        if (TradeCoin != null) price = TradeCoin.CurrentPrice;

        var (success, message) = _portfolioService.Sell(SellPosition.Symbol, amount, price);
        TradeMessage = message;

        if (success)
        {
            OnPropertyChanged(nameof(Portfolio));
            PortfolioSnapshots = Portfolio.ValueHistory.ToList();
            RefreshPortfolioPositions();
            ShowSellDialog = false;
            StatusText = message;
        }
    }

    [RelayCommand]
    private void CancelTrade()
    {
        ShowBuyDialog = false;
        ShowSellDialog = false;
        TradeMessage = string.Empty;
    }

    [RelayCommand]
    private void ResetPortfolio()
    {
        var currency = SelectedCurrency ?? "USD";
        if (!decimal.TryParse(StartingBalanceInput?.Replace(",", "."),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var balance) || balance <= 0)
            balance = 10000m;

        _portfolioService.ResetPortfolio(balance, currency);
        OnPropertyChanged(nameof(Portfolio));
        PortfolioSnapshots = Portfolio.ValueHistory.ToList();
        RefreshPortfolioPositions();
        OnPropertyChanged(nameof(CanEditStartingBalance));
        StatusText = $"Depot zurueckgesetzt auf {balance:N0} {currency}";
    }

    // ═══════════════════════════════════════════════════════════
    // AI TRADING
    // ═══════════════════════════════════════════════════════════
    [RelayCommand]
    private async Task RunAiAnalysisAsync()
    {
        if (IsAiAnalyzing || _allCoins.Count == 0) return;
        IsAiAnalyzing = true;
        AiStatusText = "KI analysiert...";

        try
        {
            _aiTradingService.Configure(AiApiKey, AiApiUrl, AiProvider);

            var recommendations = await Task.Run(() =>
                _aiTradingService.AnalyzeAndRecommend(_allCoins, Portfolio));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AiRecommendations.Clear();
                foreach (var r in recommendations) AiRecommendations.Add(r);

                var source = _aiTradingService.IsConfigured ? AiProvider : "Regelbasiert";
                AiStatusText = $"{recommendations.Count} Empfehlungen ({source})";
                AiLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {recommendations.Count} Empfehlungen von {source}");
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                AiStatusText = $"Fehler: {ex.Message}";
                AiLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] FEHLER: {ex.Message}");
            });
        }
        finally
        {
            Dispatcher.UIThread.Post(() => IsAiAnalyzing = false);
        }
    }

    [RelayCommand]
    private void ExecuteAiOrder(TradeOrder? order)
    {
        if (order == null) return;
        var (success, message) = _portfolioService.ExecuteAiTrade(order);
        if (success)
        {
            OnPropertyChanged(nameof(Portfolio));
            PortfolioSnapshots = Portfolio.ValueHistory.ToList();
            RefreshPortfolioPositions();
            AiLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {order.ActionText} {order.Symbol}: {message}");
            AiRecommendations.Remove(order);
        }
        else
        {
            AiLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] FEHLER {order.Symbol}: {message}");
        }
    }

    [RelayCommand]
    private void ExecuteAllAiOrders()
    {
        var orders = AiRecommendations.ToList();
        foreach (var order in orders)
            ExecuteAiOrder(order);
        StatusText = $"{orders.Count} KI-Trades ausgefuehrt";
    }

    // ═══════════════════════════════════════════════════════════
    // STRATEGY — evaluate per-coin for Scanner column
    // ═══════════════════════════════════════════════════════════

    private void EvaluateStrategyOnCoins()
    {
        if (!CurrentStrategy.IsActive || _allCoins.Count == 0)
        {
            foreach (var c in _allCoins) c.StrategyAction = "";
            return;
        }

        var orders = GetCurrentStrategyOrders();
        var orderMap = new Dictionary<string, TradeOrder>();
        foreach (var o in orders)
            orderMap[o.Symbol] = o;

        foreach (var coin in _allCoins)
        {
            if (orderMap.TryGetValue(coin.DisplayName, out var order))
                coin.StrategyAction = order.ActionText + (order.Action != TradeAction.Hold ? $" {order.Amount:N4}" : "");
            else
                coin.StrategyAction = "Halten";
        }
    }

    // ═══════════════════════════════════════════════════════════
    // STRATEGY BUILDER
    // ═══════════════════════════════════════════════════════════

    [RelayCommand]
    private void AddStrategyBlock(string? param)
    {
        if (string.IsNullOrEmpty(param)) return;
        var parts = param.Split('|');
        var typeStr = parts[0];
        var category = parts.Length > 1 ? parts[1] : "";
        var (x, y) = FindNextStrategyBlockPosition();

        var block = new StrategyBlock
        {
            X = x,
            Y = y
        };

        switch (typeStr)
        {
            case "Condition":
                block.Type = BlockType.Condition;
                block.Category = category;
                // Set defaults based on category
                switch (category)
                {
                    case "RSI": block.Operator = "<"; block.Value = 30; break;
                    case "Score": block.Operator = ">"; block.Value = 40; break;
                    case "Preis-Aenderung": block.Operator = ">"; block.Value = 5; break;
                    case "Orderbuch": block.Operator = ">"; block.Value = 60; break;
                    case "Preisspanne": block.Operator = "<"; block.Value = 30; break;
                    case "MACD": block.ConditionPreset = "Signal positiv"; break;
                    case "SMA": block.ConditionPreset = "Preis ueber SMA20"; break;
                    case "EMA": block.ConditionPreset = "Preis ueber EMA12"; break;
                    case "Bollinger": block.ConditionPreset = "Preis unter unteres Band"; break;
                    case "Volumen": block.ConditionPreset = "Ueber Durchschnitt"; break;
                }
                break;
            case "Buy": block.Type = BlockType.ActionBuy; block.ActionAmount = "10%"; break;
            case "Sell": block.Type = BlockType.ActionSell; block.ActionAmount = "Alles"; break;
            case "Hold": block.Type = BlockType.ActionHold; break;
            case "Alarm": block.Type = BlockType.ActionAlarm; block.AlarmText = "Signal erkannt"; break;
        }

        CurrentStrategy.Blocks.Add(block);
        OnPropertyChanged(nameof(CurrentStrategy));
        StrategyStatusText = $"Block \"{block.Title}\" hinzugefuegt";
    }

    private (double X, double Y) FindNextStrategyBlockPosition()
    {
        const double startX = 260;
        const double startY = 80;
        const double grid = 20;
        const double stepX = 220;
        const double stepY = 100;
        const double blockW = 190;
        const double blockH = 74;
        const double padding = 24;

        static double Snap(double value, double gridSize) => Math.Round(value / gridSize) * gridSize;

        bool Overlaps(double x, double y) =>
            CurrentStrategy.Blocks.Any(b =>
                x < b.X + blockW + padding &&
                x + blockW + padding > b.X &&
                y < b.Y + blockH + padding &&
                y + blockH + padding > b.Y);

        for (int row = 0; row < 12; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var x = Snap(startX + col * stepX, grid);
                var y = Snap(startY + row * stepY, grid);
                if (!Overlaps(x, y))
                    return (x, y);
            }
        }

        var fallbackX = Snap(startX + (CurrentStrategy.Blocks.Count % 6) * stepX, grid);
        var fallbackY = Snap(startY + (CurrentStrategy.Blocks.Count / 6) * stepY, grid);
        return (fallbackX, fallbackY);
    }

    [RelayCommand]
    private void SaveStrategy()
    {
        _strategyService.Save(CurrentStrategy);
        if (!SavedStrategyNames.Contains(CurrentStrategy.Name))
            SavedStrategyNames.Add(CurrentStrategy.Name);
        StrategyStatusText = $"Strategie \"{CurrentStrategy.Name}\" gespeichert";
    }

    [RelayCommand]
    private void LoadStrategy(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;

        // Check presets first
        var preset = StrategyPresets.All.FirstOrDefault(p => p.Name == name);
        if (preset.Factory != null)
        {
            CurrentStrategy = preset.Factory();
            StrategyStatusText = $"Vorlage \"{name}\" geladen";
            return;
        }

        var loaded = _strategyService.Load(name);
        if (loaded != null)
        {
            CurrentStrategy = loaded;
            StrategyStatusText = $"Strategie \"{name}\" geladen ({loaded.Blocks.Count} Bloecke)";
        }
    }

    [RelayCommand]
    private void TestStrategy()
    {
        if (_allCoins.Count == 0)
        {
            StrategyStatusText = "Bitte zuerst einen Scan durchfuehren.";
            return;
        }

        var results = GetCurrentStrategyOrders();
        StrategyResults.Clear();
        foreach (var r in results) StrategyResults.Add(r);

        StrategyLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Test: {results.Count} Trades vorgeschlagen");
        foreach (var r in results)
            StrategyLog.Insert(0, $"  {r.ActionText} {r.Symbol}: {r.Reason}");

        StrategyStatusText = $"Test abgeschlossen: {results.Count} Trades";
    }

    [RelayCommand]
    private async Task OptimizeStrategyAsync()
    {
        if (IsOptimizingStrategy || _allCoins.Count == 0)
        {
            if (_allCoins.Count == 0)
                StrategyStatusText = "Bitte zuerst einen Scan durchfuehren.";
            return;
        }

        var backtestCoins = _allCoins
            .Where(c => c.OhlcData != null && c.OhlcData.Count >= 80 && c.CurrentPrice > 0)
            .OrderByDescending(c => c.Volume24h)
            .Take(12)
            .ToList();

        if (backtestCoins.Count < 2)
        {
            StrategyStatusText = "Zu wenig OHLC-Daten fuer die Optimierung.";
            return;
        }

        IsOptimizingStrategy = true;
        StrategyStatusText = $"Optimiere Strategien mit {backtestCoins.Count} Coins...";

        try
        {
            var results = await Task.Run(() => RunStrategyOptimization(backtestCoins));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StrategyOptimizationResults.Clear();
                foreach (var result in results)
                    StrategyOptimizationResults.Add(result);

                if (results.Count > 0)
                {
                    var best = results[0];
                    StrategyStatusText = $"Optimierung fertig: {best.StrategyName} + {best.FilterPreset} ist aktuell am staerksten.";
                    StrategyLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Optimierung: bestes Setup {best.StrategyName} + {best.FilterPreset} ({best.ReturnFormatted})");
                }
                else
                {
                    StrategyStatusText = "Optimierung abgeschlossen, aber es wurden keine brauchbaren Varianten gefunden.";
                }
            });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[MainWindowViewModel] Strategy optimization failed: {ex}");
            StrategyStatusText = $"Optimierung fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsOptimizingStrategy = false;
        }
    }

    [RelayCommand]
    private void ApplyOptimizationResult(StrategyOptimizationResult? result)
    {
        if (result == null)
            return;

        var preset = StrategyPresets.All.FirstOrDefault(p => p.Name == result.StrategyName);
        if (preset.Factory == null)
        {
            StrategyStatusText = "Optimierte Strategie konnte nicht geladen werden.";
            return;
        }

        CurrentStrategy = preset.Factory();
        SelectedStrategyFilterPreset = result.FilterPreset;
        ApplyStrategyFilterPreset(result.FilterPreset);
        StrategyStatusText = $"Optimierte Strategie geladen: {result.StrategyName} + {result.FilterPreset}";
        StrategyLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Optimierung uebernommen: {result.StrategyName} + {result.FilterPreset}");
    }

    [RelayCommand]
    private void ApplyStrategyToPaperPortfolio()
    {
        if (_allCoins.Count == 0)
        {
            StrategyStatusText = "Bitte zuerst einen Scan durchfuehren.";
            return;
        }

        var orders = GetCurrentStrategyOrders();
        if (orders.Count == 0)
        {
            StrategyResults.Clear();
            StrategyStatusText = "Keine Strategie-Trades fuer das Fake-Depot gefunden.";
            StrategyLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Keine Strategie-Trades zum Ausfuehren");
            return;
        }

        StrategyResults.Clear();
        foreach (var order in orders)
            StrategyResults.Add(order);

        int executed = 0;
        foreach (var order in orders)
        {
            var (success, message) = _portfolioService.ExecuteStrategyTrade(order);
            if (success)
            {
                executed++;
                StrategyLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {order.ActionText} {order.Symbol}: {message}");
                StrategyResults.Remove(order);
            }
            else
            {
                StrategyLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] FEHLER {order.Symbol}: {message}");
            }
        }

        OnPropertyChanged(nameof(Portfolio));
        PortfolioSnapshots = Portfolio.ValueHistory.ToList();
        RefreshPortfolioPositions();
        OnPropertyChanged(nameof(CanEditStartingBalance));
        StrategyStatusText = $"Fake-Depot aktualisiert: {executed}/{orders.Count} Strategie-Trades ausgefuehrt";
    }

    [RelayCommand]
    private void ExecuteStrategyOrder(TradeOrder? order)
    {
        if (order == null) return;
        var (success, message) = _portfolioService.ExecuteStrategyTrade(order);
        if (success)
        {
            OnPropertyChanged(nameof(Portfolio));
            PortfolioSnapshots = Portfolio.ValueHistory.ToList();
            RefreshPortfolioPositions();
            StrategyLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {order.ActionText} {order.Symbol}: {message}");
            StrategyResults.Remove(order);
        }
    }

    [RelayCommand]
    private void ExecuteAllStrategyOrders()
    {
        var orders = StrategyResults.ToList();
        foreach (var o in orders) ExecuteStrategyOrder(o);
        StrategyStatusText = $"{orders.Count} Strategie-Trades ausgefuehrt";
    }

    private List<TradeOrder> GetCurrentStrategyOrders()
    {
        var orders = _strategyService.Execute(CurrentStrategy, _allCoins, Portfolio);

        return ApplyStrategyFilters(orders, _allCoins, Portfolio, CaptureCurrentStrategyFilterSettings());
    }

    private record StrategyFilterSettings(
        bool RequirePositiveDayChange,
        bool RequireAboveSma50,
        bool RequirePositiveMacd,
        bool PreferTopScoreBuys,
        bool RequireMinScore40,
        bool RequireAboveAverageVolume,
        bool SkipExistingPositions);

    private StrategyFilterSettings CaptureCurrentStrategyFilterSettings() =>
        new(
            StrategyRequirePositiveDayChange,
            StrategyRequireAboveSma50,
            StrategyRequirePositiveMacd,
            StrategyPreferTopScoreBuys,
            StrategyRequireMinScore40,
            StrategyRequireAboveAverageVolume,
            StrategySkipExistingPositions);

    private List<TradeOrder> ApplyStrategyFilters(List<TradeOrder> orders, List<CryptoCoin> coins, Portfolio portfolio, StrategyFilterSettings settings)
    {
        // Apply optional quality filters before showing or executing strategy trades.
        if (orders.Count == 0)
            return orders;

        var buyOrders = new List<TradeOrder>();
        var sellOrders = new List<TradeOrder>();

        foreach (var order in orders)
        {
            if (order.Action == TradeAction.Sell)
            {
                sellOrders.Add(order);
                continue;
            }

            if (order.Action != TradeAction.Buy)
                continue;

            var coin = coins.FirstOrDefault(c => c.DisplayName == order.Symbol);
            if (coin?.Indicators == null)
                continue;

            if (settings.RequirePositiveDayChange && coin.Change24hPercent <= 0)
                continue;

            if (settings.RequireAboveSma50 && coin.CurrentPrice <= coin.Indicators.Sma50)
                continue;

            if (settings.RequirePositiveMacd && coin.Indicators.MacdHistogram <= 0)
                continue;

            if (settings.RequireMinScore40 && coin.CompositeScore < 40)
                continue;

            if (settings.RequireAboveAverageVolume && !coin.Indicators.IsVolumeAboveAverage)
                continue;

            if (settings.SkipExistingPositions &&
                portfolio.Positions.Any(p => p.Symbol == coin.DisplayName && p.Amount > 0))
                continue;

            buyOrders.Add(order);
        }

        if (settings.PreferTopScoreBuys)
        {
            buyOrders = buyOrders
                .OrderByDescending(o => coins.FirstOrDefault(c => c.DisplayName == o.Symbol)?.CompositeScore ?? int.MinValue)
                .Take(3)
                .ToList();
        }

        return sellOrders.Concat(buyOrders).ToList();
    }

    private List<StrategyOptimizationResult> RunStrategyOptimization(List<CryptoCoin> sourceCoins)
    {
        var filterPresets = new[]
        {
            "Aggressiv",
            "Ausgewogen",
            "Konservativ"
        };

        var candidates = new List<StrategyOptimizationResult>();
        foreach (var (name, factory) in StrategyPresets.All)
        {
            foreach (var filterPreset in filterPresets)
            {
                var settings = GetFilterSettingsForPreset(filterPreset);
                var result = BacktestStrategy(factory(), name, filterPreset, settings, sourceCoins);
                if (result != null)
                    candidates.Add(result);
            }
        }

        return candidates
            .OrderByDescending(r => r.ReturnPercent)
            .ThenBy(r => r.MaxDrawdownPercent)
            .ThenByDescending(r => r.WinRatePercent)
            .Take(5)
            .ToList();
    }

    private StrategyFilterSettings GetFilterSettingsForPreset(string preset) => preset switch
    {
        "Konservativ" => new(true, true, true, true, true, true, true),
        "Aggressiv" => new(false, false, false, false, false, false, false),
        _ => new(true, true, true, true, true, false, true)
    };

    private StrategyOptimizationResult? BacktestStrategy(
        TradingStrategy strategy,
        string strategyName,
        string filterPreset,
        StrategyFilterSettings settings,
        List<CryptoCoin> sourceCoins)
    {
        var candidates = sourceCoins
            .Where(c => c.OhlcData != null && c.OhlcData.Count >= 80)
            .ToList();

        if (candidates.Count < 2)
            return null;

        var minLength = candidates.Min(c => c.OhlcData!.Count);
        if (minLength < 80)
            return null;

        var initialBalance = Portfolio.InitialBalance > 0 ? Portfolio.InitialBalance : 10000m;
        var simPortfolio = new Portfolio
        {
            Balance = initialBalance,
            InitialBalance = initialBalance,
            Currency = Portfolio.Currency,
            CreatedAt = DateTime.Now
        };

        decimal peakValue = initialBalance;
        decimal maxDrawdown = 0m;
        int sells = 0;
        int winningSells = 0;

        for (int step = 60; step < minLength - 1; step += 4)
        {
            var simCoins = BuildBacktestCoins(candidates, step);
            if (simCoins.Count < 2)
                continue;

            UpdateSimPortfolioPrices(simPortfolio, simCoins);
            var rawOrders = _strategyService.Execute(strategy, simCoins, simPortfolio);
            var filteredOrders = ApplyStrategyFilters(rawOrders, simCoins, simPortfolio, settings);
            ExecuteBacktestOrders(simPortfolio, filteredOrders, ref sells, ref winningSells);

            UpdateSimPortfolioPrices(simPortfolio, simCoins);
            var totalValue = simPortfolio.TotalValue;
            if (totalValue > peakValue)
                peakValue = totalValue;

            if (peakValue > 0)
            {
                var drawdown = (peakValue - totalValue) / peakValue * 100m;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }
        }

        var finalCoins = BuildBacktestCoins(candidates, minLength - 1);
        UpdateSimPortfolioPrices(simPortfolio, finalCoins);
        var finalValue = simPortfolio.TotalValue;
        var tradeCount = simPortfolio.TransactionHistory.Count;
        if (tradeCount == 0)
            return null;

        return new StrategyOptimizationResult
        {
            StrategyName = strategyName,
            FilterPreset = filterPreset,
            FinalValue = finalValue,
            ReturnPercent = initialBalance > 0 ? ((finalValue - initialBalance) / initialBalance) * 100m : 0,
            MaxDrawdownPercent = maxDrawdown,
            Trades = tradeCount,
            WinRatePercent = sells > 0 ? winningSells * 100m / sells : 0
        };
    }

    private List<CryptoCoin> BuildBacktestCoins(List<CryptoCoin> candidates, int step)
    {
        var list = new List<CryptoCoin>();
        foreach (var coin in candidates)
        {
            var candles = coin.OhlcData;
            if (candles == null || candles.Count <= step)
                continue;

            var slice = candles.Take(step + 1).ToList();
            if (slice.Count < 60)
                continue;

            var currentPrice = slice[^1].Close;
            var indicators = _technicalAnalysis.Calculate(slice, currentPrice);
            var (score, signal) = _scoringService.CalculateScore(indicators);
            var referenceIndex = Math.Max(0, slice.Count - 25);
            var referencePrice = slice[referenceIndex].Close;
            var change24h = referencePrice > 0 ? ((currentPrice - referencePrice) / referencePrice) * 100m : 0;

            list.Add(new CryptoCoin
            {
                PairName = coin.PairName,
                DisplayName = coin.DisplayName,
                BaseCurrency = coin.BaseCurrency,
                QuoteCurrency = coin.QuoteCurrency,
                CurrencySymbol = coin.CurrencySymbol,
                CurrentPrice = currentPrice,
                Change24hPercent = change24h,
                Volume24h = slice.TakeLast(Math.Min(24, slice.Count)).Sum(c => c.Volume),
                Indicators = indicators,
                CompositeScore = score,
                Signal = signal
            });
        }

        return list;
    }

    private void UpdateSimPortfolioPrices(Portfolio portfolio, List<CryptoCoin> coins)
    {
        foreach (var position in portfolio.Positions)
        {
            var coin = coins.FirstOrDefault(c => c.DisplayName == position.Symbol);
            if (coin != null)
                position.CurrentPrice = coin.CurrentPrice;
        }

        portfolio.RefreshComputedProperties();
    }

    private void ExecuteBacktestOrders(Portfolio portfolio, List<TradeOrder> orders, ref int sells, ref int winningSells)
    {
        foreach (var order in orders)
        {
            if (order.Action == TradeAction.Buy)
            {
                ExecuteBacktestBuy(portfolio, order);
            }
            else if (order.Action == TradeAction.Sell)
            {
                var wasWin = ExecuteBacktestSell(portfolio, order);
                if (wasWin.HasValue)
                {
                    sells++;
                    if (wasWin.Value)
                        winningSells++;
                }
            }
        }
    }

    private void ExecuteBacktestBuy(Portfolio portfolio, TradeOrder order)
    {
        if (order.Amount <= 0 || order.CurrentPrice <= 0)
            return;

        var grossCost = order.Amount * order.CurrentPrice;
        var fee = grossCost * _portfolioService.TradingFeeRate;
        var totalCost = grossCost + fee;
        if (totalCost > portfolio.Balance)
            return;

        portfolio.Balance -= totalCost;
        var effectiveUnitCost = totalCost / order.Amount;
        var position = portfolio.Positions.FirstOrDefault(p => p.Symbol == order.Symbol);
        if (position == null)
        {
            portfolio.Positions.Add(new PortfolioPosition
            {
                Symbol = order.Symbol,
                DisplayName = order.Symbol,
                Amount = order.Amount,
                AverageBuyPrice = effectiveUnitCost,
                CurrentPrice = order.CurrentPrice
            });
        }
        else
        {
            var totalAmount = position.Amount + order.Amount;
            position.AverageBuyPrice = (position.Amount * position.AverageBuyPrice + order.Amount * effectiveUnitCost) / totalAmount;
            position.Amount = totalAmount;
            position.CurrentPrice = order.CurrentPrice;
        }

        portfolio.TransactionHistory.Add(new Transaction
        {
            Timestamp = DateTime.Now,
            Symbol = order.Symbol,
            Type = TransactionType.Kauf,
            Amount = order.Amount,
            PricePerUnit = order.CurrentPrice,
            Fee = fee,
            TotalCost = totalCost,
            Source = "Backtest"
        });
        portfolio.RefreshComputedProperties();
    }

    private bool? ExecuteBacktestSell(Portfolio portfolio, TradeOrder order)
    {
        var position = portfolio.Positions.FirstOrDefault(p => p.Symbol == order.Symbol);
        if (position == null || order.Amount <= 0 || order.Amount > position.Amount)
            return null;

        var grossRevenue = order.Amount * order.CurrentPrice;
        var fee = grossRevenue * _portfolioService.TradingFeeRate;
        var totalRevenue = grossRevenue - fee;
        var costBasis = order.Amount * position.AverageBuyPrice;
        portfolio.Balance += totalRevenue;
        position.Amount -= order.Amount;
        position.CurrentPrice = order.CurrentPrice;

        if (position.Amount <= 0.000000001m)
            portfolio.Positions.Remove(position);

        portfolio.TransactionHistory.Add(new Transaction
        {
            Timestamp = DateTime.Now,
            Symbol = order.Symbol,
            Type = TransactionType.Verkauf,
            Amount = order.Amount,
            PricePerUnit = order.CurrentPrice,
            Fee = fee,
            TotalCost = totalRevenue,
            Source = "Backtest"
        });
        portfolio.RefreshComputedProperties();
        return totalRevenue > costBasis;
    }

    private void RefreshPortfolioPositions()
    {
        var sorted = (SelectedPortfolioSort ?? "G/V %") switch
        {
            "Coin" => PortfolioSortAscending
                ? Portfolio.Positions.OrderBy(p => p.Symbol)
                : Portfolio.Positions.OrderByDescending(p => p.Symbol),
            "Menge" => PortfolioSortAscending
                ? Portfolio.Positions.OrderBy(p => p.Amount)
                : Portfolio.Positions.OrderByDescending(p => p.Amount),
            "Wert" => PortfolioSortAscending
                ? Portfolio.Positions.OrderBy(p => p.TotalValue)
                : Portfolio.Positions.OrderByDescending(p => p.TotalValue),
            "Investiert" => PortfolioSortAscending
                ? Portfolio.Positions.OrderBy(p => p.TotalCost)
                : Portfolio.Positions.OrderByDescending(p => p.TotalCost),
            "G/V" => PortfolioSortAscending
                ? Portfolio.Positions.OrderBy(p => p.ProfitLoss)
                : Portfolio.Positions.OrderByDescending(p => p.ProfitLoss),
            _ => PortfolioSortAscending
                ? Portfolio.Positions.OrderBy(p => p.ProfitLossPercent)
                : Portfolio.Positions.OrderByDescending(p => p.ProfitLossPercent)
        };

        SortedPortfolioPositions.Clear();
        foreach (var position in sorted)
            SortedPortfolioPositions.Add(position);
    }

    [RelayCommand]
    private void TogglePortfolioSortDirection() => PortfolioSortAscending = !PortfolioSortAscending;

    public void ExportPortfolioPdf(string path)
    {
        var lines = new List<string>
        {
            "CryptoScanner Depotliste",
            $"Datum: {DateTime.Now:dd.MM.yyyy HH:mm}",
            $"Barguthaben: {Portfolio.BalanceFormatted}",
            $"Gesamtwert: {Portfolio.TotalValueFormatted}",
            $"Gewinn/Verlust: {Portfolio.ProfitLossFormatted} ({Portfolio.ProfitLossPercentFormatted})",
            ""
        };

        if (SortedPortfolioPositions.Count == 0)
        {
            lines.Add("Keine Positionen vorhanden.");
        }
        else
        {
            lines.Add("COIN | MENGE | INVESTIERT | WERT | G/V");
            foreach (var position in SortedPortfolioPositions)
            {
                lines.Add($"{position.Symbol} | {position.AmountFormatted} | {position.TotalCost:N2} | {position.TotalValueFormatted} | {position.ProfitLossPercentFormatted}");
            }
        }

        WriteSimplePdf(path, lines);
        StatusText = $"Depotliste als PDF exportiert: {path}";
    }

    private static void WriteSimplePdf(string path, List<string> lines)
    {
        const int linesPerPage = 40;
        var pages = lines.Chunk(linesPerPage).Select(chunk => chunk.ToList()).ToList();
        var objects = new List<string>();
        var pageNumbers = new List<int>();

        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add(string.Empty);
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        foreach (var page in pages)
        {
            var content = BuildPdfTextStream(page);
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
            var contentNumber = objects.Count;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentNumber} 0 R >>");
            pageNumbers.Add(objects.Count);
        }

        objects[1] = $"<< /Type /Pages /Count {pageNumbers.Count} /Kids [ {string.Join(" ", pageNumbers.Select(n => $"{n} 0 R"))} ] >>";

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, System.Text.Encoding.ASCII);
        writer.WriteLine("%PDF-1.4");

        var offsets = new List<long> { 0 };
        for (int i = 0; i < objects.Count; i++)
        {
            writer.Flush();
            offsets.Add(stream.Position);
            writer.WriteLine($"{i + 1} 0 obj");
            writer.WriteLine(objects[i]);
            writer.WriteLine("endobj");
        }

        writer.Flush();
        var xrefPos = stream.Position;
        writer.WriteLine($"xref\n0 {objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        for (int i = 1; i < offsets.Count; i++)
            writer.WriteLine($"{offsets[i]:D10} 00000 n ");
        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefPos);
        writer.WriteLine("%%EOF");
    }

    private static string BuildPdfTextStream(List<string> lines)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("BT");
        sb.AppendLine("/F1 11 Tf");
        sb.AppendLine("50 790 Td");
        foreach (var rawLine in lines)
        {
            var line = rawLine.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            sb.AppendLine($"({line}) Tj");
            sb.AppendLine("0 -18 Td");
        }
        sb.AppendLine("ET");
        return sb.ToString();
    }

    private void ApplyStrategyFilterPreset(string? preset)
    {
        switch (preset)
        {
            case "Konservativ":
                StrategyRequirePositiveDayChange = true;
                StrategyRequireAboveSma50 = true;
                StrategyRequirePositiveMacd = true;
                StrategyRequireMinScore40 = true;
                StrategyRequireAboveAverageVolume = true;
                StrategySkipExistingPositions = true;
                StrategyPreferTopScoreBuys = true;
                break;
            case "Aggressiv":
                StrategyRequirePositiveDayChange = false;
                StrategyRequireAboveSma50 = false;
                StrategyRequirePositiveMacd = false;
                StrategyRequireMinScore40 = false;
                StrategyRequireAboveAverageVolume = false;
                StrategySkipExistingPositions = false;
                StrategyPreferTopScoreBuys = false;
                break;
            default:
                StrategyRequirePositiveDayChange = true;
                StrategyRequireAboveSma50 = true;
                StrategyRequirePositiveMacd = true;
                StrategyRequireMinScore40 = true;
                StrategyRequireAboveAverageVolume = false;
                StrategySkipExistingPositions = true;
                StrategyPreferTopScoreBuys = true;
                break;
        }
    }

    [RelayCommand]
    private void NewStrategy()
    {
        CurrentStrategy = StrategyPresets.RsiMomentum();
        CurrentStrategy.Name = "Neue Strategie";
        StrategyResults.Clear();
        StrategyStatusText = "Neue Strategie erstellt (RSI Momentum Vorlage)";
    }

    public void OpenStrategyBlockEditor(StrategyBlock block)
    {
        EditingStrategyBlock = block;
        StrategyBlockEditorOperator = block.Operator;
        StrategyBlockEditorValue = block.Value.ToString("N0");
        StrategyBlockEditorActionAmount = block.ActionAmount;
        StrategyBlockEditorConditionPreset = block.ConditionPreset;
        StrategyBlockEditorAlarmText = block.AlarmText;
        ShowStrategyBlockEditor = true;
    }

    [RelayCommand]
    private void SaveStrategyBlockEditor()
    {
        var block = EditingStrategyBlock;
        if (block == null)
            return;

        if (block.Type == BlockType.Condition)
        {
            block.Operator = StrategyBlockEditorOperator;
            block.ConditionPreset = StrategyBlockEditorConditionPreset?.Trim() ?? "";
            if (double.TryParse(StrategyBlockEditorValue?.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                block.Value = value;
            }
        }
        else if (block.Type == BlockType.ActionBuy || block.Type == BlockType.ActionSell)
        {
            var amount = (StrategyBlockEditorActionAmount ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(amount))
                block.ActionAmount = amount;
        }
        else if (block.Type == BlockType.ActionAlarm)
        {
            block.AlarmText = StrategyBlockEditorAlarmText?.Trim() ?? "";
        }

        ShowStrategyBlockEditor = false;
        StrategyStatusText = $"Block \"{block.Title}\" aktualisiert";
        OnPropertyChanged(nameof(CurrentStrategy));
    }

    [RelayCommand]
    private void CancelStrategyBlockEditor()
    {
        ShowStrategyBlockEditor = false;
        EditingStrategyBlock = null;
    }
}
