using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoScanner.Models;

namespace CryptoScanner.Services;

public class PortfolioService
{
    private readonly string _savePath;
    private Portfolio _portfolio;

    public Portfolio Portfolio => _portfolio;

    public PortfolioService(string? savePath = null)
    {
        _savePath = savePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CryptoScanner", "portfolio.json");

        var dir = Path.GetDirectoryName(_savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _portfolio = Load() ?? new Portfolio();
    }

    public void ResetPortfolio(decimal initialBalance, string currency)
    {
        _portfolio = new Portfolio
        {
            Balance = initialBalance,
            InitialBalance = initialBalance,
            Currency = currency,
            CreatedAt = DateTime.Now
        };
        Save();
    }

    public (bool Success, string Message) Buy(string symbol, string displayName, decimal amount, decimal pricePerUnit)
    {
        var totalCost = amount * pricePerUnit;
        if (totalCost > _portfolio.Balance)
            return (false, $"Nicht genug Guthaben. Verfuegbar: {_portfolio.Balance:N2}, Kosten: {totalCost:N2}");
        if (amount <= 0)
            return (false, "Menge muss groesser als 0 sein.");

        _portfolio.Balance -= totalCost;

        // Update or create position
        var position = _portfolio.Positions.FirstOrDefault(p => p.Symbol == symbol);
        if (position != null)
        {
            // Weighted average price
            var totalAmount = position.Amount + amount;
            position.AverageBuyPrice = (position.Amount * position.AverageBuyPrice + amount * pricePerUnit) / totalAmount;
            position.Amount = totalAmount;
            position.CurrentPrice = pricePerUnit;
        }
        else
        {
            _portfolio.Positions.Add(new PortfolioPosition
            {
                Symbol = symbol,
                DisplayName = displayName,
                Amount = amount,
                AverageBuyPrice = pricePerUnit,
                CurrentPrice = pricePerUnit
            });
        }

        _portfolio.TransactionHistory.Insert(0, new Transaction
        {
            Timestamp = DateTime.Now,
            Symbol = symbol,
            Type = TransactionType.Kauf,
            Amount = amount,
            PricePerUnit = pricePerUnit,
            TotalCost = totalCost,
            Source = "Manuell"
        });

        _portfolio.RefreshComputedProperties();
        Save();
        return (true, $"{amount:N6} {symbol} gekauft fuer {totalCost:N2}");
    }

    public (bool Success, string Message) Sell(string symbol, decimal amount, decimal pricePerUnit)
    {
        var position = _portfolio.Positions.FirstOrDefault(p => p.Symbol == symbol);
        if (position == null)
            return (false, $"Keine Position fuer {symbol} vorhanden.");
        if (amount > position.Amount)
            return (false, $"Nicht genug {symbol}. Verfuegbar: {position.Amount:N6}");
        if (amount <= 0)
            return (false, "Menge muss groesser als 0 sein.");

        var totalRevenue = amount * pricePerUnit;
        _portfolio.Balance += totalRevenue;
        position.Amount -= amount;
        position.CurrentPrice = pricePerUnit;

        if (position.Amount <= 0.000000001m)
            _portfolio.Positions.Remove(position);

        _portfolio.TransactionHistory.Insert(0, new Transaction
        {
            Timestamp = DateTime.Now,
            Symbol = symbol,
            Type = TransactionType.Verkauf,
            Amount = amount,
            PricePerUnit = pricePerUnit,
            TotalCost = totalRevenue,
            Source = "Manuell"
        });

        _portfolio.RefreshComputedProperties();
        Save();
        return (true, $"{amount:N6} {symbol} verkauft fuer {totalRevenue:N2}");
    }

    public (bool Success, string Message) ExecuteAiTrade(TradeOrder order)
        => ExecuteTrade(order, "KI-Trading");

    public (bool Success, string Message) ExecuteStrategyTrade(TradeOrder order)
        => ExecuteTrade(order, "Strategie");

    public (bool Success, string Message) ExecuteTrade(TradeOrder order, string source)
    {
        if (order.Action == TradeAction.Buy)
        {
            var result = Buy(order.Symbol, order.Symbol, order.Amount, order.CurrentPrice);
            if (result.Success)
            {
                var tx = _portfolio.TransactionHistory.FirstOrDefault();
                if (tx != null) tx.Source = source;
                Save();
            }
            return result;
        }
        else if (order.Action == TradeAction.Sell)
        {
            var result = Sell(order.Symbol, order.Amount, order.CurrentPrice);
            if (result.Success)
            {
                var tx = _portfolio.TransactionHistory.FirstOrDefault();
                if (tx != null) tx.Source = source;
                Save();
            }
            return result;
        }
        return (false, "Keine Aktion (Halten)");
    }

    public void UpdatePrices(IEnumerable<CryptoCoin> coins)
    {
        foreach (var position in _portfolio.Positions)
        {
            var coin = coins.FirstOrDefault(c => c.DisplayName == position.Symbol || c.BaseCurrency == position.Symbol);
            if (coin != null)
            {
                position.CurrentPrice = coin.CurrentPrice;
            }
        }
        _portfolio.RefreshComputedProperties();
        RecordSnapshot();
    }

    public void RecordSnapshot()
    {
        _portfolio.ValueHistory.Add(new PortfolioSnapshot
        {
            Timestamp = DateTime.Now,
            TotalValue = _portfolio.TotalValue
        });
        Save();
    }

    public void Save()
    {
        try
        {
            var data = new PortfolioData
            {
                Balance = _portfolio.Balance,
                InitialBalance = _portfolio.InitialBalance,
                Currency = _portfolio.Currency,
                CreatedAt = _portfolio.CreatedAt,
                Positions = _portfolio.Positions.Select(p => new PositionData
                {
                    Symbol = p.Symbol, DisplayName = p.DisplayName,
                    Amount = p.Amount, AverageBuyPrice = p.AverageBuyPrice
                }).ToList(),
                Transactions = _portfolio.TransactionHistory.Select(t => new TransactionData
                {
                    Timestamp = t.Timestamp, Symbol = t.Symbol, Type = t.Type.ToString(),
                    Amount = t.Amount, PricePerUnit = t.PricePerUnit, TotalCost = t.TotalCost,
                    Source = t.Source
                }).ToList(),
                Snapshots = _portfolio.ValueHistory.Select(s => new SnapshotData
                {
                    Timestamp = s.Timestamp, TotalValue = s.TotalValue
                }).ToList()
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_savePath, json);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PortfolioService] Save failed: {ex}");
        }
    }

