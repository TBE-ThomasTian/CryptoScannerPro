using CommunityToolkit.Mvvm.ComponentModel;

namespace CryptoScanner.Models;

public partial class CryptoCoin : ObservableObject
{
    [ObservableProperty] private string _pairName = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _baseCurrency = string.Empty;
    [ObservableProperty] private string _quoteCurrency = string.Empty;
    [ObservableProperty] private string _currencySymbol = "$";
    [ObservableProperty] private decimal _currentPrice;
    [ObservableProperty] private decimal _change24hPercent;
    [ObservableProperty] private decimal _high24h;
    [ObservableProperty] private decimal _low24h;
    [ObservableProperty] private decimal _volume24h;
    [ObservableProperty] private int _compositeScore;
    [ObservableProperty] private SignalType _signal;
    [ObservableProperty] private TechnicalIndicators? _indicators;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // Raw OHLC data for the chart control
    private List<OhlcCandle>? _ohlcData;
    public List<OhlcCandle>? OhlcData
    {
        get => _ohlcData;
        set => SetProperty(ref _ohlcData, value);
    }

    // Order book data
    private OrderBookData? _orderBook;
    public OrderBookData? OrderBook
    {
        get => _orderBook;
        set => SetProperty(ref _orderBook, value);
    }

    public string SignalText => Signal switch
    {
        SignalType.StarkerKauf => "Starker Kauf",
        SignalType.Kauf => "Kauf",
        SignalType.Halten => "Halten",
        SignalType.Verkauf => "Verkauf",
        SignalType.StarkerVerkauf => "Starker Verkauf",
        _ => "N/A"
    };

    public string SignalColor => Signal switch
    {
        SignalType.StarkerKauf => "#00D4AA",
        SignalType.Kauf => "#4AEDC4",
        SignalType.Halten => "#F0B90B",
        SignalType.Verkauf => "#FF6B6B",
        SignalType.StarkerVerkauf => "#FF4757",
        _ => "#8899A6"
    };

    public string PriceFormatted
    {
        get
        {
            var num = CurrentPrice switch
            {
                >= 1000 => CurrentPrice.ToString("N2"),
                >= 1 => CurrentPrice.ToString("N4"),
                >= 0.01m => CurrentPrice.ToString("N6"),
                _ => CurrentPrice.ToString("N8")
            };
            return $"{CurrencySymbol}{num}";
        }
    }

    // Strategy recommendation for this coin
    private string _strategyAction = string.Empty;
    public string StrategyAction
    {
        get => _strategyAction;
        set => SetProperty(ref _strategyAction, value);
    }

    public string StrategyActionColor => StrategyAction switch
    {
        var s when s.StartsWith("Kaufen") => "#00D4AA",
        var s when s.StartsWith("Verkaufen") => "#FF4757",
        "Halten" => "#F0B90B",
        _ => "#8B949E"
    };

    public string Change24hFormatted => $"{(Change24hPercent >= 0 ? "+" : "")}{Change24hPercent:N2}%";

    public string Change24hColor => Change24hPercent >= 0 ? "#00D4AA" : "#FF4757";

    public string ScoreFormatted => $"{(CompositeScore >= 0 ? "+" : "")}{CompositeScore}";
}
