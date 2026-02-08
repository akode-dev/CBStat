using CLIStat.Services;
using CLIStat.UI;
using Spectre.Console;

var refreshInterval = TimeSpan.FromSeconds(120);
var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var runner = new CommandRunner();
var service = new CodexBarService(runner);
var renderer = new ConsoleRenderer();

AnsiConsole.MarkupLine("[dim]CLIStat - AI Provider Usage Monitor[/]");
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
