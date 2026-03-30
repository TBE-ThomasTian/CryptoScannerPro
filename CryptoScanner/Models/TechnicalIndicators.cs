using CryptoScanner.Services;

namespace CryptoScanner.Models;

public class TechnicalIndicators
{
    // RSI
    public decimal Rsi14 { get; set; }

    // MACD
    public decimal MacdLine { get; set; }
    public decimal MacdSignal { get; set; }
    public decimal MacdHistogram { get; set; }

    // Moving Averages
    public decimal Sma20 { get; set; }
    public decimal Sma50 { get; set; }
    public decimal Ema12 { get; set; }
    public decimal Ema26 { get; set; }

    // Bollinger Bands
    public decimal BollingerUpper { get; set; }
    public decimal BollingerMiddle { get; set; }
    public decimal BollingerLower { get; set; }

    // Volume
    public decimal CurrentVolume { get; set; }
    public decimal AverageVolume { get; set; }
    public bool IsVolumeAboveAverage => CurrentVolume > AverageVolume;

    // OHLC period range (full data span based on selected timeframe)
    public decimal PeriodHigh { get; set; }
    public decimal PeriodLow { get; set; }
    public string PeriodLabel { get; set; } = "7-Tage";

    // Current price for reference
    public decimal CurrentPrice { get; set; }

    /// <summary>Position of current price within the period range, 0-100%.</summary>
    public decimal PeriodRangePercent
    {
        get
        {
            if (PeriodHigh <= PeriodLow || PeriodHigh == 0) return 0;
            return Math.Round((CurrentPrice - PeriodLow) / (PeriodHigh - PeriodLow) * 100, 1);
        }
    }

    public string PeriodRangePercentFormatted => $"{PeriodRangePercent:N1}%";

    public string PeriodHighFormatted => FormatPrice(PeriodHigh);
    public string PeriodLowFormatted => FormatPrice(PeriodLow);

    private static string FormatPrice(decimal price) => price switch
    {
        >= 1000 => price.ToString("N2"),
        >= 1 => price.ToString("N4"),
        >= 0.01m => price.ToString("N6"),
        _ => price.ToString("N8")
    };

    public string RsiSignal => Rsi14 switch
    {
        < 30 => Loc.T("ti.rsi.oversold"),
        < 40 => Loc.T("ti.rsi.low"),
        < 60 => Loc.T("ti.rsi.neutral"),
        < 70 => Loc.T("ti.rsi.high"),
        _ => Loc.T("ti.rsi.overbought")
    };

    public string MacdSignalText => MacdHistogram switch
    {
        > 0 when MacdLine > MacdSignal => Loc.T("ti.macd.bullish"),
        > 0 => Loc.T("ti.macd.softbullish"),
        < 0 when MacdLine < MacdSignal => Loc.T("ti.macd.bearish"),
        < 0 => Loc.T("ti.macd.softbearish"),
        _ => Loc.T("ti.rsi.neutral")
    };

    public string BollingerSignal
    {
        get
        {
            var range = BollingerUpper - BollingerLower;
            if (CurrentPrice <= 0 || BollingerUpper <= 0 || range <= 0) return "N/A";
            var position = (CurrentPrice - BollingerLower) / range;
            return position switch
            {
                < 0.1m => Loc.T("ti.bb.strongoversold"),
                < 0.3m => Loc.T("ti.bb.oversold"),
                < 0.7m => Loc.T("ti.rsi.neutral"),
                < 0.9m => Loc.T("ti.bb.overbought"),
                _ => Loc.T("ti.bb.strongoverbought")
            };
        }
    }

    public string SmaSignal
    {
        get
        {
            if (Sma20 <= 0 || Sma50 <= 0) return "N/A";
            return Sma20 > Sma50 ? Loc.T("ti.sma.golden") : Loc.T("ti.sma.death");
        }
    }

    public string VolumeSignal => IsVolumeAboveAverage ? Loc.T("ti.volume.above") : Loc.T("ti.volume.below");
}
