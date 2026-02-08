using System.Reflection;
using Akode.CBStat.Models;
using Akode.CBStat.Services;
using Akode.CBStat.UI;
using Spectre.Console;
using Spectre.Console.Rendering;

var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "1.0.0";

if (args.Contains("--help") || args.Contains("-h"))
{
    ShowHelp();
    return;
}

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

var runner = new CommandRunner(settings.Settings.CommandTimeoutSeconds);
var service = new CodexBarService(runner, settings);
var renderer = new ConsoleRenderer();
var settingsUI = new SettingsUI(settings);

while (true)
{
    cts = new CancellationTokenSource();
    openSettings = false;

    _ = Task.Run(() => MonitorKeys(cts.Token, () => { openSettings = true; cts.Cancel(); }));

    var refreshInterval = TimeSpan.FromSeconds(settings.Settings.RefreshIntervalSeconds);

    Console.Clear();
    var isCompact = settings.Settings.DisplayMode == DisplayMode.Compact;
    if (isCompact)
    {
        AnsiConsole.MarkupLine($"[bold]CBStat[/] [dim]v{version}[/]");
    }
    else
    {
        AnsiConsole.MarkupLine($"[bold]CBStat[/] [dim]v{version}[/] - AI Provider Usage Monitor");
        if (settings.Settings.DeveloperModeEnabled)
            AnsiConsole.MarkupLine("[yellow]Developer mode: using sample data[/]");
    }
    AnsiConsole.WriteLine();

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
            using var messageCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            var messageTask = Task.Run(async () =>
            {
                try
                {
                    while (!messageCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1500, messageCts.Token);
                        messageIndex = (messageIndex + 1) % messages.Length;
                        ctx.Status(messages[messageIndex]);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, messageCts.Token);

            try
            {
                initialData = await service.GetAllUsageAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                messageCts.Cancel();
                try
                {
                    await messageTask;
                }
                catch (OperationCanceledException)
                {
                }
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
            runner = new CommandRunner(settings.Settings.CommandTimeoutSeconds);
            service = new CodexBarService(runner, settings);
            continue;
        }
        break;
    }

    try
    {
        var currentData = initialData;
        var isRefreshing = false;

        Console.Clear();

        var displayMode = settings.Settings.DisplayMode;
        var workDayStartHour = settings.Settings.WorkDayStartHour;
        await AnsiConsole.Live(BuildDisplay(renderer, currentData, refreshInterval, settings.Settings.DeveloperModeEnabled, isRefreshing, displayMode, version, workDayStartHour))
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                ctx.UpdateTarget(BuildDisplay(renderer, currentData, refreshInterval, settings.Settings.DeveloperModeEnabled, false, displayMode, version, workDayStartHour));
                ctx.Refresh();

                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(refreshInterval, cts.Token);

                        isRefreshing = true;
                        ctx.UpdateTarget(BuildDisplay(renderer, currentData, refreshInterval, settings.Settings.DeveloperModeEnabled, true, displayMode, version, workDayStartHour));
                        ctx.Refresh();

                        currentData = await service.GetAllUsageAsync(cts.Token);

                        isRefreshing = false;
                        ctx.UpdateTarget(BuildDisplay(renderer, currentData, refreshInterval, settings.Settings.DeveloperModeEnabled, false, displayMode, version, workDayStartHour));
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
    }

    if (openSettings)
    {
        await settingsUI.ShowAsync();
        runner = new CommandRunner(settings.Settings.CommandTimeoutSeconds);
        service = new CodexBarService(runner, settings);
        continue;
    }

    break;
}

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[dim]Goodbye![/]");

static IRenderable BuildDisplay(
    ConsoleRenderer renderer,
    IReadOnlyList<UsageData> data,
    TimeSpan refreshInterval,
    bool devMode,
    bool isRefreshing,
    DisplayMode displayMode,
    string version,
    int workDayStartHour)
{
    renderer.SetWorkDayStartHour(workDayStartHour);
    var content = renderer.BuildDisplay(data, displayMode);
    var now = DateTime.Now;

    var refreshIndicator = isRefreshing ? "[cyan]⟳[/]" : string.Empty;
    var refreshSeconds = (int)refreshInterval.TotalSeconds;

    if (displayMode == DisplayMode.Compact)
    {
        var lines = new List<string>(6);
        if (!string.IsNullOrEmpty(refreshIndicator))
            lines.Add(refreshIndicator);
        if (devMode)
            lines.Add("[yellow]DEV[/]");
        lines.Add($" UPD: {now:HH:mm}");
        lines.Add($"RFSH: {refreshSeconds}s");
        lines.Add(" Opt: O/Ctrl+O");
        lines.Add("Exit: Ctrl+C");

        return new Rows(
            new Markup($"[bold]CBStat[/] [dim]v{version}[/]"),
            new Markup($"[dim]Today: {now:ddd}[/]\n"),
            content,
            new Markup($"\n[dim]{string.Join("\n", lines)}[/]")
        );
    }

    var devIndicator = devMode ? "[yellow]DEV[/] | " : string.Empty;
    var statusLine = $"{refreshIndicator}{devIndicator}Updated: {now:HH:mm:ss} | Refresh: {refreshSeconds}s | [dim]O[/]=settings [dim]Ctrl+C[/]=quit";

    return new Rows(
        new Markup($"[bold]CBStat[/] [dim]v{version}[/] - AI Provider Usage Monitor\n"),
        content,
        new Markup($"\n[dim]{statusLine}[/]")
    );
}

static void MonitorKeys(CancellationToken ct, Action onSettings)
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
    AnsiConsole.MarkupLine("  O or Ctrl+O                Open settings");
    AnsiConsole.MarkupLine("  Ctrl+C                     Quit");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Examples:[/]");
    AnsiConsole.MarkupLine("  cbstat                          # Monitor all providers");
    AnsiConsole.MarkupLine("  cbstat -p claude,codex          # Monitor only Claude and Codex");
    AnsiConsole.MarkupLine("  cbstat -i 60                    # Refresh every 60 seconds");
    AnsiConsole.MarkupLine("  cbstat --dev                    # Use sample data for testing");
}
