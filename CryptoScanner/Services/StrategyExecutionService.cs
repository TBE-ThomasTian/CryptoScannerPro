using System.Text.Json;
using CryptoScanner.Models;

namespace CryptoScanner.Services;

public class StrategyExecutionService
{
    private readonly string _saveDir;
    private sealed class StrategyIntent
    {
        public required CryptoCoin Coin { get; init; }
        public required TradeAction Action { get; init; }
        public required string ActionAmount { get; init; }
        public required string Reason { get; init; }
    }

    private sealed class SimulatedPortfolioState
    {
        public decimal Balance { get; set; }
        public Dictionary<string, decimal> Positions { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public StrategyExecutionService()
    {
        _saveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CryptoScanner", "strategies");
        if (!Directory.Exists(_saveDir)) Directory.CreateDirectory(_saveDir);
    }

    // ── Execution ───────────────────────────────────────────────

    public List<TradeOrder> Execute(TradingStrategy strategy, List<CryptoCoin> coins, Portfolio portfolio)
    {
        var startBlock = strategy.Blocks.FirstOrDefault(b => b.Type == BlockType.Start);
        if (startBlock == null) return new();

        var intents = new List<StrategyIntent>();

        foreach (var coin in coins.Where(c => c.Indicators != null))
        {
            var result = TraverseTree(strategy, startBlock, coin);
            if (result != null)
                intents.Add(result);
        }

        if (intents.Count == 0) return new();

        var state = CreateState(portfolio);
        var orders = new List<TradeOrder>();

        foreach (var intent in intents.Where(i => i.Action == TradeAction.Sell))
        {
            var order = BuildSellOrder(intent, state);
            if (order != null)
                orders.Add(order);
        }

        foreach (var intent in intents.Where(i => i.Action == TradeAction.Buy))
        {
            var order = BuildBuyOrder(intent, state);
            if (order != null)
                orders.Add(order);
        }

        return orders;
    }

    private StrategyIntent? TraverseTree(TradingStrategy strat, StrategyBlock current, CryptoCoin coin)
    {
        // Find connections from this block
        if (current.Type == BlockType.Start)
        {
            var conn = strat.Connections.FirstOrDefault(c => c.FromBlockId == current.Id);
            if (conn == null) return null;
            var next = strat.Blocks.FirstOrDefault(b => b.Id == conn.ToBlockId);
            return next == null ? null : TraverseTree(strat, next, coin);
        }

        if (current.Type == BlockType.Condition)
        {
            bool result = EvaluateCondition(current, coin);
            string port = result ? "Ja" : "Nein";
            var conn = strat.Connections.FirstOrDefault(c => c.FromBlockId == current.Id && c.OutputPort == port);
            if (conn == null) return null;
            var next = strat.Blocks.FirstOrDefault(b => b.Id == conn.ToBlockId);
            return next == null ? null : TraverseTree(strat, next, coin);
        }

        if (current.Type == BlockType.ActionBuy)
        {
            return new StrategyIntent
            {
                Action = TradeAction.Buy,
                Coin = coin,
                ActionAmount = current.ActionAmount,
                Reason = $"Strategie: {current.Description}"
            };
        }

        if (current.Type == BlockType.ActionSell)
        {
            return new StrategyIntent
            {
                Action = TradeAction.Sell,
                Coin = coin,
                ActionAmount = current.ActionAmount,
                Reason = $"Strategie: {current.Description}"
            };
        }

        return null; // Hold/Alarm produce no trade
    }

    private static bool EvaluateCondition(StrategyBlock block, CryptoCoin coin)
    {
        var ind = coin.Indicators;
        if (ind == null) return false;

        // Preset-based conditions
        if (!string.IsNullOrEmpty(block.ConditionPreset))
        {
            return block.ConditionPreset switch
            {
                "Signal positiv" => ind.MacdHistogram > 0,
                "Signal negativ" => ind.MacdHistogram < 0,
                "Kreuzung aufwaerts" => ind.MacdLine > ind.MacdSignal && ind.MacdHistogram > 0,
                "Kreuzung abwaerts" => ind.MacdLine < ind.MacdSignal && ind.MacdHistogram < 0,
                "Preis ueber SMA20" => ind.CurrentPrice > ind.Sma20,
                "Preis unter SMA20" => ind.CurrentPrice < ind.Sma20,
                "Preis ueber SMA50" => ind.CurrentPrice > ind.Sma50,
                "Preis unter SMA50" => ind.CurrentPrice < ind.Sma50,
                "Preis ueber EMA12" => ind.CurrentPrice > ind.Ema12,
                "Preis unter EMA12" => ind.CurrentPrice < ind.Ema12,
                "Preis ueber EMA26" => ind.CurrentPrice > ind.Ema26,
                "Preis unter EMA26" => ind.CurrentPrice < ind.Ema26,
                "Preis ueber oberes Band" => ind.CurrentPrice > ind.BollingerUpper,
                "Preis unter unteres Band" => ind.CurrentPrice < ind.BollingerLower,
                "Preis im Band" => ind.CurrentPrice >= ind.BollingerLower && ind.CurrentPrice <= ind.BollingerUpper,
                "Ueber Durchschnitt" => ind.IsVolumeAboveAverage,
                "Unter Durchschnitt" => !ind.IsVolumeAboveAverage,
                _ => false
            };
        }

        // Operator-based conditions
        double actual = block.Category switch
        {
            "RSI" => (double)ind.Rsi14,
            "Preis-Aenderung" => (double)coin.Change24hPercent,
            "Orderbuch" => coin.OrderBook != null ? (double)coin.OrderBook.BidPercent : double.NaN,
            "Preisspanne" => (double)ind.PeriodRangePercent,
            "Score" => coin.CompositeScore,
            _ => 0
        };

        if (double.IsNaN(actual))
            return false;

        return block.Operator switch
        {
            ">" => actual > block.Value,
            "<" => actual < block.Value,
            ">=" => actual >= block.Value,
            "<=" => actual <= block.Value,
            "==" => Math.Abs(actual - block.Value) < 0.01,
            _ => false
        };
    }

    private static decimal ParsePercent(string s)
    {
        var clean = s.Replace("%", "").Replace(",", ".").Trim();
        if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var val))
            return val / 100m;
        return 0.1m;
    }

