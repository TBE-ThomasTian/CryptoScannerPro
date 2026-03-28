using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CryptoScanner.Models;

public partial class Portfolio : ObservableObject
{
    [ObservableProperty] private decimal _balance = 10000m;
    [ObservableProperty] private decimal _initialBalance = 10000m;
    [ObservableProperty] private string _currency = "USD";
    [ObservableProperty] private DateTime _createdAt = DateTime.Now;

    public ObservableCollection<PortfolioPosition> Positions { get; set; } = new();
    public ObservableCollection<Transaction> TransactionHistory { get; set; } = new();
    public List<PortfolioSnapshot> ValueHistory { get; set; } = new();

    public string CurrencySymbol => Currency == "EUR" ? "\u20AC" : "$";

    public decimal PositionsValue
    {
        get
        {
            decimal sum = 0;
            foreach (var p in Positions) sum += p.TotalValue;
            return sum;
        }
    }

    public decimal TotalValue => Balance + PositionsValue;
    public decimal TotalProfitLoss => TotalValue - InitialBalance;
    public decimal TotalProfitLossPercent => InitialBalance > 0 ? (TotalProfitLoss / InitialBalance) * 100 : 0;

    public string BalanceFormatted => $"{CurrencySymbol}{Balance:N2}";
    public string TotalValueFormatted => $"{CurrencySymbol}{TotalValue:N2}";
    public string InitialBalanceFormatted => $"{CurrencySymbol}{InitialBalance:N2}";
    public string ProfitLossFormatted => $"{(TotalProfitLoss >= 0 ? "+" : "")}{CurrencySymbol}{TotalProfitLoss:N2}";
    public string ProfitLossPercentFormatted => $"{(TotalProfitLossPercent >= 0 ? "+" : "")}{TotalProfitLossPercent:N2}%";
    public string ProfitLossColor => TotalProfitLoss >= 0 ? "#00D4AA" : "#FF4757";

    public void RefreshComputedProperties()
    {
        OnPropertyChanged(nameof(PositionsValue));
        OnPropertyChanged(nameof(TotalValue));
        OnPropertyChanged(nameof(TotalProfitLoss));
        OnPropertyChanged(nameof(TotalProfitLossPercent));
        OnPropertyChanged(nameof(BalanceFormatted));
        OnPropertyChanged(nameof(TotalValueFormatted));
        OnPropertyChanged(nameof(ProfitLossFormatted));
        OnPropertyChanged(nameof(ProfitLossPercentFormatted));
        OnPropertyChanged(nameof(ProfitLossColor));
    }
}
