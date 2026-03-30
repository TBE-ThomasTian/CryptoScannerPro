using CommunityToolkit.Mvvm.ComponentModel;
using CryptoScanner.Services;

namespace CryptoScanner.Models;

public enum BlockType { Start, Condition, ActionBuy, ActionSell, ActionHold, ActionAlarm }

public partial class StrategyBlock : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty] private BlockType _type = BlockType.Condition;
    [ObservableProperty] private string _category = "RSI";      // RSI, MACD, SMA, EMA, Bollinger, PriceChange, Volume, OrderBook, PriceRange
    [ObservableProperty] private string _operator = "<";         // >, <, ==
    [ObservableProperty] private double _value = 30;
    [ObservableProperty] private string _conditionPreset = "";   // e.g. "Signal positiv", "Preis ueber SMA20"
    [ObservableProperty] private string _actionAmount = "10%";   // for action blocks
    [ObservableProperty] private string _alarmText = "";
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;

    // Display helpers
    public string Title => Type switch
    {
        BlockType.Start => Loc.T("strategy.block.start"),
        BlockType.Condition => Category,
        BlockType.ActionBuy => Loc.T("strategy.block.buy"),
        BlockType.ActionSell => Loc.T("strategy.block.sell"),
        BlockType.ActionHold => Loc.T("strategy.block.hold"),
        BlockType.ActionAlarm => Loc.T("strategy.block.alarm"),
        _ => "?"
    };

    public string Description => Type switch
    {
        BlockType.Start => Loc.T("strategy.block.startdesc"),
        BlockType.Condition when !string.IsNullOrEmpty(ConditionPreset) => ConditionPreset,
        BlockType.Condition => $"{Category} {Operator} {Value}",
        BlockType.ActionBuy => string.Format(Loc.T("strategy.block.buydesc"), ActionAmount),
        BlockType.ActionSell => string.Format(Loc.T("strategy.block.selldesc"), ActionAmount),
        BlockType.ActionHold => Loc.T("strategy.block.holddesc"),
        BlockType.ActionAlarm => string.IsNullOrEmpty(AlarmText) ? Loc.T("strategy.block.alarmdesc") : AlarmText,
        _ => ""
    };

    public string BorderColor => Type switch
    {
        BlockType.Start => "#F0F6FC",
        BlockType.Condition => "#3B82F6",
        BlockType.ActionBuy => "#00D4AA",
        BlockType.ActionSell => "#FF4757",
        BlockType.ActionHold => "#F0B90B",
        BlockType.ActionAlarm => "#A78BFA",
        _ => "#8B949E"
    };

    public string BgColor => Type switch
    {
        BlockType.Start => "#1C2333",
        BlockType.Condition => "#0F1B2D",
        BlockType.ActionBuy => "#0A1F1A",
        BlockType.ActionSell => "#1F0A0E",
        BlockType.ActionHold => "#1F1A0A",
        BlockType.ActionAlarm => "#150F22",
        _ => "#161B22"
    };

    public bool IsCondition => Type == BlockType.Condition;
    public bool HasInput => Type != BlockType.Start;
    public int OutputCount => Type == BlockType.Condition ? 2 : 1;  // Ja/Nein vs Out

    partial void OnTypeChanged(BlockType value) => RefreshLocalization();
    partial void OnActionAmountChanged(string value) => RefreshLocalization();
    partial void OnAlarmTextChanged(string value) => RefreshLocalization();
    partial void OnConditionPresetChanged(string value) => RefreshLocalization();
    partial void OnOperatorChanged(string value) => RefreshLocalization();
    partial void OnValueChanged(double value) => RefreshLocalization();
    partial void OnCategoryChanged(string value) => RefreshLocalization();

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
    }
}

public class StrategyConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromBlockId { get; set; }
    public Guid ToBlockId { get; set; }
    public string OutputPort { get; set; } = "Out";  // "Out", "Ja", "Nein"
}

public partial class TradingStrategy : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isActive;
    public List<StrategyBlock> Blocks { get; set; } = new();
    public List<StrategyConnection> Connections { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public TradingStrategy()
    {
        Name = Loc.T("strategy.name.new");
    }
}
