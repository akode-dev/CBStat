using Akode.CBStat.Models;
using Akode.CBStat.Services.Providers;

namespace Akode.CBStat.Services;

public class UsageService
{
    private readonly SettingsService _settings;
    private readonly Dictionary<string, IUsageProvider> _providers;

    public UsageService(SettingsService settings)
    {
        _settings = settings;

        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(settings.Settings.HttpTimeoutSeconds)
        };

        _providers = new Dictionary<string, IUsageProvider>(StringComparer.OrdinalIgnoreCase)
        {
            ["claude"] = new ClaudeUsageProvider(httpClient),
            ["gemini"] = new GeminiUsageProvider(httpClient),
            ["codex"] = new CodexUsageProvider(httpClient)
        };
    }

    public async Task<UsageData> GetUsageAsync(string provider, CancellationToken ct = default)
    {
        if (_settings.Settings.DeveloperModeEnabled)
        {
            return GetSampleData(provider);
        }

        var normalized = ProviderConstants.ValidateAndNormalize(provider);

        if (!_providers.TryGetValue(normalized, out var usageProvider))
        {
            return new UsageData
            {
                Provider = provider,
                Error = $"Unknown provider: {provider}",
                FetchedAt = DateTime.UtcNow
            };
        }

        return await usageProvider.GetUsageAsync(ct);
    }

    public async Task<List<UsageData>> GetAllUsageAsync(CancellationToken ct = default)
    {
        var enabledProviders = _settings.Settings.GetEnabledProviders();
        var tasks = enabledProviders.Select(p => GetUsageAsync(p.Id, ct));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private static UsageData GetSampleData(string provider)
    {
        var random = new Random();
        var normalized = provider.ToLowerInvariant();

        return new UsageData
        {
            Provider = provider,
            Session = new UsageWindow
            {
                Used = random.Next(20, 80),
                Limit = 100,
                ResetAt = DateTime.UtcNow.AddHours(random.Next(1, 12)),
                WindowMinutes = 180
            },
            Weekly = new UsageWindow
            {
                Used = random.Next(10, 60),
                Limit = 100,
                ResetAt = DateTime.UtcNow.AddDays(random.Next(1, 5)),
                WindowMinutes = 10080
            },
            Tertiary = normalized == "claude" ? new UsageWindow
            {
                Used = random.Next(30, 90),
                Limit = 100,
                ResetAt = DateTime.UtcNow.AddDays(random.Next(1, 7)),
                WindowMinutes = 10080
            } : null,
            FetchedAt = DateTime.UtcNow
        };
    }
}
