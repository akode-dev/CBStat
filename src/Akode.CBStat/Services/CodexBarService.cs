using System.Text.Json;
using Akode.CBStat.Models;

namespace Akode.CBStat.Services;

public class CodexBarService
{
    private readonly ICommandRunner _runner;
    private readonly SettingsService _settings;

    public CodexBarService(ICommandRunner runner, SettingsService settings)
    {
        _runner = runner;
        _settings = settings;
    }

    public async Task<UsageData> GetUsageAsync(string provider, CancellationToken ct = default)
    {
        // Developer mode: return sample data
        if (_settings.Settings.DeveloperModeEnabled)
        {
            return GetSampleData(provider);
        }

        try
        {
            var normalized = ProviderConstants.ValidateAndNormalize(provider);
            var source = ProviderConstants.GetSource(normalized);
            var verboseFlag = normalized == "gemini" ? " --verbose" : "";

            var result = await _runner.ExecuteAsync(
                $"codexbar usage --provider {normalized} --format json --source {source}{verboseFlag}", ct);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            {
                return new UsageData
                {
                    Provider = provider,
                    Error = string.IsNullOrWhiteSpace(result.Error)
                        ? "Failed to fetch data"
                        : result.Error.Trim(),
                    FetchedAt = DateTime.UtcNow
                };
            }

            return ParseJson(result.Output, provider);
        }
        catch (Exception ex)
        {
            return new UsageData
            {
                Provider = provider,
                Error = ex.Message,
                FetchedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<List<UsageData>> GetAllUsageAsync(CancellationToken ct = default)
    {
        var results = new List<UsageData>();
        var enabledProviders = _settings.Settings.GetEnabledProviders();

        foreach (var provider in enabledProviders)
        {
            results.Add(await GetUsageAsync(provider.Id, ct));
        }

        return results;
    }

    private static UsageData ParseJson(string json, string provider)
    {
        try
        {
            if (json.TrimStart().StartsWith('['))
            {
                var dtos = JsonSerializer.Deserialize(json, JsonContext.Default.ListUsageDataDto);
                if (dtos?.Count > 0)
                    return dtos[0].ToUsageData();
            }
            else
            {
                var dto = JsonSerializer.Deserialize(json, JsonContext.Default.UsageDataDto);
                if (dto != null)
                    return dto.ToUsageData();
            }
        }
        catch { /* fall through */ }

        return new UsageData
        {
            Provider = provider,
            Error = "Failed to parse response",
            FetchedAt = DateTime.UtcNow
        };
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
