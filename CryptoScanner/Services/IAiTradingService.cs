using CryptoScanner.Models;

namespace CryptoScanner.Services;

/// <summary>
/// Interface for AI-based trading analysis.
/// Implementations can use OpenAI, Anthropic, or rule-based fallback.
/// </summary>
public interface IAiTradingService
{
    bool IsConfigured { get; }
    string ProviderName { get; }
    Task<List<TradeOrder>> AnalyzeAndRecommend(List<CryptoCoin> coins, Portfolio portfolio);
}
