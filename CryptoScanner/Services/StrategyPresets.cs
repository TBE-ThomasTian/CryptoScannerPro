using CryptoScanner.Models;

namespace CryptoScanner.Services;

public static class StrategyPresets
{
    // Helper to build blocks + connections quickly
    static StrategyBlock S(double x, double y) => new() { Type = BlockType.Start, X = x, Y = y };
    static StrategyBlock C(string cat, string op, double val, double x, double y) =>
        new() { Type = BlockType.Condition, Category = cat, Operator = op, Value = val, X = x, Y = y };
    static StrategyBlock CP(string cat, string preset, double x, double y) =>
        new() { Type = BlockType.Condition, Category = cat, ConditionPreset = preset, X = x, Y = y };
    static StrategyBlock Buy(string amt, double x, double y) =>
        new() { Type = BlockType.ActionBuy, ActionAmount = amt, X = x, Y = y };
    static StrategyBlock Sell(string amt, double x, double y) =>
        new() { Type = BlockType.ActionSell, ActionAmount = amt, X = x, Y = y };
    static StrategyBlock Hold(double x, double y) =>
        new() { Type = BlockType.ActionHold, X = x, Y = y };
    static StrategyConnection Conn(StrategyBlock from, StrategyBlock to, string port) =>
        new() { FromBlockId = from.Id, ToBlockId = to.Id, OutputPort = port };

    // ═══════════════════════════════════════════════════════════
    // 1. RSI Momentum
    // ═══════════════════════════════════════════════════════════
    public static TradingStrategy RsiMomentum()
    {
        var start = S(50, 200);
        var rsiLow = C("RSI", "<", 30, 280, 120);
        var volHigh = CP("Volumen", "Ueber Durchschnitt", 530, 40);
        var buy = Buy("10%", 780, 10);
        var hold1 = Hold(780, 120);
        var rsiHigh = C("RSI", ">", 70, 530, 250);
        var sell = Sell("50%", 780, 220);
        var hold2 = Hold(780, 330);

        var s = new TradingStrategy { Name = "RSI Momentum (Vorlage)" };
        s.Blocks.AddRange(new[] { start, rsiLow, volHigh, buy, hold1, rsiHigh, sell, hold2 });
        s.Connections.AddRange(new[]
        {
            Conn(start, rsiLow, "Out"), Conn(rsiLow, volHigh, "Ja"), Conn(rsiLow, rsiHigh, "Nein"),
            Conn(volHigh, buy, "Ja"), Conn(volHigh, hold1, "Nein"),
            Conn(rsiHigh, sell, "Ja"), Conn(rsiHigh, hold2, "Nein"),
        });
        return s;
    }

    // ═══════════════════════════════════════════════════════════
    // 2. MACD Crossover
    // ═══════════════════════════════════════════════════════════
    public static TradingStrategy MacdCrossover()
    {
        var start = S(50, 180);
        var macdUp = CP("MACD", "Kreuzung aufwaerts", 280, 100);
        var scoreUp = C("Score", ">", 20, 540, 30);
        var buy = Buy("5%", 790, 10);
        var hold1 = Hold(790, 120);
        var macdDn = CP("MACD", "Kreuzung abwaerts", 540, 240);
        var sell = Sell("30%", 790, 220);
        var hold2 = Hold(790, 330);

        var s = new TradingStrategy { Name = "MACD Crossover (Vorlage)" };
        s.Blocks.AddRange(new[] { start, macdUp, scoreUp, buy, hold1, macdDn, sell, hold2 });
        s.Connections.AddRange(new[]
        {
            Conn(start, macdUp, "Out"), Conn(macdUp, scoreUp, "Ja"), Conn(macdUp, macdDn, "Nein"),
            Conn(scoreUp, buy, "Ja"), Conn(scoreUp, hold1, "Nein"),
            Conn(macdDn, sell, "Ja"), Conn(macdDn, hold2, "Nein"),
        });
        return s;
    }

