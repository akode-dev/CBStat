namespace CLIStat.Models;

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
}
