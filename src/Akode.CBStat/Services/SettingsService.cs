using System.Text.Json;
using Akode.CBStat.Models;

namespace Akode.CBStat.Services;

/// <summary>
/// Service for managing application settings.
/// </summary>
public class SettingsService
{
    private AppSettings _settings = AppSettings.GetDefaults();

    public AppSettings Settings => _settings;
    public string SettingsFilePath { get; }

    public SettingsService()
    {
        // Cross-platform settings path
        var configDir = GetConfigDirectory();
        Directory.CreateDirectory(configDir);
        SettingsFilePath = Path.Combine(configDir, "settings.json");
    }

    /// <summary>
    /// Loads settings from file.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return;

            var json = await File.ReadAllTextAsync(SettingsFilePath);
            var settings = JsonSerializer.Deserialize(json, JsonContext.Default.AppSettings);

            if (settings != null)
            {
                // Filter out invalid providers for security
                settings.Providers = settings.Providers
                    .Where(p => ProviderConstants.IsValidProvider(p.Id))
                    .ToList();

                _settings = settings;
            }
        }
        catch
        {
            // Use defaults on error
        }
    }

    /// <summary>
    /// Saves settings to file.
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, JsonContext.Default.AppSettings);
            await File.WriteAllTextAsync(SettingsFilePath, json);
        }
        catch
        {
            // Ignore save errors in console app
        }
    }

    /// <summary>
    /// Applies command-line overrides to settings.
    /// </summary>
    public void ApplyCommandLineArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--interval" or "-i" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var interval) && interval > 0)
                        _settings.RefreshIntervalSeconds = interval;
                    break;

                case "--providers" or "-p" when i + 1 < args.Length:
                    ApplyProviderFilter(args[++i]);
                    break;

                case "--dev" or "--developer":
                    _settings.DeveloperModeEnabled = true;
                    break;

                case "--timeout" or "-t" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var timeout) && timeout > 0)
                        _settings.CommandTimeoutSeconds = timeout;
                    break;
            }
        }
    }

    private void ApplyProviderFilter(string providerList)
    {
        var requested = providerList
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().ToLowerInvariant())
            .Where(ProviderConstants.IsValidProvider)
            .ToHashSet();

        if (requested.Count == 0)
            return;

        foreach (var provider in _settings.Providers)
        {
            provider.IsEnabled = requested.Contains(provider.Id.ToLowerInvariant());
        }
    }

    private static string GetConfigDirectory()
    {
        // Use XDG_CONFIG_HOME on Linux, LocalApplicationData on Windows
        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdgConfig))
            return Path.Combine(xdgConfig, "cbstat");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "cbstat");
    }
}
