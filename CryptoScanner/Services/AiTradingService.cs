using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CryptoScanner.Models;

namespace CryptoScanner.Services;

public class AiTradingService : IAiTradingService
{
    private string _apiKey = string.Empty;
    private string _apiUrl = string.Empty;
    private string _provider = "ChatGPT";  // "ChatGPT" or "Claude"
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_apiUrl);
    public string ProviderName => _provider;

    public void Configure(string apiKey, string apiUrl, string provider)
    {
        _apiKey = apiKey;
        _apiUrl = apiUrl;
        _provider = provider;
    }

    public async Task<List<TradeOrder>> AnalyzeAndRecommend(List<CryptoCoin> coins, Portfolio portfolio)
    {
        if (!IsConfigured)
            return SimulateAiTrading(coins, portfolio);

        try
        {
            return await CallAiApi(coins, portfolio);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[AiTrading] Falling back to rule-based recommendations: {ex}");
            // Fall back to rule-based if API call fails
            return SimulateAiTrading(coins, portfolio);
        }
    }

    /// <summary>
    /// Rule-based fallback that uses the existing technical analysis scores
    /// to generate trade recommendations without an external AI API.
    /// </summary>
    public List<TradeOrder> SimulateAiTrading(List<CryptoCoin> coins, Portfolio portfolio)
    {
        var orders = new List<TradeOrder>();
        var availableBalance = portfolio.Balance;

        foreach (var coin in coins.Where(c => c.Indicators != null).OrderByDescending(c => Math.Abs(c.CompositeScore)))
        {
            if (coin.CurrentPrice <= 0) continue;

            // Strong buy signal: score >= 40
            if (coin.CompositeScore >= 40 && availableBalance > 10)
            {
                var maxPerTrade = availableBalance * 0.1m; // Max 10% of remaining balance per trade
                var investAmount = Math.Min(maxPerTrade, availableBalance * 0.5m);
                if (investAmount <= 0) continue;

                var coinAmount = investAmount / coin.CurrentPrice;
                var confidence = Math.Min(1.0m, (decimal)coin.CompositeScore / 100m);

                orders.Add(new TradeOrder
                {
                    Symbol = coin.DisplayName,
                    Action = TradeAction.Buy,
                    Amount = coinAmount,
                    CurrentPrice = coin.CurrentPrice,
                    Confidence = confidence,
                    Reason = $"Score {coin.CompositeScore}: {coin.SignalText}. RSI={coin.Indicators!.Rsi14:N0}, MACD={coin.Indicators.MacdSignalText}"
                });

                availableBalance -= investAmount;
            }

            // Strong sell signal: score <= -40, and we hold this coin
            if (coin.CompositeScore <= -40)
            {
                var position = portfolio.Positions.FirstOrDefault(p => p.Symbol == coin.DisplayName);
                if (position != null && position.Amount > 0)
                {
                    var confidence = Math.Min(1.0m, Math.Abs((decimal)coin.CompositeScore) / 100m);
                    orders.Add(new TradeOrder
                    {
                        Symbol = coin.DisplayName,
                        Action = TradeAction.Sell,
                        Amount = position.Amount,
                        CurrentPrice = coin.CurrentPrice,
                        Confidence = confidence,
                        Reason = $"Score {coin.CompositeScore}: {coin.SignalText}. RSI={coin.Indicators!.Rsi14:N0}"
                    });
                }
            }
        }

        return orders.Take(10).ToList(); // Limit to top 10 recommendations
    }

    private async Task<List<TradeOrder>> CallAiApi(List<CryptoCoin> coins, Portfolio portfolio)
    {
        // Build the prompt with coin data
        var sb = new StringBuilder();
        sb.AppendLine("Du bist ein Krypto-Trading-Assistent. Analysiere folgende Coins und mein Portfolio.");
        sb.AppendLine("Gib Kauf/Verkauf Empfehlungen als JSON-Array zurueck.");
        sb.AppendLine();
        sb.AppendLine($"Mein Kontostand: {portfolio.Balance:N2} {portfolio.Currency}");
        sb.AppendLine("Meine Positionen:");
        foreach (var pos in portfolio.Positions)
            sb.AppendLine($"  {pos.Symbol}: {pos.Amount:N6} (Kaufpreis: {pos.AverageBuyPrice:N4})");
        sb.AppendLine();
        sb.AppendLine("Top Coins nach Score:");
        foreach (var coin in coins.Where(c => c.Indicators != null).OrderByDescending(c => c.CompositeScore).Take(20))
        {
            sb.AppendLine($"  {coin.DisplayName}: Preis={coin.CurrentPrice}, Score={coin.CompositeScore}, " +
                          $"RSI={coin.Indicators!.Rsi14:N1}, Signal={coin.SignalText}");
        }
        sb.AppendLine();
        sb.AppendLine("Antworte NUR mit einem JSON-Array in diesem Format:");
        sb.AppendLine("[{\"symbol\":\"BTC\",\"action\":\"buy\",\"amount\":0.01,\"confidence\":0.8,\"reason\":\"Erklaerung\"}]");
        sb.AppendLine("action kann sein: buy, sell, hold");

        var prompt = sb.ToString();

        // Build API request based on provider
        string requestBody;
        if (_provider == "Claude")
        {
            requestBody = JsonSerializer.Serialize(new
            {
                model = "claude-sonnet-4-20250514",
                max_tokens = 1024,
                messages = new[] { new { role = "user", content = prompt } }
            });
        }
        else // ChatGPT / OpenAI format
        {
            requestBody = JsonSerializer.Serialize(new
            {
                model = "gpt-4o-mini",
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = 1024
            });
        }

        var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        if (_provider == "Claude")
        {
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        // Extract the text content from the AI response
        var doc = JsonDocument.Parse(json);
        string aiText;
        if (_provider == "Claude")
        {
            aiText = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "[]";
        }
        else
        {
            aiText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "[]";
        }

        // Extract JSON array from the response text
        var jsonStart = aiText.IndexOf('[');
        var jsonEnd = aiText.LastIndexOf(']');
        if (jsonStart < 0 || jsonEnd < 0) return new List<TradeOrder>();

        var jsonArray = aiText.Substring(jsonStart, jsonEnd - jsonStart + 1);
        var rawOrders = JsonSerializer.Deserialize<List<AiOrderDto>>(jsonArray,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (rawOrders == null) return new List<TradeOrder>();

        return rawOrders.Select(o => new TradeOrder
        {
            Symbol = o.Symbol ?? "",
            Action = o.Action?.ToLower() switch
            {
                "buy" => TradeAction.Buy,
                "sell" => TradeAction.Sell,
                _ => TradeAction.Hold
            },
            Amount = o.Amount,
            Confidence = o.Confidence,
            Reason = o.Reason ?? "",
            CurrentPrice = coins.FirstOrDefault(c => c.DisplayName == o.Symbol)?.CurrentPrice ?? 0
        }).ToList();
    }

    private class AiOrderDto
    {
        public string? Symbol { get; set; }
        public string? Action { get; set; }
        public decimal Amount { get; set; }
        public decimal Confidence { get; set; }
        public string? Reason { get; set; }
    }
}
