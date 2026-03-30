using CommunityToolkit.Mvvm.ComponentModel;
using CryptoScanner.Services;

namespace CryptoScanner.Models;

public partial class OrderBookData : ObservableObject
{
    [ObservableProperty] private decimal _totalBidVolume;
    [ObservableProperty] private decimal _totalAskVolume;

    public decimal TotalVolume => TotalBidVolume + TotalAskVolume;

    public decimal BidAskRatio => TotalAskVolume > 0 ? TotalBidVolume / TotalAskVolume : 0;

    public decimal BidPercent => TotalVolume > 0 ? Math.Round(TotalBidVolume / TotalVolume * 100, 1) : 50;
    public decimal AskPercent => TotalVolume > 0 ? Math.Round(TotalAskVolume / TotalVolume * 100, 1) : 50;

    public string PressureLabel => BidAskRatio switch
    {
        > 1.3m => Loc.T("orderbook.strongbuy"),
        > 1.05m => Loc.T("orderbook.buy"),
        >= 0.95m => Loc.T("orderbook.neutral"),
        >= 0.7m => Loc.T("orderbook.sell"),
        _ => Loc.T("orderbook.strongsell")
    };

    public string PressureColor => BidAskRatio switch
    {
        > 1.3m => "#00D4AA",
        > 1.05m => "#4AEDC4",
        >= 0.95m => "#F0B90B",
        >= 0.7m => "#FF6B6B",
        _ => "#FF4757"
    };

    public string BidPercentFormatted => $"{BidPercent:N1}%";
    public string AskPercentFormatted => $"{AskPercent:N1}%";
    public string RatioFormatted => $"{BidAskRatio:N2}";

    public void RefreshLocalization() => OnPropertyChanged(nameof(PressureLabel));
}
