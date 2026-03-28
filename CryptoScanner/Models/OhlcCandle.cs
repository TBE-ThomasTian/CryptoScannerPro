namespace CryptoScanner.Models;

public class OhlcCandle
{
    public long Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public decimal Vwap { get; set; }
    public int Count { get; set; }
}
