# cbstat

Cross-platform console application for monitoring AI provider usage (Claude, Codex, Gemini).

Built with .NET 10 and [Spectre.Console](https://spectreconsole.net/).

## Features

- Real-time usage monitoring for Claude, Codex, and Gemini
- Auto-refresh every 2 minutes
- Color-coded progress bars (green < 50%, yellow 50-80%, red > 80%)
- Cross-platform: Windows, Linux, macOS
- Works in Windows Terminal, WSL, and native Linux terminals

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [codexbar CLI](https://github.com/steipete/CodexBar) installed and configured

## Installation

```bash
git clone <repository-url>
cd cbstat
dotnet build
```

## Usage

### Windows (with WSL)

```bash
cd D:\cbstat
dotnet run --project src/Akode.CBStat
```

### WSL / Linux

```bash
cd /mnt/d/cbstat  # or your Linux path
dotnet run --project src/Akode.CBStat
```

## Output Example

```
╭──────────────────────────────────────╮
│  Claude                              │
│  Session  ████████████░░░░  75%      │
│  Weekly   █████░░░░░░░░░░░  32%      │
│  Sonnet   ██████████████░░  89%      │
│  Reset: 2h 15m                       │
╰──────────────────────────────────────╯

╭──────────────────────────────────────╮
│  Codex                               │
│  Session  ██████░░░░░░░░░░  40%      │
│  Weekly   ████████░░░░░░░░  55%      │
│  Reset: 5h 30m                       │
╰──────────────────────────────────────╯

╭──────────────────────────────────────╮
│  Gemini                              │
│  Pro      ██░░░░░░░░░░░░░░  12%      │
│  Flash    █████████░░░░░░░  60%      │
│  Reset: 1d 3h                        │
╰──────────────────────────────────────╯

Updated: 12:34:56  |  Refresh: 120s  |  Ctrl+C to exit
```

## Project Structure

```
cbstat/
├── CBStat.slnx
├── README.md
├── src/
│   └── Akode.CBStat/
│       ├── Models/       # Data models
│       ├── Services/     # Business logic
│       └── UI/           # Console rendering
└── tests/
    └── Akode.CBStat.Tests/
```

## Building

```bash
# Debug build
dotnet build CBStat.slnx

# Release build
dotnet build CBStat.slnx -c Release

# Run tests
dotnet test CBStat.slnx

# Publish self-contained
dotnet publish src/Akode.CBStat -c Release -r linux-x64 --self-contained
dotnet publish src/Akode.CBStat -c Release -r win-x64 --self-contained
```

## License

MIT
