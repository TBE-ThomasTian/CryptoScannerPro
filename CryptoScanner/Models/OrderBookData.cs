using CommunityToolkit.Mvvm.ComponentModel;

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
        > 1.3m => "Starker Kaufdruck",
        > 1.05m => "Kaufdruck",
        >= 0.95m => "Neutral",
        >= 0.7m => "Verkaufsdruck",
        _ => "Starker Verkaufsdruck"
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
}
