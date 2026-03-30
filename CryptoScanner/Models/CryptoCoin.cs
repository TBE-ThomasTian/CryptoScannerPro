using CommunityToolkit.Mvvm.ComponentModel;
using CryptoScanner.Services;

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
        SignalType.StarkerKauf => Loc.T("signal.strongbuy"),
        SignalType.Kauf => Loc.T("signal.buy"),
        SignalType.Halten => Loc.T("signal.hold"),
        SignalType.Verkauf => Loc.T("signal.sell"),
        SignalType.StarkerVerkauf => Loc.T("signal.strongsell"),
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
            return FormatPrice(CurrentPrice);
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
        var s when s.StartsWith("Kauf", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Buy", StringComparison.OrdinalIgnoreCase) => "#00D4AA",
        var s when s.StartsWith("Verkauf", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Sell", StringComparison.OrdinalIgnoreCase) => "#FF4757",
        var s when string.Equals(s, "Halten", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "Hold", StringComparison.OrdinalIgnoreCase) => "#F0B90B",
        _ => "#8B949E"
    };

    public string Change24hFormatted => $"{(Change24hPercent >= 0 ? "+" : "")}{Change24hPercent:N2}%";

    public string Change24hColor => Change24hPercent >= 0 ? "#00D4AA" : "#FF4757";

    public string ScoreFormatted => $"{(CompositeScore >= 0 ? "+" : "")}{CompositeScore}";

    public string BuyZoneText
    {
        get
        {
            if (CurrentPrice <= 0)
                return "-";

            if (Signal == SignalType.Verkauf || Signal == SignalType.StarkerVerkauf)
                return Loc.T("buyzone.nobuy");

            var supports = new List<decimal> { CurrentPrice * 0.98m };
            if (Indicators?.Sma20 > 0) supports.Add(Indicators.Sma20);
            if (Indicators?.BollingerLower > 0) supports.Add(Indicators.BollingerLower);

            var valid = supports.Where(v => v > 0).ToList();
            if (valid.Count == 0)
                return FormatPrice(CurrentPrice);

            var zoneLow = valid.Min();
            var zoneHigh = Math.Min(CurrentPrice, valid.Max());

            if (Signal == SignalType.StarkerKauf && zoneHigh >= CurrentPrice * 0.995m)
                return string.Format(Loc.T("buyzone.now"), FormatPrice(CurrentPrice));

            if (Signal == SignalType.Kauf || Signal == SignalType.StarkerKauf)
                return $"{FormatPrice(zoneLow)} - {FormatPrice(zoneHigh)}";

            return string.Format(Loc.T("buyzone.below"), FormatPrice(zoneHigh));
        }
    }

    public string BuyZoneColor => Signal switch
    {
        SignalType.StarkerKauf => "#00D4AA",
        SignalType.Kauf => "#4AEDC4",
        SignalType.Halten => "#F0B90B",
        _ => "#8B949E"
    };

    partial void OnCurrentPriceChanged(decimal value) => NotifyDerivedProperties();
    partial void OnCurrencySymbolChanged(string value) => NotifyDerivedProperties();
    partial void OnIndicatorsChanged(TechnicalIndicators? value) => NotifyDerivedProperties();
    partial void OnSignalChanged(SignalType value) => NotifyDerivedProperties();

    private void NotifyDerivedProperties()
    {
        OnPropertyChanged(nameof(SignalText));
        OnPropertyChanged(nameof(PriceFormatted));
        OnPropertyChanged(nameof(BuyZoneText));
        OnPropertyChanged(nameof(BuyZoneColor));
        OnPropertyChanged(nameof(StrategyActionColor));
    }

    public void RefreshLocalization() => NotifyDerivedProperties();

    private string FormatPrice(decimal price)
    {
        var num = price switch
        {
            >= 1000 => price.ToString("N2"),
            >= 1 => price.ToString("N4"),
            >= 0.01m => price.ToString("N6"),
            _ => price.ToString("N8")
        };
        return $"{CurrencySymbol}{num}";
    }
}
