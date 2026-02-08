using Spectre.Console;

namespace Akode.CBStat.UI;

public static class LoadingSpinner
{
    private static readonly string[] LoadingMessages =
    [
        "Connecting to providers",
        "Fetching usage data",
        "Querying Claude",
        "Checking Codex",
        "Reading Gemini stats",
        "Processing responses",
        "Crunching numbers",
        "Almost there",
        "Gathering metrics",
        "Syncing data"
    ];

    private static readonly string[] RefreshMessages =
    [
        "Refreshing",
        "Updating stats",
        "Fetching latest",
        "Syncing",
        "Getting fresh data"
    ];

    private static int _messageIndex;
    private static readonly Random _random = new();

    public static string GetLoadingMessage()
    {
        _messageIndex = (_messageIndex + 1) % LoadingMessages.Length;
        return LoadingMessages[_messageIndex];
    }

    public static string GetRefreshMessage()
    {
        return RefreshMessages[_random.Next(RefreshMessages.Length)];
    }

    public static async Task<T> WithSpinnerAsync<T>(
        string initialMessage,
        Func<Task<T>> action,
        bool isRefresh = false)
    {
        T result = default!;

        await AnsiConsole.Status()
            .AutoRefresh(true)
            .SpinnerStyle(Style.Parse("cyan"))
            .Spinner(Spinner.Known.Dots)
            .StartAsync(initialMessage, async ctx =>
            {
                // Change message periodically during long operations
                var messageTask = Task.Run(async () =>
                {
                    var iteration = 0;
                    while (true)
                    {
                        await Task.Delay(800);
                        var msg = isRefresh ? GetRefreshMessage() : GetLoadingMessage();
                        ctx.Status($"{msg}...");
                        iteration++;
                        if (iteration > 20) break; // Safety limit
                    }
                });

                result = await action();
            });

        return result;
    }

    public static async Task WithSpinnerAsync(
        string initialMessage,
        Func<Task> action,
        bool isRefresh = false)
    {
        await AnsiConsole.Status()
            .AutoRefresh(true)
            .SpinnerStyle(Style.Parse("cyan"))
            .Spinner(Spinner.Known.Dots)
            .StartAsync(initialMessage, async ctx =>
            {
                var cts = new CancellationTokenSource();

                // Change message periodically
                var messageTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(800, cts.Token);
                            var msg = isRefresh ? GetRefreshMessage() : GetLoadingMessage();
                            ctx.Status($"{msg}...");
                        }
                    }
                    catch (OperationCanceledException) { }
                });

                await action();
                cts.Cancel();
            });
    }
}
