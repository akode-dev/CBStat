# cbstat

Cross-platform console application for monitoring AI provider usage (Claude, Codex, Gemini).

Built with .NET 10 and [Spectre.Console](https://spectreconsole.net/).

## Features

- Real-time usage monitoring for Claude, Codex, and Gemini
- Daily budget calculation (remaining quota for today)
- Two display modes: Vertical (panels) and Compact (narrow windows)
- Auto-refresh with configurable interval
- Color-coded progress bars (green < 50%, yellow 50-80%, red > 80%)
- Interactive settings menu (press O)
- Cross-platform: Windows, Linux, macOS

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [CodexBar CLI](https://github.com/Finesssee/Win-CodexBar) installed and configured

### Windows Setup

On Windows, CodexBar must be installed inside WSL. cbstat will automatically call WSL to execute CodexBar commands.

1. Install WSL: `wsl --install`
2. Inside WSL, install CodexBar following [instructions](https://github.com/Finesssee/Win-CodexBar#installation)
3. Run cbstat from Windows - it will use WSL automatically

### Linux / macOS Setup

Install CodexBar directly:
```bash
# Follow installation instructions at:
# https://github.com/Finesssee/Win-CodexBar#installation
```

## Installation

### From Release

Download the latest release for your platform from [Releases](https://github.com/akode-dev/CBStat/releases).

### From Source

```bash
git clone https://github.com/akode-dev/CBStat.git
cd CBStat
dotnet build -c Release
```

## Usage

```bash
# Run with default settings
cbstat

# Or from source
dotnet run --project src/Akode.CBStat
```

### Command Line Options

```
cbstat [options]

Options:
  -i, --interval <seconds>   Refresh interval (default: 120)
  -p, --providers <list>     Comma-separated providers (claude,codex,gemini)
  -t, --timeout <seconds>    Command timeout (default: 30)
      --dev                  Use sample data (developer mode)
  -h, --help                 Show help
```

### Keyboard Shortcuts

| Key | Action |
|-----|--------|
| O | Open settings |
| Q | Quit |
| Ctrl+C | Force quit |

## Display Modes

### Vertical Mode (default)
```
╭─ Claude ─────────────────────────────╮
│ Session  ████████████░░░░░░░░  60%   │
│ Weekly   █████░░░░░░░░░░░░░░░  25%   │
│ Reset: 2h 15m                        │
╰──────────────────────────────────────╯
```

### Compact Mode (for narrow windows)
```
Today: Fr

Claude
S 60%(40.0) 19:00
W 25%(12.5) 12:00 Fr

Codex
S 17%(83.0) 15:50
W 26%(11.5) 12:30 Fr

UPD: 14:16
RFSH: 120s
Opt: ^O
Exit: ^Q
```

## Building

```bash
# Debug build
dotnet build CBStat.slnx

# Release build
dotnet build CBStat.slnx -c Release

# Run tests
dotnet test CBStat.slnx

# Publish for specific platform
dotnet publish src/Akode.CBStat -c Release -r win-x64 --self-contained
dotnet publish src/Akode.CBStat -c Release -r linux-x64 --self-contained
dotnet publish src/Akode.CBStat -c Release -r osx-x64 --self-contained
dotnet publish src/Akode.CBStat -c Release -r osx-arm64 --self-contained
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

## License

MIT
