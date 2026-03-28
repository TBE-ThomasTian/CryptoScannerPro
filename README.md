# CryptoScanner - Kryptowährungs-Analyse-Tool

Ein Desktop-Analysewerkzeug für Kryptowährungen, gebaut mit Avalonia UI (.NET 8). Läuft auf **Windows** und **Linux**.

> **⚠ Keine Anlageberatung** – Dieses Tool dient ausschließlich zu Informationszwecken. Alle angezeigten Signale und Bewertungen stellen keine Kauf- oder Verkaufsempfehlungen dar.

## Funktionen

- Echtzeit-Daten von der Kraken Public API (kein API-Schlüssel nötig)
- Technische Indikatoren: RSI, MACD, SMA, EMA, Bollinger Bänder, Volumen
- Composite-Score (-100 bis +100) mit Signalen: Starker Kauf, Kauf, Halten, Verkauf, Starker Verkauf
- Dunkles Krypto-Design mit farbcodierten Signalen
- Such- und Filterfunktion
- Detailansicht mit allen Indikatoren
- Auto-Aktualisierung (2-Minuten-Intervall)

## Voraussetzungen

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Version 8.0 oder höher)

### Linux (zusätzlich)

```bash
# Ubuntu/Debian
sudo apt-get install -y libx11-dev libice-dev libsm-dev libfontconfig1-dev

# Fedora
sudo dnf install libX11-devel libICE-devel libSM-devel fontconfig-devel
```

## Build & Ausführung

```bash
# Repository-Verzeichnis öffnen
cd CryptoScanner

# NuGet-Pakete wiederherstellen
dotnet restore

# Projekt bauen
dotnet build

# Anwendung starten
dotnet run --project CryptoScanner
```

### Release-Build erstellen

```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained

# Linux
dotnet publish -c Release -r linux-x64 --self-contained
```

## Bedienung

1. **Scan starten** – Klicke auf "Scan Starten", um alle USD-Handelspaare von Kraken zu laden und zu analysieren
2. **Suchen** – Tippe einen Coin-Namen in das Suchfeld (z.B. "BTC", "ETH")
3. **Filtern** – Wähle ein Signal im Dropdown (z.B. nur "Starker Kauf" anzeigen)
4. **Details** – Klicke auf einen Coin in der Liste, um die technische Analyse im Detail zu sehen
5. **Auto-Aktualisierung** – Aktiviere das Kontrollkästchen, um alle 2 Minuten automatisch neu zu scannen

## Projektstruktur

```
CryptoScanner/
├── CryptoScanner.sln
├── README.md
└── CryptoScanner/
    ├── CryptoScanner.csproj
    ├── Program.cs
    ├── App.axaml / App.axaml.cs
    ├── Models/
    │   ├── CryptoCoin.cs
    │   ├── OhlcCandle.cs
    │   ├── SignalType.cs
    │   └── TechnicalIndicators.cs
    ├── ViewModels/
    │   └── MainWindowViewModel.cs
    ├── Views/
    │   ├── MainWindow.axaml / MainWindow.axaml.cs
    │   └── Converters.cs
    └── Services/
        ├── KrakenApiService.cs
        ├── TechnicalAnalysisService.cs
        └── ScoringService.cs
```

## Technische Details

- **API**: Kraken Public REST API (keine Authentifizierung erforderlich)
- **Rate-Limiting**: 350ms Verzögerung zwischen API-Aufrufen
- **OHLC-Daten**: 1-Stunden-Kerzen für Indikatorberechnung
- **Framework**: Avalonia UI 11.x mit FluentTheme (Dark Mode)
- **Architektur**: MVVM mit CommunityToolkit.Mvvm

## Lizenz

Dieses Projekt ist für persönliche Nutzung bestimmt.
