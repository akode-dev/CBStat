using Akode.CBStat.Models;
using Akode.CBStat.Services;
using Akode.CBStat.UI;
using Spectre.Console;
using Spectre.Console.Rendering;

// Handle --help
if (args.Contains("--help") || args.Contains("-h"))
{
    ShowHelp();
    return;
}

// Initialize services
var settings = new SettingsService();
await settings.LoadAsync();
settings.ApplyCommandLineArgs(args);

var cts = new CancellationTokenSource();
var openSettings = false;

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var runner = new CommandRunner();
var service = new CodexBarService(runner, settings);
var renderer = new ConsoleRenderer();
var settingsUI = new SettingsUI(settings);

// Main loop (can restart after settings)
while (true)
{
    cts = new CancellationTokenSource();
    openSettings = false;

    // Start key monitoring
    _ = Task.Run(() => MonitorKeys(cts.Token, () => { openSettings = true; cts.Cancel(); }, () => cts.Cancel()));

    var refreshInterval = TimeSpan.FromSeconds(settings.Settings.RefreshIntervalSeconds);

    // Header
    Console.Clear();
    AnsiConsole.MarkupLine("[bold]cbstat[/] - AI Provider Usage Monitor");
    if (settings.Settings.DeveloperModeEnabled)
        AnsiConsole.MarkupLine("[yellow]Developer mode: using sample data[/]");
    AnsiConsole.MarkupLine("[dim]Press [bold]O[/] for settings, [bold]Q[/] to quit[/]");
    AnsiConsole.WriteLine();

    // Initial load with spinner
    List<UsageData>? initialData = null;
    await AnsiConsole.Status()
        .AutoRefresh(true)
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("cyan"))
        .StartAsync("Connecting to providers...", async ctx =>
        {
            var messages = new[]
            {
                "Connecting to providers...",
                "Fetching usage data...",
                "Querying Claude...",
                "Checking Codex...",
                "Reading Gemini stats...",
                "Processing responses..."
            };

            var messageIndex = 0;
            var messageTask = Task.Run(async () =>
            {
                while (initialData == null && !cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1500);
                    messageIndex = (messageIndex + 1) % messages.Length;
                    ctx.Status(messages[messageIndex]);
                }
            });

            try
            {
                initialData = await service.GetAllUsageAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Cancelled during initial load
            }
        });

    if (cts.Token.IsCancellationRequested && !openSettings)
    {
        break;
    }

    if (initialData == null)
    {
        if (openSettings)
        {
            await settingsUI.ShowAsync();
            service = new CodexBarService(runner, settings);
            continue;
        }
        break;
    }

    // Main display loop
    try
    {
        var currentData = initialData;
        var isRefreshing = false;
        var lastUpdate = DateTime.Now;

        var displayMode = settings.Settings.DisplayMode;
        await AnsiConsole.Live(BuildDisplay(currentData, refreshInterval, settings.Settings.DeveloperModeEnabled, isRefreshing, displayMode))
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                ctx.UpdateTarget(BuildDisplay(currentData, refreshInterval, settings.Settings.DeveloperModeEnabled, false, displayMode));
                ctx.Refresh();

                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Wait for refresh interval, checking frequently for cancellation
                        var elapsed = TimeSpan.Zero;
                        while (elapsed < refreshInterval && !cts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(100, cts.Token);
                            elapsed += TimeSpan.FromMilliseconds(100);
                        }

                        if (cts.Token.IsCancellationRequested)
                            break;

                        // Show refreshing indicator
                        isRefreshing = true;
                        ctx.UpdateTarget(BuildDisplay(currentData, refreshInterval, settings.Settings.DeveloperModeEnabled, true, displayMode));
                        ctx.Refresh();

                        // Fetch new data
                        currentData = await service.GetAllUsageAsync(cts.Token);
                        lastUpdate = DateTime.Now;

                        // Update display
                        isRefreshing = false;
                        ctx.UpdateTarget(BuildDisplay(currentData, refreshInterval, settings.Settings.DeveloperModeEnabled, false, displayMode));
                        ctx.Refresh();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
    }
    catch (OperationCanceledException)
    {
        // Normal interruption
    }

    if (openSettings)
    {
        await settingsUI.ShowAsync();
        service = new CodexBarService(runner, settings);
        continue;
    }

    break;
}

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[dim]Goodbye![/]");

static IRenderable BuildDisplay(List<UsageData> data, TimeSpan refreshInterval, bool devMode, bool isRefreshing, DisplayMode displayMode)
{
    var renderer = new ConsoleRenderer();
    var content = renderer.BuildDisplay(data, displayMode);

    var refreshIndicator = isRefreshing ? "[cyan]⟳[/]" : "";

    if (displayMode == DisplayMode.Compact)
    {
        // Compact header: Today: Fr
        var todayHeader = new Markup($"[dim]Today: {DateTime.Now:ddd}[/]\n");

        // Compact status: multiple short lines
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(refreshIndicator)) lines.Add(refreshIndicator);
        if (devMode) lines.Add("[yellow]DEV[/]");
        lines.Add($"UPD: {DateTime.Now:HH:mm}");
        lines.Add($"RSH: {refreshInterval.TotalSeconds}s");
        lines.Add("Opt: ^O");
        lines.Add("Exit: ^Q");

        return new Rows(
            todayHeader,
            content,
            new Markup($"\n[dim]{string.Join("\n", lines)}[/]")
        );
    }
    else
    {
        // Vertical mode: single status line
        var devIndicator = devMode ? "[yellow]DEV[/] | " : "";
        var statusLine = $"{refreshIndicator}{devIndicator}Updated: {DateTime.Now:HH:mm:ss} | Refresh: {refreshInterval.TotalSeconds}s | [dim]O[/]=settings [dim]Q[/]=quit";

        return new Rows(
            content,
            new Markup($"\n[dim]{statusLine}[/]")
        );
    }
}

static void MonitorKeys(CancellationToken ct, Action onSettings, Action onQuit)
{
    while (!ct.IsCancellationRequested)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.O)
            {
                onSettings();
                return;
            }
            else if (key.Key == ConsoleKey.Q)
            {
                onQuit();
                return;
            }
        }
        Thread.Sleep(50);
    }
}

static void ShowHelp()
{
    AnsiConsole.MarkupLine("[bold]cbstat[/] - AI Provider Usage Monitor");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Usage:[/] cbstat [options]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Options:[/]");
    AnsiConsole.MarkupLine("  -i, --interval <seconds>   Refresh interval (default: 120)");
    AnsiConsole.MarkupLine("  -p, --providers <list>     Comma-separated providers (claude,codex,gemini)");
    AnsiConsole.MarkupLine("  -t, --timeout <seconds>    Command timeout (default: 30)");
    AnsiConsole.MarkupLine("      --dev                  Use sample data (developer mode)");
    AnsiConsole.MarkupLine("  -h, --help                 Show this help");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Keyboard shortcuts:[/]");
    AnsiConsole.MarkupLine("  O                          Open settings");
    AnsiConsole.MarkupLine("  Q                          Quit");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Examples:[/]");
    AnsiConsole.MarkupLine("  cbstat                          # Monitor all providers");
    AnsiConsole.MarkupLine("  cbstat -p claude,codex          # Monitor only Claude and Codex");
    AnsiConsole.MarkupLine("  cbstat -i 60                    # Refresh every 60 seconds");
    AnsiConsole.MarkupLine("  cbstat --dev                    # Use sample data for testing");
}
