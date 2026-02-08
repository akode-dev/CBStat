namespace Akode.CBStat.Models;

/// <summary>
/// Represents usage data for a provider.
/// </summary>
public record UsageData
{
    public string Provider { get; init; } = string.Empty;
    public string? Plan { get; init; }
    public UsageWindow? Session { get; init; }
    public UsageWindow? Weekly { get; init; }
    public UsageWindow? Tertiary { get; init; }
    public DateTime FetchedAt { get; init; } = DateTime.UtcNow;
    public string? Status { get; init; }
    public string? Error { get; init; }
    public bool IsLoading { get; init; }

    public bool HasWeekly => Weekly != null;
    public bool HasTertiary => Tertiary != null;
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

/// <summary>
/// Represents a usage window (session or weekly).
/// </summary>
public record UsageWindow
{
    public int Used { get; init; }
    public int Limit { get; init; }
    public double Percent => Limit > 0 ? (double)Used / Limit * 100 : 0;
    public string PercentText => $"{Percent:F0}%";
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

    /// <summary>
    /// Formats reset time compactly for horizontal mode.
    /// Example: "S: Today 19:00", "W: 13 Feb 12:00"
    /// </summary>
    public string FormatResetCompact(string prefix)
    {
        if (ResetAt is not { } resetAt) return $"{prefix}: --";
        var local = resetAt.ToLocalTime();
        var now = DateTime.Now;

        if (local.Date == now.Date)
            return $"{prefix}: Today {local:HH:mm}";
        if (local.Date == now.Date.AddDays(1))
            return $"{prefix}: Tomorrow {local:HH:mm}";

        // Format: "W: 13 Feb 12:00" or "W: Mo 12:00" for this week
        var daysUntil = (local.Date - now.Date).Days;
        if (daysUntil <= 7)
        {
            var dayAbbr = local.ToString("ddd")[..2]; // Mo, Tu, We...
            return $"{prefix}: {dayAbbr} {local:HH:mm}";
        }

        var monthAbbr = local.ToString("MMM")[..3]; // Jan, Feb...
        return $"{prefix}: {local.Day} {monthAbbr} {local:HH:mm}";
    }

    /// <summary>
    /// Computes remaining budget for the current user day (starting at 1:00 AM local time)
    /// so total usage stays on pace until reset.
    /// </summary>
    public double? ComputeDailyBudget(DateTime? nowLocal = null)
    {
        if (ResetAt == null) return null;
        var now = nowLocal ?? DateTime.Now;
        var resetLocal = ResetAt.Value.ToLocalTime();
        if (resetLocal <= now) return null;

        var remaining = 100.0 - Percent;
        if (remaining < 0) remaining = 0;

        static DateTime GetUserDayStart(DateTime value)
        {
            var dayStart = value.Date.AddHours(1);
            return value < dayStart ? dayStart.AddDays(-1) : dayStart;
        }

        // When window length is known: compute "left for today" against full-cycle pace.
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

        // Fallback if window length is unknown.
        var dayStartNow = GetUserDayStart(now);
        var daysRemaining = Math.Max(1, (int)Math.Floor((resetLocal - dayStartNow).TotalDays));
        return remaining / daysRemaining;
    }

    /// <summary>
    /// Gets the daily budget text for display, e.g., "(14.5%)".
    /// </summary>
    public string DailyBudgetText
    {
        get
        {
            var budget = ComputeDailyBudget();
            return budget.HasValue ? $"({budget:F1}%)" : "";
        }
    }
}
