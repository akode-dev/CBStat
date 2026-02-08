using Akode.CBStat.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Akode.CBStat.UI;

public class ConsoleRenderer
{
    private int _workDayStartHour = 1;

    public void SetWorkDayStartHour(int hour) => _workDayStartHour = Math.Clamp(hour, 0, 23);

    public IRenderable BuildDisplay(IReadOnlyList<UsageData> data, DisplayMode mode) =>
        mode == DisplayMode.Compact ? BuildCompactDisplay(data) : BuildVerticalTable(data);

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

    private IRenderable BuildProgressRow(string label, UsageWindow window)
    {
        var percent = window.Percent;
        var color = GetPercentColor(percent);
        const int barWidth = 20;
        var filledWidth = (int)(barWidth * percent / 100);
        var emptyWidth = barWidth - filledWidth;

        var bar = new string('\u2588', filledWidth) + new string('\u2591', emptyWidth);
        var dailyBudget = window.GetDailyBudgetText(_workDayStartHour);
        var budgetPart = string.IsNullOrEmpty(dailyBudget) ? "" : $" [dim]{Markup.Escape(dailyBudget)}[/]";

        return new Markup($"{label,-8} [{color}]{bar}[/] {percent,3:F0}%{budgetPart}");
    }

    private IRenderable BuildCompactDisplay(IReadOnlyList<UsageData> data)
    {
        var rows = new List<IRenderable>();

        foreach (var provider in data)
        {
            if (rows.Count > 0)
                rows.Add(new Text(""));

            rows.AddRange(BuildCompactProviderBlock(provider));
        }

        return new Rows(rows);
    }

    private IEnumerable<IRenderable> BuildCompactProviderBlock(UsageData data)
    {
        var providerName = FormatProviderName(data.Provider);
        var providerColor = GetProviderColor(data.Provider);

        yield return new Markup($"[{providerColor}][bold]{providerName}[/][/]");

        if (data.HasError)
        {
            yield return new Markup($"[red]Error[/]");
            yield break;
        }

        if (data.Session != null)
        {
            foreach (var line in BuildCompactWindowRows("S", data.Session, emphasizeBudget: false))
            {
                yield return line;
            }

            if (data.Weekly != null)
                yield return new Text("");
        }

        if (data.Weekly != null)
        {
            foreach (var line in BuildCompactWindowRows("W", data.Weekly, emphasizeBudget: true))
            {
                yield return line;
            }
        }
    }

    private IEnumerable<IRenderable> BuildCompactWindowRows(string prefix, UsageWindow window, bool emphasizeBudget)
    {
        var percent = window.Percent;
        var color = GetPercentColor(percent);

        var (time, day) = FormatResetShort(window.ResetAt);
        var dayPart = string.IsNullOrEmpty(day) ? "" : $" [dim]{day}[/]";
        yield return new Markup($"[dim]{prefix}:{time}[/]{dayPart}");

        var budgetPart = BuildCompactBudgetPart(window, emphasizeBudget);
        var percentPart = $"[{color} dim]{percent:F0}%[/]";
        yield return string.IsNullOrEmpty(budgetPart)
            ? new Markup(percentPart)
            : new Markup($"{percentPart} {budgetPart}");
    }

    private string BuildCompactBudgetPart(UsageWindow window, bool emphasizeBudget)
    {
        var budget = window.GetDailyBudgetText(_workDayStartHour);
        if (string.IsNullOrEmpty(budget))
            return "";

        var escapedBudget = Markup.Escape(budget);
        return emphasizeBudget
            ? $"[bold]{escapedBudget}[/]"
            : $"[dim]{escapedBudget}[/]";
    }

    private static (string time, string day) FormatResetShort(DateTime? resetAt)
    {
        if (resetAt is not { } reset) return ("--:--", "");

        var local = reset.ToLocalTime();
        var now = DateTime.Now;
        var time = $"{local:HH:mm}";

        if (local.Date == now.Date)
            return (time, "");

        var daysUntil = (local.Date - now.Date).Days;
        if (daysUntil <= 6)
        {
            var dayAbbr = local.DayOfWeek.ToString()[..2];
            return (time, dayAbbr);
        }

        return ($"{local:d MMM}", "");
    }

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
}
