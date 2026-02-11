namespace Akode.CBStat.Models;

/// <summary>
/// Display mode for the usage output.
/// </summary>
public enum DisplayMode
{
    /// <summary>
    /// Vertical layout with panels stacked.
    /// </summary>
    Vertical = 0,

    /// <summary>
    /// Horizontal compact layout for narrow windows.
    /// </summary>
    Compact = 1
}

/// <summary>
/// Application settings for cbstat.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Settings file version for migrations.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Provider configurations (enabled state, order).
    /// </summary>
    public List<ProviderConfig> Providers { get; set; } = [];

    /// <summary>
    /// Refresh interval in seconds.
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 120;

    /// <summary>
    /// Use sample data instead of calling provider APIs (for development/testing).
    /// </summary>
    public bool DeveloperModeEnabled { get; set; } = false;

    /// <summary>
    /// HTTP request timeout in seconds.
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Display mode: Vertical (stacked panels) or Compact (horizontal single-line).
    /// </summary>
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Vertical;

    /// <summary>
    /// Hour when the "work day" starts for daily budget calculation (0-23).
    /// Default is 1 (1:00 AM). This determines when your work day begins
    /// for calculating how much quota you can use today.
    /// </summary>
    public int WorkDayStartHour { get; set; } = 1;

    /// <summary>
    /// Gets enabled providers in display order.
    /// </summary>
    public IEnumerable<ProviderConfig> GetEnabledProviders() =>
        Providers
            .Where(p => p.IsEnabled && ProviderConstants.IsValidProvider(p.Id))
            .OrderBy(p => p.Order);

    /// <summary>
    /// Gets the default settings.
    /// </summary>
    public static AppSettings GetDefaults() => new()
    {
        Providers = ProviderConfig.GetDefaults().ToList(),
        RefreshIntervalSeconds = 120,
        DeveloperModeEnabled = false,
        HttpTimeoutSeconds = 30,
        DisplayMode = DisplayMode.Vertical,
        WorkDayStartHour = 1
    };
}
