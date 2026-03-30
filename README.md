# CryptoScanner

DE: Desktop-Tool fuer Krypto-Analyse, Scanner-Signale, Paper-Trading, Strategie-Builder und KI-Unterstuetzung auf Basis von Avalonia UI und .NET 8.

EN: Desktop app for crypto analysis, scanner signals, paper trading, strategy building, and AI-assisted workflows built with Avalonia UI and .NET 8.

> DE: Keine Anlageberatung. Dieses Projekt dient nur zu Informations-, Lern- und Testzwecken.
>
> EN: Not financial advice. This project is for information, learning, and testing only.

## Features

- DE: Echtzeit-Marktdaten ueber die Kraken Public API ohne privaten API-Key
- EN: Real-time market data via the Kraken public API without a private API key
- DE: Technische Analyse mit RSI, MACD, SMA, EMA, Bollinger-Baendern und Volumen
- EN: Technical analysis with RSI, MACD, SMA, EMA, Bollinger Bands, and volume
- DE: Composite-Score mit Kauf-, Halten- und Verkaufssignalen
- EN: Composite score with buy, hold, and sell signals
- DE: Paper-Trading-Depot mit Gebuehren, Historie und Depotentwicklung
- EN: Paper trading portfolio with fees, history, and portfolio performance
- DE: Visueller Strategie-Editor mit Testen, Optimieren und Fake-Depot-Ausfuehrung
- EN: Visual strategy editor with testing, optimization, and paper portfolio execution
- DE: Benutzeroberflaeche mit Deutsch/Englisch-Umschaltung
- EN: User interface with German/English language switching

## Platforms

- Windows
- Linux
- macOS

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Linux dependencies

```bash
# Ubuntu / Debian
sudo apt-get install -y libx11-dev libice-dev libsm-dev libfontconfig1-dev

# Fedora
sudo dnf install libX11-devel libICE-devel libSM-devel fontconfig-devel
```

## Run locally

```bash
cd CryptoScanner
dotnet restore
dotnet build
dotnet run --project CryptoScanner
```

## Release builds

Manual publish examples:

```bash
# Windows
dotnet publish CryptoScanner/CryptoScanner.csproj -c Release -r win-x64 --self-contained

# Linux
dotnet publish CryptoScanner/CryptoScanner.csproj -c Release -r linux-x64 --self-contained

# macOS Intel
dotnet publish CryptoScanner/CryptoScanner.csproj -c Release -r osx-x64 --self-contained

# macOS Apple Silicon
dotnet publish CryptoScanner/CryptoScanner.csproj -c Release -r osx-arm64 --self-contained
```

Automated GitHub releases:

- DE: Ein Git-Tag wie `v1.0.0` startet den GitHub-Actions-Workflow und baut Release-Artefakte fuer Windows, Linux und macOS.
- EN: A git tag like `v1.0.0` triggers the GitHub Actions workflow and builds release artifacts for Windows, Linux, and macOS.

## Quick start

1. DE: Scan starten und Coins laden.
   EN: Start a scan and load coins.
2. DE: Coin auswaehlen und Chart plus Indikatoren ansehen.
   EN: Select a coin and inspect the chart plus indicators.
3. DE: Optional das Paper-Depot oder den Strategie-Tab verwenden.
   EN: Optionally use the paper portfolio or strategy tab.

## Project structure

```text
CryptoScanner/
├── .github/workflows/
├── CryptoScanner.sln
├── README.md
└── CryptoScanner/
    ├── Assets/
    ├── Models/
    ├── Services/
    ├── ViewModels/
    └── Views/
```

## Tech stack

- Avalonia UI 11
- .NET 8
- MVVM with CommunityToolkit.Mvvm
- Kraken public market data

## License

DE: Das Projekt ist aktuell fuer private Nutzung und Weiterentwicklung gedacht.

EN: The project is currently intended for private use and further development.