    // ═══════════════════════════════════════════════════════════
    // 3. Bollinger Bounce
    // ═══════════════════════════════════════════════════════════
    public static TradingStrategy BollingerBounce()
    {
        var start = S(50, 180);
        var bbLow = CP("Bollinger", "Preis unter unteres Band", 280, 100);
        var rsiChk = C("RSI", "<", 40, 540, 30);
        var buy = Buy("10%", 790, 10);
        var hold1 = Hold(790, 120);
        var bbHigh = CP("Bollinger", "Preis ueber oberes Band", 540, 240);
        var sell = Sell("50%", 790, 220);
        var hold2 = Hold(790, 330);

        var s = new TradingStrategy { Name = "Bollinger Bounce (Vorlage)" };
        s.Blocks.AddRange(new[] { start, bbLow, rsiChk, buy, hold1, bbHigh, sell, hold2 });
        s.Connections.AddRange(new[]
        {
            Conn(start, bbLow, "Out"), Conn(bbLow, rsiChk, "Ja"), Conn(bbLow, bbHigh, "Nein"),
            Conn(rsiChk, buy, "Ja"), Conn(rsiChk, hold1, "Nein"),
            Conn(bbHigh, sell, "Ja"), Conn(bbHigh, hold2, "Nein"),
        });
        return s;
    }

    // ═══════════════════════════════════════════════════════════
    // 4. Golden Cross / Death Cross
    // ═══════════════════════════════════════════════════════════
    public static TradingStrategy GoldenCross()
    {
        var start = S(50, 180);
        var sma20 = CP("SMA", "Preis ueber SMA20", 280, 100);
        var sma50 = CP("SMA", "Preis ueber SMA50", 540, 30);
        var buy = Buy("8%", 790, 10);
        var hold1 = Hold(790, 120);
        var below50 = CP("SMA", "Preis unter SMA50", 540, 240);
        var sell = Sell("40%", 790, 220);
        var hold2 = Hold(790, 330);

        var s = new TradingStrategy { Name = "Golden Cross (Vorlage)" };
        s.Blocks.AddRange(new[] { start, sma20, sma50, buy, hold1, below50, sell, hold2 });
        s.Connections.AddRange(new[]
        {
            Conn(start, sma20, "Out"), Conn(sma20, sma50, "Ja"), Conn(sma20, below50, "Nein"),
            Conn(sma50, buy, "Ja"), Conn(sma50, hold1, "Nein"),
            Conn(below50, sell, "Ja"), Conn(below50, hold2, "Nein"),
        });
        return s;
    }

    // ═══════════════════════════════════════════════════════════
    // 5. Volumen-Breakout
    // ═══════════════════════════════════════════════════════════
    public static TradingStrategy VolumenBreakout()
    {
        var start = S(50, 160);
        var vol = CP("Volumen", "Ueber Durchschnitt", 280, 100);
        var sma = CP("SMA", "Preis ueber SMA20", 530, 30);
        var score = C("Score", ">", 30, 770, 10);
        var buy = Buy("10%", 1010, 10);
        var hold1 = Hold(1010, 120);
        var hold2 = Hold(530, 240);

        var s = new TradingStrategy { Name = "Volumen-Breakout (Vorlage)" };
        s.Blocks.AddRange(new[] { start, vol, sma, score, buy, hold1, hold2 });
        s.Connections.AddRange(new[]
        {
            Conn(start, vol, "Out"), Conn(vol, sma, "Ja"), Conn(vol, hold2, "Nein"),
            Conn(sma, score, "Ja"), Conn(sma, hold1, "Nein"),
            Conn(score, buy, "Ja"), Conn(score, hold1, "Nein"),
        });
        return s;
    }