    private static SimulatedPortfolioState CreateState(Portfolio portfolio)
    {
        var state = new SimulatedPortfolioState
        {
            Balance = portfolio.Balance
        };

        foreach (var position in portfolio.Positions)
        {
            if (position.Amount > 0)
                state.Positions[position.Symbol] = position.Amount;
        }

        return state;
    }

    private static TradeOrder? BuildBuyOrder(StrategyIntent intent, SimulatedPortfolioState state)
    {
        if (intent.Coin.CurrentPrice <= 0)
            return null;

        var pct = ParsePercent(intent.ActionAmount);
        var investAmount = state.Balance * pct;
        if (investAmount < 1)
            return null;

        var amount = investAmount / intent.Coin.CurrentPrice;
        state.Balance -= investAmount;
        state.Positions[intent.Coin.DisplayName] = state.Positions.GetValueOrDefault(intent.Coin.DisplayName) + amount;

        return new TradeOrder
        {
            Symbol = intent.Coin.DisplayName,
            Action = TradeAction.Buy,
            Amount = amount,
            CurrentPrice = intent.Coin.CurrentPrice,
            Confidence = 0.8m,
            Reason = intent.Reason
        };
    }

    private static TradeOrder? BuildSellOrder(StrategyIntent intent, SimulatedPortfolioState state)
    {
        if (!state.Positions.TryGetValue(intent.Coin.DisplayName, out var currentAmount) || currentAmount <= 0)
            return null;

        var pct = intent.ActionAmount == "Alles" ? 1.0m : ParsePercent(intent.ActionAmount);
        var sellAmount = currentAmount * pct;
        if (sellAmount <= 0)
            return null;

        state.Balance += sellAmount * intent.Coin.CurrentPrice;
        var remaining = currentAmount - sellAmount;
        if (remaining <= 0.000000001m)
            state.Positions.Remove(intent.Coin.DisplayName);
        else
            state.Positions[intent.Coin.DisplayName] = remaining;

        return new TradeOrder
        {
            Symbol = intent.Coin.DisplayName,
            Action = TradeAction.Sell,
            Amount = sellAmount,
            CurrentPrice = intent.Coin.CurrentPrice,
            Confidence = 0.8m,
            Reason = intent.Reason
        };
    }

