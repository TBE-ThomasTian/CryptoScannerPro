namespace CryptoScanner.Models;

public class Transaction
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Symbol { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal PricePerUnit { get; set; }
    public decimal Fee { get; set; }
    public decimal TotalCost { get; set; }
    public string Source { get; set; } = "Manuell";

    public string TypeText => Type == TransactionType.Kauf ? "Kauf" : "Verkauf";
    public string TypeColor => Type == TransactionType.Kauf ? "#00D4AA" : "#FF4757";
    public string TimestampFormatted => Timestamp.ToString("dd.MM.yyyy HH:mm");
    public string FeeFormatted => Fee.ToString("N2");
    public string TotalFormatted => TotalCost.ToString("N2");
    public string PriceFormatted => PricePerUnit.ToString("N4");
    public string AmountFormatted => Amount.ToString("N6");
}
