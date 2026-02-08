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

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var runner = new CommandRunner();
var service = new CodexBarService(runner, settings);
var renderer = new ConsoleRenderer();

var refreshInterval = TimeSpan.FromSeconds(settings.Settings.RefreshIntervalSeconds);

// Header
AnsiConsole.MarkupLine("[dim]cbstat - AI Provider Usage Monitor[/]");
if (settings.Settings.DeveloperModeEnabled)
    AnsiConsole.MarkupLine("[yellow]Developer mode: using sample data[/]");
AnsiConsole.MarkupLine("[dim]Press Ctrl+C to exit[/]");
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

                var layout = new Rows(
                    table,
                    new Markup($"\n[dim]Updated: {DateTime.Now:HH:mm:ss}  |  Refresh: {refreshInterval.TotalSeconds}s  |  Ctrl+C to exit[/]")
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
    // Normal exit
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
    AnsiConsole.MarkupLine("[bold]Examples:[/]");
    AnsiConsole.MarkupLine("  cbstat                          # Monitor all providers");
    AnsiConsole.MarkupLine("  cbstat -p claude,codex          # Monitor only Claude and Codex");
    AnsiConsole.MarkupLine("  cbstat -i 60                    # Refresh every 60 seconds");
    AnsiConsole.MarkupLine("  cbstat --dev                    # Use sample data for testing");
}