    // ═══════════════════════════════════════════════════════════
    // 6. Mean Reversion
    // ═══════════════════════════════════════════════════════════
    public static TradingStrategy MeanReversion()
    {
        var start = S(50, 200);
        var rsiVLow = C("RSI", "<", 25, 280, 100);
        var bbLow = CP("Bollinger", "Preis unter unteres Band", 530, 30);
        var buyBig = Buy("15%", 780, 10);
        var buySmall = Buy("5%", 780, 120);
        var rsiVHigh = C("RSI", ">", 75, 530, 260);
        var bbHigh = CP("Bollinger", "Preis ueber oberes Band", 780, 220);
        var sellBig = Sell("60%", 1020, 200);
        var sellSmall = Sell("30%", 1020, 300);
        var hold = Hold(780, 380);

        var s = new TradingStrategy { Name = "Mean Reversion (Vorlage)" };
        s.Blocks.AddRange(new[] { start, rsiVLow, bbLow, buyBig, buySmall, rsiVHigh, bbHigh, sellBig, sellSmall, hold });
        s.Connections.AddRange(new[]
        {
            Conn(start, rsiVLow, "Out"),
            Conn(rsiVLow, bbLow, "Ja"), Conn(rsiVLow, rsiVHigh, "Nein"),
            Conn(bbLow, buyBig, "Ja"), Conn(bbLow, buySmall, "Nein"),
            Conn(rsiVHigh, bbHigh, "Ja"), Conn(rsiVHigh, hold, "Nein"),
            Conn(bbHigh, sellBig, "Ja"), Conn(bbHigh, sellSmall, "Nein"),
        });
        return s;
    }

    // ═══════════════════════════════════════════════════════════
    // 7. Trend Following
    // ═══════════════════════════════════════════════════════════
    public static TradingStrategy TrendFollowing()
    {
        var start = S(50, 200);
        var macdPos = CP("MACD", "Signal positiv", 280, 100);
        var scoreUp = C("Score", ">", 20, 530, 30);
        var volUp = CP("Volumen", "Ueber Durchschnitt", 780, 10);
        var buyBig = Buy("10%", 1020, 10);
        var buySmall = Buy("5%", 1020, 100);
        var macdNeg = CP("MACD", "Signal negativ", 530, 260);
        var scoreDn = C("Score", "<", -20, 780, 240);
        var sell = Sell("50%", 1020, 220);
        var hold1 = Hold(1020, 320);
        var hold2 = Hold(780, 380);

        var s = new TradingStrategy { Name = "Trend Following (Vorlage)" };
        s.Blocks.AddRange(new[] { start, macdPos, scoreUp, volUp, buyBig, buySmall, macdNeg, scoreDn, sell, hold1, hold2 });
        s.Connections.AddRange(new[]
        {
            Conn(start, macdPos, "Out"),
            Conn(macdPos, scoreUp, "Ja"), Conn(macdPos, macdNeg, "Nein"),
            Conn(scoreUp, volUp, "Ja"), Conn(scoreUp, hold1, "Nein"),
            Conn(volUp, buyBig, "Ja"), Conn(volUp, buySmall, "Nein"),
            Conn(macdNeg, scoreDn, "Ja"), Conn(macdNeg, hold2, "Nein"),
            Conn(scoreDn, sell, "Ja"), Conn(scoreDn, hold1, "Nein"),
        });
        return s;
    }

