using CLIStat.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace CLIStat.UI;

public class ConsoleRenderer
{
    public Table BuildTable(IReadOnlyList<UsageData> data)
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

        return new Markup($"{label,-8} [{color}]{bar}[/] {percent,3:F0}%");
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
