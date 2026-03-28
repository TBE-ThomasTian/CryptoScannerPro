using CryptoScanner.Models;

namespace CryptoScanner.Services;

public class TechnicalAnalysisService
{
    public TechnicalIndicators Calculate(List<OhlcCandle> candles, decimal currentPrice)
    {
        var indicators = new TechnicalIndicators
        {
            CurrentPrice = currentPrice
        };

        if (candles.Count < 2)
            return indicators;

        var closes = candles.Select(c => c.Close).ToList();
        var volumes = candles.Select(c => c.Volume).ToList();

        // Period high/low from full OHLC data
        indicators.PeriodHigh = candles.Max(c => c.High);
        indicators.PeriodLow = candles.Min(c => c.Low);

        // RSI (14-period)
        indicators.Rsi14 = CalculateRsi(closes, 14);

        // Moving Averages
        indicators.Sma20 = CalculateSma(closes, 20);
        indicators.Sma50 = CalculateSma(closes, 50);
        indicators.Ema12 = CalculateEma(closes, 12);
        indicators.Ema26 = CalculateEma(closes, 26);

        // MACD (12, 26, 9)
        var (macdLine, signalLine, histogram) = CalculateMacd(closes, 12, 26, 9);
        indicators.MacdLine = macdLine;
        indicators.MacdSignal = signalLine;
        indicators.MacdHistogram = histogram;

        // Bollinger Bands (20-period, 2 std dev)
        var (upper, middle, lower) = CalculateBollingerBands(closes, 20, 2m);
        indicators.BollingerUpper = upper;
        indicators.BollingerMiddle = middle;
        indicators.BollingerLower = lower;

        // Volume analysis
        indicators.CurrentVolume = volumes.Count > 0 ? volumes[^1] : 0;
        indicators.AverageVolume = volumes.Count >= 20
            ? volumes.TakeLast(20).Average()
            : volumes.Average();

        return indicators;
    }

    private static decimal CalculateRsi(List<decimal> prices, int period)
    {
        if (prices.Count < period + 1)
            return 50m; // neutral default

        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < prices.Count; i++)
        {
            var change = prices[i] - prices[i - 1];
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? Math.Abs(change) : 0);
        }

        // Initial average
        var avgGain = gains.Take(period).Average();
        var avgLoss = losses.Take(period).Average();

        // Smoothed averages (Wilder's smoothing)
        for (int i = period; i < gains.Count; i++)
        {
            avgGain = (avgGain * (period - 1) + gains[i]) / period;
            avgLoss = (avgLoss * (period - 1) + losses[i]) / period;
        }

        if (avgLoss == 0)
            return 100m;

        var rs = avgGain / avgLoss;
        return 100m - (100m / (1m + rs));
    }

    private static decimal CalculateSma(List<decimal> prices, int period)
    {
        if (prices.Count < period)
            return prices.Count > 0 ? prices.Average() : 0;

        return prices.TakeLast(period).Average();
    }

    private static decimal CalculateEma(List<decimal> prices, int period)
    {
        if (prices.Count < period)
            return prices.Count > 0 ? prices.Average() : 0;

        var multiplier = 2m / (period + 1);
        var ema = prices.Take(period).Average(); // seed with SMA

        for (int i = period; i < prices.Count; i++)
        {
            ema = (prices[i] - ema) * multiplier + ema;
        }

        return ema;
    }

    private static (decimal MacdLine, decimal SignalLine, decimal Histogram) CalculateMacd(
        List<decimal> prices, int fastPeriod, int slowPeriod, int signalPeriod)
    {
        if (prices.Count < slowPeriod + signalPeriod)
            return (0, 0, 0);

        // Calculate MACD line series for the signal EMA
        var macdSeries = new List<decimal>();
        for (int i = slowPeriod; i <= prices.Count; i++)
        {
            var subset = prices.Take(i).ToList();
            var fast = CalculateEma(subset, fastPeriod);
            var slow = CalculateEma(subset, slowPeriod);
            macdSeries.Add(fast - slow);
        }

        var macdLine = macdSeries.Count > 0 ? macdSeries[^1] : 0;

        // Signal line = EMA of MACD series
        decimal signalLine = 0;
        if (macdSeries.Count >= signalPeriod)
        {
            signalLine = CalculateEma(macdSeries, signalPeriod);
        }

        return (macdLine, signalLine, macdLine - signalLine);
    }

    private static (decimal Upper, decimal Middle, decimal Lower) CalculateBollingerBands(
        List<decimal> prices, int period, decimal numStdDev)
    {
        if (prices.Count < period)
            return (0, 0, 0);

        var recentPrices = prices.TakeLast(period).ToList();
        var middle = recentPrices.Average();

        var variance = recentPrices.Select(p => (double)(p - middle) * (double)(p - middle)).Average();
        var stdDev = (decimal)Math.Sqrt(variance);

        return (middle + numStdDev * stdDev, middle, middle - numStdDev * stdDev);
    }
}