    // ═══════════════════════════════════════════════════════════
    // 8. Konservativ
    // ═══════════════════════════════════════════════════════════
    public static TradingStrategy Konservativ()
    {
        var start = S(50, 200);
        var rsiMid = C("RSI", "<", 45, 280, 100);
        var macd = CP("MACD", "Signal positiv", 530, 30);
        var vol = CP("Volumen", "Ueber Durchschnitt", 780, 10);
        var score = C("Score", ">", 40, 1020, 10);
        var buy = Buy("5%", 1260, 10);
        var hold1 = Hold(1260, 100);
        var rsiHigh = C("RSI", ">", 65, 530, 260);
        var scoreDn = C("Score", "<", -30, 780, 240);
        var sell = Sell("30%", 1020, 220);
        var hold2 = Hold(1020, 330);
        var hold3 = Hold(780, 380);

        var s = new TradingStrategy { Name = "Konservativ (Vorlage)" };
        s.Blocks.AddRange(new[] { start, rsiMid, macd, vol, score, buy, hold1, rsiHigh, scoreDn, sell, hold2, hold3 });
        s.Connections.AddRange(new[]
        {
            Conn(start, rsiMid, "Out"),
            Conn(rsiMid, macd, "Ja"), Conn(rsiMid, rsiHigh, "Nein"),
            Conn(macd, vol, "Ja"), Conn(macd, hold1, "Nein"),
            Conn(vol, score, "Ja"), Conn(vol, hold1, "Nein"),
            Conn(score, buy, "Ja"), Conn(score, hold1, "Nein"),
            Conn(rsiHigh, scoreDn, "Ja"), Conn(rsiHigh, hold3, "Nein"),
            Conn(scoreDn, sell, "Ja"), Conn(scoreDn, hold2, "Nein"),
        });
        return s;
    }

    // ═══════════════════════════════════════════════════════════
    // 9. Aggressiv
    // ═══════════════════════════════════════════════════════════
    public static TradingStrategy Aggressiv()
    {
        var start = S(50, 160);
        var scoreUp = C("Score", ">", 15, 280, 100);
        var buy = Buy("3%", 530, 60);
        var scoreDn = C("Score", "<", -15, 530, 220);
        var sell = Sell("25%", 780, 200);
        var hold = Hold(780, 300);

        var s = new TradingStrategy { Name = "Aggressiv (Vorlage)" };
        s.Blocks.AddRange(new[] { start, scoreUp, buy, scoreDn, sell, hold });
        s.Connections.AddRange(new[]
        {
            Conn(start, scoreUp, "Out"),
            Conn(scoreUp, buy, "Ja"), Conn(scoreUp, scoreDn, "Nein"),
            Conn(scoreDn, sell, "Ja"), Conn(scoreDn, hold, "Nein"),
        });
        return s;
    }

    // ═══════════════════════════════════════════════════════════
    // 10. DCA (Dollar Cost Averaging)
    // ═══════════════════════════════════════════════════════════
    public static TradingStrategy DCA()
    {
        var start = S(50, 160);
        var rsiMid = C("RSI", "<", 50, 280, 100);
        var buy = Buy("2%", 530, 60);
        var rsiHigh = C("RSI", ">", 80, 530, 220);
        var sell = Sell("20%", 780, 200);
        var hold = Hold(780, 300);

        var s = new TradingStrategy { Name = "DCA (Vorlage)" };
        s.Blocks.AddRange(new[] { start, rsiMid, buy, rsiHigh, sell, hold });
        s.Connections.AddRange(new[]
        {
            Conn(start, rsiMid, "Out"),
            Conn(rsiMid, buy, "Ja"), Conn(rsiMid, rsiHigh, "Nein"),
            Conn(rsiHigh, sell, "Ja"), Conn(rsiHigh, hold, "Nein"),
        });
        return s;
    }

    // ═══════════════════════════════════════════════════════════
    // Registry of all presets
    // ═══════════════════════════════════════════════════════════
    public static List<(string Name, Func<TradingStrategy> Factory)> All => new()
    {
        ("RSI Momentum (Vorlage)", RsiMomentum),
        ("MACD Crossover (Vorlage)", MacdCrossover),
        ("Bollinger Bounce (Vorlage)", BollingerBounce),
        ("Golden Cross (Vorlage)", GoldenCross),
        ("Volumen-Breakout (Vorlage)", VolumenBreakout),
        ("Mean Reversion (Vorlage)", MeanReversion),
        ("Trend Following (Vorlage)", TrendFollowing),
        ("Konservativ (Vorlage)", Konservativ),
        ("Aggressiv (Vorlage)", Aggressiv),
        ("DCA (Vorlage)", DCA),
    };
}
