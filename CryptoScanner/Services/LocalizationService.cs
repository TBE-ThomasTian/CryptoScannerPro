namespace CryptoScanner.Services;

/// <summary>
/// Simple localization service with German (de) and English (en) dictionaries.
/// Access via Loc.T("key") or Loc["key"].
/// </summary>
public static class Loc
{
    private static string _lang = "de";
    public static event Action? LanguageChanged;

    public static string Language
    {
        get => _lang;
        set
        {
            if (_lang == value) return;
            _lang = value;
            SavePreference(value);
            LanguageChanged?.Invoke();
        }
    }

    public static string T(string key) => Get(key);

    public static string Get(string key)
    {
        var dict = _lang == "en" ? En : De;
        return dict.TryGetValue(key, out var val) ? val : key;
    }

    static Loc()
    {
        // Load saved preference
        try
        {
            var path = PreferencePath();
            if (File.Exists(path))
            {
                var saved = File.ReadAllText(path).Trim();
                if (saved is "de" or "en") _lang = saved;
            }
        }
        catch { }
    }

    private static void SavePreference(string lang)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoScanner");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "language.txt"), lang);
        }
        catch { }
    }

    private static string PreferencePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoScanner", "language.txt");

    // ═══════════════════════════════════════════════════════════
    // GERMAN
    // ═══════════════════════════════════════════════════════════
    private static readonly Dictionary<string, string> De = new()
    {
        // Tabs
        ["tab.scanner"] = "Scanner",
        ["tab.depot"] = "Depot",
        ["tab.ai"] = "KI-Trading",
        ["tab.strategy"] = "Strategie",

        // Scanner toolbar
        ["scan.start"] = "Scan Starten",
        ["scan.stop"] = "Abbrechen",
        ["scan.start.tip"] = "Alle Coins scannen und technisch analysieren",
        ["scan.stop.tip"] = "Laufenden Scan abbrechen",
        ["search.tip"] = "Nach Coin-Name oder Symbol suchen",
        ["filter.tip"] = "Nur Coins mit bestimmtem Signal anzeigen",
        ["sort.tip"] = "Tabelle nach gewaehltem Kriterium sortieren",
        ["timeframe.tip"] = "Zeitraum fuer OHLC-Daten und technische Analyse",
        ["currency.tip"] = "Handelswaehrung auswaehlen (USD oder EUR)",
        ["autorefresh.tip"] = "Automatisch in regelmaessigen Abstaenden neu scannen",
        ["interval.tip"] = "Intervall fuer automatische Aktualisierung",

        // DataGrid columns
        ["col.coin"] = "COIN",
        ["col.price"] = "PREIS",
        ["col.change"] = "24H %",
        ["col.score"] = "SCORE",
        ["col.signal"] = "SIGNAL",
        ["col.volume"] = "VOLUMEN",
        ["col.coin.tip"] = "Kryptowaehrung / Handelspaar",
        ["col.price.tip"] = "Aktueller Marktpreis",
        ["col.change.tip"] = "Preisaenderung in den letzten 24 Stunden",
        ["col.score.tip"] = "Composite-Score basierend auf technischen Indikatoren (-100 bis +100)",
        ["col.signal.tip"] = "Handelssignal basierend auf dem Composite-Score",
        ["col.volume.tip"] = "Handelsvolumen in den letzten 24 Stunden",

        // Signals
        ["signal.strongbuy"] = "Starker Kauf",
        ["signal.buy"] = "Kauf",
        ["signal.hold"] = "Halten",
        ["signal.sell"] = "Verkauf",
        ["signal.strongsell"] = "Starker Verkauf",

        // Detail panel
        ["detail.score"] = "Bewertung",
        ["detail.indicators"] = "Technische Indikatoren",
        ["detail.pricerange"] = "Preisspanne",
        ["detail.pricoverview"] = "Preisuebersicht",
        ["detail.orderbook"] = "Orderbuch (Kauf-/Verkaufsdruck)",

        // Indicators with tooltips
        ["rsi.label"] = "RSI (14)",
        ["rsi.tip"] = "Relative Strength Index (14 Perioden)\n< 30 = Ueberverkauft (Kaufsignal)\n> 70 = Ueberkauft (Verkaufssignal)",
        ["macd.label"] = "MACD",
        ["macd.tip"] = "Moving Average Convergence Divergence\nPositiv = Aufwaertstrend\nNegativ = Abwaertstrend",
        ["sma.label"] = "SMA 20/50",
        ["sma.tip"] = "Simple Moving Average\nSMA20 = kurzfristiger Durchschnitt\nSMA50 = mittelfristiger Durchschnitt",
        ["ema.tip"] = "Exponential Moving Average\nReagiert schneller auf Preisaenderungen als SMA",
        ["bollinger.label"] = "Bollinger",
        ["bollinger.tip"] = "Bollinger Band (20 Perioden, 2 Std.abw.)\nNahe oberem Band = moeglicherweise ueberkauft\nNahe unterem Band = moeglicherweise ueberverkauft",
        ["volume.label"] = "Volumen",
        ["volume.tip"] = "Handelsvolumen im Vergleich zum Durchschnitt",
        ["range.tip"] = "Position des aktuellen Preises zwischen Hoch und Tief\n0% = am Tief, 100% = am Hoch",
        ["orderbook.tip"] = "Live-Orderbuchdaten von Kraken\nZeigt das Verhaeltnis von Kaeufern zu Verkaeufern",
        ["bid.tip"] = "Anteil der Kaufauftraege am gesamten Ordervolumen",
        ["ask.tip"] = "Anteil der Verkaufsauftraege am gesamten Ordervolumen",

        // Chart toggles
        ["chart.candles"] = "Kerzen",
        ["chart.candles.tip"] = "Zwischen Linien- und Kerzenchart umschalten",
        ["chart.sma20.tip"] = "Simple Moving Average 20 Perioden (kurzfristig)",
        ["chart.sma50.tip"] = "Simple Moving Average 50 Perioden (mittelfristig)",
        ["chart.ema12.tip"] = "Exponential Moving Average 12 Perioden",
        ["chart.ema26.tip"] = "Exponential Moving Average 26 Perioden",
        ["chart.bb.tip"] = "Bollinger Baender (20 Perioden, 2 Std.abw.)",

        // Depot
        ["depot.balance"] = "Kontostand",
        ["depot.balance.tip"] = "Verfuegbares Barguthaben fuer neue Kaeufe",
        ["depot.totalvalue"] = "Gesamtwert",
        ["depot.totalvalue.tip"] = "Barguthaben + Wert aller gehaltenen Positionen",
        ["depot.pnl"] = "Gewinn/Verlust",
        ["depot.pnl.tip"] = "Gesamtgewinn oder -verlust seit Depotstart",
        ["depot.return"] = "Rendite",
        ["depot.return.tip"] = "Prozentuale Rendite seit Depotstart",
        ["depot.startcapital"] = "Startkapital",
        ["depot.startcapital.tip"] = "Anfangskapital fuer das Uebungsdepot eingeben",
        ["depot.reset"] = "Depot Zuruecksetzen",
        ["depot.reset.tip"] = "Alle Positionen und Transaktionen loeschen und mit neuem Startkapital beginnen",
        ["depot.positions"] = "Positionen",
        ["depot.history"] = "Transaktionshistorie",
        ["buy"] = "Kaufen",
        ["buy.tip"] = "Diese Kryptowaehrung zum aktuellen Preis kaufen",
        ["sell"] = "Verkaufen",
        ["sell.tip"] = "Diese Position zum aktuellen Preis verkaufen",

        // AI Trading
        ["ai.provider"] = "Anbieter",
        ["ai.provider.tip"] = "KI-Anbieter fuer automatische Handelsempfehlungen",
        ["ai.apikey"] = "API-Schluessel",
        ["ai.apikey.tip"] = "API-Schluessel fuer den gewaehlten KI-Anbieter\n(wird verschluesselt gespeichert)",
        ["ai.apiurl"] = "API-URL",
        ["ai.apiurl.tip"] = "API-Endpunkt URL (Standard wird automatisch gesetzt)",
        ["ai.analyze"] = "KI-Analyse Starten",
        ["ai.analyze.tip"] = "KI analysiert alle gescannten Coins und erstellt Handelsempfehlungen",
        ["ai.execall"] = "Alle Ausfuehren",
        ["ai.autotrading"] = "Auto-Trading (nach Scan)",
        ["ai.autotrading.tip"] = "KI handelt automatisch nach jedem Scan basierend auf den Empfehlungen",
        ["ai.settings"] = "KI-Trading Einstellungen",
        ["ai.recommendations"] = "KI-Empfehlungen",
        ["ai.log"] = "KI-Protokoll",
        ["ai.norule"] = "Ohne API-Schluessel wird der regelbasierte Modus verwendet (technische Indikatoren).",

        // Strategy
        ["strat.name.tip"] = "Name der Strategie (wird beim Speichern verwendet)",
        ["strat.condition"] = "Bedingung:",
        ["strat.action"] = "Aktion:",
        ["strat.rsi.tip"] = "Erstellt eine RSI-Bedingung (z.B. RSI < 30 = ueberverkauft)",
        ["strat.macd.tip"] = "Erstellt eine MACD-Bedingung (Signal, Kreuzung)",
        ["strat.sma.tip"] = "Erstellt eine Preisvergleich-Bedingung mit SMA",
        ["strat.ema.tip"] = "Erstellt eine Preisvergleich-Bedingung mit EMA",
        ["strat.bb.tip"] = "Erstellt eine Bollinger-Band-Bedingung",
        ["strat.vol.tip"] = "Erstellt eine Volumen-Bedingung (ueber/unter Durchschnitt)",
        ["strat.score.tip"] = "Erstellt eine Composite-Score-Bedingung",
        ["strat.buy.tip"] = "Kaufaktion hinzufuegen (Prozent des Guthabens)",
        ["strat.sell.tip"] = "Verkaufaktion hinzufuegen (Prozent der Position)",
        ["strat.hold.tip"] = "Halteaktion hinzufuegen (keine Transaktion)",
        ["strat.save"] = "Speichern",
        ["strat.save.tip"] = "Aktuelle Strategie als JSON speichern",
        ["strat.new"] = "Neu",
        ["strat.test"] = "Testen",
        ["strat.test.tip"] = "Strategie gegen aktuelle Scan-Daten testen (kein echter Handel)",
        ["strat.active"] = "Aktiv",
        ["strat.active.tip"] = "Strategie nach jedem Scan automatisch ausfuehren",
        ["strat.load"] = "Laden",
        ["strat.load.tip"] = "Gespeicherte Strategie laden",
        ["strat.results"] = "Strategie-Ergebnisse",
        ["strat.log"] = "Protokoll",
        ["strat.execall"] = "Alle ausfuehren",
        ["execute"] = "Ausfuehren",

        // General
        ["status.ready"] = "Bereit",
        ["status.lastupdate"] = "Update:",
        ["close"] = "Schliessen",
        ["cancel"] = "Abbrechen",
        ["amount"] = "Menge",
        ["price"] = "Preis",
        ["total"] = "Gesamt",
        ["date"] = "Datum",
        ["type"] = "Typ",
        ["source"] = "Quelle",
        ["confidence"] = "Konfidenz",
        ["reason"] = "Begruendung",
        ["action"] = "Aktion",
        ["language"] = "DE",
    };

    // ═══════════════════════════════════════════════════════════
    // ENGLISH
    // ═══════════════════════════════════════════════════════════
    private static readonly Dictionary<string, string> En = new()
    {
        ["tab.scanner"] = "Scanner",
        ["tab.depot"] = "Portfolio",
        ["tab.ai"] = "AI Trading",
        ["tab.strategy"] = "Strategy",

        ["scan.start"] = "Start Scan",
        ["scan.stop"] = "Cancel",
        ["scan.start.tip"] = "Scan all coins and run technical analysis",
        ["scan.stop.tip"] = "Cancel the running scan",
        ["search.tip"] = "Search by coin name or symbol",
        ["filter.tip"] = "Show only coins with a specific signal",
        ["sort.tip"] = "Sort the table by selected criteria",
        ["timeframe.tip"] = "Timeframe for OHLC data and technical analysis",
        ["currency.tip"] = "Select trading currency (USD or EUR)",
        ["autorefresh.tip"] = "Automatically rescan at regular intervals",
        ["interval.tip"] = "Interval for automatic refresh",

        ["col.coin"] = "COIN",
        ["col.price"] = "PRICE",
        ["col.change"] = "24H %",
        ["col.score"] = "SCORE",
        ["col.signal"] = "SIGNAL",
        ["col.volume"] = "VOLUME",
        ["col.coin.tip"] = "Cryptocurrency / trading pair",
        ["col.price.tip"] = "Current market price",
        ["col.change.tip"] = "Price change in the last 24 hours",
        ["col.score.tip"] = "Composite score based on technical indicators (-100 to +100)",
        ["col.signal.tip"] = "Trading signal based on the composite score",
        ["col.volume.tip"] = "Trading volume in the last 24 hours",

        ["signal.strongbuy"] = "Strong Buy",
        ["signal.buy"] = "Buy",
        ["signal.hold"] = "Hold",
        ["signal.sell"] = "Sell",
        ["signal.strongsell"] = "Strong Sell",

        ["detail.score"] = "Rating",
        ["detail.indicators"] = "Technical Indicators",
        ["detail.pricerange"] = "Price Range",
        ["detail.pricoverview"] = "Price Overview",
        ["detail.orderbook"] = "Order Book (Buy/Sell Pressure)",

        ["rsi.label"] = "RSI (14)",
        ["rsi.tip"] = "Relative Strength Index (14 periods)\n< 30 = Oversold (buy signal)\n> 70 = Overbought (sell signal)",
        ["macd.label"] = "MACD",
        ["macd.tip"] = "Moving Average Convergence Divergence\nPositive = Uptrend\nNegative = Downtrend",
        ["sma.label"] = "SMA 20/50",
        ["sma.tip"] = "Simple Moving Average\nSMA20 = short-term average\nSMA50 = medium-term average",
        ["ema.tip"] = "Exponential Moving Average\nReacts faster to price changes than SMA",
        ["bollinger.label"] = "Bollinger",
        ["bollinger.tip"] = "Bollinger Bands (20 periods, 2 std dev)\nNear upper band = possibly overbought\nNear lower band = possibly oversold",
        ["volume.label"] = "Volume",
        ["volume.tip"] = "Trading volume compared to average",
        ["range.tip"] = "Current price position between high and low\n0% = at low, 100% = at high",
        ["orderbook.tip"] = "Live order book data from Kraken\nShows the ratio of buyers to sellers",
        ["bid.tip"] = "Share of buy orders in total order volume",
        ["ask.tip"] = "Share of sell orders in total order volume",

        ["chart.candles"] = "Candles",
        ["chart.candles.tip"] = "Toggle between line and candlestick chart",
        ["chart.sma20.tip"] = "Simple Moving Average 20 periods (short-term)",
        ["chart.sma50.tip"] = "Simple Moving Average 50 periods (medium-term)",
        ["chart.ema12.tip"] = "Exponential Moving Average 12 periods",
        ["chart.ema26.tip"] = "Exponential Moving Average 26 periods",
        ["chart.bb.tip"] = "Bollinger Bands (20 periods, 2 std dev)",

        ["depot.balance"] = "Balance",
        ["depot.balance.tip"] = "Available cash balance for new purchases",
        ["depot.totalvalue"] = "Total Value",
        ["depot.totalvalue.tip"] = "Cash balance + value of all held positions",
        ["depot.pnl"] = "Profit/Loss",
        ["depot.pnl.tip"] = "Total profit or loss since portfolio start",
        ["depot.return"] = "Return",
        ["depot.return.tip"] = "Percentage return since portfolio start",
        ["depot.startcapital"] = "Starting Capital",
        ["depot.startcapital.tip"] = "Enter starting capital for the practice portfolio",
        ["depot.reset"] = "Reset Portfolio",
        ["depot.reset.tip"] = "Delete all positions and transactions and start with new capital",
        ["depot.positions"] = "Positions",
        ["depot.history"] = "Transaction History",
        ["buy"] = "Buy",
        ["buy.tip"] = "Buy this cryptocurrency at the current price",
        ["sell"] = "Sell",
        ["sell.tip"] = "Sell this position at the current price",

        ["ai.provider"] = "Provider",
        ["ai.provider.tip"] = "AI provider for automated trade recommendations",
        ["ai.apikey"] = "API Key",
        ["ai.apikey.tip"] = "API key for the selected AI provider\n(stored encrypted)",
        ["ai.apiurl"] = "API URL",
        ["ai.apiurl.tip"] = "API endpoint URL (default is set automatically)",
        ["ai.analyze"] = "Start AI Analysis",
        ["ai.analyze.tip"] = "AI analyzes all scanned coins and creates trade recommendations",
        ["ai.execall"] = "Execute All",
        ["ai.autotrading"] = "Auto-Trading (after scan)",
        ["ai.autotrading.tip"] = "AI trades automatically after each scan based on recommendations",
        ["ai.settings"] = "AI Trading Settings",
        ["ai.recommendations"] = "AI Recommendations",
        ["ai.log"] = "AI Log",
        ["ai.norule"] = "Without an API key, rule-based mode is used (technical indicators).",

        ["strat.name.tip"] = "Strategy name (used when saving)",
        ["strat.condition"] = "Condition:",
        ["strat.action"] = "Action:",
        ["strat.rsi.tip"] = "Create an RSI condition (e.g. RSI < 30 = oversold)",
        ["strat.macd.tip"] = "Create a MACD condition (signal, crossover)",
        ["strat.sma.tip"] = "Create a price comparison condition with SMA",
        ["strat.ema.tip"] = "Create a price comparison condition with EMA",
        ["strat.bb.tip"] = "Create a Bollinger Band condition",
        ["strat.vol.tip"] = "Create a volume condition (above/below average)",
        ["strat.score.tip"] = "Create a composite score condition",
        ["strat.buy.tip"] = "Add buy action (percent of balance)",
        ["strat.sell.tip"] = "Add sell action (percent of position)",
        ["strat.hold.tip"] = "Add hold action (no transaction)",
        ["strat.save"] = "Save",
        ["strat.save.tip"] = "Save current strategy as JSON",
        ["strat.new"] = "New",
        ["strat.test"] = "Test",
        ["strat.test.tip"] = "Test strategy against current scan data (no real trading)",
        ["strat.active"] = "Active",
        ["strat.active.tip"] = "Automatically run strategy after each scan",
        ["strat.load"] = "Load",
        ["strat.load.tip"] = "Load a saved strategy",
        ["strat.results"] = "Strategy Results",
        ["strat.log"] = "Log",
        ["strat.execall"] = "Execute all",
        ["execute"] = "Execute",

        ["status.ready"] = "Ready",
        ["status.lastupdate"] = "Update:",
        ["close"] = "Close",
        ["cancel"] = "Cancel",
        ["amount"] = "Amount",
        ["price"] = "Price",
        ["total"] = "Total",
        ["date"] = "Date",
        ["type"] = "Type",
        ["source"] = "Source",
        ["confidence"] = "Confidence",
        ["reason"] = "Reason",
        ["action"] = "Action",
        ["language"] = "EN",
    };
}