    // ── Save / Load ─────────────────────────────────────────────

    public void Save(TradingStrategy strategy)
    {
        try
        {
            var data = new StrategyData
            {
                Name = strategy.Name,
                IsActive = strategy.IsActive,
                CreatedAt = strategy.CreatedAt,
                Blocks = strategy.Blocks.Select(b => new BlockData
                {
                    Id = b.Id, Type = b.Type.ToString(), Category = b.Category,
                    Operator = b.Operator, Value = b.Value,
                    ConditionPreset = b.ConditionPreset, ActionAmount = b.ActionAmount,
                    AlarmText = b.AlarmText, X = b.X, Y = b.Y
                }).ToList(),
                Connections = strategy.Connections.Select(c => new ConnData
                {
                    Id = c.Id, FromBlockId = c.FromBlockId, ToBlockId = c.ToBlockId, OutputPort = c.OutputPort
                }).ToList()
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            var path = Path.Combine(_saveDir, SanitizeName(strategy.Name) + ".json");
            File.WriteAllText(path, json);
        }
        catch { }
    }

    public TradingStrategy? Load(string name)
    {
        try
        {
            var path = Path.Combine(_saveDir, SanitizeName(name) + ".json");
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<StrategyData>(json);
            if (data == null) return null;
            return DataToStrategy(data);
        }
        catch { return null; }
    }

    public List<string> ListSavedStrategies()
    {
        try
        {
            return Directory.GetFiles(_saveDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();
        }
        catch { return new(); }
    }

    private static TradingStrategy DataToStrategy(StrategyData d)
    {
        var s = new TradingStrategy { Name = d.Name, IsActive = d.IsActive, CreatedAt = d.CreatedAt };
        foreach (var b in d.Blocks)
        {
            Enum.TryParse<BlockType>(b.Type, out var bt);
            s.Blocks.Add(new StrategyBlock
            {
                Id = b.Id, Type = bt, Category = b.Category, Operator = b.Operator,
                Value = b.Value, ConditionPreset = b.ConditionPreset,
                ActionAmount = b.ActionAmount, AlarmText = b.AlarmText, X = b.X, Y = b.Y
            });
        }
        foreach (var c in d.Connections)
            s.Connections.Add(new StrategyConnection { Id = c.Id, FromBlockId = c.FromBlockId, ToBlockId = c.ToBlockId, OutputPort = c.OutputPort });
        return s;
    }

    private static string SanitizeName(string n) =>
        string.Join("_", n.Split(Path.GetInvalidFileNameChars()));

    // DTOs
    private class StrategyData
    {
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<BlockData> Blocks { get; set; } = new();
        public List<ConnData> Connections { get; set; } = new();
    }
    private class BlockData
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public string Operator { get; set; } = "";
        public double Value { get; set; }
        public string ConditionPreset { get; set; } = "";
        public string ActionAmount { get; set; } = "";
        public string AlarmText { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
    }
    private class ConnData
    {
        public Guid Id { get; set; }
        public Guid FromBlockId { get; set; }
        public Guid ToBlockId { get; set; }
        public string OutputPort { get; set; } = "";
    }
}
