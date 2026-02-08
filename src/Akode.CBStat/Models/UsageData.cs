namespace Akode.CBStat.Models;

public record UsageData
{
    public string Provider { get; init; } = string.Empty;
    public UsageWindow? Session { get; init; }
    public UsageWindow? Weekly { get; init; }
    public UsageWindow? Tertiary { get; init; }
    public DateTime FetchedAt { get; init; } = DateTime.UtcNow;
    public string? Error { get; init; }

    public bool HasError => !string.IsNullOrEmpty(Error);

    public string SessionLabel => Provider.ToLowerInvariant() switch
    {
        "gemini" => "Pro",
        _ => "Session"
    };

    public string WeeklyLabel => Provider.ToLowerInvariant() switch
    {
        "gemini" => "Flash",
        _ => "Weekly"
    };

    public string TertiaryLabel => Provider.ToLowerInvariant() switch
    {
        "claude" => "Sonnet",
        _ => "Additional"
    };
}

public record UsageWindow
{
    public int Used { get; init; }
    public int Limit { get; init; }
    public double Percent => Limit > 0 ? (double)Used / Limit * 100 : 0;
    public int WindowMinutes { get; init; }
    public DateTime? ResetAt { get; init; }
    public string? ResetIn { get; init; }
    public TimeSpan? TimeUntilReset => ResetAt.HasValue ? ResetAt.Value - DateTime.UtcNow : null;

    public string FormatResetIn()
    {
        if (TimeUntilReset is not { } timeLeft) return ResetIn ?? "";
        if (timeLeft.TotalSeconds <= 0) return "now";
        if (timeLeft.TotalDays >= 1) return $"{(int)timeLeft.TotalDays}d {timeLeft.Hours}h";
        if (timeLeft.TotalHours >= 1) return $"{(int)timeLeft.TotalHours}h {timeLeft.Minutes}m";
        return $"{timeLeft.Minutes}m";
    }

    public double? ComputeDailyBudget(int workDayStartHour = 1, DateTime? nowLocal = null)
    {
        if (ResetAt == null) return null;
        var now = nowLocal ?? DateTime.Now;
        var resetLocal = ResetAt.Value.ToLocalTime();
        if (resetLocal <= now) return null;

        var remaining = 100.0 - Percent;
        if (remaining < 0) remaining = 0;

        DateTime GetUserDayStart(DateTime value)
        {
            var dayStart = value.Date.AddHours(workDayStartHour);
            return value < dayStart ? dayStart.AddDays(-1) : dayStart;
        }

        if (WindowMinutes > 0)
        {
            var windowStartLocal = resetLocal - TimeSpan.FromMinutes(WindowMinutes);
            var cycleDayStart = GetUserDayStart(windowStartLocal);
            var cycleDayEnd = GetUserDayStart(resetLocal.AddTicks(-1));
            var currentDayStart = GetUserDayStart(now);

            if (currentDayStart < cycleDayStart) currentDayStart = cycleDayStart;
            if (currentDayStart > cycleDayEnd) currentDayStart = cycleDayEnd;

            var totalDays = Math.Max(1, (int)(cycleDayEnd - cycleDayStart).TotalDays + 1);
            var currentDayIndex = Math.Max(1, (int)(currentDayStart - cycleDayStart).TotalDays + 1);

            var cumulativeAllowed = 100.0 * currentDayIndex / totalDays;
            var todayBudget = cumulativeAllowed - Percent;
            if (todayBudget < 0) todayBudget = 0;
            if (todayBudget > remaining) todayBudget = remaining;
            return todayBudget;
        }

        var dayStartNow = GetUserDayStart(now);
        var daysRemaining = Math.Max(1, (int)Math.Floor((resetLocal - dayStartNow).TotalDays));
        return remaining / daysRemaining;
    }

    public string GetDailyBudgetText(int workDayStartHour = 1)
    {
        var budget = ComputeDailyBudget(workDayStartHour);
        return budget.HasValue ? $"({budget:F1}%)" : "";
    }
}
