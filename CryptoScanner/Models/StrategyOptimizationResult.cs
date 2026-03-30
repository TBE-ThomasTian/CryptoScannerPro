namespace CryptoScanner.Models;

public class StrategyOptimizationResult
{
    public string StrategyName { get; set; } = string.Empty;
    public string FilterPreset { get; set; } = string.Empty;
    public decimal ReturnPercent { get; set; }
    public decimal FinalValue { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public int Trades { get; set; }
    public decimal WinRatePercent { get; set; }
    public string Summary => $"{StrategyName} + {FilterPreset}";
    public string ReturnFormatted => $"{(ReturnPercent >= 0 ? "+" : "")}{ReturnPercent:N2}%";
    public string FinalValueFormatted => $"{FinalValue:N2}";
    public string DrawdownFormatted => $"{MaxDrawdownPercent:N2}%";
    public string WinRateFormatted => $"{WinRatePercent:N0}%";
}