    private Portfolio? Load()
    {
        try
        {
            if (!File.Exists(_savePath)) return null;
            var json = File.ReadAllText(_savePath);
            var data = JsonSerializer.Deserialize<PortfolioData>(json);
            if (data == null) return null;

            var portfolio = new Portfolio
            {
                Balance = data.Balance,
                InitialBalance = data.InitialBalance,
                Currency = data.Currency,
                CreatedAt = data.CreatedAt
            };

            foreach (var p in data.Positions)
                portfolio.Positions.Add(new PortfolioPosition
                {
                    Symbol = p.Symbol, DisplayName = p.DisplayName,
                    Amount = p.Amount, AverageBuyPrice = p.AverageBuyPrice
                });

            foreach (var t in data.Transactions)
                portfolio.TransactionHistory.Add(new Transaction
                {
                    Timestamp = t.Timestamp, Symbol = t.Symbol,
                    Type = t.Type == "Verkauf" ? TransactionType.Verkauf : TransactionType.Kauf,
                    Amount = t.Amount, PricePerUnit = t.PricePerUnit,
                    TotalCost = t.TotalCost, Source = t.Source
                });

            if (data.Snapshots != null)
                foreach (var s in data.Snapshots)
                    portfolio.ValueHistory.Add(new PortfolioSnapshot
                    {
                        Timestamp = s.Timestamp, TotalValue = s.TotalValue
                    });

            return portfolio;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PortfolioService] Load failed: {ex}");
            return null;
        }
    }

    // ── JSON serialization DTOs ───────────────────────────────
    private class PortfolioData
    {
        public decimal Balance { get; set; }
        public decimal InitialBalance { get; set; }
        public string Currency { get; set; } = "USD";
        public DateTime CreatedAt { get; set; }
        public List<PositionData> Positions { get; set; } = new();
        public List<TransactionData> Transactions { get; set; } = new();
        public List<SnapshotData> Snapshots { get; set; } = new();
    }

    private class PositionData
    {
        public string Symbol { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal AverageBuyPrice { get; set; }
    }

    private class TransactionData
    {
        public DateTime Timestamp { get; set; }
        public string Symbol { get; set; } = "";
        public string Type { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal TotalCost { get; set; }
        public string Source { get; set; } = "";
    }

    private class SnapshotData
    {
        public DateTime Timestamp { get; set; }
        public decimal TotalValue { get; set; }
    }
}
