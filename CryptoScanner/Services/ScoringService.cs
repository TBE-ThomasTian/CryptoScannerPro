using CryptoScanner.Models;

namespace CryptoScanner.Services;

public class ScoringService
{
    /// <summary>
    /// Calculates a composite score from -100 to +100 based on technical indicators.
    /// </summary>
    public (int Score, SignalType Signal) CalculateScore(TechnicalIndicators indicators, OrderBookData? orderBook = null)
    {
        var scores = new List<double>();

        // 1. RSI Score (weight: 18%)
        double rsiScore = CalculateRsiScore((double)indicators.Rsi14);
        scores.Add(rsiScore * 0.18);

        // 2. MACD Score (weight: 22%)
        double macdScore = CalculateMacdScore(indicators);
        scores.Add(macdScore * 0.22);

        // 3. Moving Average Score (weight: 18%)
        double maScore = CalculateMovingAverageScore(indicators);
        scores.Add(maScore * 0.18);

        // 4. Bollinger Bands Score (weight: 18%)
        double bbScore = CalculateBollingerScore(indicators);
        scores.Add(bbScore * 0.18);

        // 5. Volume Score (weight: 14%)
        double volScore = CalculateVolumeScore(indicators);
        scores.Add(volScore * 0.14);

        // 6. Order Book Pressure Score (weight: 10%)
        double obScore = CalculateOrderBookScore(orderBook);
        scores.Add(obScore * 0.10);

        var compositeScore = (int)Math.Round(scores.Sum());
        compositeScore = Math.Clamp(compositeScore, -100, 100);

        var signal = compositeScore switch
        {
            >= 50 => SignalType.StarkerKauf,
            >= 20 => SignalType.Kauf,
            >= -20 => SignalType.Halten,
            >= -50 => SignalType.Verkauf,
            _ => SignalType.StarkerVerkauf
        };

        return (compositeScore, signal);
    }

    private static double CalculateRsiScore(double rsi)
    {
        // RSI < 30 = oversold = buy signal (+100)
        // RSI > 70 = overbought = sell signal (-100)
        // RSI 50 = neutral (0)
        return rsi switch
        {
            <= 20 => 100,
            <= 30 => 80,
            <= 40 => 40,
            <= 60 => 0,
            <= 70 => -40,
            <= 80 => -80,
            _ => -100
        };
    }

    private static double CalculateMacdScore(TechnicalIndicators ind)
    {
        var histogram = (double)ind.MacdHistogram;
        var macdLine = (double)ind.MacdLine;

        double score = 0;

        // Histogram positive = bullish momentum
        if (histogram > 0)
            score += 50;
        else
            score -= 50;

        // MACD above signal = bullish
        if (macdLine > (double)ind.MacdSignal)
            score += 50;
        else
            score -= 50;

        return Math.Clamp(score, -100, 100);
    }

    private static double CalculateMovingAverageScore(TechnicalIndicators ind)
    {
        if (ind.Sma20 == 0 || ind.Sma50 == 0 || ind.CurrentPrice == 0)
            return 0;

        double score = 0;
        var price = (double)ind.CurrentPrice;

        // Price above SMA20 = bullish
        if (price > (double)ind.Sma20)
            score += 30;
        else
            score -= 30;

        // Price above SMA50 = bullish
        if (price > (double)ind.Sma50)
            score += 20;
        else
            score -= 20;

        // Golden Cross (SMA20 > SMA50) = strongly bullish
        if ((double)ind.Sma20 > (double)ind.Sma50)
            score += 50;
        else
            score -= 50;

        return Math.Clamp(score, -100, 100);
    }

    private static double CalculateBollingerScore(TechnicalIndicators ind)
    {
        if (ind.BollingerUpper == 0 || ind.BollingerLower == 0 || ind.CurrentPrice == 0)
            return 0;

        var range = (double)(ind.BollingerUpper - ind.BollingerLower);
        if (range <= 0) return 0;

        var position = ((double)ind.CurrentPrice - (double)ind.BollingerLower) / range;

        // Near lower band = oversold = buy; near upper band = overbought = sell
        return position switch
        {
            < 0.1 => 100,
            < 0.2 => 70,
            < 0.4 => 30,
            < 0.6 => 0,
            < 0.8 => -30,
            < 0.9 => -70,
            _ => -100
        };
    }

    private static double CalculateVolumeScore(TechnicalIndicators ind)
    {
        if (ind.AverageVolume == 0) return 0;

        var ratio = (double)(ind.CurrentVolume / ind.AverageVolume);

        return ratio switch
        {
            > 2.0 => 40,
            > 1.5 => 25,
            > 1.0 => 10,
            > 0.5 => -10,
            _ => -25
        };
    }

    private static double CalculateOrderBookScore(OrderBookData? ob)
    {
        if (ob == null || ob.TotalVolume == 0) return 0;

        var ratio = (double)ob.BidAskRatio;
        // >1 = more buyers (bullish), <1 = more sellers (bearish)
        return ratio switch
        {
            > 1.5 => 100,
            > 1.2 => 60,
            > 1.05 => 25,
            >= 0.95 => 0,
            >= 0.8 => -25,
            >= 0.65 => -60,
            _ => -100
        };
    }
}
