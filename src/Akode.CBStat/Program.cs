using Akode.CBStat.Services;
using Akode.CBStat.UI;
using Spectre.Console;

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

// Key monitoring thread
var keyMonitorTask = Task.Run(() =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);

            // Ctrl+O for settings
            if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.O)
            {
                openSettings = true;
                cts.Cancel();
            }
            // 'o' or 'O' also opens settings (easier to use)
            else if (key.Key == ConsoleKey.O)
            {
                openSettings = true;
                cts.Cancel();
            }
            // 'q' or 'Q' to quit
            else if (key.Key == ConsoleKey.Q)
            {
                cts.Cancel();
            }
        }
        Thread.Sleep(50);
    }
});

var runner = new CommandRunner();
var service = new CodexBarService(runner, settings);
var renderer = new ConsoleRenderer();
var settingsUI = new SettingsUI(settings);

// Main loop (can restart after settings)
while (true)
{
    cts = new CancellationTokenSource();
    openSettings = false;

    var refreshInterval = TimeSpan.FromSeconds(settings.Settings.RefreshIntervalSeconds);

    // Header
    Console.Clear();
    AnsiConsole.MarkupLine("[bold]cbstat[/] - AI Provider Usage Monitor");
    if (settings.Settings.DeveloperModeEnabled)
        AnsiConsole.MarkupLine("[yellow]Developer mode: using sample data[/]");
    AnsiConsole.MarkupLine("[dim]Press [bold]O[/] for settings, [bold]Q[/] to quit[/]");
    AnsiConsole.WriteLine();

    try
    {
        await AnsiConsole.Live(new Text("Loading..."))
            .AutoClear(true)
            .StartAsync(async ctx =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var data = await service.GetAllUsageAsync(cts.Token);
                    var table = renderer.BuildTable(data);

                    var statusLine = settings.Settings.DeveloperModeEnabled
                        ? $"[yellow]DEV[/] | Updated: {DateTime.Now:HH:mm:ss} | Refresh: {refreshInterval.TotalSeconds}s | [dim]O[/]=settings [dim]Q[/]=quit"
                        : $"Updated: {DateTime.Now:HH:mm:ss} | Refresh: {refreshInterval.TotalSeconds}s | [dim]O[/]=settings [dim]Q[/]=quit";

                    var layout = new Rows(
                        table,
                        new Markup($"\n[dim]{statusLine}[/]")
                    );

                    ctx.UpdateTarget(layout);
                    ctx.Refresh();

                    try
                    {
                        await Task.Delay(refreshInterval, cts.Token);
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
        // Show settings UI
        await settingsUI.ShowAsync();
        // Reload settings into service
        service = new CodexBarService(runner, settings);
        continue;
    }

    // Exit
    break;
}

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[dim]Goodbye![/]");

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
