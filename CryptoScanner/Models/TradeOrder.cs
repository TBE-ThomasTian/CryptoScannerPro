using CryptoScanner.Services;

namespace CryptoScanner.Models;

public class TradeOrder
{
    public string Symbol { get; set; } = string.Empty;
    public TradeAction Action { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public decimal CurrentPrice { get; set; }

    public string ActionText => Action switch
    {
        TradeAction.Buy => Loc.T("strategy.action.buy"),
        TradeAction.Sell => Loc.T("strategy.action.sell"),
        _ => Loc.T("strategy.action.hold")
    };

    public string ActionColor => Action switch
    {
        TradeAction.Buy => "#00D4AA",
        TradeAction.Sell => "#FF4757",
        _ => "#F0B90B"
    };

    public string ConfidenceFormatted => $"{Confidence * 100:N0}%";
}
