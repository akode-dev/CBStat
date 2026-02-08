using Akode.CBStat.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Akode.CBStat.UI;

public class ConsoleRenderer
{
    public IRenderable BuildDisplay(IReadOnlyList<UsageData> data, DisplayMode mode)
    {
        return mode switch
        {
            DisplayMode.Compact => BuildCompactDisplay(data),
            _ => BuildVerticalDisplay(data)
        };
    }

    // Legacy method for backward compatibility
    public Table BuildTable(IReadOnlyList<UsageData> data) => BuildVerticalTable(data);

    #region Vertical Mode (default)

    private IRenderable BuildVerticalDisplay(IReadOnlyList<UsageData> data)
    {
        return BuildVerticalTable(data);
    }

    private Table BuildVerticalTable(IReadOnlyList<UsageData> data)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .HideHeaders();

        table.AddColumn(new TableColumn("Content").NoWrap());

        foreach (var provider in data)
        {
            table.AddRow(BuildProviderPanel(provider));
        }

        return table;
    }

    private Panel BuildProviderPanel(UsageData data)
    {
        var content = new Rows();

        if (data.HasError)
        {
            content = new Rows(
                new Markup($"[red]{Markup.Escape(data.Error ?? "Error")}[/]")
            );
        }
        else
        {
            var rows = new List<IRenderable>();

            if (data.Session != null)
            {
                rows.Add(BuildProgressRow(data.SessionLabel, data.Session));
            }

            if (data.Weekly != null)
            {
                rows.Add(BuildProgressRow(data.WeeklyLabel, data.Weekly));
            }

            if (data.Tertiary != null)
            {
                rows.Add(BuildProgressRow(data.TertiaryLabel, data.Tertiary));
            }

            // Show reset time from the first available window
            var resetWindow = data.Session ?? data.Weekly ?? data.Tertiary;
            if (resetWindow != null)
            {
                var resetText = resetWindow.FormatResetIn();
                if (!string.IsNullOrEmpty(resetText))
                {
                    rows.Add(new Markup($"[dim]Reset: {resetText}[/]"));
                }
            }

            if (rows.Count == 0)
            {
                rows.Add(new Markup("[dim]No data available[/]"));
            }

            content = new Rows(rows);
        }

        var providerTitle = FormatProviderName(data.Provider);
        var titleColor = GetProviderColor(data.Provider);

        return new Panel(content)
        {
            Header = new PanelHeader($"[{titleColor}]{providerTitle}[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0),
            Width = 50
        };
    }

    private static IRenderable BuildProgressRow(string label, UsageWindow window)
    {
        var percent = window.Percent;
        var color = GetPercentColor(percent);
        var barWidth = 20;
        var filledWidth = (int)(barWidth * percent / 100);
        var emptyWidth = barWidth - filledWidth;

        var bar = new string('\u2588', filledWidth) + new string('\u2591', emptyWidth);
        var dailyBudget = window.DailyBudgetText;
        var budgetPart = string.IsNullOrEmpty(dailyBudget) ? "" : $" [dim]{dailyBudget}[/]";

        return new Markup($"{label,-8} [{color}]{bar}[/] {percent,3:F0}%{budgetPart}");
    }

    #endregion

    #region Compact Mode (narrow vertical window)

    private IRenderable BuildCompactDisplay(IReadOnlyList<UsageData> data)
    {
        var rows = new List<IRenderable>();

        foreach (var provider in data)
        {
            if (rows.Count > 0)
                rows.Add(new Markup("")); // Empty line between providers

            rows.AddRange(BuildCompactProviderBlock(provider));
        }

        return new Rows(rows);
    }

    private IEnumerable<IRenderable> BuildCompactProviderBlock(UsageData data)
    {
        var providerName = FormatProviderName(data.Provider);
        var providerColor = GetProviderColor(data.Provider);

        // Provider name header
        yield return new Markup($"[{providerColor} bold]{providerName}[/]");

        if (data.HasError)
        {
            yield return new Markup($"[red]Error[/]");
            yield break;
        }

        // Session line: S  3%(97.0) 19:00
        if (data.Session != null)
        {
            yield return new Markup(BuildCompactLine("S", data.Session));
        }

        // Weekly line: W 23%(14.5) 12:00 Fr
        if (data.Weekly != null)
        {
            yield return new Markup(BuildCompactLine("W", data.Weekly));
        }

        // Note: Tertiary (T) not shown in compact mode
    }

    private static string BuildCompactLine(string prefix, UsageWindow window)
    {
        var percent = window.Percent;
        var color = GetPercentColor(percent);

        // Budget always with one decimal: (14.5)
        var budget = window.ComputeDailyBudget();
        var budgetPart = budget.HasValue ? $"({budget:F1})" : "";

        // Reset time and day
        var (time, day) = FormatResetShort(window.ResetAt);

        // Format: "W 23%(14.5) 12:00 Fr" - day at the end
        var dayPart = string.IsNullOrEmpty(day) ? "" : $" [dim]{day}[/]";
        return $"{prefix} [{color}]{percent,2:F0}%[/][dim]{budgetPart,-6}[/] {time}{dayPart}";
    }

    /// <summary>
    /// Formats reset time for compact mode. Returns (time, dayAbbr).
    /// Examples: ("19:00", ""), ("12:00", "Fr"), ("14:00", "Sa")
    /// </summary>
    private static (string time, string day) FormatResetShort(DateTime? resetAt)
    {
        if (resetAt is not { } reset) return ("--:--", "");

        var local = reset.ToLocalTime();
        var now = DateTime.Now;
        var time = $"{local:HH:mm}";

        // Today: just time, no day
        if (local.Date == now.Date)
            return (time, "");

        // Any other day: show day abbreviation (including tomorrow)
        var daysUntil = (local.Date - now.Date).Days;
        if (daysUntil <= 6)
        {
            var dayAbbr = local.DayOfWeek.ToString()[..2]; // Mo, Tu, We, Th, Fr, Sa, Su
            return (time, dayAbbr);
        }

        // Further: show date instead of time
        return ($"{local:d MMM}", "");
    }

    #endregion

    #region Common Helpers

    private static string GetPercentColor(double percent) => percent switch
    {
        < 50 => "green",
        < 80 => "yellow",
        _ => "red"
    };

    private static string GetProviderColor(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => "darkorange",
        "codex" => "deepskyblue1",
        "gemini" => "dodgerblue1",
        _ => "white"
    };

    private static string FormatProviderName(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => "Claude",
        "codex" => "Codex",
        "gemini" => "Gemini",
        _ => provider
    };

    #endregion
}
