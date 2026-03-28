using System.Text.Json;
using CryptoScanner.Models;

namespace CryptoScanner.Services;

public class KrakenApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _rateLimiter;
    private const string BaseUrl = "https://api.kraken.com/0/public";

    /// <param name="maxConcurrency">Max parallel requests (default 5).</param>
    public KrakenApiService(int maxConcurrency = 5)
    {
        _rateLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "CryptoScanner/1.0");
    }

    /// <summary>
    /// Acquire a concurrency slot. The SemaphoreSlim(5) caps parallel requests;
    /// no artificial delay needed — Kraken's public API allows 15-20 req/s.
    /// </summary>
    private async Task<IDisposable> AcquireSlotAsync(CancellationToken ct)
    {
        await _rateLimiter.WaitAsync(ct);
        return new SlotReleaser(_rateLimiter);
    }

    private sealed class SlotReleaser(SemaphoreSlim sem) : IDisposable
    {
        public void Dispose() => sem.Release();
    }

    private async Task<JsonElement> GetJsonAsync(string url, CancellationToken ct)
    {
        using var _ = await AcquireSlotAsync(ct);

        var response = await _httpClient.GetStringAsync(url, ct);
        var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errors) && errors.GetArrayLength() > 0)
        {
            var errorMsg = string.Join(", ", errors.EnumerateArray().Select(e => e.GetString()));
            throw new Exception($"Kraken API Fehler: {errorMsg}");
        }

        return root.GetProperty("result");
    }

    /// <summary>Fetch a raw JSON result without going through the concurrency gate
    /// (used for the single bulk ticker call that doesn't need throttling).</summary>
    private async Task<JsonElement> GetJsonDirectAsync(string url, CancellationToken ct)
    {
        var response = await _httpClient.GetStringAsync(url, ct);
        var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errors) && errors.GetArrayLength() > 0)
        {
            var errorMsg = string.Join(", ", errors.EnumerateArray().Select(e => e.GetString()));
            throw new Exception($"Kraken API Fehler: {errorMsg}");
        }

        return root.GetProperty("result");
    }

    /// <summary>
    /// Fetch trading pairs for a given quote currency ("USD" or "EUR").
    /// </summary>
    public async Task<List<(string PairName, string Base, string Quote, string AltName)>> GetTradingPairsAsync(string quoteCurrency = "USD", CancellationToken ct = default)
    {
        var result = await GetJsonDirectAsync($"{BaseUrl}/AssetPairs", ct);
        var pairs = new List<(string, string, string, string)>();

        // Kraken uses ZUSD/ZEUR internally
        var zQuote = $"Z{quoteCurrency}";  // "ZUSD" or "ZEUR"

        foreach (var prop in result.EnumerateObject())
        {
            var pair = prop.Value;
            var name = prop.Name;

            if (name.EndsWith(".d")) continue;

            string quote = "";
            string baseCur = "";
            string altName = "";

            if (pair.TryGetProperty("quote", out var quoteEl))
                quote = quoteEl.GetString() ?? "";
            if (pair.TryGetProperty("base", out var baseEl))
                baseCur = baseEl.GetString() ?? "";
            if (pair.TryGetProperty("altname", out var altEl))
                altName = altEl.GetString() ?? "";

            bool isMatch = quote == zQuote
                || quote == quoteCurrency
                || name.EndsWith(quoteCurrency)
                || altName.EndsWith(quoteCurrency);

            if (isMatch)
            {
                var displayBase = baseCur
                    .Replace("XXBT", "BTC")
                    .Replace("XETH", "ETH")
                    .Replace("XXRP", "XRP")
                    .Replace("XLTC", "LTC")
                    .Replace("XXLM", "XLM")
                    .Replace("XXDG", "DOGE")
                    .Replace("XZEC", "ZEC")
                    .Replace("XXMR", "XMR")
                    .TrimStart('X', 'Z');

                pairs.Add((name, displayBase, quoteCurrency, altName));
            }
        }

        return pairs.OrderBy(p => p.Item2).ToList();
    }

    /// <summary>
    /// Fetch ticker data for ALL pairs in a single API call.
    /// Kraken's /Ticker endpoint accepts a comma-separated list with no practical limit.
    /// </summary>
    public async Task<Dictionary<string, TickerData>> GetAllTickerDataAsync(IEnumerable<string> pairNames, CancellationToken ct = default)
    {
        var pairsParam = string.Join(",", pairNames);
        var result = await GetJsonDirectAsync($"{BaseUrl}/Ticker?pair={pairsParam}", ct);
        var tickers = new Dictionary<string, TickerData>();

        foreach (var prop in result.EnumerateObject())
        {
            try
            {
                var t = prop.Value;
                var ticker = new TickerData();

                if (t.TryGetProperty("c", out var close))
                    ticker.LastPrice = ParseDecimal(close[0]);
                if (t.TryGetProperty("o", out var open))
                    ticker.OpenPrice = ParseDecimal(open);
                if (t.TryGetProperty("h", out var high))
                    ticker.High24h = ParseDecimal(high[1]); // 24h high
                if (t.TryGetProperty("l", out var low))
                    ticker.Low24h = ParseDecimal(low[1]); // 24h low
                if (t.TryGetProperty("v", out var vol))
                    ticker.Volume24h = ParseDecimal(vol[1]); // 24h volume

                if (ticker.OpenPrice > 0)
                    ticker.Change24hPercent = ((ticker.LastPrice - ticker.OpenPrice) / ticker.OpenPrice) * 100;

                tickers[prop.Name] = ticker;
            }
            catch
            {
                // Skip malformed ticker entries
            }
        }

        return tickers;
    }

    /// <summary>
    /// Fetch OHLC candles for one pair.
    /// Uses the concurrency-limited slot so multiple calls can run in parallel safely.
    /// </summary>
    public async Task<List<OhlcCandle>> GetOhlcDataAsync(string pairName, int interval = 60, long since = 0, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/OHLC?pair={pairName}&interval={interval}";
        if (since > 0)
            url += $"&since={since}";
        var result = await GetJsonAsync(url, ct);
        var candles = new List<OhlcCandle>();

        foreach (var prop in result.EnumerateObject())
        {
            if (prop.Name == "last") continue;

            foreach (var item in prop.Value.EnumerateArray())
            {
                try
                {
                    var candle = new OhlcCandle
                    {
                        Timestamp = item[0].GetInt64(),
                        Open = ParseDecimal(item[1]),
                        High = ParseDecimal(item[2]),
                        Low = ParseDecimal(item[3]),
                        Close = ParseDecimal(item[4]),
                        Vwap = ParseDecimal(item[5]),
                        Volume = ParseDecimal(item[6]),
                        Count = item[7].GetInt32()
                    };
                    candles.Add(candle);
                }
                catch
                {
                    // Skip malformed candle data
                }
            }
        }

        return candles.OrderBy(c => c.Timestamp).ToList();
    }

    /// <summary>Fetch order book depth for a pair.</summary>
    public async Task<OrderBookData> GetOrderBookAsync(string pairName, int count = 50, CancellationToken ct = default)
    {
        var result = await GetJsonAsync($"{BaseUrl}/Depth?pair={pairName}&count={count}", ct);
        decimal totalBids = 0, totalAsks = 0;

        foreach (var prop in result.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("bids", out var bids))
            {
                foreach (var bid in bids.EnumerateArray())
                    totalBids += ParseDecimal(bid[1]); // volume
            }
            if (prop.Value.TryGetProperty("asks", out var asks))
            {
                foreach (var ask in asks.EnumerateArray())
                    totalAsks += ParseDecimal(ask[1]);
            }
        }

        return new OrderBookData { TotalBidVolume = totalBids, TotalAskVolume = totalAsks };
    }

    private static decimal ParseDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return decimal.Parse(element.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        return element.GetDecimal();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _rateLimiter.Dispose();
    }
}

public class TickerData
{
    public decimal LastPrice { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal High24h { get; set; }
    public decimal Low24h { get; set; }
    public decimal Volume24h { get; set; }
    public decimal Change24hPercent { get; set; }
}
