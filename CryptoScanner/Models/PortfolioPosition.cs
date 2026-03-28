using CommunityToolkit.Mvvm.ComponentModel;

namespace CryptoScanner.Models;

public partial class PortfolioPosition : ObservableObject
{
    [ObservableProperty] private string _symbol = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private decimal _averageBuyPrice;
    [ObservableProperty] private decimal _currentPrice;

    public decimal TotalValue => Amount * CurrentPrice;
    public decimal TotalCost => Amount * AverageBuyPrice;
    public decimal ProfitLoss => TotalValue - TotalCost;
    public decimal ProfitLossPercent => TotalCost > 0 ? (ProfitLoss / TotalCost) * 100 : 0;

    public string TotalValueFormatted => TotalValue.ToString("N2");
    public string ProfitLossFormatted => $"{(ProfitLoss >= 0 ? "+" : "")}{ProfitLoss:N2}";
    public string ProfitLossPercentFormatted => $"{(ProfitLossPercent >= 0 ? "+" : "")}{ProfitLossPercent:N2}%";
    public string ProfitLossColor => ProfitLoss >= 0 ? "#00D4AA" : "#FF4757";
    public string AmountFormatted => Amount.ToString("N6");
    public string AvgPriceFormatted => AverageBuyPrice.ToString("N4");
    public string CurrentPriceFormatted => CurrentPrice.ToString("N4");
}
