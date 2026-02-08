using Akode.CBStat.Models;
using Akode.CBStat.Services;
using Spectre.Console;

namespace Akode.CBStat.UI;

public class SettingsUI
{
    private readonly SettingsService _settingsService;
    private const string Back = "← Back";

    public SettingsUI(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<bool> ShowAsync()
    {
        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine("[bold]cbstat Settings[/]");
            AnsiConsole.MarkupLine("[dim]───────────────────────────────────────[/]");
            AnsiConsole.MarkupLine("[dim]Use arrow keys to navigate, Enter to select[/]");
            AnsiConsole.WriteLine();

            var devStatus = _settingsService.Settings.DeveloperModeEnabled ? "[yellow]ON[/]" : "[dim]OFF[/]";
            var enabledCount = _settingsService.Settings.Providers.Count(p => p.IsEnabled);
            var interval = _settingsService.Settings.RefreshIntervalSeconds;

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select option:")
                    .HighlightStyle(Style.Parse("cyan"))
                    .AddChoices([
                        $"Providers          [[{enabledCount}/3 enabled]]",
                        $"Refresh Interval   [[{FormatInterval(interval)}]]",
                        $"Developer Mode     {devStatus}",
                        "───────────────────",
                        "[green]Save & Exit[/]",
                        "[dim]Cancel[/]"
                    ]));

            if (choice.StartsWith("Providers"))
            {
                ConfigureProviders();
            }
            else if (choice.StartsWith("Refresh Interval"))
            {
                ConfigureRefreshInterval();
            }
            else if (choice.StartsWith("Developer Mode"))
            {
                ToggleDeveloperMode();
            }
            else if (choice.Contains("Save"))
            {
                await _settingsService.SaveAsync();
                AnsiConsole.MarkupLine("[green]Settings saved![/]");
                await Task.Delay(500);
                return true;
            }
            else if (choice.Contains("Cancel"))
            {
                return false;
            }
            // Separator - do nothing, show menu again
        }
    }

    private void ConfigureProviders()
    {
        Console.Clear();
        AnsiConsole.MarkupLine("[bold]Configure Providers[/]");
        AnsiConsole.MarkupLine("[dim]───────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[dim]Space to toggle, Enter to confirm, select Back to return[/]");
        AnsiConsole.WriteLine();

        var allProviders = ProviderConfig.GetDefaults().ToList();
        var currentEnabled = _settingsService.Settings.Providers
            .Where(p => p.IsEnabled)
            .Select(p => p.DisplayName)
            .ToHashSet();

        var choices = new List<string> { Back };
        choices.AddRange(allProviders.Select(p => p.DisplayName));

        var prompt = new MultiSelectionPrompt<string>()
            .Title("Select providers to enable:")
            .NotRequired()
            .InstructionsText("")
            .AddChoices(choices);

        // Pre-select currently enabled providers
        foreach (var name in currentEnabled)
        {
            prompt.Select(name);
        }

        var selected = AnsiConsole.Prompt(prompt);

        // If only Back selected or Back is in selection, just return
        if (selected.Contains(Back))
        {
            selected.Remove(Back);
        }

        // Update settings
        foreach (var provider in _settingsService.Settings.Providers)
        {
            provider.IsEnabled = selected.Contains(provider.DisplayName);
        }

        if (selected.Count > 0)
        {
            AnsiConsole.MarkupLine($"[green]Enabled: {string.Join(", ", selected)}[/]");
            Thread.Sleep(400);
        }
    }

    private void ConfigureRefreshInterval()
    {
        Console.Clear();
        AnsiConsole.MarkupLine("[bold]Refresh Interval[/]");
        AnsiConsole.MarkupLine("[dim]───────────────────────────────────────[/]");
        AnsiConsole.WriteLine();

        var intervals = new Dictionary<string, int>
        {
            [Back] = -1,
            ["30 seconds"] = 30,
            ["1 minute"] = 60,
            ["2 minutes"] = 120,
            ["5 minutes"] = 300,
            ["10 minutes"] = 600
        };

        var current = _settingsService.Settings.RefreshIntervalSeconds;

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Current: [yellow]{FormatInterval(current)}[/]")
                .HighlightStyle(Style.Parse("cyan"))
                .AddChoices(intervals.Keys));

        if (choice == Back)
            return;

        _settingsService.Settings.RefreshIntervalSeconds = intervals[choice];
        AnsiConsole.MarkupLine($"[green]Set to: {choice}[/]");
        Thread.Sleep(400);
    }

    private void ToggleDeveloperMode()
    {
        _settingsService.Settings.DeveloperModeEnabled = !_settingsService.Settings.DeveloperModeEnabled;
        var status = _settingsService.Settings.DeveloperModeEnabled ? "[yellow]ON[/]" : "[dim]OFF[/]";
        AnsiConsole.MarkupLine($"Developer mode: {status}");
        Thread.Sleep(300);
    }

    private static string FormatInterval(int seconds) => seconds switch
    {
        30 => "30s",
        60 => "1m",
        120 => "2m",
        300 => "5m",
        600 => "10m",
        _ => $"{seconds}s"
    };
}
