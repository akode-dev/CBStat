using Akode.CBStat.Models;
using Akode.CBStat.Services;
using Spectre.Console;

namespace Akode.CBStat.UI;

public class SettingsUI
{
    private readonly SettingsService _settingsService;

    public SettingsUI(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<bool> ShowAsync()
    {
        Console.Clear();
        AnsiConsole.MarkupLine("[bold]cbstat Settings[/]");
        AnsiConsole.MarkupLine("[dim]───────────────────────────────────────[/]");
        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select option:")
                .AddChoices([
                    "Providers",
                    "Refresh Interval",
                    "Developer Mode",
                    "Save & Exit",
                    "Cancel"
                ]));

        switch (choice)
        {
            case "Providers":
                ConfigureProviders();
                return await ShowAsync();

            case "Refresh Interval":
                ConfigureRefreshInterval();
                return await ShowAsync();

            case "Developer Mode":
                ToggleDeveloperMode();
                return await ShowAsync();

            case "Save & Exit":
                await _settingsService.SaveAsync();
                AnsiConsole.MarkupLine("[green]Settings saved![/]");
                await Task.Delay(500);
                return true;

            case "Cancel":
                return false;
        }

        return false;
    }

    private void ConfigureProviders()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]Configure Providers[/]");
        AnsiConsole.WriteLine();

        var allProviders = ProviderConfig.GetDefaults();
        var currentEnabled = _settingsService.Settings.Providers
            .Where(p => p.IsEnabled)
            .Select(p => p.DisplayName)
            .ToHashSet();

        var prompt = new MultiSelectionPrompt<string>()
            .Title("Select providers to enable:")
            .NotRequired()
            .InstructionsText("[dim](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
            .AddChoices(allProviders.Select(p => p.DisplayName));

        // Pre-select currently enabled providers
        foreach (var name in currentEnabled)
        {
            prompt.Select(name);
        }

        var selected = AnsiConsole.Prompt(prompt);

        // Update settings
        foreach (var provider in _settingsService.Settings.Providers)
        {
            provider.IsEnabled = selected.Contains(provider.DisplayName);
        }

        AnsiConsole.MarkupLine($"[green]Enabled: {string.Join(", ", selected)}[/]");
        Thread.Sleep(500);
    }

    private void ConfigureRefreshInterval()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]Refresh Interval[/]");
        AnsiConsole.WriteLine();

        var intervals = new Dictionary<string, int>
        {
            ["30 seconds"] = 30,
            ["1 minute"] = 60,
            ["2 minutes (default)"] = 120,
            ["5 minutes"] = 300,
            ["10 minutes"] = 600
        };

        var current = _settingsService.Settings.RefreshIntervalSeconds;
        var currentLabel = intervals.FirstOrDefault(x => x.Value == current).Key ?? $"{current} seconds";

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Current: [yellow]{currentLabel}[/]")
                .AddChoices(intervals.Keys));

        _settingsService.Settings.RefreshIntervalSeconds = intervals[choice];
        AnsiConsole.MarkupLine($"[green]Set to: {choice}[/]");
        Thread.Sleep(500);
    }

    private void ToggleDeveloperMode()
    {
        _settingsService.Settings.DeveloperModeEnabled = !_settingsService.Settings.DeveloperModeEnabled;
        var status = _settingsService.Settings.DeveloperModeEnabled ? "[yellow]ON[/]" : "[dim]OFF[/]";
        AnsiConsole.MarkupLine($"Developer mode: {status}");
        Thread.Sleep(500);
    }
}
