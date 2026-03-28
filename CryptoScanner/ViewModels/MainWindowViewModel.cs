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

    public bool CanEditStartingBalance => Portfolio.Positions.Count == 0 && Portfolio.TransactionHistory.Count == 0;

    // ── Buy/Sell dialog state ──────────────────────────────────
    [ObservableProperty] private bool _showBuyDialog;
    [ObservableProperty] private bool _showSellDialog;
    [ObservableProperty] private CryptoCoin? _tradeCoin;
    [ObservableProperty] private string _tradeAmount = "";
    [ObservableProperty] private string _tradeMessage = string.Empty;
    [ObservableProperty] private PortfolioPosition? _sellPosition;
    [ObservableProperty] private bool _tradeByValue;  // false=coin amount, true=currency value
    [ObservableProperty] private string _tradeValueInput = "";

    public string TradePreview
    {
        get
        {
            if (TradeCoin == null || TradeCoin.CurrentPrice <= 0) return "";
            var sym = TradeCoin.CurrencySymbol;
            if (TradeByValue)
            {
                if (decimal.TryParse(TradeValueInput?.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var val) && val > 0)
                    return $"= {val / TradeCoin.CurrentPrice:N6} {TradeCoin.DisplayName}";
            }
            else
            {
                if (decimal.TryParse(TradeAmount?.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var amt) && amt > 0)
                    return $"= {sym}{amt * TradeCoin.CurrentPrice:N2}";
            }
            return "";
        }
    }

    public string TradeMaxInfo
    {
        get
        {
            if (ShowBuyDialog && TradeCoin != null)
                return $"Max: {Portfolio.CurrencySymbol}{Portfolio.Balance:N2}";
            if (ShowSellDialog && SellPosition != null)
                return $"Max: {SellPosition.Amount:N6} {SellPosition.Symbol}";
            return "";
        }
    }

    partial void OnTradeAmountChanged(string value) => OnPropertyChanged(nameof(TradePreview));
    partial void OnTradeValueInputChanged(string value) => OnPropertyChanged(nameof(TradePreview));
    partial void OnTradeByValueChanged(bool value) => OnPropertyChanged(nameof(TradePreview));

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
    public ObservableCollection<TradeOrder> StrategyResults { get; } = new();
    public ObservableCollection<string> StrategyLog { get; } = new();
    public ObservableCollection<string> SavedStrategyNames { get; } = new();
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

    [ObservableProperty] private string _selectedLanguage = Loc.Language == "en" ? "EN" : "DE";

    partial void OnSelectedLanguageChanged(string value)
    {
        Loc.Language = value == "EN" ? "en" : "de";
    }
    partial void OnSelectedStrategyFilterPresetChanged(string value) => ApplyStrategyFilterPreset(value);
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
                TradeValueInput = Portfolio.Balance.ToString("N2");
            else
                TradeAmount = (Portfolio.Balance / TradeCoin.CurrentPrice).ToString("N6");
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

        var block = new StrategyBlock
        {
            X = 250 + CurrentStrategy.Blocks.Count * 30,
            Y = 80 + (CurrentStrategy.Blocks.Count % 4) * 90
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

            var coin = _allCoins.FirstOrDefault(c => c.DisplayName == order.Symbol);
            if (coin?.Indicators == null)
                continue;

            if (StrategyRequirePositiveDayChange && coin.Change24hPercent <= 0)
                continue;

            if (StrategyRequireAboveSma50 && coin.CurrentPrice <= coin.Indicators.Sma50)
                continue;

            if (StrategyRequirePositiveMacd && coin.Indicators.MacdHistogram <= 0)
                continue;

            if (StrategyRequireMinScore40 && coin.CompositeScore < 40)
                continue;

            if (StrategyRequireAboveAverageVolume && !coin.Indicators.IsVolumeAboveAverage)
                continue;

            if (StrategySkipExistingPositions &&
                Portfolio.Positions.Any(p => p.Symbol == coin.DisplayName && p.Amount > 0))
                continue;

            buyOrders.Add(order);
        }

        if (StrategyPreferTopScoreBuys)
        {
            buyOrders = buyOrders
                .OrderByDescending(o => _allCoins.FirstOrDefault(c => c.DisplayName == o.Symbol)?.CompositeScore ?? int.MinValue)
                .Take(3)
                .ToList();
        }

        return sellOrders.Concat(buyOrders).ToList();
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
}
