namespace Akode.CBStat.Models;

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
    /// Use sample data instead of calling codexbar CLI (for development/testing).
    /// </summary>
    public bool DeveloperModeEnabled { get; set; } = false;

    /// <summary>
    /// Command timeout in seconds.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

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
        CommandTimeoutSeconds = 30
    };
}
